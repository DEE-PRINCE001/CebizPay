using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
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

public sealed class CardVerificationApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(ICardVerificationService verificationService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(CardVerificationController).Assembly);
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
                    services.AddSingleton(verificationService);
                    var currentUserService = Substitute.For<ICurrentUserService>();
                    currentUserService.UserId.Returns("usr_api_verify_user");
                    services.AddSingleton(currentUserService);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        return (host, client);
    }

    [Fact]
    public async Task Initialize_ValidPayload_Returns200OkWithAuthUrl()
    {
        var service = Substitute.For<ICardVerificationService>();
        var walletId = Guid.NewGuid();
        var verificationDto = new CardVerificationResponseDto(
            Id: Guid.NewGuid(),
            UserId: "usr_api_verify_user",
            WalletId: walletId,
            Provider: "Flutterwave",
            Reference: "CBZVR-API-01",
            ProviderReference: null,
            SavedCardId: null,
            Amount: 50m,
            Currency: "NGN",
            Status: "Pending",
            AuthorizationUrl: "https://checkout.flutterwave.com/v3/hosted/pay/ver-api",
            FailureReason: null,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: null);

        service.InitializeCardVerificationAsync(walletId, "usr_api_verify_user", "user@test.com", "https://callback.com", Arg.Any<PaymentProvider?>(), Arg.Any<CancellationToken>())
            .Returns(verificationDto);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var request = new InitializeCardVerificationApiRequest(
                WalletId: walletId,
                Email: "user@test.com",
                CallbackUrl: "https://callback.com");

            var response = await client.PostAsJsonAsync("/api/v1/card-verification/initialize", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CardVerificationResponseDto>();
            Assert.NotNull(result);
            Assert.Equal("https://checkout.flutterwave.com/v3/hosted/pay/ver-api", result.AuthorizationUrl);
        }
    }

    [Fact]
    public async Task Complete_ValidReference_Returns200OkWithVerifiedCard()
    {
        var service = Substitute.For<ICardVerificationService>();
        var savedCardId = Guid.NewGuid();
        var verificationDto = new CardVerificationResponseDto(
            Id: Guid.NewGuid(),
            UserId: "usr_api_verify_user",
            WalletId: Guid.NewGuid(),
            Provider: "Flutterwave",
            Reference: "CBZVR-API-02",
            ProviderReference: "flw_ref_ver_02",
            SavedCardId: savedCardId,
            Amount: 50m,
            Currency: "NGN",
            Status: "Verified",
            AuthorizationUrl: null,
            FailureReason: null,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: DateTime.UtcNow);

        service.CompleteCardVerificationAsync("CBZVR-API-02", Arg.Any<CancellationToken>())
            .Returns(verificationDto);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var request = new CompleteCardVerificationApiRequest(Reference: "CBZVR-API-02");
            var response = await client.PostAsJsonAsync("/api/v1/card-verification/complete", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CardVerificationResponseDto>();
            Assert.NotNull(result);
            Assert.Equal("Verified", result.Status);
            Assert.Equal(savedCardId, result.SavedCardId);
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "usr_api_verify_user"),
                new Claim(ClaimTypes.Name, "testverifyuser@cebizpay.com")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
