using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Application.UseCases.Vas.Commands.PurchaseAirtime;
using CebizPay.Application.UseCases.Vas.Commands.PurchaseData;
using CebizPay.Application.UseCases.Vas.Queries.DetectOperator;
using CebizPay.Application.UseCases.Vas.Queries.GetDataBundles;
using CebizPay.Application.UseCases.Vas.Queries.GetVasTransactionById;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Enums;
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

public sealed class VasApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(ISender sender)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(VasController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestVasAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(sender);
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

        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task PurchaseAirtime_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        var (host, client) = await CreateTestServer(sender);

        var responseDto = new VasPurchaseResponseDto(
            Reference: "CBZVAS-AIR-12345",
            Type: "AIRTIME",
            Status: "SUCCEEDED",
            Amount: 1000m,
            Currency: "NGN",
            Network: "MTN",
            MaskedPhoneNumber: "0803***4567",
            ProductCode: null,
            ProductName: "Airtime Top-up",
            CreatedAtUtc: DateTime.UtcNow);

        sender.Send(Arg.Any<PurchaseAirtimeCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var request = new PurchaseAirtimeApiRequest(
            PhoneNumber: "08031234567",
            Network: "MTN",
            Amount: 1000m,
            TransactionPin: "1234",
            IdempotencyKey: "idemp-airtime-1");

        // Act
        var httpResponse = await client.PostAsJsonAsync("/api/v1/vas/airtime", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var body = await httpResponse.Content.ReadFromJsonAsync<VasPurchaseResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("CBZVAS-AIR-12345", body!.Reference);
        Assert.Equal("SUCCEEDED", body.Status);

        await host.StopAsync();
    }

    [Fact]
    public async Task PurchaseData_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        var (host, client) = await CreateTestServer(sender);

        var responseDto = new VasPurchaseResponseDto(
            Reference: "CBZVAS-DAT-67890",
            Type: "DATA",
            Status: "SUCCEEDED",
            Amount: 280m,
            Currency: "NGN",
            Network: "MTN",
            MaskedPhoneNumber: "0803***4567",
            ProductCode: "MTN-1GB",
            ProductName: "Data Bundle (MTN-1GB)",
            CreatedAtUtc: DateTime.UtcNow);

        sender.Send(Arg.Any<PurchaseDataCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var request = new PurchaseDataApiRequest(
            PhoneNumber: "08031234567",
            Network: "MTN",
            ProductCode: "MTN-1GB",
            Amount: 280m,
            TransactionPin: "1234",
            IdempotencyKey: "idemp-data-1");

        // Act
        var httpResponse = await client.PostAsJsonAsync("/api/v1/vas/data", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var body = await httpResponse.Content.ReadFromJsonAsync<VasPurchaseResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("CBZVAS-DAT-67890", body!.Reference);

        await host.StopAsync();
    }

    [Fact]
    public async Task GetDataBundles_ReturnsCatalog()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        var (host, client) = await CreateTestServer(sender);

        IReadOnlyList<DataBundleDto> bundles =
        [
            new("MTN-1GB", VasNetwork.Mtn, "MTN 1GB 30-Day", "1GB", "30 Days", 280m, Currency.NGN)
        ];

        sender.Send(Arg.Any<GetDataBundlesQuery>(), Arg.Any<CancellationToken>())
            .Returns(bundles);

        // Act
        var httpResponse = await client.GetAsync("/api/v1/vas/data/bundles?network=MTN");

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var body = await httpResponse.Content.ReadFromJsonAsync<List<DataBundleDto>>();
        Assert.NotNull(body);
        Assert.Single(body!);
        Assert.Equal("MTN-1GB", body![0].ProductCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task DetectOperator_WithValidPhone_ReturnsDetectedNetwork()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        var (host, client) = await CreateTestServer(sender);

        var responseDto = new OperatorDetectionResponseDto(
            Succeeded: true,
            Network: "MTN",
            ErrorMessage: null);

        sender.Send(Arg.Any<DetectOperatorQuery>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        // Act
        var httpResponse = await client.GetAsync("/api/v1/vas/operators/detect?phoneNumber=08031234567");

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var body = await httpResponse.Content.ReadFromJsonAsync<OperatorDetectionResponseDto>();
        Assert.NotNull(body);
        Assert.True(body!.Succeeded);
        Assert.Equal("MTN", body.Network);

        await host.StopAsync();
    }

    [Fact]
    public async Task GetVasTransactionById_ReturnsTransactionDetails()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        var (host, client) = await CreateTestServer(sender);
        var txnId = Guid.NewGuid();

        var responseDto = new VasTransactionResponseDto(
            Id: txnId,
            Reference: "CBZVAS-AIR-5555",
            Type: "AIRTIME",
            Status: "SUCCEEDED",
            Amount: 1000m,
            Currency: "NGN",
            Network: "MTN",
            MaskedPhoneNumber: "0803***4567",
            ProductCode: null,
            ProductName: "Airtime Top-up",
            ProviderReference: "VTU-REF-5555",
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: DateTime.UtcNow,
            ReversedAtUtc: null,
            FailureReason: null);

        sender.Send(Arg.Any<GetVasTransactionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        // Act
        var httpResponse = await client.GetAsync($"/api/v1/vas/transactions/{txnId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var body = await httpResponse.Content.ReadFromJsonAsync<VasTransactionResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(txnId, body!.Id);
        Assert.Equal("CBZVAS-AIR-5555", body.Reference);

        await host.StopAsync();
    }
}

internal sealed class TestVasAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestVasAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("sub", "test-user-id"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
