#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Organizations;
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

public sealed class AdminOrganizationsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, string role = "Admin")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminOrganizationsController).Assembly);
                    services.AddAuthentication("AdminTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAdminOrgAuthHandler>("AdminTestScheme", _ => { });
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
                    services.AddSingleton(new TestAdminOrgRole(role));
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
    public async Task GetOrganizationsDirectory_AsAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pageResult = new PagedResult<AdminOrganizationSummaryDto>(
            new List<AdminOrganizationSummaryDto>
            {
                new(Guid.NewGuid(), "Cebis Tech", "Technology", "cebistech@gmail.com", "Abuja", "Pending", 5, null, DateTime.UtcNow)
            }, 1, 1, 10);

        mediator.Send(Arg.Any<GetAdminOrganizationsDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(pageResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/organizations?pageNumber=1&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PagedResult<AdminOrganizationSummaryDto>>();
            Assert.NotNull(body);
            Assert.Equal(1, body.TotalCount);
            Assert.Equal("Cebis Tech", body.Items[0].Name);
        }
    }

    [Fact]
    public async Task GetOrganizationById_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var details = new AdminOrganizationDetailsDto(
            orgId,
            "Cebis Tech",
            "Technology",
            "cebistech@gmail.com",
            "Abuja",
            "Pending",
            10,
            null,
            null,
            DateTime.UtcNow,
            new List<AdminOrganizationCredentialDto>
            {
                new("doc-001", "CAC Certificate", "CAC_CERTIFICATE", "https://storage.url/cac.pdf", DateTime.UtcNow)
            });

        mediator.Send(Arg.Any<GetAdminOrganizationDetailsQuery>(), Arg.Any<CancellationToken>())
            .Returns(details);

        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminOrganizationDetailsDto>();
            Assert.NotNull(body);
            Assert.Equal(orgId, body.Id);
            Assert.Equal("Cebis Tech", body.Name);
            Assert.Single(body.Credentials);
        }
    }

    [Fact]
    public async Task GetOrganizationById_WhenNotFound_Returns404NotFound()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAdminOrganizationDetailsQuery>(), Arg.Any<CancellationToken>())
            .Returns((AdminOrganizationDetailsDto?)null);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetOrganizationStaff_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var staffResult = new PagedResult<AdminStaffRosterItemDto>(
            new List<AdminStaffRosterItemDto>
            {
                new("staff-001", "Johnson Mike", "wallet-123", "02826893 Access", "mike@gmail.com", "50,000.00", "Verified", null)
            }, 1, 1, 10);

        mediator.Send(Arg.Any<GetAdminOrganizationStaffRosterQuery>(), Arg.Any<CancellationToken>())
            .Returns(staffResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}/staff?pageNumber=1&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PagedResult<AdminStaffRosterItemDto>>();
            Assert.NotNull(body);
            Assert.Equal(1, body.TotalCount);
            Assert.Equal("Johnson Mike", body.Items[0].Name);
        }
    }

    [Fact]
    public async Task GetOrganizationDocuments_WhenFound_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var docsResult = new AdminOrganizationDocumentsResponseDto(
            orgId,
            new List<AdminOrganizationDocumentDto>
            {
                new("doc-001", "CAC Certificate", "CAC_CERTIFICATE", "https://storage.url/cac.pdf", 1024, DateTime.UtcNow)
            });

        mediator.Send(Arg.Any<GetAdminOrganizationDocumentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(docsResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/admin/organizations/{orgId}/documents");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AdminOrganizationDocumentsResponseDto>();
            Assert.NotNull(body);
            Assert.Equal(orgId, body.OrganizationId);
            Assert.Single(body.Documents);
        }
    }

    [Fact]
    public async Task ExportOrganizations_ReturnsCsvFile()
    {
        var mediator = Substitute.For<IMediator>();
        var csvBytes = Encoding.UTF8.GetBytes("OrgId,Name\n1,Cebis Tech");
        var exportResult = new ExportAdminOrganizationsResult(csvBytes, "text/csv", "organizations_export.csv");

        mediator.Send(Arg.Any<ExportAdminOrganizationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(exportResult);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/organizations/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.NotNull(response.Content.Headers.ContentDisposition);
            Assert.Equal("organizations_export.csv", response.Content.Headers.ContentDisposition.FileName);
        }
    }

    [Fact]
    public async Task GetOrganizationsDirectory_WhenUnauthorizedRole_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "StandardUser");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/organizations");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}

public sealed record TestAdminOrgRole(string Role);

public sealed class TestAdminOrgAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestAdminOrgRole _role;

    public TestAdminOrgAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAdminOrgRole role)
        : base(options, logger, encoder)
    {
        _role = role;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test_admin_user"),
            new Claim(ClaimTypes.Role, _role.Role)
        };
        var identity = new ClaimsIdentity(claims, "AdminTestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "AdminTestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
