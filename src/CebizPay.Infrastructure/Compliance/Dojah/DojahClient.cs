#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Infrastructure.Compliance.Dojah.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Compliance.Dojah;

/// <summary>
/// Resilient HTTP client implementation for the Dojah compliance verification gateway.
/// </summary>
public sealed class DojahClient : IDojahClient
{
    private readonly HttpClient _httpClient;
    private readonly DojahOptions _options;
    private readonly ILogger<DojahClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DojahClient(
        HttpClient httpClient,
        IOptions<DojahOptions> options,
        ILogger<DojahClient> logger)
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
        if (!string.IsNullOrWhiteSpace(_options.AppId))
        {
            request.Headers.Remove("AppId");
            request.Headers.Add("AppId", _options.AppId);
        }

        if (!string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", _options.PrivateKey);
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
            return VerificationProviderResult.Unavailable("Dojah provider is disabled.");

        try
        {
            var uri = $"api/v1/kyc/bvn/verify?bvn={Uri.EscapeDataString(bvn)}&first_name={Uri.EscapeDataString(firstName)}&last_name={Uri.EscapeDataString(lastName)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return VerificationProviderResult.NotFound(failureCode: "BVN_NOT_FOUND", failureReason: "BVN record not found in NIBSS registry.");

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Dojah server returned error {(int)response.StatusCode}.");

                return VerificationProviderResult.Mismatch(failureCode: "BVN_MISMATCH", failureReason: "Submitted details do not match NIBSS BVN records.");
            }

            var apiResponse = JsonSerializer.Deserialize<DojahApiResponse<DojahBvnVerifyResponseBody>>(content, JsonOptions);
            if (apiResponse?.Entity != null && apiResponse.Entity.Status)
            {
                var firstNameMatch = string.Equals(apiResponse.Entity.FirstName, firstName, StringComparison.OrdinalIgnoreCase);
                var lastNameMatch = string.Equals(apiResponse.Entity.LastName, lastName, StringComparison.OrdinalIgnoreCase);

                if (!firstNameMatch || !lastNameMatch)
                {
                    return VerificationProviderResult.Mismatch(
                        failureCode: "NAME_MISMATCH",
                        failureReason: "Provided first or last name did not match NIBSS BVN records.");
                }

                var safeMeta = JsonSerializer.Serialize(new
                {
                    bvn_verified = true,
                    first_name_match = true,
                    last_name_match = true
                });

                return VerificationProviderResult.Match(
                    providerReference: apiResponse.Entity.Bvn != null ? $"DOJAH-BVN-{apiResponse.Entity.Bvn[^4..]}" : null,
                    confidenceScore: 100m,
                    safeSummary: "BVN verified against NIBSS registry.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.Mismatch(failureCode: "BVN_MISMATCH", failureReason: apiResponse?.Message ?? "BVN details did not match.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Dojah BVN verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Dojah request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Dojah BVN verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Dojah provider is disabled.");

        try
        {
            var uri = $"api/v1/kyc/nin/verify?nin={Uri.EscapeDataString(nin)}&first_name={Uri.EscapeDataString(firstName)}&last_name={Uri.EscapeDataString(lastName)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return VerificationProviderResult.NotFound(failureCode: "NIN_NOT_FOUND", failureReason: "NIN record not found in NIMC registry.");

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Dojah server returned error {(int)response.StatusCode}.");

                return VerificationProviderResult.Mismatch(failureCode: "NIN_MISMATCH", failureReason: "Submitted details do not match NIMC NIN records.");
            }

            var apiResponse = JsonSerializer.Deserialize<DojahApiResponse<DojahNinVerifyResponseBody>>(content, JsonOptions);
            if (apiResponse?.Entity != null && apiResponse.Entity.Status)
            {
                var firstNameMatch = string.Equals(apiResponse.Entity.FirstName, firstName, StringComparison.OrdinalIgnoreCase);
                var surnameMatch = string.Equals(apiResponse.Entity.Surname, lastName, StringComparison.OrdinalIgnoreCase);

                if (!firstNameMatch || !surnameMatch)
                {
                    return VerificationProviderResult.Mismatch(
                        failureCode: "NAME_MISMATCH",
                        failureReason: "Provided first or last name did not match NIMC NIN records.");
                }

                var safeMeta = JsonSerializer.Serialize(new
                {
                    nin_verified = true,
                    first_name_match = true,
                    last_name_match = true
                });

                return VerificationProviderResult.Match(
                    providerReference: apiResponse.Entity.Nin != null ? $"DOJAH-NIN-{apiResponse.Entity.Nin[^4..]}" : null,
                    confidenceScore: 100m,
                    safeSummary: "NIN verified against NIMC registry.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.Mismatch(failureCode: "NIN_MISMATCH", failureReason: apiResponse?.Message ?? "NIN details did not match.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Dojah NIN verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Dojah request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Dojah NIN verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> VerifyPhotoIdAsync(
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Dojah provider is disabled.");

        try
        {
            var body = new DojahPhotoIdVerifyRequest
            {
                SelfieImage = selfieImageBase64,
                PhotoIdImage = referenceImageBase64 ?? selfieImageBase64
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/kyc/photoid/verify")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Dojah server returned error {(int)response.StatusCode}.");

                return VerificationProviderResult.Mismatch(failureCode: "BIOMETRIC_MISMATCH", failureReason: "Selfie does not match photo ID.");
            }

            var apiResponse = JsonSerializer.Deserialize<DojahApiResponse<DojahPhotoIdVerifyResponseBody>>(content, JsonOptions);
            if (apiResponse?.Entity != null && (apiResponse.Entity.Match || apiResponse.Entity.SelfieVerification))
            {
                var score = apiResponse.Entity.ConfidenceValue ?? 95m;
                var safeMeta = JsonSerializer.Serialize(new
                {
                    liveness_verified = apiResponse.Entity.SelfieVerification,
                    match_score = score
                });

                return VerificationProviderResult.Match(
                    confidenceScore: score,
                    safeSummary: "Liveness and biometric facial match confirmed.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.Mismatch(
                failureCode: "BIOMETRIC_MISMATCH",
                failureReason: apiResponse?.Message ?? "Biometric facial comparison failed.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Dojah photo ID verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Dojah request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Dojah photo ID verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> AnalyzeDocumentAsync(
        string documentImageBase64,
        string? docType = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Dojah provider is disabled.");

        try
        {
            var body = new DojahDocumentAnalysisRequest
            {
                Image = documentImageBase64,
                DocType = docType
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/document/analysis")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Dojah server returned error {(int)response.StatusCode}.");

                return VerificationProviderResult.Mismatch(failureCode: "DOC_VERIFICATION_FAILED", failureReason: "Document analysis rejected.");
            }

            var apiResponse = JsonSerializer.Deserialize<DojahApiResponse<DojahDocumentAnalysisResponseBody>>(content, JsonOptions);
            if (apiResponse?.Entity != null && string.Equals(apiResponse.Entity.Status, "VALID", StringComparison.OrdinalIgnoreCase))
            {
                var safeMeta = JsonSerializer.Serialize(new
                {
                    document_type = apiResponse.Entity.DocumentType,
                    status = apiResponse.Entity.Status,
                    expiry_date = apiResponse.Entity.ExpiryDate
                });

                return VerificationProviderResult.Match(
                    providerReference: apiResponse.Entity.DocumentNumber != null ? $"DOJAH-DOC-{apiResponse.Entity.DocumentNumber[^4..]}" : null,
                    confidenceScore: 95m,
                    safeSummary: "Government identity document validated successfully.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.ReviewRequired(
                reviewReason: apiResponse?.Message ?? "Document could not be verified automatically; requires compliance review.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Dojah document analysis timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Dojah request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Dojah document analysis.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> ScreenAmlAsync(
        string name,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Dojah provider is disabled.");

        try
        {
            var body = new DojahAmlScreeningRequest
            {
                Name = name,
                DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd")
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/aml/screening/platform")
            {
                Content = JsonContent.Create(body)
            };
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Dojah server returned error {(int)response.StatusCode}.");

                return VerificationProviderResult.TechnicalFailure("AML_SCREENING_FAILED", "Dojah AML screening request rejected.");
            }

            var apiResponse = JsonSerializer.Deserialize<DojahApiResponse<DojahAmlScreeningResponseBody>>(content, JsonOptions);
            if (apiResponse?.Entity != null)
            {
                var isClean = apiResponse.Entity.NumberOfMatches == 0 && !apiResponse.Entity.Pep && !apiResponse.Entity.Sanction;
                var safeMeta = JsonSerializer.Serialize(new
                {
                    matches_count = apiResponse.Entity.NumberOfMatches,
                    pep_flag = apiResponse.Entity.Pep,
                    sanction_flag = apiResponse.Entity.Sanction
                });

                if (isClean)
                {
                    return VerificationProviderResult.Match(
                        confidenceScore: 100m,
                        safeSummary: "AML/PEP screening clear: no sanctions or PEP matches found.",
                        safeMetadata: safeMeta);
                }

                return VerificationProviderResult.ReviewRequired(
                    reviewReason: "AML/PEP screening identified potential sanctions or PEP matches; compliance review required.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.ReviewRequired("Inconclusive AML response from provider.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Dojah AML screening timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Dojah request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Dojah AML screening.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> LookupCacAsync(
        string rcNumber,
        string companyName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Dojah provider is disabled.");

        try
        {
            var uri = $"api/v1/kyb/cac?rc_number={Uri.EscapeDataString(rcNumber)}&company_name={Uri.EscapeDataString(companyName)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return VerificationProviderResult.NotFound(failureCode: "CAC_NOT_FOUND", failureReason: "Company not found in CAC registry.");

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                    return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)response.StatusCode}", $"Dojah server returned error {(int)response.StatusCode}.");

                return VerificationProviderResult.Mismatch(failureCode: "CAC_MISMATCH", failureReason: "Company details do not match CAC records.");
            }

            var apiResponse = JsonSerializer.Deserialize<DojahApiResponse<DojahCacResponseBody>>(content, JsonOptions);
            if (apiResponse?.Entity != null && !string.IsNullOrWhiteSpace(apiResponse.Entity.CompanyName))
            {
                var directorCount = apiResponse.Entity.Directors?.Count ?? 0;
                var safeMeta = JsonSerializer.Serialize(new
                {
                    rc_number = apiResponse.Entity.RcNumber,
                    company_type = apiResponse.Entity.CompanyType,
                    status = apiResponse.Entity.Status,
                    directors_count = directorCount
                });

                return VerificationProviderResult.Match(
                    providerReference: $"DOJAH-CAC-{apiResponse.Entity.RcNumber}",
                    confidenceScore: 100m,
                    safeSummary: "Corporate CAC registry verified.",
                    safeMetadata: safeMeta);
            }

            return VerificationProviderResult.NotFound(failureCode: "CAC_NOT_FOUND", failureReason: "CAC record not found.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Dojah CAC lookup timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Dojah request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Dojah CAC lookup.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }
}
