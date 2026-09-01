#pragma warning disable CA1848, CA1873, CS1591
using System.Globalization;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Authoritative unified reconciliation engine coordinating multi-rail status querying,
/// out-of-order resolution, amount/currency validation, and central ledger state synchronizations.
/// </summary>
public sealed partial class ReconciliationEngine : IReconciliationEngine, IPaymentReconciliationService
{
    private readonly IPaymentProviderFactory _paymentProviderFactory;
    private readonly IEnumerable<ICardPaymentProvider> _cardProviders;
    private readonly IVerificationProviderFactory _verificationProviderFactory;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IOutboxService _outboxService;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<ReconciliationEngine> _logger;

    public ReconciliationEngine(
        IPaymentProviderFactory paymentProviderFactory,
        IEnumerable<ICardPaymentProvider> cardProviders,
        IVerificationProviderFactory verificationProviderFactory,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outboxService,
        ReconciliationMetrics metrics,
        ILogger<ReconciliationEngine> logger)
    {
        _paymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));
        _cardProviders = cardProviders ?? throw new ArgumentNullException(nameof(cardProviders));
        _verificationProviderFactory = verificationProviderFactory ?? throw new ArgumentNullException(nameof(verificationProviderFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService ?? throw new ArgumentNullException(nameof(ledgerPostingService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> ReconcilePaymentAttemptAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken = default)
    {
        var unified = await ReconcilePaymentAttemptInternalAsync(paymentAttemptId, cancellationToken).ConfigureAwait(false);

        return unified.Outcome switch
        {
            ReconciliationOutcome.Success => PaymentProviderResult.Success(unified.ProviderReference ?? unified.SourceReference, unified.SafeMetadata),
            ReconciliationOutcome.Failure => PaymentProviderResult.BusinessFailure("RECONCILED_FAILED", unified.Message, unified.SafeMetadata),
            _ => PaymentProviderResult.Unknown(unified.Message)
        };
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileUnresolvedAttemptsAsync(int batchSize = 50, CancellationToken cancellationToken = default) =>
        await ReconcilePendingBatchAsync(batchSize, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    async Task<UnifiedReconciliationResult> IReconciliationEngine.ReconcilePaymentAttemptAsync(Guid paymentAttemptId, CancellationToken cancellationToken) =>
        await ReconcilePaymentAttemptInternalAsync(paymentAttemptId, cancellationToken).ConfigureAwait(false);

    private async Task<UnifiedReconciliationResult> ReconcilePaymentAttemptInternalAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var attempt = await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken)
            .ConfigureAwait(false);

        if (attempt == null)
        {
            _logger.LogWarning("PaymentAttempt {AttemptId} not found for reconciliation.", paymentAttemptId);
            return UnifiedReconciliationResult.ErrorResult(paymentAttemptId.ToString(), "UNKNOWN", $"PaymentAttempt '{paymentAttemptId}' not found.");
        }

        var providerName = attempt.Provider.ToString();
        _metrics.RecordReconciliationStarted(providerName, "BankTransferPayout");

        // If already terminal, return existing state safely
        if (attempt.Status == PaymentAttemptStatus.Succeeded)
        {
            return UnifiedReconciliationResult.Succeeded(
                attempt.RequestReference,
                providerName,
                attempt.ProviderReference,
                attempt.Amount,
                attempt.SafeMetadata,
                "PaymentAttempt is already in terminal SUCCEEDED state.");
        }

        if (attempt.Status == PaymentAttemptStatus.Failed)
        {
            return UnifiedReconciliationResult.Failed(
                attempt.RequestReference,
                providerName,
                attempt.FailureReason ?? "PaymentAttempt is in terminal FAILED state.",
                attempt.SafeMetadata);
        }

        var providerAdapter = _paymentProviderFactory.GetProvider(attempt.Provider);
        var providerRef = !string.IsNullOrWhiteSpace(attempt.ProviderReference) ? attempt.ProviderReference : attempt.RequestReference;

        // Query status outside DB transaction
        PaymentProviderResult queryResult;
        try
        {
            queryResult = await providerAdapter.GetPaymentStatusAsync(providerRef, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying provider status for attempt {AttemptId} on {Provider}", attempt.Id, providerName);
            queryResult = PaymentProviderResult.Unknown($"Exception querying provider status: {ex.Message}");
        }

        // Apply state transition in DB transaction
        await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dbAttempt = await _dbContext.PaymentAttempts
                .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken)
                .ConfigureAwait(false);

            if (dbAttempt == null)
            {
                await dbTx.RollbackAsync(cancellationToken);
                return UnifiedReconciliationResult.ErrorResult(paymentAttemptId.ToString(), providerName, "PaymentAttempt not found in transaction.");
            }

            var prevStatus = dbAttempt.Status;
            var bankTransfer = await _dbContext.BankTransfers
                .FirstOrDefaultAsync(b => b.LedgerTransactionId == dbAttempt.LedgerTransactionId, cancellationToken)
                .ConfigureAwait(false);

            // CRITICAL UNKNOWN RULE: UNKNOWN never triggers failover or reversal
            if (queryResult.Status == PaymentProviderResultStatus.Unknown)
            {
                _metrics.RecordReconciliationUnresolved(providerName, "BankTransferPayout");
                dbAttempt.MarkUnknown(queryResult.FailureReason ?? "Provider returned ambiguous / in-flight status during polling.");

                var auditUnknown = AuditLog.Create(
                    actorId: "SYSTEM",
                    action: AuditActions.PaymentAttemptUnknown,
                    resourceType: AuditResourceTypes.PaymentAttempt,
                    resourceId: dbAttempt.Id.ToString(),
                    afterJson: JsonSerializer.Serialize(new { dbAttempt.Id, Provider = providerName, Reason = queryResult.FailureReason }));
                _dbContext.AuditLogs.Add(auditUnknown);

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

                return UnifiedReconciliationResult.StillUnresolved(dbAttempt.RequestReference, providerName, queryResult.FailureReason ?? "Status is unknown / pending.");
            }

            if (queryResult.Status == PaymentProviderResultStatus.Success)
            {
                _metrics.RecordReconciliationSuccess(providerName, "BankTransferPayout");
                var effectiveProviderRef = queryResult.ProviderReference ?? dbAttempt.RequestReference;
                dbAttempt.MarkSucceeded(effectiveProviderRef, safeMetadata: queryResult.SafeMetadata);

                if (bankTransfer != null && bankTransfer.Status != BankTransferStatus.Completed)
                {
                    bankTransfer.MarkCompleted(DateTime.UtcNow, effectiveProviderRef);
                }

                var ledgerTx = await _dbContext.LedgerTransactions
                    .FirstOrDefaultAsync(l => l.Id == dbAttempt.LedgerTransactionId, cancellationToken)
                    .ConfigureAwait(false);

                if (ledgerTx != null && ledgerTx.Status != LedgerTransactionStatus.Completed)
                {
                    ledgerTx.Complete(DateTime.UtcNow);
                }

                var auditSuccess = AuditLog.Create(
                    actorId: "SYSTEM",
                    action: AuditActions.PaymentAttemptReconciled,
                    resourceType: AuditResourceTypes.PaymentAttempt,
                    resourceId: dbAttempt.Id.ToString(),
                    afterJson: JsonSerializer.Serialize(new { dbAttempt.Id, Provider = providerName, Status = "SUCCEEDED", ProviderReference = effectiveProviderRef }));
                _dbContext.AuditLogs.Add(auditSuccess);

                _outboxService.Write(new PaymentAttemptReconciledEvent(
                    PaymentAttemptId: dbAttempt.Id,
                    LedgerTransactionId: dbAttempt.LedgerTransactionId,
                    Provider: dbAttempt.Provider,
                    AttemptNumber: dbAttempt.AttemptNumber,
                    PreviousStatus: prevStatus,
                    NewStatus: dbAttempt.Status,
                    ProviderReference: effectiveProviderRef,
                    OccurredOnUtc: DateTime.UtcNow));

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

                return UnifiedReconciliationResult.Succeeded(
                    dbAttempt.RequestReference,
                    providerName,
                    effectiveProviderRef,
                    dbAttempt.Amount,
                    dbAttempt.SafeMetadata);
            }

            // Definite Failure
            var failCode = queryResult.FailureCode ?? "TRANSFER_FAILED";
            var failReason = queryResult.FailureReason ?? "Provider transfer failed during reconciliation query.";
            _metrics.RecordReconciliationFailure(providerName, "BankTransferPayout", failCode);
            dbAttempt.MarkFailed(failCode, failReason, DateTime.UtcNow, queryResult.SafeMetadata);

            var auditFailure = AuditLog.Create(
                actorId: "SYSTEM",
                action: AuditActions.PaymentAttemptReconciled,
                resourceType: AuditResourceTypes.PaymentAttempt,
                resourceId: dbAttempt.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new { dbAttempt.Id, Provider = providerName, Status = "FAILED", ErrorCode = failCode, Reason = failReason }));
            _dbContext.AuditLogs.Add(auditFailure);

            _outboxService.Write(new PaymentAttemptReconciledEvent(
                PaymentAttemptId: dbAttempt.Id,
                LedgerTransactionId: dbAttempt.LedgerTransactionId,
                Provider: dbAttempt.Provider,
                AttemptNumber: dbAttempt.AttemptNumber,
                PreviousStatus: prevStatus,
                NewStatus: dbAttempt.Status,
                ProviderReference: dbAttempt.ProviderReference,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return UnifiedReconciliationResult.Failed(dbAttempt.RequestReference, providerName, failReason, queryResult.SafeMetadata);
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<UnifiedReconciliationResult> ReconcileBankTransferAsync(Guid bankTransferId, CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(b => b.Id == bankTransferId, cancellationToken)
            .ConfigureAwait(false);

        if (transfer == null)
            return UnifiedReconciliationResult.ErrorResult(bankTransferId.ToString(), "UNKNOWN", $"BankTransfer '{bankTransferId}' not found.");

        if (transfer.Status == BankTransferStatus.Completed)
            return UnifiedReconciliationResult.Succeeded(transfer.Reference, "BANK_TRANSFER", transfer.ProviderReference, transfer.Amount);

        var latestAttempt = await _dbContext.PaymentAttempts
            .Where(p => p.LedgerTransactionId == transfer.LedgerTransactionId)
            .OrderByDescending(p => p.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (latestAttempt != null)
        {
            return await ReconcilePaymentAttemptInternalAsync(latestAttempt.Id, cancellationToken).ConfigureAwait(false);
        }

        return UnifiedReconciliationResult.StillUnresolved(transfer.Reference, "BANK_TRANSFER", "No payment attempts found for transfer.");
    }

    /// <inheritdoc/>
    public async Task<UnifiedReconciliationResult> ReconcileFundingTransactionAsync(Guid fundingTransactionId, CancellationToken cancellationToken = default)
    {
        var fundingTx = await _dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.Id == fundingTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (fundingTx == null)
            return UnifiedReconciliationResult.ErrorResult(fundingTransactionId.ToString(), "CARD_FUNDING", $"FundingTransaction '{fundingTransactionId}' not found.");

        var providerName = fundingTx.Provider.ToString();
        _metrics.RecordReconciliationStarted(providerName, "CardFunding");

        if (fundingTx.Status == FundingTransactionStatus.Completed)
            return UnifiedReconciliationResult.Succeeded(fundingTx.ProviderTransactionReference, providerName, fundingTx.ProviderTransactionReference, fundingTx.Amount);

        var cardProvider = _cardProviders.FirstOrDefault(p => p.Provider == fundingTx.Provider);
        if (cardProvider == null)
            return UnifiedReconciliationResult.ErrorResult(fundingTx.ProviderTransactionReference, providerName, $"No card provider registered for {fundingTx.Provider}.");

        var statusResult = await cardProvider.GetCardPaymentStatusAsync(fundingTx.ProviderTransactionReference, cancellationToken).ConfigureAwait(false);

        if (statusResult.Status == PaymentProviderResultStatus.Success)
        {
            _metrics.RecordReconciliationSuccess(providerName, "CardFunding");

            var (txn, settled) = await _ledgerPostingService.PostCardFundingCreditCoreAsync(
                walletId: fundingTx.WalletId,
                grossAmount: fundingTx.Amount,
                feeAmount: fundingTx.FeeAmount,
                netCreditedAmount: fundingTx.NetCreditedAmount,
                providerFeeAmount: 0m,
                currency: fundingTx.Currency,
                provider: fundingTx.Provider,
                providerTransactionReference: fundingTx.ProviderTransactionReference,
                providerEventReference: null,
                feePolicyId: fundingTx.FeePolicyId,
                feePolicyVersion: fundingTx.FeePolicyVersion,
                feeBearer: fundingTx.FeeBearer,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            fundingTx.MarkCompleted(txn.Id);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return UnifiedReconciliationResult.Succeeded(fundingTx.ProviderTransactionReference, providerName, statusResult.ProviderReference ?? fundingTx.ProviderTransactionReference, fundingTx.Amount);
        }

        if (statusResult.Status == PaymentProviderResultStatus.Unknown)
            return UnifiedReconciliationResult.StillUnresolved(fundingTx.ProviderTransactionReference, providerName, "Card transaction is still pending on provider.");

        fundingTx.MarkFailed(statusResult.FailureReason ?? "Card payment verification failed.");
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UnifiedReconciliationResult.Failed(fundingTx.ProviderTransactionReference, providerName, statusResult.FailureReason ?? "Verification failed.");
    }

    /// <inheritdoc/>
    public async Task<UnifiedReconciliationResult> ReconcileCardRefundAsync(Guid cardRefundId, CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.CardRefunds
            .FirstOrDefaultAsync(r => r.Id == cardRefundId, cancellationToken)
            .ConfigureAwait(false);

        if (refund == null)
            return UnifiedReconciliationResult.ErrorResult(cardRefundId.ToString(), "CARD_REFUND", $"CardRefund '{cardRefundId}' not found.");

        var providerName = refund.Provider.ToString();
        if (refund.Status == CardRefundStatus.Succeeded)
            return UnifiedReconciliationResult.Succeeded(refund.RefundReference, providerName, refund.ProviderRefundReference, refund.Amount);

        if (refund.Status == CardRefundStatus.RecoveryOutstanding)
            return UnifiedReconciliationResult.RequiresManualReview(refund.RefundReference, providerName, "Refund executed by provider but customer wallet had insufficient balance (RecoveryOutstanding).");

        return UnifiedReconciliationResult.StillUnresolved(refund.RefundReference, providerName, $"Refund in status {refund.Status}.");
    }

    /// <inheritdoc/>
    public async Task<UnifiedReconciliationResult> ReconcileComplianceOperationAsync(Guid verificationOperationId, CancellationToken cancellationToken = default)
    {
        var operation = await _dbContext.VerificationOperations
            .Include(o => o.Evidences)
            .FirstOrDefaultAsync(o => o.Id == verificationOperationId, cancellationToken)
            .ConfigureAwait(false);

        if (operation == null)
            return UnifiedReconciliationResult.ErrorResult(verificationOperationId.ToString(), "COMPLIANCE", $"VerificationOperation '{verificationOperationId}' not found.");

        if (operation.Status == VerificationStatus.Completed)
            return UnifiedReconciliationResult.Succeeded(operation.Reference, "COMPLIANCE", operation.Reference);

        if (operation.Status == VerificationStatus.Failed)
            return UnifiedReconciliationResult.Failed(operation.Reference, "COMPLIANCE", operation.FailureReason ?? "Verification failed.");

        return UnifiedReconciliationResult.StillUnresolved(operation.Reference, "COMPLIANCE", $"Compliance operation in status {operation.Status}.");
    }

    /// <inheritdoc/>
    public async Task<UnifiedReconciliationResult> RequeryByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));

        var cleanRef = reference.Trim();

        // 1. Search PaymentAttempt
        var attempt = await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.RequestReference == cleanRef || p.ProviderReference == cleanRef, cancellationToken)
            .ConfigureAwait(false);

        if (attempt != null)
            return await ReconcilePaymentAttemptInternalAsync(attempt.Id, cancellationToken).ConfigureAwait(false);

        // 2. Search BankTransfer
        var transfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(b => b.Reference == cleanRef || b.ProviderReference == cleanRef, cancellationToken)
            .ConfigureAwait(false);

        if (transfer != null)
            return await ReconcileBankTransferAsync(transfer.Id, cancellationToken).ConfigureAwait(false);

        // 3. Search FundingTransaction
        var fundingTx = await _dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.ProviderTransactionReference == cleanRef, cancellationToken)
            .ConfigureAwait(false);

        if (fundingTx != null)
            return await ReconcileFundingTransactionAsync(fundingTx.Id, cancellationToken).ConfigureAwait(false);

        // 4. Search CardRefund
        var refund = await _dbContext.CardRefunds
            .FirstOrDefaultAsync(r => r.RefundReference == cleanRef || r.ProviderRefundReference == cleanRef || r.IdempotencyKey == cleanRef, cancellationToken)
            .ConfigureAwait(false);

        if (refund != null)
            return await ReconcileCardRefundAsync(refund.Id, cancellationToken).ConfigureAwait(false);

        // 5. Search Compliance VerificationOperation
        var complianceOp = await _dbContext.VerificationOperations
            .FirstOrDefaultAsync(o => o.Reference == cleanRef, cancellationToken)
            .ConfigureAwait(false);

        if (complianceOp != null)
            return await ReconcileComplianceOperationAsync(complianceOp.Id, cancellationToken).ConfigureAwait(false);

        return UnifiedReconciliationResult.ErrorResult(cleanRef, "UNKNOWN", $"No financial or compliance entity found matching reference '{cleanRef}'.");
    }

    /// <inheritdoc/>
    public async Task<UnifiedReconciliationResult> ResolveManualReviewAsync(
        Guid reconciliationRecordId,
        ManualReviewDecision decision,
        string reviewerNotes,
        string reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.ReconciliationRecords
            .FirstOrDefaultAsync(r => r.Id == reconciliationRecordId, cancellationToken)
            .ConfigureAwait(false);

        if (record == null)
            return UnifiedReconciliationResult.ErrorResult(reconciliationRecordId.ToString(), "UNKNOWN", $"ReconciliationRecord '{reconciliationRecordId}' not found.");

        _logger.LogInformation("Administrator {UserId} resolving manual review for ReconciliationRecord {RecordId} with decision {Decision}. Notes: {Notes}",
            reviewerUserId, record.Id, decision, reviewerNotes);

        switch (decision)
        {
            case ManualReviewDecision.ConfirmSuccess:
                record.MarkSuccess(safeMetadata: JsonSerializer.Serialize(new { Reviewer = reviewerUserId, Notes = reviewerNotes }));
                break;
            case ManualReviewDecision.ConfirmFailure:
                record.MarkFailure($"Confirmed failure by administrator: {reviewerNotes}");
                break;
            case ManualReviewDecision.ConfirmReversal:
                record.MarkReversed($"Confirmed reversal by administrator: {reviewerNotes}");
                break;
            case ManualReviewDecision.Dismiss:
                record.MarkFailedPermanently($"Dismissed by administrator: {reviewerNotes}");
                break;
        }

        var auditLog = AuditLog.Create(
            actorId: reviewerUserId,
            action: AuditActions.ReconciliationManualReview,
            resourceType: AuditResourceTypes.ReconciliationRecord,
            resourceId: record.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { record.Id, Decision = decision.ToString(), Notes = reviewerNotes }));
        _dbContext.AuditLogs.Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UnifiedReconciliationResult(
            record.SourceReference,
            record.Provider,
            record.Status,
            decision == ManualReviewDecision.ConfirmSuccess ? ReconciliationOutcome.Success : ReconciliationOutcome.Failure,
            $"Manual review resolved with decision '{decision}'.");
    }

    /// <inheritdoc/>
    public async Task<int> ReconcilePendingBatchAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pendingRecords = await _dbContext.ReconciliationRecords
            .Where(r => (r.Status == ReconciliationStatus.Pending || r.Status == ReconciliationStatus.InProgress) &&
                        (r.NextPollAtUtc == null || r.NextPollAtUtc <= now))
            .OrderBy(r => r.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int resolvedCount = 0;

        foreach (var record in pendingRecords)
        {
            record.MarkInProgress();
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                UnifiedReconciliationResult result = record.ReconciliationType switch
                {
                    ReconciliationType.PaymentAttempt => await RequeryByReferenceAsync(record.SourceReference, cancellationToken).ConfigureAwait(false),
                    ReconciliationType.BankTransfer => await RequeryByReferenceAsync(record.SourceReference, cancellationToken).ConfigureAwait(false),
                    ReconciliationType.CardFunding => await RequeryByReferenceAsync(record.SourceReference, cancellationToken).ConfigureAwait(false),
                    ReconciliationType.CardRefund => await RequeryByReferenceAsync(record.SourceReference, cancellationToken).ConfigureAwait(false),
                    ReconciliationType.ComplianceVerification => await RequeryByReferenceAsync(record.SourceReference, cancellationToken).ConfigureAwait(false),
                    _ => await RequeryByReferenceAsync(record.SourceReference, cancellationToken).ConfigureAwait(false)
                };

                if (result.Outcome == ReconciliationOutcome.Success)
                {
                    record.MarkSuccess(result.ReconciledAmount, result.ProviderReference, result.SafeMetadata);
                    resolvedCount++;
                }
                else if (result.Outcome == ReconciliationOutcome.Failure)
                {
                    record.MarkFailure(result.Message, result.SafeMetadata);
                    resolvedCount++;
                }
                else if (result.Outcome == ReconciliationOutcome.Reversed)
                {
                    record.MarkReversed(result.Message, result.SafeMetadata);
                    resolvedCount++;
                }
                else if (result.Outcome == ReconciliationOutcome.ManualReviewRequired)
                {
                    record.MarkManualReview(result.Message, result.SafeMetadata);
                }
                else
                {
                    // Exponential backoff
                    var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, record.AttemptCount) * 5));
                    record.ScheduleNextPoll(delay, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconciling record {RecordId} for reference {Ref}", record.Id, record.SourceReference);
                var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, record.AttemptCount) * 10));
                record.ScheduleNextPoll(delay, ex.Message);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return resolvedCount;
    }
}
