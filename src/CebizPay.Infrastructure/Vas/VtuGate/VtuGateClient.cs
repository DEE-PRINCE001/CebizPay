using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Vas.VtuGate.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Vas.VtuGate;

/// <summary>
/// Resilient HTTP client for interacting with VTUGATE Value-Added Services (VAS) APIs.
/// Enforces safe logging, token authentication, and timeout boundaries.
/// </summary>
public sealed partial class VtuGateClient
{
    private readonly HttpClient _httpClient;
    private readonly VtuGateOptions _options;
    private readonly ILogger<VtuGateClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="VtuGateClient"/>.
    /// </summary>
    public VtuGateClient(
        HttpClient httpClient,
        IOptions<VtuGateOptions> options,
        ILogger<VtuGateClient> logger)
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
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        }
    }

    /// <summary>
    /// Resolves telecommunications operator for a recipient phone number via VTUGATE.
    /// </summary>
    public async Task<VtuGateResponse> ResolveOperatorAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var cleanPhone = phoneNumber.Trim();
        var maskedPhone = MaskPhoneNumber(cleanPhone);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"operators/resolve?phone={Uri.EscapeDataString(cleanPhone)}");
        ApplyAuthentication(request);

        LogOperatorResolutionStarted(_logger, maskedPhone);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<VtuGateResponse>(content, JsonOptions)
                    ?? new VtuGateResponse("success", "Operator resolved.", null, null, null, null);
                LogOperatorResolutionSucceeded(_logger, maskedPhone);
                return result;
            }

            LogOperatorResolutionFailed(_logger, (int)response.StatusCode, maskedPhone);
            return TryParseErrorResponse(content, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogClientException(_logger, "ResolveOperator", maskedPhone, ex.Message);
            return new VtuGateResponse("error", ex.Message, null, null, "HTTP_ERROR", null);
        }
    }

    /// <summary>
    /// Retrieves catalog of available data bundle plans from VTUGATE.
    /// </summary>
    public async Task<IReadOnlyList<VtuGateBundleItem>> GetDataBundlesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "data/bundles");
        ApplyAuthentication(request);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<VtuGateResponse>(content, JsonOptions);

            if (result?.Data.HasValue == true && result.Data.Value.ValueKind == JsonValueKind.Array)
            {
                var bundles = JsonSerializer.Deserialize<List<VtuGateBundleItem>>(result.Data.Value.GetRawText(), JsonOptions);
                return bundles ?? [];
            }

            return [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogClientException(_logger, "GetDataBundles", "N/A", ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Executes airtime purchase via VTUGATE.
    /// </summary>
    public async Task<VtuGateResponse> PurchaseAirtimeAsync(
        string reference,
        string phoneNumber,
        string networkCode,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var cleanPhone = phoneNumber.Trim();
        var maskedPhone = MaskPhoneNumber(cleanPhone);

        var payload = new VtuGateAirtimeRequest(
            RequestId: reference.Trim(),
            Phone: cleanPhone,
            Network: networkCode.Trim().ToUpperInvariant(),
            Amount: amount);

        using var request = new HttpRequestMessage(HttpMethod.Post, "airtime/purchase")
        {
            Content = JsonContent.Create(payload)
        };
        ApplyAuthentication(request);

        LogAirtimePurchaseStarted(_logger, reference, networkCode, amount, maskedPhone);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<VtuGateResponse>(content, JsonOptions)
                    ?? new VtuGateResponse("success", "Airtime top-up successful.", reference, null, "00", null);

                LogAirtimePurchaseCompleted(_logger, reference, result.Status ?? "unknown");
                return result;
            }

            LogAirtimePurchaseFailed(_logger, reference, (int)response.StatusCode);
            return TryParseErrorResponse(content, (int)response.StatusCode);
        }
        catch (TaskCanceledException ex)
        {
            LogTimeoutException(_logger, "PurchaseAirtime", reference, ex.Message);
            return new VtuGateResponse("unknown", "Request timed out during airtime purchase.", reference, null, "TIMEOUT", null);
        }
        catch (HttpRequestException ex)
        {
            LogClientException(_logger, "PurchaseAirtime", reference, ex.Message);
            return new VtuGateResponse("error", ex.Message, reference, null, "HTTP_ERROR", null);
        }
    }

    /// <summary>
    /// Executes mobile data bundle purchase via VTUGATE.
    /// </summary>
    public async Task<VtuGateResponse> PurchaseDataAsync(
        string reference,
        string phoneNumber,
        string networkCode,
        string productCode,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var cleanPhone = phoneNumber.Trim();
        var maskedPhone = MaskPhoneNumber(cleanPhone);

        var payload = new VtuGateDataRequest(
            RequestId: reference.Trim(),
            Phone: cleanPhone,
            Network: networkCode.Trim().ToUpperInvariant(),
            PlanId: productCode.Trim(),
            Amount: amount);

        using var request = new HttpRequestMessage(HttpMethod.Post, "data/purchase")
        {
            Content = JsonContent.Create(payload)
        };
        ApplyAuthentication(request);

        LogDataPurchaseStarted(_logger, reference, networkCode, productCode, amount, maskedPhone);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<VtuGateResponse>(content, JsonOptions)
                    ?? new VtuGateResponse("success", "Data bundle purchase successful.", reference, null, "00", null);

                LogDataPurchaseCompleted(_logger, reference, result.Status ?? "unknown");
                return result;
            }

            LogDataPurchaseFailed(_logger, reference, (int)response.StatusCode);
            return TryParseErrorResponse(content, (int)response.StatusCode);
        }
        catch (TaskCanceledException ex)
        {
            LogTimeoutException(_logger, "PurchaseData", reference, ex.Message);
            return new VtuGateResponse("unknown", "Request timed out during data purchase.", reference, null, "TIMEOUT", null);
        }
        catch (HttpRequestException ex)
        {
            LogClientException(_logger, "PurchaseData", reference, ex.Message);
            return new VtuGateResponse("error", ex.Message, reference, null, "HTTP_ERROR", null);
        }
    }

    /// <summary>
    /// Queries the status of a transaction from VTUGATE.
    /// </summary>
    public async Task<VtuGateResponse> GetTransactionStatusAsync(
        string reference,
        string? providerReference,
        CancellationToken cancellationToken = default)
    {
        var url = !string.IsNullOrWhiteSpace(providerReference)
            ? $"transactions/{Uri.EscapeDataString(providerReference.Trim())}"
            : $"transactions/query?request_id={Uri.EscapeDataString(reference.Trim())}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuthentication(request);

        LogStatusQueryStarted(_logger, reference);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<VtuGateResponse>(content, JsonOptions)
                    ?? new VtuGateResponse("unknown", "Status query returned empty payload.", reference, null, null, null);
                return result;
            }

            return TryParseErrorResponse(content, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogClientException(_logger, "GetTransactionStatus", reference, ex.Message);
            return new VtuGateResponse("unknown", ex.Message, reference, null, "TIMEOUT", null);
        }
    }

    private static VtuGateResponse TryParseErrorResponse(string rawJson, int statusCode)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<VtuGateResponse>(rawJson, JsonOptions);
            if (parsed != null)
                return parsed;
        }
        catch (JsonException)
        {
            // Ignore parse errors on HTML or raw error strings
        }

        var isClientError = statusCode is >= 400 and < 500;
        return new VtuGateResponse(
            isClientError ? "failed" : "error",
            $"HTTP error {statusCode}",
            null,
            null,
            statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            null);
    }

    /// <summary>Masks a phone number for safe logging (e.g. 0803***4567).</summary>
    public static string MaskPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 7)
            return "***";

        var clean = phoneNumber.Trim();
        return $"{clean[..4]}***{clean[^4..]}";
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Resolving operator for phone: {MaskedPhone}")]
    private static partial void LogOperatorResolutionStarted(ILogger logger, string maskedPhone);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Operator resolved successfully for phone: {MaskedPhone}")]
    private static partial void LogOperatorResolutionSucceeded(ILogger logger, string maskedPhone);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Operator resolution failed with HTTP {StatusCode} for phone: {MaskedPhone}")]
    private static partial void LogOperatorResolutionFailed(ILogger logger, int statusCode, string maskedPhone);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Starting VTUGATE airtime purchase {Reference} for {Network} ₦{Amount} to {MaskedPhone}")]
    private static partial void LogAirtimePurchaseStarted(ILogger logger, string reference, string network, decimal amount, string maskedPhone);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "VTUGATE airtime purchase {Reference} completed with status: {Status}")]
    private static partial void LogAirtimePurchaseCompleted(ILogger logger, string reference, string status);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "VTUGATE airtime purchase {Reference} failed with HTTP {StatusCode}")]
    private static partial void LogAirtimePurchaseFailed(ILogger logger, string reference, int statusCode);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Starting VTUGATE data purchase {Reference} for {Network} plan {ProductCode} ₦{Amount} to {MaskedPhone}")]
    private static partial void LogDataPurchaseStarted(ILogger logger, string reference, string network, string productCode, decimal amount, string maskedPhone);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "VTUGATE data purchase {Reference} completed with status: {Status}")]
    private static partial void LogDataPurchaseCompleted(ILogger logger, string reference, string status);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "VTUGATE data purchase {Reference} failed with HTTP {StatusCode}")]
    private static partial void LogDataPurchaseFailed(ILogger logger, string reference, int statusCode);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Querying status for VAS transaction {Reference}")]
    private static partial void LogStatusQueryStarted(ILogger logger, string reference);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "VTUGATE HTTP client error during {Operation} for target {Target}: {ErrorMessage}")]
    private static partial void LogClientException(ILogger logger, string operation, string target, string errorMessage);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "VTUGATE request timed out during {Operation} for target {Target}: {ErrorMessage}")]
    private static partial void LogTimeoutException(ILogger logger, string operation, string target, string errorMessage);
}
