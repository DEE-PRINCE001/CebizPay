using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
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

public sealed class SavedCardsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(ISavedCardService savedCardService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(SavedCardsController).Assembly);
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
                    services.AddSingleton(savedCardService);
                    var currentUserService = Substitute.For<ICurrentUserService>();
                    currentUserService.UserId.Returns("usr_api_card_user");
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
    public async Task GetSavedCards_AuthenticatedUser_Returns200OkWithCardList()
    {
        var service = Substitute.For<ISavedCardService>();
        var cards = new List<SavedCardResponseDto>
        {
            new(Guid.NewGuid(), "usr_api_card_user", Guid.NewGuid(), "Flutterwave", "4242", "Visa", "12", "2030", "Test User", "Active", true, DateTime.UtcNow, null)
        };

        service.GetSavedCardsForUserAsync("usr_api_card_user", Arg.Any<CancellationToken>())
            .Returns(cards);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/saved-cards");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<List<SavedCardResponseDto>>();
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("4242", result[0].Last4);
        }
    }

    [Fact]
    public async Task SetDefaultCard_ValidId_Returns200Ok()
    {
        var service = Substitute.For<ISavedCardService>();
        var cardId = Guid.NewGuid();
        var cardDto = new SavedCardResponseDto(
            cardId, "usr_api_card_user", Guid.NewGuid(), "Paystack", "1111", "Mastercard", "05", "2029", "Test User", "Active", true, DateTime.UtcNow, null);

        service.SetDefaultCardAsync(cardId, "usr_api_card_user", Arg.Any<CancellationToken>())
            .Returns(cardDto);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var response = await client.PostAsync($"/api/v1/saved-cards/{cardId}/default", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SavedCardResponseDto>();
            Assert.NotNull(result);
            Assert.True(result.IsDefault);
        }
    }

    [Fact]
    public async Task RevokeCard_ValidId_Returns200OkWithRevokedStatus()
    {
        var service = Substitute.For<ISavedCardService>();
        var cardId = Guid.NewGuid();
        var cardDto = new SavedCardResponseDto(
            cardId, "usr_api_card_user", Guid.NewGuid(), "Flutterwave", "9999", "Visa", "01", "2028", "Test User", "Revoked", false, DateTime.UtcNow, null);

        service.RevokeSavedCardAsync(cardId, "usr_api_card_user", Arg.Any<CancellationToken>())
            .Returns(cardDto);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var response = await client.DeleteAsync($"/api/v1/saved-cards/{cardId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SavedCardResponseDto>();
            Assert.NotNull(result);
            Assert.Equal("Revoked", result.Status);
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
                new Claim(ClaimTypes.NameIdentifier, "usr_api_card_user"),
                new Claim(ClaimTypes.Name, "testcarduser@cebizpay.com")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
