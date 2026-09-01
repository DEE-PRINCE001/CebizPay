#pragma warning disable CS1591
using System.Net;
using System.Text.Json;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Ninja;
using CebizPay.Infrastructure.Compliance.Ninja.Models;
using CebizPay.UnitTests.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class NinjaClientTests
{
    private readonly IOptions<NinjaOptions> _validOptions = Options.Create(new NinjaOptions
    {
        ClientId = "ninja_client_123",
        ClientSecret = "ninja_secret_456",
        BaseUrl = "https://api.ninjakyc.com",
        Enabled = true
    });

    [Fact]
    public async Task VerifyBvnAsync_WhenMatch_ReturnsMatch()
    {
        var responsePayload = JsonSerializer.Serialize(new NinjaApiResponse<NinjaIdentityData>
        {
            Success = true,
            Reference = "nj_bvn_123",
            Data = new NinjaIdentityData
            {
                Match = true,
                ConfidenceScore = 100m,
                Status = "VERIFIED"
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.ninjakyc.com") };
        var client = new NinjaClient(httpClient, _validOptions, NullLogger<NinjaClient>.Instance);

        var result = await client.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", new DateTime(1990, 1, 15));

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
        Assert.Equal("nj_bvn_123", result.ProviderReference);
    }

    [Fact]
    public async Task VerifyCacAsync_WhenActive_ReturnsMatch()
    {
        var responsePayload = JsonSerializer.Serialize(new NinjaApiResponse<NinjaCacData>
        {
            Success = true,
            Reference = "nj_cac_456",
            Data = new NinjaCacData
            {
                RcNumber = "RC-998877",
                CompanyName = "Ninja Corp Ltd",
                Status = "ACTIVE"
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.ninjakyc.com") };
        var client = new NinjaClient(httpClient, _validOptions, NullLogger<NinjaClient>.Instance);

        var result = await client.VerifyCacAsync("RC-998877", "Ninja Corp Ltd");

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
    }

    [Fact]
    public async Task ScreenAmlAsync_WhenClear_ReturnsMatch()
    {
        var responsePayload = JsonSerializer.Serialize(new NinjaApiResponse<NinjaAmlData>
        {
            Success = true,
            Data = new NinjaAmlData
            {
                MatchesCount = 0,
                RiskLevel = "LOW",
                PepMatch = false,
                SanctionMatch = false
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, responsePayload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.ninjakyc.com") };
        var client = new NinjaClient(httpClient, _validOptions, NullLogger<NinjaClient>.Instance);

        var result = await client.ScreenAmlAsync("Clear Entity");

        Assert.True(result.Succeeded);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
    }
}
