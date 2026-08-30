using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Provider-backed implementation of <see cref="IBankAccountResolver"/> resolving bank account beneficiary names
/// via external payment providers (Monnify / Flutterwave / Paystack) with capability routing and strict format pre-validation.
/// </summary>
public sealed partial class PaymentProviderBankAccountResolver : IBankAccountResolver
{
    private readonly IMonnifyClient? _monnifyClient;
    private readonly FlutterwaveClient _flutterwaveClient;
    private readonly PaystackClient _paystackClient;
    private readonly IPaymentRoutingService? _routingService;
    private readonly ILogger<PaymentProviderBankAccountResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentProviderBankAccountResolver"/> class.
    /// </summary>
    public PaymentProviderBankAccountResolver(
        IMonnifyClient monnifyClient,
        FlutterwaveClient flutterwaveClient,
        PaystackClient paystackClient,
        IPaymentRoutingService? routingService,
        ILogger<PaymentProviderBankAccountResolver> logger)
    {
        _monnifyClient = monnifyClient;
        _flutterwaveClient = flutterwaveClient ?? throw new ArgumentNullException(nameof(flutterwaveClient));
        _paystackClient = paystackClient ?? throw new ArgumentNullException(nameof(paystackClient));
        _routingService = routingService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Backward-compatible constructor for testing and legacy registrations.
    /// </summary>
    public PaymentProviderBankAccountResolver(
        FlutterwaveClient flutterwaveClient,
        PaystackClient paystackClient,
        ILogger<PaymentProviderBankAccountResolver> logger)
        : this(null!, flutterwaveClient, paystackClient, null, logger)
    {
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

        var route = _routingService?.GetRoute(PaymentCapability.BankAccountResolution)
            ?? new[] { PaymentProvider.Monnify, PaymentProvider.Flutterwave, PaymentProvider.Paystack };

        foreach (var provider in route)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var result = provider switch
                {
                    PaymentProvider.Monnify when _monnifyClient != null =>
                        await _monnifyClient.ResolveAccountAsync(cleanBankCode, cleanAccountNumber, cancellationToken).ConfigureAwait(false),
                    PaymentProvider.Flutterwave =>
                        await _flutterwaveClient.ResolveAccountAsync(cleanBankCode, cleanAccountNumber, cancellationToken).ConfigureAwait(false),
                    PaymentProvider.Paystack =>
                        await _paystackClient.ResolveAccountAsync(cleanBankCode, cleanAccountNumber, cancellationToken).ConfigureAwait(false),
                    _ => null
                };

                if (result != null && result.Succeeded && !string.IsNullOrWhiteSpace(result.AccountName))
                {
                    return result;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var providerName = provider.ToString();
                LogProviderResolutionWarning(_logger, providerName, ex);
            }
        }

        return new BankAccountResolutionResult(
            Succeeded: false,
            AccountName: null,
            BankCode: cleanBankCode,
            AccountNumber: cleanAccountNumber,
            ErrorMessage: "Failed to resolve destination bank account name from available payment providers.");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "{Provider} account resolution encountered an error. Attempting next provider in route.")]
    private static partial void LogProviderResolutionWarning(ILogger logger, string provider, Exception exception);
}
