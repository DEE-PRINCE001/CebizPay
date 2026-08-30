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
/// Infrastructure implementation of <see cref="IPaymentFailoverService"/> orchestrating safe,
/// sequential fallback provider dispatch in accordance with locked failover rules:
/// MONNIFY -> FLUTTERWAVE -> PAYSTACK (Technical Failure only).
/// UNKNOWN requires reconciliation first; BUSINESS FAILURE prohibits failover.
/// </summary>
public sealed partial class PaymentFailoverService : IPaymentFailoverService
{
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly IPaymentRoutingService? _routingService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentFailoverService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentFailoverService"/> class.
    /// </summary>
    public PaymentFailoverService(
        IPaymentProviderFactory providerFactory,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outboxService,
        ILogger<PaymentFailoverService> logger)
        : this(providerFactory, null, dbContext, ledgerPostingService, outboxService, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentFailoverService"/> class with capability routing support.
    /// </summary>
    public PaymentFailoverService(
        IPaymentProviderFactory providerFactory,
        IPaymentRoutingService? routingService,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outboxService,
        ILogger<PaymentFailoverService> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _routingService = routingService;
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService ?? throw new ArgumentNullException(nameof(ledgerPostingService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PaymentFailoverResult> FailoverAsync(
        Guid ledgerTransactionId,
        CancellationToken cancellationToken = default)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));

        // Fetch all existing attempts for this financial transaction ordered by attempt number
        var existingAttempts = await _dbContext.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTransactionId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingAttempts.Count == 0)
        {
            LogNoAttemptsFound(_logger, ledgerTransactionId);
            return PaymentFailoverResult.Failure($"No payment attempts found for transaction '{ledgerTransactionId}'.");
        }

        // Rule: If any attempt succeeded, NEVER retry or failover
        if (existingAttempts.Any(p => p.Status == PaymentAttemptStatus.Succeeded))
        {
            LogFailoverRejectedSuccess(_logger, ledgerTransactionId);
            return PaymentFailoverResult.Failure("Transaction already succeeded. Failover is prohibited.");
        }

        // Inspect latest attempt
        var latestAttempt = existingAttempts.Last();

        // Rule: UNKNOWN / PROCESSING must be reconciled first — NEVER immediately fail over
        if (latestAttempt.Status == PaymentAttemptStatus.Unknown || latestAttempt.Status == PaymentAttemptStatus.Processing)
        {
            LogFailoverRejectedUnknown(_logger, ledgerTransactionId, latestAttempt.Id);
            return PaymentFailoverResult.Failure("Latest attempt outcome is UNKNOWN or PROCESSING. Reconcile current provider before failover.");
        }

        // Rule: BUSINESS FAILURE is a terminal customer/data failure — NEVER failover
        if (latestAttempt.Status == PaymentAttemptStatus.Failed &&
            !string.IsNullOrWhiteSpace(latestAttempt.FailureCode) &&
            IsBusinessFailureCode(latestAttempt.FailureCode))
        {
            LogFailoverRejectedBusinessFailure(_logger, ledgerTransactionId, latestAttempt.Id);
            return PaymentFailoverResult.Failure("Latest attempt failed due to Business Rejection. Automatic failover is prohibited.");
        }

        // Determine fallback provider via capability router or default
        PaymentProvider fallbackProviderType;
        if (_routingService != null)
        {
            var nextFallback = _routingService.GetNextFallbackProvider(PaymentCapability.BankTransfer, latestAttempt.Provider);
            if (!nextFallback.HasValue)
            {
                var currentProviderStr = latestAttempt.Provider.ToString();
                LogFailoverRejectedNotPrimary(_logger, ledgerTransactionId, currentProviderStr);
                return PaymentFailoverResult.Failure($"No further fallback provider is configured or available for '{currentProviderStr}'.");
            }
            fallbackProviderType = nextFallback.Value;
        }
        else
        {
            fallbackProviderType = latestAttempt.Provider switch
            {
                PaymentProvider.Monnify => PaymentProvider.Flutterwave,
                PaymentProvider.Flutterwave => PaymentProvider.Paystack,
                _ => throw new InvalidOperationException($"No default fallback configured for provider {latestAttempt.Provider}.")
            };
        }

        // Rule: Concurrency protection — check if fallback attempt is already in progress or completed
        var fallbackAttempt = existingAttempts.FirstOrDefault(p => p.Provider == fallbackProviderType);
        if (fallbackAttempt != null)
        {
            LogFallbackAlreadyExists(_logger, ledgerTransactionId, fallbackAttempt.Id);
            return PaymentFailoverResult.Failure($"Fallback attempt already exists with ID '{fallbackAttempt.Id}'. Duplicate failover prohibited.");
        }

        var bankTransfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(b => b.LedgerTransactionId == ledgerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (bankTransfer == null)
        {
            LogBankTransferNotFound(_logger, ledgerTransactionId);
            return PaymentFailoverResult.Failure($"BankTransfer not found for LedgerTransactionId '{ledgerTransactionId}'.");
        }

        var nextAttemptNumber = existingAttempts.Count + 1;
        var requestReference = $"CBZBT-{bankTransfer.Reference}-A{nextAttemptNumber}-{fallbackProviderType.ToString().ToUpperInvariant()}";

        // Step 1: Create fallback PaymentAttempt in CREATED status
        var newAttempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTransactionId,
            provider: fallbackProviderType,
            attemptNumber: nextAttemptNumber,
            requestReference: requestReference,
            amount: bankTransfer.Amount,
            currency: bankTransfer.Currency);

        _dbContext.PaymentAttempts.Add(newAttempt);

        var fromProviderName = latestAttempt.Provider.ToString();
        var fallbackProviderName = fallbackProviderType.ToString();

        // Record Audit & Outbox for Failover Initiation
        RecordAudit(AuditActions.ProviderFailoverInitiated, AuditResourceTypes.PaymentFailover, ledgerTransactionId.ToString(),
            JsonSerializer.Serialize(new
            {
                LedgerTransactionId = ledgerTransactionId,
                FromProvider = fromProviderName,
                ToProvider = fallbackProviderName,
                AttemptNumber = nextAttemptNumber
            }));

        _outboxService.Write(new ProviderFailoverStartedEvent(
            LedgerTransactionId: ledgerTransactionId,
            FailedProvider: latestAttempt.Provider,
            FallbackProvider: fallbackProviderType,
            PreviousAttemptNumber: latestAttempt.AttemptNumber,
            NewAttemptNumber: nextAttemptNumber,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 2: Transition fallback attempt to PROCESSING
        newAttempt.MarkProcessing();
        _outboxService.Write(new PaymentAttemptProcessingEvent(
            PaymentAttemptId: newAttempt.Id,
            LedgerTransactionId: ledgerTransactionId,
            Provider: fallbackProviderType,
            AttemptNumber: nextAttemptNumber,
            RequestReference: requestReference,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 3: Dispatch to fallback provider
        var fallbackProvider = _providerFactory.GetProvider(fallbackProviderType);
        PaymentProviderResult result;
        try
        {
            result = await fallbackProvider.InitializePaymentAsync(newAttempt, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFallbackDispatchException(_logger, bankTransfer.Reference, fallbackProviderName, ex);
            result = PaymentProviderResult.Unknown($"Exception during fallback dispatch: {ex.Message}");
        }

        // Step 4: Update fallback attempt state
        switch (result.Status)
        {
            case PaymentProviderResultStatus.Success:
                newAttempt.MarkSucceeded(result.ProviderReference ?? requestReference, safeMetadata: result.SafeMetadata);

                if (bankTransfer.Status != BankTransferStatus.Completed)
                {
                    bankTransfer.MarkCompleted(DateTime.UtcNow, result.ProviderReference ?? requestReference);

                    _outboxService.Write(new BankTransferCompletedEvent(
                        TransferId: bankTransfer.Id,
                        TransactionReference: bankTransfer.Reference,
                        ProviderReference: result.ProviderReference ?? requestReference,
                        OccurredOnUtc: DateTime.UtcNow));

                    RecordAudit(AuditActions.BankTransferCompleted, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                        JsonSerializer.Serialize(new { bankTransfer.Reference, ProviderReference = result.ProviderReference }));
                }

                _outboxService.Write(new ProviderFailoverSucceededEvent(
                    LedgerTransactionId: ledgerTransactionId,
                    FallbackAttemptId: newAttempt.Id,
                    FallbackProvider: fallbackProviderType,
                    ProviderReference: result.ProviderReference ?? requestReference,
                    OccurredOnUtc: DateTime.UtcNow));

                RecordAudit(AuditActions.ProviderFailoverSucceeded, AuditResourceTypes.PaymentFailover, ledgerTransactionId.ToString(),
                    JsonSerializer.Serialize(new { FallbackAttemptId = newAttempt.Id, ProviderReference = result.ProviderReference }));
                break;

            case PaymentProviderResultStatus.BusinessFailure:
                var busFailReason = result.FailureReason ?? "Fallback attempt rejected by provider";
                newAttempt.MarkFailed(result.FailureCode, busFailReason, safeMetadata: result.SafeMetadata);

                // Business failure on fallback -> definitive failure and ledger reversal
                if (bankTransfer.Status != BankTransferStatus.Failed)
                {
                    await _ledgerPostingService.PostBankTransferReversalCoreAsync(bankTransfer.Id, busFailReason, cancellationToken).ConfigureAwait(false);

                    _outboxService.Write(new BankTransferFailedEvent(
                        TransferId: bankTransfer.Id,
                        TransactionReference: bankTransfer.Reference,
                        Reason: busFailReason,
                        OccurredOnUtc: DateTime.UtcNow));

                    RecordAudit(AuditActions.BankTransferReversed, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                        JsonSerializer.Serialize(new { bankTransfer.Reference, Reason = busFailReason }));
                }

                _outboxService.Write(new ProviderFailoverFailedEvent(
                    LedgerTransactionId: ledgerTransactionId,
                    FallbackProvider: fallbackProviderType,
                    FailureReason: busFailReason,
                    OccurredOnUtc: DateTime.UtcNow));

                RecordAudit(AuditActions.ProviderFailoverFailed, AuditResourceTypes.PaymentFailover, ledgerTransactionId.ToString(),
                    JsonSerializer.Serialize(new { FallbackAttemptId = newAttempt.Id, Reason = busFailReason }));
                break;

            case PaymentProviderResultStatus.TechnicalFailure:
                var techFailReason = result.FailureReason ?? "Technical failure on fallback provider";
                newAttempt.MarkFailed(result.FailureCode, techFailReason, safeMetadata: result.SafeMetadata);

                // Check if further fallback is possible
                var hasFurtherFallback = _routingService?.GetNextFallbackProvider(PaymentCapability.BankTransfer, fallbackProviderType).HasValue ?? false;
                if (!hasFurtherFallback && bankTransfer.Status != BankTransferStatus.Failed)
                {
                    await _ledgerPostingService.PostBankTransferReversalCoreAsync(bankTransfer.Id, techFailReason, cancellationToken).ConfigureAwait(false);

                    _outboxService.Write(new BankTransferFailedEvent(
                        TransferId: bankTransfer.Id,
                        TransactionReference: bankTransfer.Reference,
                        Reason: techFailReason,
                        OccurredOnUtc: DateTime.UtcNow));

                    RecordAudit(AuditActions.BankTransferReversed, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                        JsonSerializer.Serialize(new { bankTransfer.Reference, Reason = techFailReason }));
                }

                _outboxService.Write(new ProviderFailoverFailedEvent(
                    LedgerTransactionId: ledgerTransactionId,
                    FallbackProvider: fallbackProviderType,
                    FailureReason: techFailReason,
                    OccurredOnUtc: DateTime.UtcNow));

                RecordAudit(AuditActions.ProviderFailoverFailed, AuditResourceTypes.PaymentFailover, ledgerTransactionId.ToString(),
                    JsonSerializer.Serialize(new { FallbackAttemptId = newAttempt.Id, Reason = techFailReason }));
                break;

            case PaymentProviderResultStatus.Unknown:
            default:
                newAttempt.MarkUnknown(result.FailureReason ?? "Fallback outcome unknown", safeMetadata: result.SafeMetadata);
                if (bankTransfer.Status != BankTransferStatus.Completed && bankTransfer.Status != BankTransferStatus.Failed)
                {
                    bankTransfer.MarkUnknown(result.FailureReason);
                }

                _outboxService.Write(new PaymentAttemptUnknownEvent(
                    PaymentAttemptId: newAttempt.Id,
                    LedgerTransactionId: ledgerTransactionId,
                    Provider: fallbackProviderType,
                    AttemptNumber: nextAttemptNumber,
                    RequestReference: requestReference,
                    Reason: result.FailureReason,
                    OccurredOnUtc: DateTime.UtcNow));
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var resultStatusStr = result.Status.ToString();
        LogFailoverCompleted(_logger, ledgerTransactionId, fallbackProviderName, resultStatusStr);
        return PaymentFailoverResult.Success(newAttempt.Id, fallbackProviderType, result.Status);
    }

    private static bool IsBusinessFailureCode(string code)
    {
        return string.Equals(code, "BUSINESS_REJECTION", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "INVALID_ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "INVALID_BANK", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "BLOCKED_ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "ACCOUNT_BLOCKED", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "INSUFFICIENT_FUNDS", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "LIMIT_EXCEEDED", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "PAYMENT_FAILED", StringComparison.OrdinalIgnoreCase);
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "No payment attempts found for LedgerTransactionId {LedgerTransactionId}")]
    private static partial void LogNoAttemptsFound(ILogger logger, Guid ledgerTransactionId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failover rejected for {LedgerTransactionId}: Transaction already succeeded.")]
    private static partial void LogFailoverRejectedSuccess(ILogger logger, Guid ledgerTransactionId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Fallback attempt already exists for {LedgerTransactionId}, AttemptId: {AttemptId}")]
    private static partial void LogFallbackAlreadyExists(ILogger logger, Guid ledgerTransactionId, Guid attemptId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Failover rejected for {LedgerTransactionId}: Primary attempt {AttemptId} is UNKNOWN/PROCESSING. Reconcile first.")]
    private static partial void LogFailoverRejectedUnknown(ILogger logger, Guid ledgerTransactionId, Guid attemptId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failover rejected for {LedgerTransactionId}: Primary attempt {AttemptId} failed due to Business Rejection.")]
    private static partial void LogFailoverRejectedBusinessFailure(ILogger logger, Guid ledgerTransactionId, Guid attemptId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Failover rejected for {LedgerTransactionId}: Current provider is {Provider}, only configured failovers are permitted.")]
    private static partial void LogFailoverRejectedNotPrimary(ILogger logger, Guid ledgerTransactionId, string provider);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "BankTransfer not found for LedgerTransactionId {LedgerTransactionId}")]
    private static partial void LogBankTransferNotFound(ILogger logger, Guid ledgerTransactionId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Exception dispatching fallback transfer {Reference} to provider {Provider}")]
    private static partial void LogFallbackDispatchException(ILogger logger, string reference, string provider, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Failover completed for {LedgerTransactionId} with provider {Provider}, result: {ResultStatus}")]
    private static partial void LogFailoverCompleted(ILogger logger, Guid ledgerTransactionId, string provider, string resultStatus);
}
