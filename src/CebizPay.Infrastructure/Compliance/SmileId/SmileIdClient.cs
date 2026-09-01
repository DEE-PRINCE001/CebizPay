#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Infrastructure.Compliance.SmileId.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Compliance.SmileId;

/// <summary>
/// Resilient HTTP client implementation for Smile ID verification gateway.
/// </summary>
public sealed class SmileIdClient : ISmileIdClient
{
    private readonly HttpClient _httpClient;
    private readonly SmileIdOptions _options;
    private readonly ILogger<SmileIdClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmileIdClient(
        HttpClient httpClient,
        IOptions<SmileIdOptions> options,
        ILogger<SmileIdClient> logger)
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

    private (string Signature, string Timestamp) GenerateSignature()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var message = $"{timestamp}{_options.PartnerId}sid_request";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ApiKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToBase64String(hash);

        return (signature, timestamp);
    }

    public async Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Smile ID provider is disabled.");

        try
        {
            var (signature, timestamp) = GenerateSignature();
            var jobId = $"CBZSM-BVN-{Guid.NewGuid():N}";

            var requestBody = new SmileIdIdVerificationRequest
            {
                PartnerId = _options.PartnerId,
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = jobId,
                    UserId = $"USER-{Guid.NewGuid():N}",
                    JobType = 5 // Enhanced KYC
                },
                IdInfo = new SmileIdIdInfo
                {
                    Country = "NG",
                    IdType = "BVN",
                    IdNumber = bvn,
                    FirstName = firstName,
                    LastName = lastName,
                    Dob = dateOfBirth?.ToString("yyyy-MM-dd"),
                    Entered = "true"
                },
                Signature = signature,
                Timestamp = timestamp
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/id_verification", requestBody, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseJobResponse(response.StatusCode, content, jobId, "BVN");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Smile ID BVN verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Smile ID request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Smile ID BVN verification.");
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
            return VerificationProviderResult.Unavailable("Smile ID provider is disabled.");

        try
        {
            var (signature, timestamp) = GenerateSignature();
            var jobId = $"CBZSM-NIN-{Guid.NewGuid():N}";

            var requestBody = new SmileIdIdVerificationRequest
            {
                PartnerId = _options.PartnerId,
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = jobId,
                    UserId = $"USER-{Guid.NewGuid():N}",
                    JobType = 5 // Enhanced KYC
                },
                IdInfo = new SmileIdIdInfo
                {
                    Country = "NG",
                    IdType = "NIN",
                    IdNumber = nin,
                    FirstName = firstName,
                    LastName = lastName,
                    Dob = dateOfBirth?.ToString("yyyy-MM-dd"),
                    Entered = "true"
                },
                Signature = signature,
                Timestamp = timestamp
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/id_verification", requestBody, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseJobResponse(response.StatusCode, content, jobId, "NIN");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Smile ID NIN verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Smile ID request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Smile ID NIN verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> VerifyBiometricsAsync(
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        string? idNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Smile ID provider is disabled.");

        try
        {
            var (signature, timestamp) = GenerateSignature();
            var jobId = $"CBZSM-BIO-{Guid.NewGuid():N}";

            var images = new List<SmileIdImageInfo>
            {
                new() { ImageTypeId = 2, Image = selfieImageBase64 } // Selfie
            };

            if (!string.IsNullOrWhiteSpace(referenceImageBase64))
            {
                images.Add(new() { ImageTypeId = 3, Image = referenceImageBase64 }); // ID Photo
            }

            var requestBody = new SmileIdBiometricKycRequest
            {
                PartnerId = _options.PartnerId,
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = jobId,
                    UserId = $"USER-{Guid.NewGuid():N}",
                    JobType = 1 // Biometric KYC
                },
                Images = images,
                Signature = signature,
                Timestamp = timestamp
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/biometric_kyc", requestBody, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseJobResponse(response.StatusCode, content, jobId, "Biometrics");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Smile ID biometric verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Smile ID request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Smile ID biometric verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> VerifyDocumentAsync(
        string documentImageBase64,
        string idType,
        string idNumber,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Smile ID provider is disabled.");

        try
        {
            var (signature, timestamp) = GenerateSignature();
            var jobId = $"CBZSM-DOC-{Guid.NewGuid():N}";

            var requestBody = new SmileIdDocumentVerificationRequest
            {
                PartnerId = _options.PartnerId,
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = jobId,
                    UserId = $"USER-{Guid.NewGuid():N}",
                    JobType = 6 // Document Verification
                },
                IdInfo = new SmileIdIdInfo
                {
                    Country = "NG",
                    IdType = idType,
                    IdNumber = idNumber,
                    FirstName = firstName,
                    LastName = lastName
                },
                Images = new List<SmileIdImageInfo>
                {
                    new() { ImageTypeId = 0, Image = documentImageBase64 } // ID Front
                },
                Signature = signature,
                Timestamp = timestamp
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/document_verification", requestBody, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseJobResponse(response.StatusCode, content, jobId, "Document");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Smile ID document verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Smile ID request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Smile ID document verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> ScreenAmlAsync(
        string name,
        bool isEntity = false,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Smile ID provider is disabled.");

        try
        {
            var (signature, timestamp) = GenerateSignature();
            var jobId = $"CBZSM-AML-{Guid.NewGuid():N}";

            var requestBody = new SmileIdAmlRequest
            {
                PartnerId = _options.PartnerId,
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = jobId,
                    UserId = $"USER-{Guid.NewGuid():N}",
                    JobType = 8 // AML Check
                },
                SearchType = isEntity ? "business" : "individual",
                Name = name,
                Signature = signature,
                Timestamp = timestamp
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/aml", requestBody, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseJobResponse(response.StatusCode, content, jobId, "AML");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Smile ID AML screening timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Smile ID request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Smile ID AML screening.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    public async Task<VerificationProviderResult> VerifyBusinessAsync(
        string registrationNumber,
        string? businessName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return VerificationProviderResult.Unavailable("Smile ID provider is disabled.");

        try
        {
            var (signature, timestamp) = GenerateSignature();
            var jobId = $"CBZSM-BIZ-{Guid.NewGuid():N}";

            var requestBody = new SmileIdBusinessVerificationRequest
            {
                PartnerId = _options.PartnerId,
                PartnerParams = new SmileIdPartnerParams
                {
                    JobId = jobId,
                    UserId = $"ORG-{Guid.NewGuid():N}",
                    JobType = 7 // Business Verification
                },
                Country = "NG",
                RegistrationNumber = registrationNumber,
                Signature = signature,
                Timestamp = timestamp
            };

            using var response = await _httpClient.PostAsJsonAsync("v1/business_verification", requestBody, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return ParseJobResponse(response.StatusCode, content, jobId, "Business");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Smile ID business verification timed out.");
            return VerificationProviderResult.TechnicalFailure("TIMEOUT", "Smile ID request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing Smile ID business verification.");
            return VerificationProviderResult.TechnicalFailure("UNEXPECTED_ERROR", ex.Message);
        }
    }

    private static VerificationProviderResult ParseJobResponse(HttpStatusCode statusCode, string content, string jobId, string capability)
    {
        if (statusCode == HttpStatusCode.NotFound)
            return VerificationProviderResult.NotFound("NOT_FOUND", $"{capability} record not found in Smile ID registry.");

        if ((int)statusCode >= 500)
            return VerificationProviderResult.TechnicalFailure($"HTTP_{(int)statusCode}", $"Smile ID returned server error {(int)statusCode}.");

        try
        {
            var jobResp = JsonSerializer.Deserialize<SmileIdJobResponse>(content, JsonOptions);
            if (jobResp == null)
                return VerificationProviderResult.TechnicalFailure("INVALID_RESPONSE", "Empty response from Smile ID.");

            var code = jobResp.ResultCode ?? string.Empty;

            // Completed Match
            if (code == "1012" || (code == "0810" && (jobResp.Success || jobResp.Actions?.VerifyIdNumber == "Passed" || jobResp.Actions?.LivenessCheck == "Passed")) || (jobResp.Success && string.IsNullOrEmpty(code)))
            {
                var safeMeta = JsonSerializer.Serialize(new
                {
                    job_id = jobResp.JobId ?? jobId,
                    result_code = code,
                    verify_id = jobResp.Actions?.VerifyIdNumber,
                    liveness = jobResp.Actions?.LivenessCheck
                });

                return VerificationProviderResult.Match(
                    providerReference: jobResp.JobId ?? jobId,
                    confidenceScore: jobResp.ConfidenceValue ?? 100m,
                    safeSummary: $"{capability} verified successfully via Smile ID.",
                    safeMetadata: safeMeta);
            }

            // 1013 = ID Number Not Found
            if (code == "1013")
            {
                return VerificationProviderResult.NotFound("ID_NOT_FOUND", jobResp.ResultText ?? "Identifier not found.", providerReference: jobResp.JobId ?? jobId);
            }

            // 1014 or 0811 = ID Information Mismatch / Face Mismatch
            if (code == "1014" || code == "0811" || (!jobResp.Success && jobResp.Actions?.VerifyIdNumber == "Failed"))
            {
                return VerificationProviderResult.Mismatch("ID_MISMATCH", jobResp.ResultText ?? "Submitted information does not match registry.", providerReference: jobResp.JobId ?? jobId);
            }

            // 1015 = Human review / Under review
            if (code == "1015" || code == "0812")
            {
                return VerificationProviderResult.ReviewRequired(jobResp.ResultText ?? "Inconclusive result; compliance review required.", providerReference: jobResp.JobId ?? jobId);
            }

            // Asynchronous job pending
            if (code is "0820" or "0821" or "0800" || (string.IsNullOrEmpty(code) && !jobResp.Success))
            {
                return VerificationProviderResult.Pending(jobResp.JobId ?? jobId, "Smile ID job processing asynchronously.");
            }

            return VerificationProviderResult.Mismatch(code, jobResp.ResultText ?? "Verification failed.", providerReference: jobResp.JobId ?? jobId);
        }
        catch
        {
            return VerificationProviderResult.TechnicalFailure("DESERIALIZATION_ERROR", "Failed to deserialize Smile ID response.");
        }
    }
}
