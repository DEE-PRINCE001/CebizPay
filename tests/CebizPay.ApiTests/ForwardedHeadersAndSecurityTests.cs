using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class ForwardedHeadersAndSecurityTests
{
    [Fact]
    public async Task ForwardedHeaders_WithXForwardedProtoHttps_TranslatesRequestSchemeToHttps()
    {
        // Arrange
        string? observedScheme = null;
        string? observedClientIp = null;

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Clear();
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(context =>
                    {
                        observedScheme = context.Request.Scheme;
                        observedClientIp = context.Connection.RemoteIpAddress?.ToString();
                        return context.Response.WriteAsync("OK");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.195");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https", observedScheme);
        Assert.Equal("203.0.113.195", observedClientIp);
    }

    [Fact]
    public async Task HealthCheck_WhenInProduction_DoesNotGetRedirectedByHttpsPolicy()
    {
        // Arrange
        using var host = await new HostBuilder()
            .UseEnvironment(Environments.Production)
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHealthChecks();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/health"), branch =>
                    {
                        branch.UseHttpsRedirection();
                    });
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHealthChecks("/health/live");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act - Plain HTTP request to /health/live
        var response = await client.GetAsync("/health/live");

        // Assert - Should NOT return 307/308 Redirect
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
