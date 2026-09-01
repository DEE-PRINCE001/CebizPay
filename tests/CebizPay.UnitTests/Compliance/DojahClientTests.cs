#pragma warning disable CS1591
using System.Net;
using System.Text.Json;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Dojah;
using CebizPay.Infrastructure.Compliance.Dojah.Models;
using CebizPay.UnitTests.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class DojahClientTests
{
    private readonly IOptions<DojahOptions> _validOptions = Options.Create(new DojahOptions
    {
        AppId = "test_app_id",
        PrivateKey = "test_private_key",
        BaseUrl = "https://api.dojah.io",
        Enabled = true
    });

    [Fact]
    public async Task VerifyBvnAsync_WhenMatch_ReturnsSuccessAndMaskedData()
    {
        var responsePayload = JsonSerializer.Serialize(new DojahApiResponse<DojahBvnVerifyResponseBody>
        {
            Entity = new DojahBvnVerifyResponseBody
            {
                Bvn = "22222222222",
                FirstName = "Emeka",
                LastName = "Okonkwo",
                DateOfBirth = "1990-01-15",
                PhoneNumber1 = "08012345678",
                Status = true
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", new DateTime(1990, 1, 15));

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
        Assert.NotNull(result.SafeMetadata);
        Assert.Contains("bvn_verified", result.SafeMetadata);
    }

    [Fact]
    public async Task VerifyBvnAsync_WhenMismatch_ReturnsMismatchStatus()
    {
        var responsePayload = JsonSerializer.Serialize(new DojahApiResponse<DojahBvnVerifyResponseBody>
        {
            Entity = new DojahBvnVerifyResponseBody
            {
                Bvn = "22222222222",
                FirstName = "Different",
                LastName = "Name",
                DateOfBirth = "1985-05-20",
                Status = true
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", new DateTime(1990, 1, 15));

        Assert.False(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Mismatch, result.ResultStatus);
    }

    [Fact]
    public async Task VerifyBvnAsync_When404NotFound_ReturnsNotFoundStatus()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound, "{\"error\": \"BVN not found\"}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.VerifyBvnAsync("11111111111", "Non", "Existent");

        Assert.False(result.Succeeded);
        Assert.Equal(VerificationResultStatus.NotFound, result.ResultStatus);
    }

    [Fact]
    public async Task VerifyNinAsync_WhenMatch_ReturnsSuccess()
    {
        var responsePayload = JsonSerializer.Serialize(new DojahApiResponse<DojahNinVerifyResponseBody>
        {
            Entity = new DojahNinVerifyResponseBody
            {
                Nin = "12345678901",
                FirstName = "Ada",
                Surname = "Eze",
                BirthDate = "1995-03-10",
                Status = true
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.VerifyNinAsync("12345678901", "Ada", "Eze", new DateTime(1995, 3, 10));

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
    }

    [Fact]
    public async Task ScreenAmlAsync_WhenPepMatch_ReturnsReviewRequired()
    {
        var responsePayload = JsonSerializer.Serialize(new DojahApiResponse<DojahAmlScreeningResponseBody>
        {
            Entity = new DojahAmlScreeningResponseBody
            {
                MatchStatus = "MATCH",
                NumberOfMatches = 1,
                Pep = true,
                Sanction = false
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.ScreenAmlAsync("John Doe");

        Assert.Equal(VerificationResultStatus.ReviewRequired, result.ResultStatus);
    }

    [Fact]
    public async Task LookupCacAsync_WhenActive_ReturnsMatch()
    {
        var responsePayload = JsonSerializer.Serialize(new DojahApiResponse<DojahCacResponseBody>
        {
            Entity = new DojahCacResponseBody
            {
                RcNumber = "RC-123456",
                CompanyName = "Acme Global Limited",
                Status = "ACT",
                RegistrationDate = "2020-01-01"
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.LookupCacAsync("RC-123456", "Acme Global Limited");

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
    }

    [Fact]
    public async Task VerifyBvnAsync_WhenTimeout_ReturnsTechnicalFailure()
    {
        var handler = new MockHttpMessageHandler(simulateTimeout: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.dojah.io") };
        var client = new DojahClient(httpClient, _validOptions, NullLogger<DojahClient>.Instance);

        var result = await client.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo");

        Assert.False(result.Succeeded);
        Assert.True(result.IsTechnicalFailure);
        Assert.Equal(VerificationResultStatus.TechnicalFailure, result.ResultStatus);
    }
}
