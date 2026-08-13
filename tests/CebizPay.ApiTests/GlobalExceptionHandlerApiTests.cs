using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class GlobalExceptionHandlerApiTests
{
    [Fact]
    public async Task GlobalExceptionHandler_IdempotencyConflictException_Returns409ConflictWithProblemDetails()
    {
        // Arrange
        using var host = await new HostBuilder()
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
                    app.Run(context =>
                    {
                        if (context.Request.Path == "/test-idempotency-conflict")
                        {
                            throw new IdempotencyConflictException(
                                "idemp_12345",
                                "Idempotency key conflict: key 'idemp_12345' was previously used with a different request payload.");
                        }

                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/test-idempotency-conflict");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.Equal("Idempotency Conflict", root.GetProperty("title").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", root.GetProperty("code").GetString());
    }
}
