using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
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
/// Infrastructure implementation of <see cref="IPaymentReconciliationService"/> resolving in-flight
/// and unknown payment provider outcomes via external status polling and financial state synchronization.
/// </summary>
public sealed partial class PaymentReconciliationService : IPaymentReconciliationService
{
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentReconciliationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentReconciliationService"/> class.
    /// </summary>
    public PaymentReconciliationService(
        IPaymentProviderFactory providerFactory,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outboxService,
        ILogger<PaymentReconciliationService> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService ?? throw new ArgumentNullException(nameof(ledgerPostingService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> ReconcilePaymentAttemptAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken)
            .ConfigureAwait(false);

        if (attempt == null)
        {
            LogAttemptNotFound(_logger, paymentAttemptId);
            return PaymentProviderResult.BusinessFailure("ATTEMPT_NOT_FOUND", $"PaymentAttempt '{paymentAttemptId}' not found.");
        }

        // If already terminal, return existing state
        if (attempt.Status == PaymentAttemptStatus.Succeeded)
        {
            return PaymentProviderResult.Success(attempt.ProviderReference ?? attempt.RequestReference, attempt.SafeMetadata);
        }

        if (attempt.Status == PaymentAttemptStatus.Failed)
        {
            return PaymentProviderResult.BusinessFailure(attempt.FailureCode ?? "FAILED", attempt.FailureReason ?? "Failed", attempt.SafeMetadata);
        }

        var provider = _providerFactory.GetProvider(attempt.Provider);
        var providerRef = !string.IsNullOrWhiteSpace(attempt.ProviderReference)
            ? attempt.ProviderReference
            : attempt.RequestReference;

        var providerName = attempt.Provider.ToString();

        // Query status OUTSIDE DB transaction to prevent holding locks over network I/O
        PaymentProviderResult queryResult;
        try
        {
            queryResult = await provider.GetPaymentStatusAsync(providerRef, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogStatusQueryException(_logger, attempt.Id, providerName, ex);
            queryResult = PaymentProviderResult.Unknown($"Exception querying provider status: {ex.Message}");
        }

        // Apply state transition in DB transaction
        await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dbAttempt = await _dbContext.PaymentAttempts
                .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken)
                .ConfigureAwait(false);

            if (dbAttempt == null || dbAttempt.Status == PaymentAttemptStatus.Succeeded)
            {
                await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return queryResult;
            }

            var bankTransfer = await _dbContext.BankTransfers
                .FirstOrDefaultAsync(b => b.LedgerTransactionId == dbAttempt.LedgerTransactionId, cancellationToken)
                .ConfigureAwait(false);

            var prevStatus = dbAttempt.Status;

            switch (queryResult.Status)
            {
                case PaymentProviderResultStatus.Success:
                    dbAttempt.MarkSucceeded(queryResult.ProviderReference ?? providerRef, safeMetadata: queryResult.SafeMetadata);

                    if (bankTransfer != null && bankTransfer.Status != BankTransferStatus.Completed)
                    {
                        bankTransfer.MarkCompleted(DateTime.UtcNow, queryResult.ProviderReference ?? providerRef);

                        _outboxService.Write(new BankTransferCompletedEvent(
                            TransferId: bankTransfer.Id,
                            TransactionReference: bankTransfer.Reference,
                            ProviderReference: queryResult.ProviderReference ?? providerRef,
                            OccurredOnUtc: DateTime.UtcNow));

                        RecordAudit(AuditActions.BankTransferCompleted, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                            JsonSerializer.Serialize(new { bankTransfer.Reference, ProviderReference = queryResult.ProviderReference }));
                    }

                    _outboxService.Write(new PaymentAttemptReconciledEvent(
                        PaymentAttemptId: dbAttempt.Id,
                        LedgerTransactionId: dbAttempt.LedgerTransactionId,
                        Provider: dbAttempt.Provider,
                        AttemptNumber: dbAttempt.AttemptNumber,
                        PreviousStatus: prevStatus,
                        NewStatus: PaymentAttemptStatus.Succeeded,
                        ProviderReference: queryResult.ProviderReference ?? providerRef,
                        OccurredOnUtc: DateTime.UtcNow));

                    var prevStatusStr = prevStatus.ToString();
                    RecordAudit(AuditActions.PaymentAttemptReconciled, AuditResourceTypes.PaymentAttempt, dbAttempt.Id.ToString(),
                        JsonSerializer.Serialize(new { AttemptId = dbAttempt.Id, PreviousStatus = prevStatusStr, NewStatus = "Succeeded" }));
                    break;

                case PaymentProviderResultStatus.BusinessFailure:
                case PaymentProviderResultStatus.TechnicalFailure:
                    var failReason = queryResult.FailureReason ?? "Reconciliation confirmed failure";
                    dbAttempt.MarkFailed(queryResult.FailureCode, failReason, safeMetadata: queryResult.SafeMetadata);

                    if (bankTransfer != null && bankTransfer.Status != BankTransferStatus.Failed)
                    {
                        await _ledgerPostingService.PostBankTransferReversalCoreAsync(bankTransfer.Id, failReason, cancellationToken).ConfigureAwait(false);

                        _outboxService.Write(new BankTransferFailedEvent(
                            TransferId: bankTransfer.Id,
                            TransactionReference: bankTransfer.Reference,
                            Reason: failReason,
                            OccurredOnUtc: DateTime.UtcNow));

                        RecordAudit(AuditActions.BankTransferReversed, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                            JsonSerializer.Serialize(new { bankTransfer.Reference, Reason = failReason }));
                    }

                    _outboxService.Write(new PaymentAttemptReconciledEvent(
                        PaymentAttemptId: dbAttempt.Id,
                        LedgerTransactionId: dbAttempt.LedgerTransactionId,
                        Provider: dbAttempt.Provider,
                        AttemptNumber: dbAttempt.AttemptNumber,
                        PreviousStatus: prevStatus,
                        NewStatus: PaymentAttemptStatus.Failed,
                        ProviderReference: queryResult.ProviderReference ?? providerRef,
                        OccurredOnUtc: DateTime.UtcNow));

                    var prevStatusFailureStr = prevStatus.ToString();
                    RecordAudit(AuditActions.PaymentAttemptReconciled, AuditResourceTypes.PaymentAttempt, dbAttempt.Id.ToString(),
                        JsonSerializer.Serialize(new { AttemptId = dbAttempt.Id, PreviousStatus = prevStatusFailureStr, NewStatus = "Failed", failReason }));
                    break;

                case PaymentProviderResultStatus.Unknown:
                default:
                    dbAttempt.MarkUnknown(queryResult.FailureReason ?? "Status still unknown", safeMetadata: queryResult.SafeMetadata);
                    break;
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var queryResultStatusStr = queryResult.Status.ToString();
            LogReconciliationCompleted(_logger, attempt.Id, queryResultStatusStr);
            return queryResult;
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogReconciliationException(_logger, attempt.Id, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileUnresolvedAttemptsAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-2);

        var unresolvedAttempts = await _dbContext.PaymentAttempts
            .Where(p => p.Status == PaymentAttemptStatus.Unknown || (p.Status == PaymentAttemptStatus.Processing && p.CreatedAtUtc <= staleThreshold))
            .OrderBy(p => p.CreatedAtUtc)
            .Take(batchSize)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var resolvedCount = 0;
        foreach (var attemptId in unresolvedAttempts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var result = await ReconcilePaymentAttemptAsync(attemptId, cancellationToken).ConfigureAwait(false);
                if (result.Status == PaymentProviderResultStatus.Success ||
                    result.Status == PaymentProviderResultStatus.BusinessFailure ||
                    result.Status == PaymentProviderResultStatus.TechnicalFailure)
                {
                    resolvedCount++;
                }
            }
            catch (Exception ex)
            {
                LogBatchItemException(_logger, attemptId, ex);
            }
        }

        return resolvedCount;
    }

    private void RecordAudit(string action, string resourceType, string resourceId, string detailsJson)
    {
        var audit = AuditLog.Create(
            actorId: "SYSTEM",
            action: action,
            resourceType: resourceType,
            resourceId: resourceId,
            afterJson: detailsJson);

        _dbContext.AuditLogs.Add(audit);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "PaymentAttempt {AttemptId} not found for reconciliation.")]
    private static partial void LogAttemptNotFound(ILogger logger, Guid attemptId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Exception querying provider status for PaymentAttempt {AttemptId} with provider {Provider}")]
    private static partial void LogStatusQueryException(ILogger logger, Guid attemptId, string provider, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Reconciliation completed for PaymentAttempt {AttemptId} with result {ResultStatus}")]
    private static partial void LogReconciliationCompleted(ILogger logger, Guid attemptId, string resultStatus);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Database error during reconciliation commit for PaymentAttempt {AttemptId}")]
    private static partial void LogReconciliationException(ILogger logger, Guid attemptId, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Error reconciling batch attempt {AttemptId}")]
    private static partial void LogBatchItemException(ILogger logger, Guid attemptId, Exception exception);
}
