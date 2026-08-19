using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class HealthCheckApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLivenessEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReadinessEndpoint_ShouldReturnResponse()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        Assert.NotNull(response);
    }
}
