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
/// Infrastructure implementation of <see cref="IBankTransferExecutor"/> managing external payment provider dispatch
/// and the sequential <see cref="PaymentAttempt"/> lifecycle.
/// Primary: Monnify -> Technical Failure Fallback: Flutterwave -> Technical Failure Fallback: Paystack.
/// Business Failure terminates transfer and reverses clearing debit.
/// UNKNOWN keeps clearing hold and requires reconciliation.
/// </summary>
public sealed partial class PaymentProviderBankTransferExecutor : IBankTransferExecutor
{
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly IPaymentRoutingService? _routingService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService? _ledgerPostingService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentProviderBankTransferExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentProviderBankTransferExecutor"/> class.
    /// </summary>
    public PaymentProviderBankTransferExecutor(
        IPaymentProviderFactory providerFactory,
        IPaymentRoutingService? routingService,
        ApplicationDbContext dbContext,
        ILedgerPostingService? ledgerPostingService,
        IOutboxService outboxService,
        ILogger<PaymentProviderBankTransferExecutor> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _routingService = routingService;
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService;
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Backward-compatible constructor for testing and legacy registrations.
    /// </summary>
    public PaymentProviderBankTransferExecutor(
        IPaymentProviderFactory providerFactory,
        ApplicationDbContext dbContext,
        ILedgerPostingService? ledgerPostingService,
        IOutboxService outboxService,
        ILogger<PaymentProviderBankTransferExecutor> logger)
        : this(providerFactory, null, dbContext, ledgerPostingService, outboxService, logger)
    {
    }

    /// <summary>
    /// Backward-compatible constructor for testing.
    /// </summary>
    public PaymentProviderBankTransferExecutor(
        IPaymentProviderFactory providerFactory,
        ApplicationDbContext dbContext,
        IOutboxService outboxService,
        ILogger<PaymentProviderBankTransferExecutor> logger)
        : this(providerFactory, null, dbContext, null, outboxService, logger)
    {
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(BankTransfer transfer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        // Determine active primary provider via capability router (Monnify -> Flutterwave -> Paystack)
        var providerType = _routingService?.ResolvePrimaryProvider(PaymentCapability.BankTransfer)
            ?? PaymentProvider.Monnify;
        var provider = _providerFactory.GetProvider(providerType);

        // Determine sequential attempt number for this financial transaction
        var existingAttemptsCount = await _dbContext.PaymentAttempts
            .CountAsync(p => p.LedgerTransactionId == transfer.LedgerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        var attemptNumber = existingAttemptsCount + 1;
        var requestReference = $"CBZBT-{transfer.Reference}-A{attemptNumber}-{providerType.ToString().ToUpperInvariant()}";

        // Step 1: Create PaymentAttempt in CREATED status
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: transfer.LedgerTransactionId,
            provider: providerType,
            attemptNumber: attemptNumber,
            requestReference: requestReference,
            amount: transfer.Amount,
            currency: transfer.Currency);

        _dbContext.PaymentAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 2: Transition PaymentAttempt to PROCESSING and BankTransfer to PROCESSING
        attempt.MarkProcessing();
        if (transfer.Status == BankTransferStatus.Pending)
        {
            transfer.MarkProcessing();
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 3: Publish Outbox Event for Processing
        _outboxService.Write(new PaymentAttemptProcessingEvent(
            PaymentAttemptId: attempt.Id,
            LedgerTransactionId: transfer.LedgerTransactionId,
            Provider: providerType,
            AttemptNumber: attemptNumber,
            RequestReference: requestReference,
            OccurredOnUtc: DateTime.UtcNow));

        // Step 4: Dispatch to provider (HTTP call outside DB transaction)
        PaymentProviderResult result;
        try
        {
            result = await provider.InitializePaymentAsync(attempt, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogDispatchException(_logger, transfer.Reference, providerType.ToString(), ex);
            result = PaymentProviderResult.Unknown($"Exception during gateway communication: {ex.Message}");
        }

        // Step 5: Update PaymentAttempt & BankTransfer based on provider result
        switch (result.Status)
        {
            case PaymentProviderResultStatus.Success:
                attempt.MarkSucceeded(result.ProviderReference ?? requestReference, safeMetadata: result.SafeMetadata);

                if (transfer.Status != BankTransferStatus.Completed)
                {
                    transfer.MarkCompleted(DateTime.UtcNow, result.ProviderReference ?? requestReference);

                    _outboxService.Write(new BankTransferCompletedEvent(
                        TransferId: transfer.Id,
                        TransactionReference: transfer.Reference,
                        ProviderReference: result.ProviderReference ?? requestReference,
                        OccurredOnUtc: DateTime.UtcNow));

                    RecordAudit(AuditActions.BankTransferCompleted, AuditResourceTypes.BankTransfer, transfer.Id.ToString(),
                        JsonSerializer.Serialize(new { transfer.Reference, ProviderReference = result.ProviderReference }));
                }

                _outboxService.Write(new PaymentAttemptSucceededEvent(
                    PaymentAttemptId: attempt.Id,
                    LedgerTransactionId: transfer.LedgerTransactionId,
                    Provider: providerType,
                    AttemptNumber: attemptNumber,
                    RequestReference: requestReference,
                    ProviderReference: result.ProviderReference ?? requestReference,
                    Amount: attempt.Amount,
                    Currency: attempt.Currency.ToString(),
                    OccurredOnUtc: DateTime.UtcNow));
                break;

            case PaymentProviderResultStatus.BusinessFailure:
                var busFailReason = result.FailureReason ?? "Business rejection";
                attempt.MarkFailed(result.FailureCode, busFailReason, safeMetadata: result.SafeMetadata);

                // Business failure is terminal -> execute definitive ledger reversal
                if (transfer.Status != BankTransferStatus.Failed)
                {
                    if (_ledgerPostingService != null)
                    {
                        await _ledgerPostingService.PostBankTransferReversalCoreAsync(transfer.Id, busFailReason, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        transfer.MarkFailed(busFailReason);
                    }

                    _outboxService.Write(new BankTransferFailedEvent(
                        TransferId: transfer.Id,
                        TransactionReference: transfer.Reference,
                        Reason: busFailReason,
                        OccurredOnUtc: DateTime.UtcNow));

                    RecordAudit(AuditActions.BankTransferReversed, AuditResourceTypes.BankTransfer, transfer.Id.ToString(),
                        JsonSerializer.Serialize(new { transfer.Reference, Reason = busFailReason }));
                }

                _outboxService.Write(new PaymentAttemptFailedEvent(
                    PaymentAttemptId: attempt.Id,
                    LedgerTransactionId: transfer.LedgerTransactionId,
                    Provider: providerType,
                    AttemptNumber: attemptNumber,
                    RequestReference: requestReference,
                    FailureCode: result.FailureCode,
                    FailureReason: busFailReason,
                    OccurredOnUtc: DateTime.UtcNow));
                break;

            case PaymentProviderResultStatus.TechnicalFailure:
                var techFailReason = result.FailureReason ?? "Technical failure";
                attempt.MarkFailed(result.FailureCode, techFailReason, safeMetadata: result.SafeMetadata);

                _outboxService.Write(new PaymentAttemptFailedEvent(
                    PaymentAttemptId: attempt.Id,
                    LedgerTransactionId: transfer.LedgerTransactionId,
                    Provider: providerType,
                    AttemptNumber: attemptNumber,
                    RequestReference: requestReference,
                    FailureCode: result.FailureCode,
                    FailureReason: techFailReason,
                    OccurredOnUtc: DateTime.UtcNow));
                break;

            case PaymentProviderResultStatus.Unknown:
            default:
                attempt.MarkUnknown(result.FailureReason ?? "Timeout / Indeterminate outcome", safeMetadata: result.SafeMetadata);
                if (transfer.Status != BankTransferStatus.Completed && transfer.Status != BankTransferStatus.Failed)
                {
                    transfer.MarkUnknown(result.FailureReason);
                }

                _outboxService.Write(new PaymentAttemptUnknownEvent(
                    PaymentAttemptId: attempt.Id,
                    LedgerTransactionId: transfer.LedgerTransactionId,
                    Provider: providerType,
                    AttemptNumber: attemptNumber,
                    RequestReference: requestReference,
                    Reason: result.FailureReason,
                    OccurredOnUtc: DateTime.UtcNow));
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception dispatching transfer {Reference} to provider {Provider}")]
    private static partial void LogDispatchException(ILogger logger, string reference, string provider, Exception exception);
}
