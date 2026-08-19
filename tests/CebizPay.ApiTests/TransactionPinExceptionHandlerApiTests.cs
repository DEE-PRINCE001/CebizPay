using System.Net;
using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class TransactionPinExceptionHandlerApiTests
{
    private static async Task<HttpClient> CreateClientWithException(Exception exceptionToThrow)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddProblemDetails();
                    services.AddExceptionHandler<Api.Middleware.GlobalExceptionHandler>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.Run(_ => throw exceptionToThrow);
                });
            })
            .StartAsync();

        return host.GetTestClient();
    }

    [Fact]
    public async Task InvalidPinException_Returns400BadRequestWithProblemDetails()
    {
        // Arrange
        using var client = await CreateClientWithException(new InvalidPinException("Invalid transaction PIN. Attempts remaining: 2."));

        // Act
        var response = await client.GetAsync("/test-pin");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("Invalid Transaction PIN", root.GetProperty("title").GetString());
        Assert.Equal("INVALID_TRANSACTION_PIN", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PinLockedException_Returns423LockedWithProblemDetails()
    {
        // Arrange
        using var client = await CreateClientWithException(new PinLockedException("Transaction PIN debit lock activated for 15 minutes due to 3 failed attempts."));

        // Act
        var response = await client.GetAsync("/test-pin");

        // Assert
        Assert.Equal((HttpStatusCode)423, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(423, root.GetProperty("status").GetInt32());
        Assert.Equal("PIN Locked", root.GetProperty("title").GetString());
        Assert.Equal("TRANSFER_PIN_LOCKED", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PinRequiredException_Returns422UnprocessableEntityWithProblemDetails()
    {
        // Arrange
        using var client = await CreateClientWithException(new PinRequiredException());

        // Act
        var response = await client.GetAsync("/test-pin");

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(422, root.GetProperty("status").GetInt32());
        Assert.Equal("PIN Required", root.GetProperty("title").GetString());
        Assert.Equal("TRANSFER_PIN_REQUIRED", root.GetProperty("code").GetString());
    }
}
