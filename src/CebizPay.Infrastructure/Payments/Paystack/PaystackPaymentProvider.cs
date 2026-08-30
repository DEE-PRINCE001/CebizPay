using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Paystack;

/// <summary>
/// Infrastructure payment provider adapter for Paystack.
/// Implements <see cref="IPaymentProvider"/>, <see cref="IVirtualAccountProvider"/>, and <see cref="ICardPaymentProvider"/>
/// without leaking Paystack-specific recipient codes or details to upper layers.
/// </summary>
public sealed partial class PaystackPaymentProvider : IPaymentProvider, IVirtualAccountProvider, ICardPaymentProvider
{
    private readonly PaystackClient _client;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PaystackPaymentProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaystackPaymentProvider"/> class.
    /// </summary>
    public PaystackPaymentProvider(
        PaystackClient client,
        ApplicationDbContext dbContext,
        ILogger<PaystackPaymentProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public PaymentProvider Provider => PaymentProvider.Paystack;

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

        var accountName = string.IsNullOrWhiteSpace(bankTransfer.DestinationAccountName)
            ? "Beneficiary"
            : bankTransfer.DestinationAccountName;

        // Step 1: Create or resolve recipient on Paystack (internal infrastructure workflow)
        var recipientCode = await _client.CreateRecipientAsync(
            accountName: accountName,
            accountNumber: bankTransfer.DestinationAccountNumber,
            bankCode: bankTransfer.DestinationBankCode,
            currency: attempt.Currency.ToString(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(recipientCode))
        {
            LogRecipientCreationFailure(_logger, attempt.Id);
            return PaymentProviderResult.BusinessFailure(
                "RECIPIENT_CREATION_FAILED",
                "Failed to register beneficiary recipient with payment gateway.");
        }

        // Step 2: Initiate transfer using recipient code
        var narration = $"CebizPay Payout {attempt.RequestReference}";

        return await _client.InitiateTransferAsync(
            recipientCode: recipientCode,
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

    /// <inheritdoc/>
    public async Task<VirtualAccountCreationResult> CreateVirtualAccountAsync(
        VirtualAccountCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: Create or resolve customer in Paystack
        var names = (request.AccountName ?? "Customer").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = names.Length > 0 ? names[0] : "Customer";
        var lastName = names.Length > 1 ? names[1] : "CebizPay";

        var customerCode = await _client.CreateCustomerAsync(
            email: request.Email,
            firstName: firstName,
            lastName: lastName,
            phone: request.PhoneNumber,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(customerCode))
        {
            return VirtualAccountCreationResult.Failure("Failed to register customer with Paystack.");
        }

        // Step 2: Provision Dedicated NUBAN Account
        return await _client.CreateDedicatedVirtualAccountAsync(
            customerCode: customerCode,
            accountName: request.AccountName ?? "Customer",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<VirtualAccountStatusResult> GetVirtualAccountStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new VirtualAccountStatusResult(true, null));
    }

    /// <inheritdoc/>
    public Task<CardPaymentInitializationResult> InitializeCardPaymentAsync(
        CardPaymentInitializationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _client.InitializeTransactionAsync(
            amount: request.Amount,
            email: request.Email,
            reference: request.Reference,
            callbackUrl: request.CallbackUrl,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentProviderResult> GetCardPaymentStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new ArgumentException("ProviderReference is required.", nameof(providerReference));
        }

        return _client.VerifyTransactionAsync(providerReference, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CardChargeResult> ChargeSavedCardAsync(
        CardSavedChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _client.ChargeAuthorizationAsync(
            authorizationCode: request.ProviderToken,
            email: request.Email,
            amount: request.Amount,
            reference: request.Reference,
            currency: request.Currency.ToString(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CardRefundResult> RefundCardPaymentAsync(
        CardRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _client.RefundTransactionAsync(
            transactionReferenceOrId: request.ProviderTransactionReference,
            amount: request.Amount,
            currency: request.Currency.ToString(),
            merchantNote: request.Reason,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CardVerificationResult> VerifyCardAsync(
        CardVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var initResult = await _client.InitializeTransactionAsync(
            amount: request.Amount > 0 ? request.Amount : 50m, // Paystack nominal verification charge
            email: request.Email,
            reference: request.Reference,
            callbackUrl: request.CallbackUrl,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (initResult.Succeeded && !string.IsNullOrWhiteSpace(initResult.AuthorizationUrl))
        {
            return CardVerificationResult.Success(initResult.AuthorizationUrl, request.Reference);
        }

        return CardVerificationResult.Failure(initResult.ErrorMessage ?? "Paystack card verification session initialization failed.");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Parent BankTransfer not found for PaymentAttempt {AttemptId} (LedgerTransactionId: {TxId})")]
    private static partial void LogBankTransferNotFound(ILogger logger, Guid attemptId, Guid txId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failed to create Paystack transfer recipient for attempt {AttemptId}")]
    private static partial void LogRecipientCreationFailure(ILogger logger, Guid attemptId);
}
