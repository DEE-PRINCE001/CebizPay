using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
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
/// </summary>
public sealed partial class PaymentProviderBankTransferExecutor : IBankTransferExecutor
{
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentProviderBankTransferExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentProviderBankTransferExecutor"/> class.
    /// </summary>
    public PaymentProviderBankTransferExecutor(
        IPaymentProviderFactory providerFactory,
        ApplicationDbContext dbContext,
        IOutboxService outboxService,
        ILogger<PaymentProviderBankTransferExecutor> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(BankTransfer transfer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        // Determine active primary provider (Flutterwave by default)
        var providerType = PaymentProvider.Flutterwave;
        var provider = _providerFactory.GetProvider(providerType);

        // Determine sequential attempt number for this financial transaction
        var existingAttemptsCount = await _dbContext.PaymentAttempts
            .CountAsync(p => p.LedgerTransactionId == transfer.LedgerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        var attemptNumber = existingAttemptsCount + 1;
        var requestReference = $"CBZPA-{transfer.Reference}-{attemptNumber}";

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

        // Step 2: Transition PaymentAttempt to PROCESSING
        attempt.MarkProcessing();
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 3: Publish Outbox Event for Processing
        _outboxService.Write(new PaymentAttemptProcessingEvent(
            PaymentAttemptId: attempt.Id,
            LedgerTransactionId: transfer.LedgerTransactionId,
            Provider: providerType,
            AttemptNumber: attemptNumber,
            RequestReference: requestReference,
            OccurredOnUtc: DateTime.UtcNow));

        // Step 4: Dispatch to provider
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

        // Step 5: Update PaymentAttempt based on provider result
        switch (result.Status)
        {
            case PaymentProviderResultStatus.Success:
                attempt.MarkSucceeded(result.ProviderReference ?? requestReference, safeMetadata: result.SafeMetadata);
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
                attempt.MarkFailed(result.FailureCode, result.FailureReason ?? "Business rejection", safeMetadata: result.SafeMetadata);
                _outboxService.Write(new PaymentAttemptFailedEvent(
                    PaymentAttemptId: attempt.Id,
                    LedgerTransactionId: transfer.LedgerTransactionId,
                    Provider: providerType,
                    AttemptNumber: attemptNumber,
                    RequestReference: requestReference,
                    FailureCode: result.FailureCode,
                    FailureReason: result.FailureReason ?? "Business rejection",
                    OccurredOnUtc: DateTime.UtcNow));
                break;

            case PaymentProviderResultStatus.TechnicalFailure:
                attempt.MarkFailed(result.FailureCode, result.FailureReason ?? "Technical failure", safeMetadata: result.SafeMetadata);
                _outboxService.Write(new PaymentAttemptFailedEvent(
                    PaymentAttemptId: attempt.Id,
                    LedgerTransactionId: transfer.LedgerTransactionId,
                    Provider: providerType,
                    AttemptNumber: attemptNumber,
                    RequestReference: requestReference,
                    FailureCode: result.FailureCode,
                    FailureReason: result.FailureReason ?? "Technical failure",
                    OccurredOnUtc: DateTime.UtcNow));
                break;

            case PaymentProviderResultStatus.Unknown:
            default:
                attempt.MarkUnknown(result.FailureReason ?? "Timeout / Indeterminate outcome", safeMetadata: result.SafeMetadata);
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception dispatching transfer {Reference} to provider {Provider}")]
    private static partial void LogDispatchException(ILogger logger, string reference, string provider, Exception exception);
}
