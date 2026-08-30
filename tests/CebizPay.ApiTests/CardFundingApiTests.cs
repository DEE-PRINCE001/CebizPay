using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Payments;
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

public sealed class CardFundingApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(ICardFundingService cardFundingService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(CardFundingController).Assembly);
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
                    services.AddSingleton(cardFundingService);
                    var currentUserService = Substitute.For<CebizPay.Application.Common.Interfaces.Security.ICurrentUserService>();
                    currentUserService.UserId.Returns("usr_test_123");
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
    public async Task ChargeSavedCard_ValidRequest_Returns200Ok()
    {
        // Arrange
        var service = Substitute.For<ICardFundingService>();
        var savedCardId = Guid.NewGuid();
        var expectedResponse = new ChargeSavedCardResponseDto(
            FundingTransactionId: Guid.NewGuid(),
            Reference: "CBZCD-SAVED-100",
            Status: "Completed",
            GrossAmount: 5000m,
            FeeAmount: 0m,
            NetCreditedAmount: 5000m,
            Currency: "NGN",
            Provider: "Flutterwave");

        service.ChargeSavedCardAsync(savedCardId, 5000m, Currency.NGN, Arg.Any<string>(), "usr_test_123", Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var request = new ChargeSavedCardApiRequest(
                SavedCardId: savedCardId,
                Amount: 5000m,
                Currency: Currency.NGN,
                IdempotencyKey: "idem_test_charge");

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/funding/card/charge-saved", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ChargeSavedCardResponseDto>();
            Assert.NotNull(result);
            Assert.Equal("Completed", result.Status);
            Assert.Equal(5000m, result.GrossAmount);
        }
    }

    [Fact]
    public async Task Initialize_ValidRequest_Returns200OkWithCheckoutUrl()
    {
        // Arrange
        var service = Substitute.For<ICardFundingService>();
        var walletId = Guid.NewGuid();
        var expectedResponse = new CardFundingInitializationResponse(
            FundingTransactionId: Guid.NewGuid(),
            Reference: "CBZCD-12345",
            AuthorizationUrl: "https://checkout.flutterwave.com/pay/xyz",
            Provider: "Flutterwave");

        service.InitializeCardFundingAsync(walletId, 10000m, Currency.NGN, PaymentProvider.Flutterwave, "https://callback.com", Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var request = new InitializeCardFundingApiRequest(
                WalletId: walletId,
                Amount: 10000m,
                Currency: Currency.NGN,
                Provider: PaymentProvider.Flutterwave,
                CallbackUrl: "https://callback.com");

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/funding/card/initialize", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CardFundingInitializationResponse>();
            Assert.NotNull(result);
            Assert.Equal("https://checkout.flutterwave.com/pay/xyz", result.AuthorizationUrl);
        }
    }

    [Fact]
    public async Task Initialize_ZeroOrNegativeAmount_Returns400BadRequest()
    {
        // Arrange
        var service = Substitute.For<ICardFundingService>();
        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var request = new InitializeCardFundingApiRequest(
                WalletId: Guid.NewGuid(),
                Amount: -500m,
                Currency: Currency.NGN,
                Provider: PaymentProvider.Flutterwave,
                CallbackUrl: "https://callback.com");

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/funding/card/initialize", request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Reconcile_ValidId_Returns200Ok()
    {
        // Arrange
        var service = Substitute.For<ICardFundingService>();
        var fundingId = Guid.NewGuid();

        service.ReconcileCardFundingAsync(fundingId, Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("provider_ref_999"));

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            // Act
            var response = await client.PostAsync($"/api/v1/funding/card/{fundingId}/reconcile", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<PaymentProviderResult>();
            Assert.NotNull(result);
            Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
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
                new Claim(ClaimTypes.NameIdentifier, "usr_test_123"),
                new Claim(ClaimTypes.Name, "testuser@cebizpay.com")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
