using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Monnify.Models;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Monnify;

/// <summary>
/// Infrastructure adapter implementing <see cref="IVirtualAccountProvider"/> and <see cref="IPaymentProvider"/> for Monnify.
/// Translates domain/application models to Monnify API calls and normalizes provider responses.
/// </summary>
public sealed partial class MonnifyPaymentProvider : IVirtualAccountProvider, IPaymentProvider
{
    private readonly IMonnifyClient _client;
    private readonly ApplicationDbContext? _dbContext;
    private readonly MonnifyOptions _options;
    private readonly ILogger<MonnifyPaymentProvider> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MonnifyPaymentProvider"/>.
    /// </summary>
    public MonnifyPaymentProvider(
        IMonnifyClient client,
        ApplicationDbContext dbContext,
        IOptions<MonnifyOptions> options,
        ILogger<MonnifyPaymentProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _dbContext = dbContext;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MonnifyPaymentProvider"/> for lightweight contexts or testing.
    /// </summary>
    public MonnifyPaymentProvider(
        IMonnifyClient client,
        IOptions<MonnifyOptions> options,
        ILogger<MonnifyPaymentProvider> logger)
        : this(client, null!, options, logger)
    {
    }

    /// <inheritdoc/>
    public PaymentProvider Provider => PaymentProvider.Monnify;

    /// <inheritdoc/>
    public async Task<VirtualAccountCreationResult> CreateVirtualAccountAsync(
        VirtualAccountCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return VirtualAccountCreationResult.Failure("Monnify provider is disabled in configuration.");
        }

        var accountRef = $"CBZ_MNFY_{Guid.NewGuid():N}";

        var monnifyRequest = new MonnifyCreateReservedAccountRequest
        {
            AccountReference = accountRef,
            AccountName = request.AccountName,
            CurrencyCode = request.Currency.ToString(),
            ContractCode = _options.ContractCode,
            CustomerEmail = request.Email,
            CustomerName = request.AccountName,
            Bvn = request.Bvn,
            GetAllAvailableBanks = true
        };

        var response = await _client.CreateReservedAccountAsync(monnifyRequest, cancellationToken).ConfigureAwait(false);
        if (response == null || !response.RequestSuccessful || response.ResponseBody == null)
        {
            var errorMsg = response?.ResponseMessage ?? "Monnify reserved account creation failed.";
            LogProvisioningFailed(_logger, accountRef, errorMsg);
            return VirtualAccountCreationResult.Failure(errorMsg);
        }

        var body = response.ResponseBody;
        var primaryAccount = body.Accounts?.FirstOrDefault();
        if (primaryAccount == null || string.IsNullOrWhiteSpace(primaryAccount.AccountNumber))
        {
            LogNoBankAccountsReturned(_logger, accountRef);
            return VirtualAccountCreationResult.Failure("Monnify succeeded but did not return any allocated bank accounts.");
        }

        var effectiveAccountRef = body.AccountReference ?? accountRef;
        var bankCode = primaryAccount.BankCode ?? "035";
        var bankName = primaryAccount.BankName ?? "Wema Bank";
        var accountName = primaryAccount.AccountName ?? body.AccountName ?? request.AccountName;
        var accountNumber = primaryAccount.AccountNumber;

        LogProvisioningSuccess(_logger, effectiveAccountRef, accountNumber, bankName);
        return VirtualAccountCreationResult.Success(
            accountNumber: accountNumber,
            accountName: accountName,
            bankCode: bankCode,
            bankName: bankName,
            providerReference: effectiveAccountRef);
    }

    /// <inheritdoc/>
    public Task<VirtualAccountStatusResult> GetVirtualAccountStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new VirtualAccountStatusResult(true, null));
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> InitializePaymentAsync(
        PaymentAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (!_options.Enabled)
        {
            return PaymentProviderResult.TechnicalFailure("PROVIDER_DISABLED", "Monnify provider is disabled in configuration.");
        }

        if (_dbContext == null)
        {
            return PaymentProviderResult.TechnicalFailure("DB_UNAVAILABLE", "Database context is required for payment dispatch.");
        }

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
            destinationBankCode: bankTransfer.DestinationBankCode,
            destinationAccountNumber: bankTransfer.DestinationAccountNumber,
            amount: attempt.Amount,
            currency: attempt.Currency.ToString(),
            reference: attempt.RequestReference,
            narration: narration,
            sourceAccountNumber: _options.SourceAccountNumber,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> GetPaymentStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
            return PaymentProviderResult.Unknown("Provider reference is empty.");

        // First attempt transfer status query
        var transferStatus = await _client.GetTransferStatusAsync(providerReference, cancellationToken).ConfigureAwait(false);
        if (transferStatus != null && (transferStatus.Status == PaymentProviderResultStatus.Success ||
            transferStatus.Status == PaymentProviderResultStatus.BusinessFailure))
        {
            return transferStatus;
        }

        // Fallback to transaction details query
        var response = await _client.GetTransactionDetailsAsync(providerReference, cancellationToken).ConfigureAwait(false);
        if (response == null || !response.RequestSuccessful || response.ResponseBody == null)
        {
            return (transferStatus != null && transferStatus.Status != PaymentProviderResultStatus.Unknown)
                ? transferStatus
                : PaymentProviderResult.Unknown(response?.ResponseMessage ?? "Failed to query Monnify transaction status.");
        }

        var status = response.ResponseBody.PaymentStatus?.ToUpperInvariant();
        var txRef = response.ResponseBody.TransactionReference ?? providerReference;
        var safeMeta = JsonSerializer.Serialize(new
        {
            transaction_reference = txRef,
            payment_reference = response.ResponseBody.PaymentReference,
            payment_status = status,
            amount = response.ResponseBody.AmountPaid
        });

        return status switch
        {
            "PAID" or "SUCCESS" or "SUCCESSFUL" => PaymentProviderResult.Success(txRef, safeMeta),
            "FAILED" or "EXPIRED" or "CANCELLED" => PaymentProviderResult.BusinessFailure("PAYMENT_FAILED", "Monnify transaction failed.", safeMeta),
            _ => PaymentProviderResult.Unknown("Monnify payment pending.", safeMeta)
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Monnify reserved account provisioning failed for reference {AccountReference}: {Error}")]
    private static partial void LogProvisioningFailed(ILogger logger, string accountReference, string error);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Monnify reserved account creation returned zero allocated bank accounts for reference {AccountReference}.")]
    private static partial void LogNoBankAccountsReturned(ILogger logger, string accountReference);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Monnify reserved account provisioned successfully. Ref: {AccountReference}, AccountNumber: {AccountNumber}, Bank: {BankName}")]
    private static partial void LogProvisioningSuccess(ILogger logger, string accountReference, string accountNumber, string bankName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "BankTransfer not found for PaymentAttempt {AttemptId} with LedgerTransactionId {LedgerTransactionId}")]
    private static partial void LogBankTransferNotFound(ILogger logger, Guid attemptId, Guid ledgerTransactionId);
}
