#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Organizations;
using CebizPay.Application.UseCases.Admin.Wallets;
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

public sealed class AdminWalletManagementApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, string role = "Admin")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminWalletsController).Assembly);
                    services.AddAuthentication("AdminWalletTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAdminWalletAuthHandler>("AdminWalletTestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
                    services.AddSingleton<ISender>(mediator);
                    services.AddSingleton(new TestAdminWalletRole(role));
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
    public async Task GetOrganizationWalletsDirectory_ShouldReturn200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var fakeResult = new PagedResult<AdminOrganizationWalletSummaryDto>(
            new List<AdminOrganizationWalletSummaryDto>
            {
                new(Guid.NewGuid(), "Cebis Tech", null, 100000m, 50000m, 20000m, "NGN", "Active")
            },
            1, 1, 10);

        mediator.Send(Arg.Any<GetAdminOrganizationWalletsDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/admin/wallets/organizations");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<AdminOrganizationWalletSummaryDto>>();
            Assert.NotNull(content);
            Assert.Equal(1, content.TotalCount);
            Assert.Equal("Cebis Tech", content.Items[0].Name);
        }
    }

    [Fact]
    public async Task ExportOrganizationWallets_ShouldReturn200File()
    {
        var mediator = Substitute.For<IMediator>();
        var fakeResult = new ExportAdminOrganizationWalletsResult(
            System.Text.Encoding.UTF8.GetBytes("Id,Name\n1,Cebis"),
            "text/csv; charset=utf-8",
            "organization_wallets_export.csv");

        mediator.Send(Arg.Any<ExportAdminOrganizationWalletsQuery>(), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/admin/wallets/organizations/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        }
    }

    [Fact]
    public async Task GetIndividualWalletsDirectory_ShouldReturn200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var fakeResult = new PagedResult<AdminIndividualWalletSummaryDto>(
            new List<AdminIndividualWalletSummaryDto>
            {
                new(Guid.NewGuid(), "Adejumo Michael", null, 80000m, 15000m, "NGN", "Active")
            },
            1, 1, 10);

        mediator.Send(Arg.Any<GetAdminIndividualWalletsDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/admin/wallets/individuals");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<AdminIndividualWalletSummaryDto>>();
            Assert.NotNull(content);
            Assert.Equal(1, content.TotalCount);
            Assert.Equal("Adejumo Michael", content.Items[0].Name);
        }
    }

    [Fact]
    public async Task ExportIndividualWallets_ShouldReturn200File()
    {
        var mediator = Substitute.For<IMediator>();
        var fakeResult = new ExportAdminIndividualWalletsResult(
            System.Text.Encoding.UTF8.GetBytes("Id,Name\n1,Michael"),
            "text/csv; charset=utf-8",
            "individual_wallets_export.csv");

        mediator.Send(Arg.Any<ExportAdminIndividualWalletsQuery>(), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/admin/wallets/individuals/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetOrganizationWallet_ShouldReturn200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var fakeResult = new AdminOrganizationWalletDetailsDto(
            orgId,
            "WAL-ORG-12345678",
            "NGN",
            250000m,
            120000m,
            50000m,
            "0123456789",
            "Wema Bank / CebizPay",
            "Active");

        mediator.Send(Arg.Is<GetAdminOrganizationWalletQuery>(q => q.Id == orgId), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}/wallet");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<AdminOrganizationWalletDetailsDto>();
            Assert.NotNull(content);
            Assert.Equal(orgId, content.OrganizationId);
            Assert.Equal(250000m, content.CurrentBalance);
            Assert.Equal("0123456789", content.VirtualAccountNumber);
        }
    }

    [Fact]
    public async Task GetOrganizationSalaries_ShouldReturn200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var fakeResult = new PagedResult<AdminOrganizationSalaryItemDto>(
            new List<AdminOrganizationSalaryItemDto>
            {
                new("sal-1029384756", 34000m, "2619861816688", "Wallet ID", "156191667631", "January", DateTime.UtcNow, "Successfull")
            },
            1, 1, 10);

        mediator.Send(Arg.Is<GetAdminOrganizationSalariesQuery>(q => q.OrganizationId == orgId), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}/salaries");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<AdminOrganizationSalaryItemDto>>();
            Assert.NotNull(content);
            Assert.Equal(1, content.TotalCount);
            Assert.Equal("Successfull", content.Items[0].Status);
            Assert.Equal(34000m, content.Items[0].Amount);
        }
    }

    [Fact]
    public async Task ExportOrganizationSalaries_ShouldReturn200File()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var fakeResult = new ExportAdminOrganizationSalariesResult(
            System.Text.Encoding.UTF8.GetBytes("Disbursement ID,Amount\nsal-1,34000"),
            "text/csv; charset=utf-8",
            "org_salaries.csv");

        mediator.Send(Arg.Is<ExportAdminOrganizationSalariesQuery>(q => q.OrganizationId == orgId), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}/salaries/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetOrganizationSavings_ShouldReturn200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var fakeResult = new AdminOrganizationSavingsListDto(
            new List<AdminOrganizationSavingsItemDto>
            {
                new("sav-org-001", "Corporate Reserve Fund", 10000000m, 4500000m, "Monthly", 12.0m, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Active")
            },
            1);

        mediator.Send(Arg.Is<GetAdminOrganizationSavingsQuery>(q => q.OrganizationId == orgId), Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}/savings");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<AdminOrganizationSavingsListDto>();
            Assert.NotNull(content);
            Assert.Equal(1, content.TotalCount);
            Assert.Equal("Corporate Reserve Fund", content.Items[0].Name);
            Assert.Equal(4500000m, content.Items[0].CurrentAmount);
        }
    }
}

public sealed record TestAdminWalletRole(string Role);

public sealed class TestAdminWalletAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestAdminWalletRole _role;

    public TestAdminWalletAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAdminWalletRole role)
        : base(options, logger, encoder)
    {
        _role = role;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-admin-wallet-user"),
            new Claim(ClaimTypes.Role, _role.Role)
        };
        var identity = new ClaimsIdentity(claims, "AdminWalletTestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "AdminWalletTestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
