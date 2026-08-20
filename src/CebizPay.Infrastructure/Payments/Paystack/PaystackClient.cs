using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Paystack.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Paystack;

/// <summary>
/// Resilient HTTP client for interacting with Paystack payment APIs.
/// </summary>
public sealed partial class PaystackClient
{
    private readonly HttpClient _httpClient;
    private readonly PaystackOptions _options;
    private readonly ILogger<PaystackClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PaystackClient"/> class.
    /// </summary>
    public PaystackClient(
        HttpClient httpClient,
        IOptions<PaystackOptions> options,
        ILogger<PaystackClient> logger)
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
    /// Resolves destination bank account name using Paystack resolve account API.
    /// </summary>
    public async Task<BankAccountResolutionResult> ResolveAccountAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var cleanBankCode = bankCode.Trim();
        var cleanAccountNumber = accountNumber.Trim();
        var maskedAccount = MaskAccountNumber(cleanAccountNumber);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"bank/resolve?account_number={cleanAccountNumber}&bank_code={cleanBankCode}");
        ApplyAuthentication(request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PaystackAccountResolveResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
                if (result != null && result.Status && result.Data != null && !string.IsNullOrWhiteSpace(result.Data.AccountName))
                {
                    PaymentMetrics.RecordAccountResolution("Paystack", succeeded: true);
                    LogAccountResolutionSuccess(_logger, cleanBankCode, maskedAccount);

                    return new BankAccountResolutionResult(
                        Succeeded: true,
                        AccountName: result.Data.AccountName.Trim(),
                        BankCode: cleanBankCode,
                        AccountNumber: cleanAccountNumber);
                }
            }

            PaymentMetrics.RecordAccountResolution("Paystack", succeeded: false);
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
            PaymentMetrics.RecordAccountResolution("Paystack", succeeded: false);
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
    /// Creates a transfer recipient in Paystack to obtain a recipient code for transfer dispatch.
    /// </summary>
    public async Task<string?> CreateRecipientAsync(
        string accountName,
        string accountNumber,
        string bankCode,
        string currency,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "transferrecipient");
        ApplyAuthentication(request);

        var payload = new PaystackCreateRecipientRequest(
            Type: "nuban",
            Name: accountName.Trim(),
            AccountNumber: accountNumber.Trim(),
            BankCode: bankCode.Trim(),
            Currency: currency.Trim().ToUpperInvariant());

        request.Content = JsonContent.Create(payload);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PaystackCreateRecipientResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
                return result?.Data?.RecipientCode;
            }

            LogRecipientCreationFailed(_logger, (int)response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            LogRecipientCreationException(_logger, ex);
            return null;
        }
    }

    /// <summary>
    /// Initiates a bank transfer payout through Paystack.
    /// </summary>
    public async Task<PaymentProviderResult> InitiateTransferAsync(
        string recipientCode,
        decimal amount,
        string currency,
        string reference,
        string narration,
        CancellationToken cancellationToken = default)
    {
        var cleanReference = reference.Trim();

        // Convert NGN amount to kobo (Paystack subunit requirement: 1 NGN = 100 kobo)
        var subunitAmount = currency.Equals("NGN", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(amount * 100, 0)
            : amount;

        using var request = new HttpRequestMessage(HttpMethod.Post, "transfer");
        ApplyAuthentication(request);

        var payload = new PaystackTransferRequest(
            Source: "balance",
            Amount: subunitAmount,
            Reference: cleanReference,
            Recipient: recipientCode.Trim(),
            Reason: narration.Trim());

        request.Content = JsonContent.Create(payload);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<PaystackTransferResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status && parsed.Data != null)
            {
                var providerRef = parsed.Data.TransferCode ?? parsed.Data.Reference ?? cleanReference;
                var safeMeta = JsonSerializer.Serialize(new
                {
                    transfer_code = parsed.Data.TransferCode,
                    status = parsed.Data.Status,
                    paystack_id = parsed.Data.Id
                });

                PaymentMetrics.RecordRequest("Paystack", "InitiateTransfer", "Success", stopwatch.Elapsed.TotalMilliseconds);
                LogTransferInitiated(_logger, cleanReference, providerRef);

                return PaymentProviderResult.Success(providerRef, safeMeta);
            }

            // Handle HTTP 400 / 422 / Business failure
            if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var failReason = parsed?.Message ?? "Validation error";
                PaymentMetrics.RecordRequest("Paystack", "InitiateTransfer", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
                LogBusinessRejection(_logger, cleanReference, failReason);

                return PaymentProviderResult.BusinessFailure("BUSINESS_REJECTION", failReason, responseString);
            }

            // Handle 401/403 or 5xx technical failures
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode >= 500)
            {
                var failReason = parsed?.Message ?? string.Format(CultureInfo.InvariantCulture, "HTTP error {0}", statusCode);
                PaymentMetrics.RecordRequest("Paystack", "InitiateTransfer", "TechnicalFailure", stopwatch.Elapsed.TotalMilliseconds);
                LogTechnicalFailure(_logger, cleanReference, statusCode, failReason);

                return PaymentProviderResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), failReason);
            }

            var generalReason = parsed?.Message ?? string.Format(CultureInfo.InvariantCulture, "HTTP error {0}", statusCode);
            PaymentMetrics.RecordRequest("Paystack", "InitiateTransfer", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
            return PaymentProviderResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), generalReason);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordRequest("Paystack", "InitiateTransfer", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
            LogTransferTimeout(_logger, cleanReference);

            return PaymentProviderResult.Unknown("HTTP request timed out. Outcome unknown, reconciliation required.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordRequest("Paystack", "InitiateTransfer", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
            LogTransferCommunicationError(_logger, cleanReference, ex);

            return PaymentProviderResult.Unknown($"Communication failure: {ex.Message}");
        }
    }

    /// <summary>
    /// Queries transfer status from Paystack using reference or transfer code.
    /// </summary>
    public async Task<PaymentProviderResult> GetTransferStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        var cleanRef = providerReference.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"transfer/verify/{cleanRef}");
        ApplyAuthentication(request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryDeserialize<PaystackVerifyTransferResponse>(responseString);

            if (response.IsSuccessStatusCode && parsed != null && parsed.Status && parsed.Data != null)
            {
                var pstkStatus = parsed.Data.Status?.ToLowerInvariant();
                var safeMeta = JsonSerializer.Serialize(new
                {
                    transfer_code = parsed.Data.TransferCode,
                    status = parsed.Data.Status,
                    amount = parsed.Data.Amount
                });

                if (pstkStatus == "success")
                {
                    PaymentMetrics.RecordRequest("Paystack", "GetTransferStatus", "Success", stopwatch.Elapsed.TotalMilliseconds);
                    return PaymentProviderResult.Success(cleanRef, safeMeta);
                }

                if (pstkStatus == "failed" || pstkStatus == "reversed")
                {
                    PaymentMetrics.RecordRequest("Paystack", "GetTransferStatus", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
                    return PaymentProviderResult.BusinessFailure("TRANSFER_FAILED", $"Transfer status is '{pstkStatus}'", safeMeta);
                }

                PaymentMetrics.RecordRequest("Paystack", "GetTransferStatus", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
                return PaymentProviderResult.Unknown($"Transfer status is '{pstkStatus}'", safeMeta);
            }

            if (statusCode >= 500)
            {
                PaymentMetrics.RecordRequest("Paystack", "GetTransferStatus", "TechnicalFailure", stopwatch.Elapsed.TotalMilliseconds);
                return PaymentProviderResult.TechnicalFailure(string.Format(CultureInfo.InvariantCulture, "HTTP_{0}", statusCode), parsed?.Message ?? "Server error");
            }

            PaymentMetrics.RecordRequest("Paystack", "GetTransferStatus", "BusinessFailure", stopwatch.Elapsed.TotalMilliseconds);
            return PaymentProviderResult.BusinessFailure(string.Format(CultureInfo.InvariantCulture, "STATUS_{0}", statusCode), parsed?.Message ?? "Status query failed");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            PaymentMetrics.RecordRequest("Paystack", "GetTransferStatus", "Unknown", stopwatch.Elapsed.TotalMilliseconds);
            return PaymentProviderResult.Unknown($"Failed to query status: {ex.Message}");
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Paystack account resolution succeeded for bank {BankCode}, account {MaskedAccount}")]
    private static partial void LogAccountResolutionSuccess(ILogger logger, string bankCode, string maskedAccount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Paystack account resolution failed for bank {BankCode}, account {MaskedAccount} with status {StatusCode}")]
    private static partial void LogAccountResolutionFailed(ILogger logger, string bankCode, string maskedAccount, int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Exception resolving Paystack account for bank {BankCode}, account {MaskedAccount}")]
    private static partial void LogAccountResolutionException(ILogger logger, string bankCode, string maskedAccount, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Paystack recipient creation failed with status {StatusCode}")]
    private static partial void LogRecipientCreationFailed(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Exception creating Paystack transfer recipient.")]
    private static partial void LogRecipientCreationException(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Paystack transfer initiated successfully. Reference: {Reference}, ProviderRef: {ProviderRef}")]
    private static partial void LogTransferInitiated(ILogger logger, string reference, string providerRef);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Paystack business rejection for reference {Reference}: {Reason}")]
    private static partial void LogBusinessRejection(ILogger logger, string reference, string reason);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Paystack technical failure for reference {Reference}: HTTP {StatusCode} - {Reason}")]
    private static partial void LogTechnicalFailure(ILogger logger, string reference, int statusCode, string reason);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Paystack transfer timed out for reference {Reference}. Outcome UNKNOWN.")]
    private static partial void LogTransferTimeout(ILogger logger, string reference);

    [LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "Paystack transfer communication error for reference {Reference}. Outcome UNKNOWN.")]
    private static partial void LogTransferCommunicationError(ILogger logger, string reference, Exception exception);
}
