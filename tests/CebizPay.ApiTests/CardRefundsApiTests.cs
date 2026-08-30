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

public sealed class CardRefundsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(ICardRefundService refundService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(CardRefundsController).Assembly);
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
                    services.AddSingleton(refundService);
                    var currentUserService = Substitute.For<ICurrentUserService>();
                    currentUserService.UserId.Returns("usr_api_refund_user");
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
    public async Task RequestRefund_ValidPayload_Returns200OkWithSucceededRefund()
    {
        var service = Substitute.For<ICardRefundService>();
        var fundingId = Guid.NewGuid();
        var refundDto = new CardRefundResponseDto(
            Id: Guid.NewGuid(),
            FundingTransactionId: fundingId,
            WalletId: Guid.NewGuid(),
            Provider: "Flutterwave",
            RefundReference: "CBZRF-100",
            ProviderRefundReference: "flw_ref_100",
            Amount: 5000m,
            Currency: "NGN",
            Status: "Succeeded",
            Reason: "Customer requested",
            LedgerTransactionId: Guid.NewGuid(),
            FailureReason: null,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: DateTime.UtcNow);

        service.RequestCardRefundAsync(fundingId, 5000m, "Customer requested", Arg.Any<string>(), "usr_api_refund_user", Arg.Any<CancellationToken>())
            .Returns(refundDto);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var request = new RequestCardRefundApiRequest(
                FundingTransactionId: fundingId,
                Amount: 5000m,
                Reason: "Customer requested",
                IdempotencyKey: "idem_refund_01");

            var response = await client.PostAsJsonAsync("/api/v1/card-refunds", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CardRefundResponseDto>();
            Assert.NotNull(result);
            Assert.Equal("Succeeded", result.Status);
            Assert.Equal(5000m, result.Amount);
        }
    }

    [Fact]
    public async Task GetRefundById_ExistingRefund_Returns200Ok()
    {
        var service = Substitute.For<ICardRefundService>();
        var refundId = Guid.NewGuid();
        var refundDto = new CardRefundResponseDto(
            Id: refundId,
            FundingTransactionId: Guid.NewGuid(),
            WalletId: Guid.NewGuid(),
            Provider: "Flutterwave",
            RefundReference: "CBZRF-101",
            ProviderRefundReference: "flw_ref_101",
            Amount: 3000m,
            Currency: "NGN",
            Status: "Succeeded",
            Reason: "Partial reversal",
            LedgerTransactionId: Guid.NewGuid(),
            FailureReason: null,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: DateTime.UtcNow);

        service.GetRefundByIdAsync(refundId, "usr_api_refund_user", Arg.Any<CancellationToken>())
            .Returns(refundDto);

        var (host, client) = await CreateTestServer(service);
        using (host)
        {
            var response = await client.GetAsync($"/api/v1/card-refunds/{refundId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CardRefundResponseDto>();
            Assert.NotNull(result);
            Assert.Equal(refundId, result.Id);
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
                new Claim(ClaimTypes.NameIdentifier, "usr_api_refund_user"),
                new Claim(ClaimTypes.Name, "testrefunduser@cebizpay.com")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
