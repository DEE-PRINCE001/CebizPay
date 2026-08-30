using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Flutterwave;

/// <summary>
/// Resilient HTTP client for interacting with Flutterwave payment APIs.
/// </summary>
public sealed partial class FlutterwaveClient
{
    private readonly HttpClient _httpClient;
    private readonly FlutterwaveOptions _options;
    private readonly ILogger<FlutterwaveClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FlutterwaveClient"/> class.
    /// </summary>
    public FlutterwaveClient(
        HttpClient httpClient,
        IOptions<FlutterwaveOptions> options,
        ILogger<FlutterwaveClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 30);
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey.Trim());
        }
    }

    /// <summary>
    /// Resolves destination bank account name using Flutterwave account resolution API.
    /// </summary>
    public async Task<BankAccountResolutionResult> ResolveAccountAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var cleanBankCode = bankCode.Trim();
        var cleanAccountNumber = accountNumber.Trim();
        var maskedAccount = MaskAccountNumber(cleanAccountNumber);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/accounts/resolve");
        ApplyAuthentication(request);
        request.Content = JsonContent.Create(new FlutterwaveAccountResolveRequest(cleanAccountNumber, cleanBankCode));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FlutterwaveAccountResolveResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
                if (result != null && result.Status == "success" && result.Data != null && !string.IsNullOrWhiteSpace(result.Data.AccountName))
                {
                    PaymentMetrics.RecordAccountResolution("Flutterwave", succeeded: true);
                    LogAccountResolutionSuccess(_logger, cleanBankCode, maskedAccount);

                    return new BankAccountResolutionResult(
                        Succeeded: true,
                        AccountName: result.Data.AccountName.Trim(),
                        BankCode: cleanBankCode,
                        AccountNumber: cleanAccountNumber);
                }
            }

            PaymentMetrics.RecordAccountResolution("Flutterwave", succeeded: false);
            LogAccountResolutionFailed(_logger, cleanBankCode, maskedAccount, (int)response.StatusCode);

            return new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: cleanBankCode,
                AccountNumber: cleanAccountNumber,
                ErrorMessage: string.Format(CultureInfo.InvariantCulture, "Account resolution failed with HTTP {0}.", (int)response.StatusCode));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordAccountResolution("Flutterwave", succeeded: false);
            LogAccountResolutionException(_logger, cleanBankCode, maskedAccount, ex);

            return new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: cleanBankCode,
                AccountNumber: cleanAccountNumber,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Initiates a bank transfer payout through Flutterwave.
    /// </summary>
    public async Task<PaymentProviderResult> InitiateTransferAsync(
        string bankCode,
        string accountNumber,
        decimal amount,
        string currency,
        string reference,
        string narration,
        CancellationToken cancellationToken = default)
    {
        var cleanBankCode = bankCode.Trim();
        var cleanAccountNumber = accountNumber.Trim();
        var cleanReference = reference.Trim();

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/transfers");
        ApplyAuthentication(request);
        request.Headers.Add("Idempotency-Key", cleanReference);

        var payload = new FlutterwaveTransferRequest(
            AccountBank: cleanBankCode,
            AccountNumber: cleanAccountNumber,
            Amount: amount,
            Narration: narration,
            Currency: currency,
            Reference: cleanReference,
            DebitCurrency: currency);

        request.Content = JsonContent.Create(payload);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveTransferResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                var providerRef = parsed.Data.Id > 0 ? parsed.Data.Id.ToString(CultureInfo.InvariantCulture) : parsed.Data.Reference;
                var safeMeta = JsonSerializer.Serialize(new
                {
                    provider_id = parsed.Data.Id,
                    status = parsed.Data.Status,
                    bank_name = parsed.Data.BankName,
                    fee = parsed.Data.Fee
                });

                PaymentMetrics.RecordRequest("Flutterwave", "InitiateTransfer", "Success", stopwatch.Elapsed.TotalMilliseconds);
                LogTransferInitiated(_logger, cleanReference, providerRef ?? cleanReference);

                return PaymentProviderResult.Success(providerRef ?? cleanReference, safeMeta);
            }

            // Handle HTTP 400 / 422 / Business failure
            if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var failReason = parsed?.Message ?? "Validation failed";
                PaymentMetrics.RecordRequest("Flutterwave", "InitiateTransfer", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
                LogBusinessRejection(_logger, cleanReference, failReason);

                return PaymentProviderResult.BusinessFailure("BUSINESS_REJECTION", failReason, responseString);
            }

            // Handle 401/403 or 5xx technical failures
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode >= 500)
            {
                var failReason = parsed?.Message ?? string.Format(CultureInfo.InvariantCulture, "HTTP error {0}", statusCode);
                PaymentMetrics.RecordRequest("Flutterwave", "InitiateTransfer", "TechnicalFailure", stopwatch.Elapsed.TotalMilliseconds);
                LogTechnicalFailure(_logger, cleanReference, statusCode, failReason);

                return PaymentProviderResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), failReason);
            }

            var generalReason = parsed?.Message ?? string.Format(CultureInfo.InvariantCulture, "HTTP error {0}", statusCode);
            PaymentMetrics.RecordRequest("Flutterwave", "InitiateTransfer", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
            return PaymentProviderResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), generalReason);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordRequest("Flutterwave", "InitiateTransfer", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
            LogTransferTimeout(_logger, cleanReference);

            return PaymentProviderResult.Unknown("HTTP request timed out. Outcome unknown, reconciliation required.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordRequest("Flutterwave", "InitiateTransfer", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
            LogTransferCommunicationError(_logger, cleanReference, ex);

            return PaymentProviderResult.Unknown($"Communication failure: {ex.Message}");
        }
    }

    /// <summary>
    /// Queries transfer status from Flutterwave using provider reference or ID.
    /// </summary>
    public async Task<PaymentProviderResult> GetTransferStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        var cleanRef = providerReference.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v3/transfers/{cleanRef}");
        ApplyAuthentication(request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveTransferStatusResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                var flwStatus = parsed.Data.Status?.ToUpperInvariant();
                var safeMeta = JsonSerializer.Serialize(new
                {
                    provider_id = parsed.Data.Id,
                    status = parsed.Data.Status,
                    complete_message = parsed.Data.CompleteMessage
                });

                if (flwStatus == "SUCCESSFUL")
                {
                    PaymentMetrics.RecordRequest("Flutterwave", "GetTransferStatus", "Success", stopwatch.Elapsed.TotalMilliseconds);
                    return PaymentProviderResult.Success(cleanRef, safeMeta);
                }

                if (flwStatus == "FAILED")
                {
                    var reason = parsed.Data.CompleteMessage ?? "Transfer failed at gateway";
                    PaymentMetrics.RecordRequest("Flutterwave", "GetTransferStatus", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
                    return PaymentProviderResult.BusinessFailure("TRANSFER_FAILED", reason, safeMeta);
                }

                PaymentMetrics.RecordRequest("Flutterwave", "GetTransferStatus", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
                return PaymentProviderResult.Unknown($"Transfer is in status '{flwStatus}'", safeMeta);
            }

            if (statusCode >= 500)
            {
                PaymentMetrics.RecordRequest("Flutterwave", "GetTransferStatus", "TechnicalFailure", stopwatch.Elapsed.TotalMilliseconds);
                return PaymentProviderResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), parsed?.Message ?? "Server error");
            }

            PaymentMetrics.RecordRequest("Flutterwave", "GetTransferStatus", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
            return PaymentProviderResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), parsed?.Message ?? "Status query failed");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordRequest("Flutterwave", "GetTransferStatus", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
            return PaymentProviderResult.Unknown($"Failed to query status: {ex.Message}");
        }
    }

    /// <summary>
    /// Provisions a dedicated permanent virtual account via Flutterwave.
    /// </summary>
    public async Task<VirtualAccountCreationResult> CreateVirtualAccountAsync(
        string email,
        string name,
        string? phone,
        string? bvn,
        string txRef,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/virtual-account-numbers");
        ApplyAuthentication(request);

        var names = (name ?? "Customer").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = names.Length > 0 ? names[0] : "Customer";
        var lastName = names.Length > 1 ? names[1] : "CebizPay";

        var payload = new FlutterwaveVirtualAccountCreateRequest(
            Email: email.Trim(),
            IsPermanent: true,
            Bvn: bvn?.Trim(),
            TxRef: txRef.Trim(),
            Phonenumber: phone?.Trim(),
            FirstName: firstName,
            LastName: lastName,
            Narration: $"CebizPay Virtual Account for {name}");

        request.Content = JsonContent.Create(payload);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveVirtualAccountResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                var accountNumber = parsed.Data.AccountNumber;
                var bankName = parsed.Data.BankName ?? "Flutterwave Virtual Bank";
                var orderRef = parsed.Data.OrderRef ?? parsed.Data.FlwRef ?? txRef;

                if (!string.IsNullOrWhiteSpace(accountNumber))
                {
                    return VirtualAccountCreationResult.Success(
                        accountNumber: accountNumber.Trim(),
                        accountName: (name ?? "Customer").Trim(),
                        bankCode: "035", // Default Wema / standard virtual bank routing
                        bankName: bankName.Trim(),
                        providerReference: orderRef);
                }
            }

            return VirtualAccountCreationResult.Failure(parsed?.Message ?? $"Failed to provision virtual account (HTTP {(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return VirtualAccountCreationResult.Failure($"Virtual account communication error: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes a hosted card payment checkout session via Flutterwave Standard.
    /// </summary>
    public async Task<CardPaymentInitializationResult> InitializePaymentAsync(
        decimal amount,
        string currency,
        string email,
        string txRef,
        string redirectUrl,
        string? customerName,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/payments");
        ApplyAuthentication(request);

        var payload = new FlutterwaveInitializePaymentRequest(
            TxRef: txRef.Trim(),
            Amount: amount,
            Currency: currency.Trim().ToUpperInvariant(),
            RedirectUrl: redirectUrl.Trim(),
            Customer: new FlutterwaveCustomer(email.Trim(), customerName?.Trim()),
            Customizations: new FlutterwaveCustomizations("CebizPay Wallet Funding", "Fund your CebizPay wallet"));

        request.Content = JsonContent.Create(payload);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveInitializePaymentResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null && !string.IsNullOrWhiteSpace(parsed.Data.Link))
            {
                return CardPaymentInitializationResult.Success(
                    authorizationUrl: parsed.Data.Link.Trim(),
                    accessCode: null,
                    reference: txRef.Trim());
            }

            return CardPaymentInitializationResult.Failure(parsed?.Message ?? $"Card payment initialization failed (HTTP {(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CardPaymentInitializationResult.Failure($"Card payment communication error: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies the outcome of a transaction (card payment) with Flutterwave.
    /// </summary>
    public async Task<PaymentProviderResult> VerifyTransactionAsync(
        string transactionIdOrRef,
        CancellationToken cancellationToken = default)
    {
        var cleanRef = transactionIdOrRef.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v3/transactions/{cleanRef}/verify");
        ApplyAuthentication(request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveVerifyTransactionResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                var status = parsed.Data.Status?.ToUpperInvariant();
                var safeMeta = JsonSerializer.Serialize(new
                {
                    flw_id = parsed.Data.Id,
                    flw_ref = parsed.Data.FlwRef,
                    status = parsed.Data.Status,
                    processor_response = parsed.Data.ProcessorResponse
                });

                if (status == "SUCCESSFUL")
                {
                    return PaymentProviderResult.Success(parsed.Data.FlwRef ?? cleanRef, safeMeta);
                }

                if (status == "FAILED")
                {
                    return PaymentProviderResult.BusinessFailure("PAYMENT_FAILED", parsed.Data.ProcessorResponse ?? "Card transaction failed", safeMeta);
                }

                return PaymentProviderResult.Unknown($"Card transaction status is '{status}'", safeMeta);
            }

            if (statusCode >= 500)
            {
                return PaymentProviderResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), parsed?.Message ?? "Server error");
            }

            return PaymentProviderResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), parsed?.Message ?? "Transaction verification failed");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return PaymentProviderResult.Unknown($"Transaction verification communication error: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies transaction outcome and extracts tokenized card details if available.
    /// </summary>
    public async Task<(PaymentProviderResult Result, CardTokenDetails? TokenDetails)> VerifyTransactionWithDetailsAsync(
        string transactionIdOrRef,
        CancellationToken cancellationToken = default)
    {
        var cleanRef = transactionIdOrRef.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v3/transactions/{cleanRef}/verify");
        ApplyAuthentication(request);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveVerifyTransactionResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                var status = parsed.Data.Status?.ToUpperInvariant();
                var safeMeta = JsonSerializer.Serialize(new
                {
                    flw_id = parsed.Data.Id,
                    flw_ref = parsed.Data.FlwRef,
                    status = parsed.Data.Status,
                    processor_response = parsed.Data.ProcessorResponse
                });

                CardTokenDetails? tokenDetails = null;
                if (parsed.Data.Card != null && !string.IsNullOrWhiteSpace(parsed.Data.Card.Last4Digits))
                {
                    var tokenValue = !string.IsNullOrWhiteSpace(parsed.Data.Card.Token)
                        ? parsed.Data.Card.Token.Trim()
                        : (parsed.Data.FlwRef ?? cleanRef);
                    var expParts = (parsed.Data.Card.Expiry ?? string.Empty).Split('/');
                    var expMonth = expParts.Length > 0 ? expParts[0].Trim() : null;
                    var expYear = expParts.Length > 1 ? expParts[1].Trim() : null;
                    tokenDetails = new CardTokenDetails(
                        Token: tokenValue,
                        Last4: parsed.Data.Card.Last4Digits.Trim(),
                        Brand: parsed.Data.Card.Type ?? "Card",
                        ExpiryMonth: expMonth,
                        ExpiryYear: expYear,
                        CardHolderName: parsed.Data.Customer?.Name);
                }

                if (status == "SUCCESSFUL")
                {
                    return (PaymentProviderResult.Success(parsed.Data.FlwRef ?? cleanRef, safeMeta), tokenDetails);
                }

                if (status == "FAILED")
                {
                    return (PaymentProviderResult.BusinessFailure("PAYMENT_FAILED", parsed.Data.ProcessorResponse ?? "Card transaction failed", safeMeta), tokenDetails);
                }

                return (PaymentProviderResult.Unknown($"Card transaction status is '{status}'", safeMeta), tokenDetails);
            }

            if (statusCode >= 500)
            {
                return (PaymentProviderResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), parsed?.Message ?? "Server error"), null);
            }

            return (PaymentProviderResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), parsed?.Message ?? "Transaction verification failed"), null);
        }
        catch (Exception ex)
        {
            return (PaymentProviderResult.Unknown($"Transaction verification communication error: {ex.Message}"), null);
        }
    }

    /// <summary>
    /// Charges a tokenized card using Flutterwave's tokenized charges API.
    /// </summary>
    public async Task<CardChargeResult> ChargeTokenizedCardAsync(
        string token,
        decimal amount,
        string currency,
        string email,
        string txRef,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/tokenized-charges");
        ApplyAuthentication(request);

        var payload = new FlutterwaveTokenizedChargeRequest(
            Token: token.Trim(),
            Currency: currency.Trim().ToUpperInvariant(),
            Country: "NG",
            Amount: amount,
            Email: email.Trim(),
            FirstName: firstName?.Trim() ?? "Customer",
            LastName: lastName?.Trim() ?? "CebizPay",
            TxRef: txRef.Trim(),
            Narration: "CebizPay Wallet Funding");

        request.Content = JsonContent.Create(payload);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveTokenizedChargeResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                var status = parsed.Data.Status?.ToUpperInvariant();
                var safeMeta = JsonSerializer.Serialize(new
                {
                    flw_id = parsed.Data.Id,
                    flw_ref = parsed.Data.FlwRef,
                    status = parsed.Data.Status,
                    processor_response = parsed.Data.ProcessorResponse
                });

                CardTokenDetails? tokenDetails = null;
                if (parsed.Data.Card != null && !string.IsNullOrWhiteSpace(parsed.Data.Card.Last4Digits))
                {
                    var tokenValue = !string.IsNullOrWhiteSpace(parsed.Data.Card.Token)
                        ? parsed.Data.Card.Token.Trim()
                        : token;
                    var expParts = (parsed.Data.Card.Expiry ?? string.Empty).Split('/');
                    tokenDetails = new CardTokenDetails(
                        Token: tokenValue,
                        Last4: parsed.Data.Card.Last4Digits.Trim(),
                        Brand: parsed.Data.Card.Type ?? "Card",
                        ExpiryMonth: expParts.Length > 0 ? expParts[0].Trim() : null,
                        ExpiryYear: expParts.Length > 1 ? expParts[1].Trim() : null,
                        CardHolderName: null);
                }

                if (status == "SUCCESSFUL")
                {
                    var refToReturn = parsed.Data.FlwRef ?? (parsed.Data.Id > 0 ? parsed.Data.Id.ToString(CultureInfo.InvariantCulture) : txRef);
                    return CardChargeResult.Success(refToReturn, safeMeta, tokenDetails);
                }

                if (status == "FAILED")
                {
                    return CardChargeResult.BusinessFailure("PAYMENT_FAILED", parsed.Data.ProcessorResponse ?? "Tokenized charge failed", safeMeta);
                }

                return CardChargeResult.Unknown($"Tokenized charge status is '{status}'", safeMeta);
            }

            if (statusCode >= 500)
            {
                return CardChargeResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), parsed?.Message ?? "Server error");
            }

            return CardChargeResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), parsed?.Message ?? "Tokenized charge failed");
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return CardChargeResult.Unknown("Tokenized charge timed out.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CardChargeResult.Unknown($"Tokenized charge communication error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a refund for a transaction on Flutterwave.
    /// </summary>
    public async Task<CardRefundResult> RefundTransactionAsync(
        string transactionIdOrRef,
        decimal? amount = null,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var cleanRef = transactionIdOrRef.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v3/transactions/{cleanRef}/refund");
        ApplyAuthentication(request);

        var payload = new FlutterwaveRefundRequest(amount, comments ?? "Customer refund request");
        request.Content = JsonContent.Create(payload);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<FlutterwaveRefundResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status == "success" && parsed.Data != null)
            {
                return CardRefundResult.Success(parsed.Data.FlwRef ?? parsed.Data.Id.ToString(CultureInfo.InvariantCulture), parsed.Data.Status ?? "completed");
            }

            return CardRefundResult.Failure(parsed?.Message ?? $"Refund request failed (HTTP {(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return CardRefundResult.Failure($"Refund communication error: {ex.Message}");
        }
    }

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string MaskAccountNumber(string account)
    {
        if (string.IsNullOrWhiteSpace(account)) return string.Empty;
        var clean = account.Trim();
        return clean.Length <= 4 ? new string('*', clean.Length) : new string('*', clean.Length - 4) + clean[^4..];
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Flutterwave account resolution succeeded for bank {BankCode}, account {MaskedAccount}")]
    private static partial void LogAccountResolutionSuccess(ILogger logger, string bankCode, string maskedAccount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Flutterwave account resolution failed for bank {BankCode}, account {MaskedAccount} with status {StatusCode}")]
    private static partial void LogAccountResolutionFailed(ILogger logger, string bankCode, string maskedAccount, int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Exception resolving Flutterwave account for bank {BankCode}, account {MaskedAccount}")]
    private static partial void LogAccountResolutionException(ILogger logger, string bankCode, string maskedAccount, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Flutterwave transfer initiated successfully. Reference: {Reference}, ProviderRef: {ProviderRef}")]
    private static partial void LogTransferInitiated(ILogger logger, string reference, string providerRef);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Flutterwave business rejection for reference {Reference}: {Reason}")]
    private static partial void LogBusinessRejection(ILogger logger, string reference, string reason);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Flutterwave technical failure for reference {Reference}: HTTP {StatusCode} - {Reason}")]
    private static partial void LogTechnicalFailure(ILogger logger, string reference, int statusCode, string reason);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Flutterwave transfer timed out for reference {Reference}. Outcome UNKNOWN.")]
    private static partial void LogTransferTimeout(ILogger logger, string reference);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Flutterwave transfer communication error for reference {Reference}. Outcome UNKNOWN.")]
    private static partial void LogTransferCommunicationError(ILogger logger, string reference, Exception exception);
}
