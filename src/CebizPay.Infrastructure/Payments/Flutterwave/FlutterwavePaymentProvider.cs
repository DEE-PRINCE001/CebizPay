using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Flutterwave;

/// <summary>
/// Infrastructure payment provider adapter for Flutterwave.
/// Implements <see cref="IPaymentProvider"/> without leaking Flutterwave-specific details to upper layers.
/// </summary>
public sealed partial class FlutterwavePaymentProvider : IPaymentProvider
{
    private readonly FlutterwaveClient _client;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<FlutterwavePaymentProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlutterwavePaymentProvider"/> class.
    /// </summary>
    public FlutterwavePaymentProvider(
        FlutterwaveClient client,
        ApplicationDbContext dbContext,
        ILogger<FlutterwavePaymentProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public PaymentProvider Provider => PaymentProvider.Flutterwave;

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> InitializePaymentAsync(
        PaymentAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        // Fetch parent bank transfer details from DB using LedgerTransactionId
        var bankTransfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(t => t.LedgerTransactionId == attempt.LedgerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (bankTransfer == null)
        {
            LogBankTransferNotFound(_logger, attempt.Id, attempt.LedgerTransactionId);

            return PaymentProviderResult.BusinessFailure(
                "TRANSACTION_NOT_FOUND",
                $"BankTransfer associated with LedgerTransactionId '{attempt.LedgerTransactionId}' was not found.");
        }

        var narration = $"CebizPay Payout {attempt.RequestReference}";

        return await _client.InitiateTransferAsync(
            bankCode: bankTransfer.DestinationBankCode,
            accountNumber: bankTransfer.DestinationAccountNumber,
            amount: attempt.Amount,
            currency: attempt.Currency.ToString(),
            reference: attempt.RequestReference,
            narration: narration,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<PaymentProviderResult> GetPaymentStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new ArgumentException("ProviderReference is required.", nameof(providerReference));
        }

        return _client.GetTransferStatusAsync(providerReference, cancellationToken);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Parent BankTransfer not found for PaymentAttempt {AttemptId} (LedgerTransactionId: {TxId})")]
    private static partial void LogBankTransferNotFound(ILogger logger, Guid attemptId, Guid txId);
}
