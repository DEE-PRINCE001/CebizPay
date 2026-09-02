#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.ThriftOversight;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;
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

public sealed class AdminThriftOversightApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, string role = "SuperAdmin")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminThriftController).Assembly);
                    services.AddAuthentication("AdminThriftTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestThriftAuthHandler>("AdminThriftTestScheme", _ => { });
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
                    services.AddSingleton(new TestUserRole(role));
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
    public async Task GetThriftDirectory_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pageResult = new PagedResult<AdminThriftGroupSummaryDto>(
            new List<AdminThriftGroupSummaryDto>
            {
                new(Guid.NewGuid(), null, null, "creator_1", "Esusu Group 1", null, Currency.NGN, 50000m, ThriftFrequency.Monthly, 5, 5, ThriftStatus.Active, 1, 250000m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(5), DateTime.UtcNow)
            }, 1, 1, 20);

        mediator.Send(Arg.Any<GetAdminThriftDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(pageResult);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/thrifts?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetThriftDelinquencies_AsAuditor_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pageResult = new PagedResult<AdminThriftDelinquencyDto>(
            new List<AdminThriftDelinquencyDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Group Alpha", "usr_missed", "Suspended", 2, 100000m, 0m, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow)
            }, 1, 1, 20);

        mediator.Send(Arg.Any<GetAdminThriftDelinquenciesQuery>(), Arg.Any<CancellationToken>())
            .Returns(pageResult);

        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/thrifts/delinquencies?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task PauseThriftGroup_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var groupId = Guid.NewGuid();

        mediator.Send(Arg.Any<PauseThriftGroupCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/admin/thrifts/{groupId}/pause", new
            {
                reason = "Suspicious non-contribution wave"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task PauseThriftGroup_AsAuditor_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/admin/thrifts/{Guid.NewGuid()}/pause", new
            {
                reason = "Suspicious non-contribution wave"
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ResumeThriftGroup_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var groupId = Guid.NewGuid();

        mediator.Send(Arg.Any<ResumeThriftGroupCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsync($"/api/v1/admin/thrifts/{groupId}/resume", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task CreateDispute_AuthenticatedUser_Returns201Created()
    {
        var mediator = Substitute.For<IMediator>();
        var disputeId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var disputeDto = new ThriftDisputeDto(
            disputeId,
            groupId,
            "Group Beta",
            null,
            null,
            "usr_reporter",
            "Did not receive payout for cycle",
            "Open",
            null,
            null,
            DateTime.UtcNow,
            null);

        mediator.Send(Arg.Any<CreateThriftDisputeCommand>(), Arg.Any<CancellationToken>())
            .Returns(disputeDto);

        var (host, client) = await CreateTestServer(mediator, "Admin");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/thrifts/disputes", new
            {
                thriftGroupId = groupId,
                reason = "Did not receive payout for cycle"
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task ResolveDispute_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var disputeId = Guid.NewGuid();
        var disputeDto = new ThriftDisputeDto(
            disputeId,
            Guid.NewGuid(),
            "Group Beta",
            null,
            null,
            "usr_reporter",
            "Did not receive payout for cycle",
            "Resolved",
            "Investigated and manual credit executed",
            "super_admin_001",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);

        mediator.Send(Arg.Any<ResolveThriftDisputeCommand>(), Arg.Any<CancellationToken>())
            .Returns(disputeDto);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/admin/thrifts/disputes/{disputeId}/resolve", new
            {
                resolutionNotes = "Investigated and manual credit executed",
                reject = false
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed record TestUserRole(string Role);

    private sealed class TestThriftAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TestUserRole _role;

        public TestThriftAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TestUserRole role)
            : base(options, logger, encoder)
        {
            _role = role;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin_user_001"),
                new Claim(ClaimTypes.Name, "admin@cebizpay.com"),
                new Claim(ClaimTypes.Role, _role.Role)
            };
            var identity = new ClaimsIdentity(claims, "AdminThriftTestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "AdminThriftTestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
