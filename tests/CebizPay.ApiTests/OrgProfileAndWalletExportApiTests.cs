#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Profile;
using CebizPay.Application.UseCases.Organizations.Wallet;
using CebizPay.Domain.Permissions;
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

public sealed class OrgProfileAndWalletExportApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        ISender sender,
        ICurrentOrganizationContext orgContext)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers()
                            .AddApplicationPart(typeof(OrgProfileController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestProfileAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(sender);
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

        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task GetOrgProfile_WhenFound_Returns200WithProfilePayload()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);

        var profileDto = new OrgProfileDto(
            OrganizationId: orgId,
            Name: "Cebis Technologies",
            Email: "support@cebistech.com",
            PhoneNumber: "+234 801 234 5678",
            Address: "Victoria Island, Lagos",
            Category: "Technology",
            Status: "Active",
            LogoUrl: "https://example.com/logo.png",
            CacNumber: "RC1234567",
            CacCertificateUrl: "https://example.com/cac.pdf",
            RegisteredAtUtc: DateTime.UtcNow.AddYears(-1));

        sender.Send(Arg.Is<GetOrgProfileQuery>(q => q.OrganizationId == orgId), Arg.Any<CancellationToken>())
              .Returns(profileDto);

        var (host, client) = await CreateTestServer(sender, orgContext);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/profile");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("success").GetBoolean());
            var data = json.GetProperty("data");
            Assert.Equal("Cebis Technologies", data.GetProperty("name").GetString());
            Assert.Equal("support@cebistech.com", data.GetProperty("email").GetString());
            Assert.Equal("RC1234567", data.GetProperty("cacNumber").GetString());
            Assert.Equal("https://example.com/cac.pdf", data.GetProperty("cacCertificateUrl").GetString());
        }
    }

    [Fact]
    public async Task GetOrgProfile_WhenNotFound_Returns404()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        sender.Send(Arg.Any<GetOrgProfileQuery>(), Arg.Any<CancellationToken>())
              .Returns((OrgProfileDto?)null);

        var (host, client) = await CreateTestServer(sender, orgContext);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/profile");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task ExportWalletTransactions_WhenAuthorized_Returns200WithCsvFile()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        orgContext.HasPermissionAsync(orgId, Permissions.WalletView, Arg.Any<CancellationToken>())
                  .Returns(true);

        const string csvContent = "Transaction ID,Reference,Date (UTC),Type,Direction,Amount (NGN),Status,Counterparty,Description\n1,REF-1,2026-09-17,Transfer,Debit,500.00,Completed,Bank,Vendor Payment\n";
        var exportResult = new ExportOrgWalletTransactionsResult(
            System.Text.Encoding.UTF8.GetBytes(csvContent),
            "text/csv; charset=utf-8",
            "org-wallet-transactions.csv");

        sender.Send(Arg.Any<ExportOrgWalletTransactionsQuery>(), Arg.Any<CancellationToken>())
              .Returns(exportResult);

        var (host, client) = await CreateTestServer(sender, orgContext);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/wallet/transactions/export");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("REF-1", content);
            Assert.Contains("Vendor Payment", content);
        }
    }

    [Fact]
    public async Task ExportWalletTransactions_WhenForbidden_Returns403()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.CurrentOrganizationId.Returns(orgId);
        orgContext.HasPermissionAsync(orgId, Permissions.WalletView, Arg.Any<CancellationToken>())
                  .Returns(false);

        var (host, client) = await CreateTestServer(sender, orgContext);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/wallet/transactions/export");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}

public sealed class TestProfileAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestProfileAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "OrganizationAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
