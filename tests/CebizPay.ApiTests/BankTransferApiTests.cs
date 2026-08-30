using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.UseCases.Wallet.Transfer;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

/// <summary>
/// API integration tests for Bank Transfer endpoints (Resolution, Initiation, and Idempotency).
/// </summary>
public sealed class BankTransferApiTests
{
    [Fact]
    public async Task ResolveBankAccount_ValidAccount_ShouldReturn200WithAccountName()
    {
        // Arrange
        var resolver = Substitute.For<IBankAccountResolver>();
        resolver.ResolveAsync("058", "0123456789", Arg.Any<CancellationToken>())
            .Returns(new BankAccountResolutionResult(
                Succeeded: true,
                AccountName: "ALICE CHUKWU",
                BankCode: "058",
                AccountNumber: "0123456789"));

        var mediator = Substitute.For<ISender>();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(WalletController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
                    services.AddSingleton(resolver);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/v1/wallet/transfer/resolve-account?bankCode=058&accountNumber=0123456789");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ALICE CHUKWU", doc.RootElement.GetProperty("accountName").GetString());
        Assert.Equal("058", doc.RootElement.GetProperty("bankCode").GetString());
        Assert.Equal("0123456789", doc.RootElement.GetProperty("accountNumber").GetString());
    }

    [Fact]
    public async Task ResolveBankAccount_InvalidAccount_ShouldReturn400BadRequest()
    {
        // Arrange
        var resolver = Substitute.For<IBankAccountResolver>();
        resolver.ResolveAsync("058", "0000000000", Arg.Any<CancellationToken>())
            .Returns(new BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: "058",
                AccountNumber: "0000000000",
                ErrorMessage: "Account not found"));

        var mediator = Substitute.For<ISender>();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(WalletController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
                    services.AddSingleton(resolver);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/v1/wallet/transfer/resolve-account?bankCode=058&accountNumber=0000000000");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ACCOUNT_RESOLUTION_FAILED", doc.RootElement.GetProperty("code").GetString());
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-id") };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
