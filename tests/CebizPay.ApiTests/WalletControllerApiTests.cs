using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
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

public sealed class WalletControllerApiTests
{
    [Fact]
    public async Task PeerTransfer_WithCanonicalIdempotencyKeyHeader_ShouldPassHeaderToCommand()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var idempotencyKey = Guid.NewGuid().ToString();

        mediator.Send(Arg.Any<PeerTransferCommand>(), Arg.Any<CancellationToken>())
            .Returns(new PeerTransferResponseDto(
                TransactionReference: "CBZPT-TESTREF12345",
                Status: "COMPLETED",
                Amount: 1000m,
                Currency: "NGN",
                FeeAmount: 10m,
                TotalDebited: 1010m,
                RecipientDisplay: "recipient@example.com",
                AppliedFeePolicyVersion: 1,
                CreatedAtUtc: DateTime.UtcNow));

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
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/transfer/peer")
        {
            Content = JsonContent.Create(new PeerTransferRequest(
                RecipientIdentifier: "recipient@example.com",
                Amount: 1000m,
                Currency: "NGN",
                TransactionPin: "1234"))
        };

        // Set canonical header
        requestMessage.Headers.Add("Idempotency-Key", idempotencyKey);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await mediator.Received(1).Send(
            Arg.Is<PeerTransferCommand>(c => c.IdempotencyKey == idempotencyKey && c.Amount == 1000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PeerTransfer_MissingIdempotencyKeyHeaderAndBody_ShouldReturn400BadRequest()
    {
        // Arrange
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
        var requestContent = JsonContent.Create(new PeerTransferRequest(
            RecipientIdentifier: "recipient@example.com",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: null));

        // Act
        var response = await client.PostAsync("/api/v1/wallet/transfer/peer", requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task BankTransfer_WithCanonicalIdempotencyKeyHeader_ShouldPassHeaderToCommand()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var idempotencyKey = Guid.NewGuid().ToString();

        mediator.Send(Arg.Any<BankTransferCommand>(), Arg.Any<CancellationToken>())
            .Returns(new BankTransferResponseDto(
                TransactionReference: "CBZBT-TESTREF12345",
                Status: "PENDING",
                Amount: 5000m,
                Currency: "NGN",
                FeeAmount: 50m,
                TotalDebited: 5050m,
                DestinationBankCode: "058",
                DestinationAccountNumber: "******6789",
                DestinationAccountName: "John Doe",
                AppliedFeePolicyVersion: 1,
                CreatedAtUtc: DateTime.UtcNow));

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
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/transfer/bank")
        {
            Content = JsonContent.Create(new BankTransferRequest(
                DestinationBankCode: "058",
                DestinationAccountNumber: "0123456789",
                Amount: 5000m,
                Currency: "NGN",
                TransactionPin: "1234"))
        };

        requestMessage.Headers.Add("Idempotency-Key", idempotencyKey);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await mediator.Received(1).Send(
            Arg.Is<BankTransferCommand>(c => c.IdempotencyKey == idempotencyKey && c.Amount == 5000m && c.DestinationBankCode == "058"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BankTransfer_MissingIdempotencyKeyHeaderAndBody_ShouldReturn400BadRequest()
    {
        // Arrange
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
        var requestContent = JsonContent.Create(new BankTransferRequest(
            DestinationBankCode: "058",
            DestinationAccountNumber: "0123456789",
            Amount: 5000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: null));

        // Act
        var response = await client.PostAsync("/api/v1/wallet/transfer/bank", requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", doc.RootElement.GetProperty("code").GetString());
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
