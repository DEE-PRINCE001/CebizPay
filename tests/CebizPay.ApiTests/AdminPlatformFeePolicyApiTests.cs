using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.UseCases.Admin.Fees;
using CebizPay.Domain.Finance.Enums;
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
/// API integration tests for platform fee policy endpoints on <see cref="AdminFeesController"/>.
/// </summary>
public sealed class AdminPlatformFeePolicyApiTests
{
    private static IHostBuilder CreateHostBuilder(ISender mediator)
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminFeesController).Assembly);
                    services.AddAuthentication("TestAdminScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAdminAuthHandler>("TestAdminScheme", _ => { });
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
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });
    }

    [Fact]
    public async Task GetActivePlatformPolicy_WhenExists_ShouldReturnOk()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var policyDto = new PlatformFeePolicyResponseDto(
            Id: Guid.NewGuid(),
            OperationType: "VirtualAccountFunding",
            CalculationMethod: "Fixed",
            FeeBearer: "CustomerPays",
            FixedAmount: 50.00m,
            PercentageRate: null,
            MinimumFee: null,
            MaximumFee: null,
            Currency: "NGN",
            IsEnabled: true,
            Version: 1,
            CreatedByUserId: "admin-user-id",
            EffectiveFromUtc: DateTime.UtcNow,
            CreatedAtUtc: DateTime.UtcNow,
            DeactivatedAtUtc: null);

        mediator.Send(Arg.Any<GetActivePlatformFeePolicyQuery>(), Arg.Any<CancellationToken>())
            .Returns(policyDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/fees/platform/active?operationType=VirtualAccountFunding");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlatformFeePolicyResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(50.00m, body.FixedAmount);
    }

    [Fact]
    public async Task CreatePlatformPolicy_ValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var policyDto = new PlatformFeePolicyResponseDto(
            Id: Guid.NewGuid(),
            OperationType: "CardFunding",
            CalculationMethod: "Percentage",
            FeeBearer: "DeductFromFunds",
            FixedAmount: null,
            PercentageRate: 0.015m,
            MinimumFee: null,
            MaximumFee: null,
            Currency: "NGN",
            IsEnabled: true,
            Version: 1,
            CreatedByUserId: "admin-user-id",
            EffectiveFromUtc: DateTime.UtcNow,
            CreatedAtUtc: DateTime.UtcNow,
            DeactivatedAtUtc: null);

        mediator.Send(Arg.Any<CreatePlatformFeePolicyCommand>(), Arg.Any<CancellationToken>())
            .Returns(policyDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        var request = new CreatePlatformFeePolicyRequest(
            OperationType: FeeOperationType.CardFunding,
            CalculationMethod: FeeCalculationMethod.Percentage,
            FeeBearer: FeeBearer.DeductFromFunds,
            PercentageRate: 0.015m,
            Currency: Currency.NGN);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/admin/fees/platform", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlatformFeePolicyResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(0.015m, body.PercentageRate);
    }

    private sealed class TestAdminAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAdminAuthHandler(
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
                new Claim(ClaimTypes.NameIdentifier, "admin-user-id"),
                new Claim(ClaimTypes.Role, "SuperAdmin")
            };
            var identity = new ClaimsIdentity(claims, "TestAdminScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestAdminScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
