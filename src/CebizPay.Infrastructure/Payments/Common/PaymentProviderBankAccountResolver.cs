using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Provider-backed implementation of <see cref="IBankAccountResolver"/> resolving bank account beneficiary names
/// via external payment providers (Flutterwave / Paystack) with strict format pre-validation.
/// </summary>
public sealed partial class PaymentProviderBankAccountResolver : IBankAccountResolver
{
    private readonly FlutterwaveClient _flutterwaveClient;
    private readonly PaystackClient _paystackClient;
    private readonly ILogger<PaymentProviderBankAccountResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentProviderBankAccountResolver"/> class.
    /// </summary>
    public PaymentProviderBankAccountResolver(
        FlutterwaveClient flutterwaveClient,
        PaystackClient paystackClient,
        ILogger<PaymentProviderBankAccountResolver> logger)
    {
        _flutterwaveClient = flutterwaveClient ?? throw new ArgumentNullException(nameof(flutterwaveClient));
        _paystackClient = paystackClient ?? throw new ArgumentNullException(nameof(paystackClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<BankAccountResolutionResult> ResolveAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
        {
            return new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: "Bank code is required.");
        }

        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length != 10 || !accountNumber.Trim().All(char.IsDigit))
        {
            return new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: "Account number must be a 10-digit numeric NUBAN string.");
        }

        var cleanBankCode = bankCode.Trim();
        var cleanAccountNumber = accountNumber.Trim();

        // Attempt 1: Resolve with primary provider (Flutterwave)
        try
        {
            var flwResult = await _flutterwaveClient.ResolveAccountAsync(cleanBankCode, cleanAccountNumber, cancellationToken).ConfigureAwait(false);
            if (flwResult.Succeeded && !string.IsNullOrWhiteSpace(flwResult.AccountName))
            {
                return flwResult;
            }
        }
        catch (Exception ex)
        {
            LogFlutterwaveResolutionWarning(_logger, ex);
        }

        // Attempt 2: Resolve with secondary provider (Paystack)
        try
        {
            var pstkResult = await _paystackClient.ResolveAccountAsync(cleanBankCode, cleanAccountNumber, cancellationToken).ConfigureAwait(false);
            if (pstkResult.Succeeded && !string.IsNullOrWhiteSpace(pstkResult.AccountName))
            {
                return pstkResult;
            }

            return pstkResult;
        }
        catch (Exception ex)
        {
            LogPaystackResolutionError(_logger, ex);
            return new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: cleanBankCode,
                AccountNumber: cleanAccountNumber,
                ErrorMessage: "Failed to resolve destination bank account name from payment providers.");
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Flutterwave account resolution encountered an error. Attempting Paystack fallback.")]
    private static partial void LogFlutterwaveResolutionWarning(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Paystack account resolution encountered an error.")]
    private static partial void LogPaystackResolutionError(ILogger logger, Exception exception);
}
