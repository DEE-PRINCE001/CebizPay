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
