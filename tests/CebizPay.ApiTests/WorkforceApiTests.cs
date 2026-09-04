using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Staff;
using CebizPay.Application.UseCases.Organizations.Workforce;
using CebizPay.Application.UseCases.StaffInvitations.AcceptInvitation;
using CebizPay.Domain.Enums;
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

public sealed class WorkforceApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        ISender sender,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        orgContext.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(DepartmentsController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestWorkforceAuthHandler>("TestScheme", _ => { });
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

        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task GetDepartments_Returns200OkWithPagedResult()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var paged = new PagedResult<DepartmentDto>(
            new[] { new DepartmentDto(Guid.NewGuid(), orgId, "Engineering", "Core dev", DateTime.UtcNow, 5) },
            1, 1, 20);

        sender.Send(Arg.Any<GetDepartmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/departments");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<DepartmentDto>>();
            Assert.NotNull(content);
            Assert.Equal(1, content.TotalCount);
            Assert.Equal("Engineering", content.Items[0].Name);
        }
    }

    [Fact]
    public async Task CreateDepartment_Returns201CreatedWithId()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<CreateDepartmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(deptId);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var req = new CreateDepartmentApiRequest("Finance", "Finance and Accounts");
            var response = await client.PostAsJsonAsync("/api/v1/org/departments", req);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetRoles_Returns200OkWithPagedResult()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var paged = new PagedResult<WorkforceRoleDto>(
            new[] { new WorkforceRoleDto(Guid.NewGuid(), orgId, null, null, "Senior Engineer", "Dev", DateTime.UtcNow, 2) },
            1, 1, 20);

        sender.Send(Arg.Any<GetWorkforceRolesQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/roles");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<WorkforceRoleDto>>();
            Assert.NotNull(content);
            Assert.Equal("Senior Engineer", content.Items[0].Title);
        }
    }

    [Fact]
    public async Task GetSalaryLevels_Returns200OkWithPagedResult()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var paged = new PagedResult<SalaryLevelDto>(
            new[] { new SalaryLevelDto(Guid.NewGuid(), orgId, "Level 1", 500000m, "NGN", DateTime.UtcNow, 3) },
            1, 1, 20);

        sender.Send(Arg.Any<GetSalaryLevelsQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/levels");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<SalaryLevelDto>>();
            Assert.NotNull(content);
            Assert.Equal("Level 1", content.Items[0].LevelName);
            Assert.Equal(500000m, content.Items[0].BaseAmount);
        }
    }

    [Fact]
    public async Task CreateStaffDirect_Returns201CreatedWithMembershipId()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<CreateStaffDirectCommand>(), Arg.Any<CancellationToken>())
            .Returns(membershipId);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var req = new CreateStaffDirectApiRequest("john@test.com", "John", "Doe", "+2348011223344", null, null, null, null);
            var response = await client.PostAsJsonAsync("/api/v1/org/staff/create", req);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task TerminateStaff_Returns200Ok()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<TerminateStaffMembershipCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var req = new TerminateStaffApiRequest("Resignation");
            var response = await client.PostAsJsonAsync($"/api/v1/org/staff/{membershipId}/terminate", req);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task WorkJoinOrganization_Returns200Ok()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        userService.UserId.Returns("test-user-id");

        sender.Send(Arg.Any<AcceptStaffInvitationCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AcceptStaffInvitationResponseDto(Guid.NewGuid(), Guid.NewGuid(), "test-user-id", "Accepted"));

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var req = new JoinOrganizationApiRequest("INVITE-12345");
            var response = await client.PostAsJsonAsync("/api/v1/work/organisation/join", req);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<AcceptStaffInvitationResponseDto>();
            Assert.NotNull(content);
            Assert.Equal("Accepted", content.Status);
        }
    }
}

internal sealed class TestWorkforceAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestWorkforceAuthHandler(
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
