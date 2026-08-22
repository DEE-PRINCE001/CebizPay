using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

public sealed class VirtualAccountsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        IVirtualAccountService virtualAccountService,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(VirtualAccountsController).Assembly);
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
                    services.AddSingleton(virtualAccountService);
                    services.AddSingleton(currentUserService);
                    services.AddSingleton(orgContext);
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
    public async Task Provision_AuthenticatedIndividual_Returns200OkWithVirtualAccount()
    {
        // Arrange
        var vaService = Substitute.For<IVirtualAccountService>();
        var userService = Substitute.For<ICurrentUserService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        userService.UserId.Returns("usr_test_123");
        orgContext.CurrentOrganizationId.Returns((Guid?)null);

        var expectedDto = new VirtualAccountDto(
            Id: Guid.NewGuid(),
            IndividualId: "usr_test_123",
            OrganizationId: null,
            Provider: PaymentProvider.Flutterwave,
            AccountNumber: "0123456789",
            AccountName: "Test User",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: Currency.NGN,
            Status: VirtualAccountStatus.Active,
            CreatedAtUtc: DateTime.UtcNow);

        vaService.ProvisionIndividualVirtualAccountAsync("usr_test_123", Currency.NGN, PaymentProvider.Flutterwave, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var (host, client) = await CreateTestServer(vaService, userService, orgContext);
        using (host)
        {
            var request = new ProvisionVirtualAccountApiRequest(Currency.NGN, PaymentProvider.Flutterwave);

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/virtual-accounts/provision", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<VirtualAccountDto>();
            Assert.NotNull(result);
            Assert.Equal("0123456789", result.AccountNumber);
        }
    }

    [Fact]
    public async Task GetPrimary_WhenAccountExists_Returns200Ok()
    {
        // Arrange
        var vaService = Substitute.For<IVirtualAccountService>();
        var userService = Substitute.For<ICurrentUserService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        userService.UserId.Returns("usr_test_123");
        orgContext.CurrentOrganizationId.Returns((Guid?)null);

        var expectedDto = new VirtualAccountDto(
            Id: Guid.NewGuid(),
            IndividualId: "usr_test_123",
            OrganizationId: null,
            Provider: PaymentProvider.Flutterwave,
            AccountNumber: "0123456789",
            AccountName: "Test User",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: Currency.NGN,
            Status: VirtualAccountStatus.Active,
            CreatedAtUtc: DateTime.UtcNow);

        vaService.GetVirtualAccountForOwnerAsync("usr_test_123", null, Currency.NGN, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var (host, client) = await CreateTestServer(vaService, userService, orgContext);
        using (host)
        {
            // Act
            var response = await client.GetAsync("/api/v1/virtual-accounts/primary?currency=1");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<VirtualAccountDto>();
            Assert.NotNull(result);
            Assert.Equal("0123456789", result.AccountNumber);
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
