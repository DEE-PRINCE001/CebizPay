#pragma warning disable CS1591
using System.Net;
using System.Text.Json;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Compliance.SmileId;
using CebizPay.Infrastructure.Compliance.SmileId.Models;
using CebizPay.UnitTests.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class SmileIdClientTests
{
    private readonly IOptions<SmileIdOptions> _validOptions = Options.Create(new SmileIdOptions
    {
        PartnerId = "test_partner",
        ApiKey = "test_api_key_smile_id",
        BaseUrl = "https://api.smileidentity.com",
        Enabled = true
    });

    [Fact]
    public async Task VerifyBiometricsAsync_WhenHighConfidence_ReturnsMatch()
    {
        var responsePayload = JsonSerializer.Serialize(new SmileIdJobResponse
        {
            JobId = "job_12345",
            ResultCode = "0810",
            ResultText = "Exact match and live selfie",
            ConfidenceValue = 98.5m,
            Actions = new SmileIdActions
            {
                LivenessCheck = "Passed",
                VerifyIdNumber = "Passed"
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.smileidentity.com") };
        var client = new SmileIdClient(httpClient, _validOptions, NullLogger<SmileIdClient>.Instance);

        var result = await client.VerifyBiometricsAsync("dGVzdF9zZWxmaWU=", "dGVzdF9yZWY=", "22222222222");

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
        Assert.Equal(98.5m, result.ConfidenceScore);
        Assert.Equal("job_12345", result.ProviderReference);
    }

    [Fact]
    public async Task VerifyBiometricsAsync_WhenLowConfidence_ReturnsMismatch()
    {
        var responsePayload = JsonSerializer.Serialize(new SmileIdJobResponse
        {
            JobId = "job_99999",
            ResultCode = "0811",
            ResultText = "Face did not match reference",
            ConfidenceValue = 42.0m,
            Actions = new SmileIdActions
            {
                LivenessCheck = "Passed",
                VerifyIdNumber = "Failed"
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.smileidentity.com") };
        var client = new SmileIdClient(httpClient, _validOptions, NullLogger<SmileIdClient>.Instance);

        var result = await client.VerifyBiometricsAsync("dGVzdF9zZWxmaWU=");

        Assert.False(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Mismatch, result.ResultStatus);
    }

    [Fact]
    public async Task VerifyDocumentAsync_WhenValidDoc_ReturnsMatch()
    {
        var responsePayload = JsonSerializer.Serialize(new SmileIdJobResponse
        {
            JobId = "doc_job_001",
            ResultCode = "0810",
            ResultText = "Document verified successfully",
            ConfidenceValue = 95.0m,
            Actions = new SmileIdActions
            {
                VerifyIdNumber = "Passed"
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.smileidentity.com") };
        var client = new SmileIdClient(httpClient, _validOptions, NullLogger<SmileIdClient>.Instance);

        var result = await client.VerifyDocumentAsync(
            "dGVzdF9kb2M=",
            "NIN_SLIP",
            "12345678901",
            "Ada",
            "Eze");

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
    }

    [Fact]
    public async Task VerifyBiometricsAsync_WhenHttpTimeout_ReturnsTechnicalFailure()
    {
        var handler = new MockHttpMessageHandler(simulateTimeout: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.smileidentity.com") };
        var client = new SmileIdClient(httpClient, _validOptions, NullLogger<SmileIdClient>.Instance);

        var result = await client.VerifyBiometricsAsync("dGVzdF9zZWxmaWU=");

        Assert.False(result.Succeeded);
        Assert.True(result.IsTechnicalFailure);
        Assert.Equal(VerificationResultStatus.TechnicalFailure, result.ResultStatus);
    }
}
