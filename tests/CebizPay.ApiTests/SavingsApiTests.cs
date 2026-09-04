using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;
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

public sealed class SavingsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        ISavingsService savingsService,
        ISavingsInterestPolicyService policyService,
        ICurrentOrganizationContext? orgContext = null,
        ICurrentUserService? currentUserService = null)
    {
        if (orgContext == null)
        {
            var mockOrg = Substitute.For<ICurrentOrganizationContext>();
            var testOrgId = Guid.NewGuid();
            mockOrg.CurrentOrganizationId.Returns(testOrgId);
            mockOrg.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            mockOrg.HasAccessToOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
            orgContext = mockOrg;
        }

        if (currentUserService == null)
        {
            var mockUser = Substitute.For<ICurrentUserService>();
            mockUser.UserId.Returns("test-user-id");
            currentUserService = mockUser;
        }

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(StaffSavingsController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestSavingsAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(savingsService);
                    services.AddSingleton(policyService);
                    services.AddSingleton(orgContext);
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
    public async Task PreviewSavings_WithValidRequest_ReturnsOkWithCalculations()
    {
        // Arrange
        var savingsService = Substitute.For<ISavingsService>();
        var policyService = Substitute.For<ISavingsInterestPolicyService>();

        var expectedResult = new SavingsPreviewResult(
            SavingsPlanType.FixedLock,
            100_000m,
            90,
            0.12m,
            2_958.90m,
            102_958.90m,
            0.025m,
            2_500m,
            97_500m);

        savingsService.PreviewSavingsAsync(Arg.Any<SavingsPreviewRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var (host, client) = await CreateTestServer(savingsService, policyService);
        using (host)
        {
            var request = new SavingsPreviewRequest(SavingsPlanType.FixedLock, 100_000m, 90);

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/work/savings/preview", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SavingsPreviewResult>();
            Assert.NotNull(result);
            Assert.Equal(100_000m, result.PrincipalAmount);
            Assert.Equal(0.12m, result.AnnualInterestRate);
            Assert.Equal(102_958.90m, result.EstimatedMaturityPayout);
            Assert.Equal(2_500m, result.EstimatedEarlyWithdrawalPenalty);
        }
    }

    [Fact]
    public async Task OpenAccount_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var savingsService = Substitute.For<ISavingsService>();
        var policyService = Substitute.For<ISavingsInterestPolicyService>();
        var accountId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var accountDto = new SavingsAccountDto(
            accountId,
            planId,
            "test-user-id",
            Guid.NewGuid(),
            Currency.NGN,
            SavingsPlanType.FixedLock,
            50_000m,
            0m,
            0m,
            SavingsAccountStatus.Active,
            0.10m,
            1,
            0.025m,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(90),
            null,
            null,
            DateTime.UtcNow);

        savingsService.OpenAccountAsync(Arg.Any<string>(), Arg.Any<OpenSavingsAccountRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(accountDto);

        var (host, client) = await CreateTestServer(savingsService, policyService);
        using (host)
        {
            var request = new OpenSavingsAccountRequest(planId, null, 50_000m, 90, null, null, null);

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/work/savings", request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SavingsAccountDto>();
            Assert.NotNull(result);
            Assert.Equal(accountId, result.Id);
            Assert.Equal(50_000m, result.PrincipalBalance);
            Assert.Equal(SavingsAccountStatus.Active, result.Status);
        }
    }
}

internal sealed class TestSavingsAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestSavingsAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("OrganizationId", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
