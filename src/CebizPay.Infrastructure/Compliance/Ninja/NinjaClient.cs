#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Infrastructure.Compliance.Ninja.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Compliance.Ninja;

/// <summary>
/// Resilient HTTP client implementation for Ninja compliance verification gateway.
/// </summary>
public sealed class NinjaClient : INinjaClient
{
    private readonly HttpClient _httpClient;
    private readonly NinjaOptions _options;
    private readonly ILogger<NinjaClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NinjaClient(
        HttpClient httpClient,
        IOptions<NinjaOptions> options,
        ILogger<NinjaClient> logger)
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

    private void ApplyAuthHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ClientId))
        {
            request.Headers.Remove("X-Client-Id");
            request.Headers.Add("X-Client-Id", _options.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            request.Headers.Remove("X-Client-Secret");
            request.Headers.Add("X-Client-Secret", _options.ClientSecret);
        }
    }

    public async Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Ninja provider is disabled.");

        try
        {
            var body = new NinjaIdentityRequest
            {
                Identifier = bvn,
                FirstName = firstName,
                LastName = lastName,
                Dob = dateOfBirth?.ToString("yyyy-MM-dd")
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/identity/bvn/resolve")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseIdentityResponse(response.StatusCode, content, "BVN");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Ninja BVN verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Ninja request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Ninja BVN verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Ninja provider is disabled.");

        try
        {
            var body = new NinjaIdentityRequest
            {
                Identifier = nin,
                FirstName = firstName,
                LastName = lastName,
                Dob = dateOfBirth?.ToString("yyyy-MM-dd")
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/identity/nin/resolve")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseIdentityResponse(response.StatusCode, content, "NIN");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Ninja NIN verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Ninja request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Ninja NIN verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> VerifyCacAsync(
        string rcNumber,
        string companyName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Ninja provider is disabled.");

        try
        {
            var body = new NinjaCacRequest
            {
                RcNumber = rcNumber,
                CompanyName = companyName
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/kyb/cac/verify")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return VerificationProviderResult.NotFound("CAC_NOT_FOUND", "Company not found in CAC registry.");

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Ninja server error {(int)response.StatusCode}.");

                return VerificationProviderResult.Mismatch("CAC_MISMATCH", "Corporate details do not match CAC records.");
            }

            var apiResp = JsonSerializer.Deserialize<NinjaApiResponse<NinjaCacData>>(content, JsonOptions);
            if (apiResp?.Success == true && apiResp.Data != null)
            {
                var safeMeta = JsonSerializer.Serialize(new
                {
                    rc_number = apiResp.Data.RcNumber,
                    company_type = apiResp.Data.CompanyType,
                    status = apiResp.Data.Status,
                    registration_date = apiResp.Data.RegistrationDate
                });

                return VerificationProviderResult.Match(
                    providerReference: apiResp.Reference ?? $"NINJA-CAC-{rcNumber}",
                    confidenceScore: 100m,
                    safeSummary: "CAC business record verified via Ninja.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.Mismatch("CAC_MISMATCH", apiResp?.Message ?? "CAC verification failed.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Ninja CAC lookup timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Ninja request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Ninja CAC lookup.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> ScreenAmlAsync(
        string name,
        bool isEntity = false,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Ninja provider is disabled.");

        try
        {
            var body = new NinjaAmlRequest
            {
                Name = name,
                Type = isEntity ? "business" : "individual",
                Country = "NG"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/compliance/aml/search")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Ninja server error {(int)response.StatusCode}.");

                return VerificationProviderResult.TechnicalFailure("AML_ERROR", "Ninja AML search failed.");
            }

            var apiResp = JsonSerializer.Deserialize<NinjaApiResponse<NinjaAmlData>>(content, JsonOptions);
            if (apiResp?.Success == true && apiResp.Data != null)
            {
                var isClean = apiResp.Data.MatchesCount == 0 && !apiResp.Data.PepMatch && !apiResp.Data.SanctionMatch;
                var safeMeta = JsonSerializer.Serialize(new
                {
                    matches_count = apiResp.Data.MatchesCount,
                    pep = apiResp.Data.PepMatch,
                    sanctions = apiResp.Data.SanctionMatch,
                    risk = apiResp.Data.RiskLevel
                });

                if (isClean)
                {
                    return VerificationProviderResult.Match(
                        providerReference: apiResp.Reference,
                        confidenceScore: 100m,
                        safeSummary: "AML screening clear.",
                        safeMetadata: safeMeta);
                }

                return VerificationProviderResult.ReviewRequired(
                    reviewReason: "Potential AML/PEP match detected via Ninja; review required.",
                    providerReference: apiResp.Reference,
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.ReviewRequired("Inconclusive AML response from Ninja.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Ninja AML screening timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Ninja request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Ninja AML screening.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    private static VerificationProviderResult ParseIdentityResponse(HttpStatusCode statusCode, string content, string type)
    {
        if (statusCode == HttpStatusCode.NotFound)
            return VerificationProviderResult.NotFound("NOT_FOUND", $"{type} record not found in registry.");

        if ((int)statusCode >= 500)
            return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)statusCode}", $"Ninja returned server error {(int)statusCode}.");

        try
        {
            var apiResp = JsonSerializer.Deserialize<NinjaApiResponse<NinjaIdentityData>>(content, JsonOptions);
            if (apiResp?.Success == true && apiResp.Data != null && apiResp.Data.Match)
            {
                var safeMeta = JsonSerializer.Serialize(new
                {
                    type,
                    status = apiResp.Data.Status,
                    score = apiResp.Data.ConfidenceScore
                });

                return VerificationProviderResult.Match(
                    providerReference: apiResp.Reference,
                    confidenceScore: apiResp.Data.ConfidenceScore ?? 100m,
                    safeSummary: $"{type} identity verified via Ninja.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.Mismatch("IDENTITY_MISMATCH", apiResp?.Message ?? $"{type} details did not match registry records.");
        }
        catch
        {
            return VerificationProviderResult.TechnicalFailure("DESERIALIZATION_ERROR", "Failed to deserialize Ninja response.");
        }
    }
}
