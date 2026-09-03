#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Announcements;
using CebizPay.Domain.Communication.Enums;
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

public sealed class AnnouncementsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, string role = "SuperAdmin", Guid? orgId = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AnnouncementsController).Assembly);
                    services.AddAuthentication("AnnouncementTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAnnouncementAuthHandler>("AnnouncementTestScheme", _ => { });
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
                    services.AddSingleton(new TestAuthContext(role, orgId));
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
    public async Task CreateAnnouncement_PlatformScope_Returns201Created()
    {
        var mediator = Substitute.For<IMediator>();
        var announcementId = Guid.NewGuid();
        var dto = new AnnouncementDto(
            announcementId,
            null,
            null,
            "System Upgrade",
            "Platform upgrade details",
            AnnouncementScope.Platform,
            AnnouncementStatus.Published,
            DateTime.UtcNow,
            "admin-1",
            DateTime.UtcNow,
            "admin-1",
            null,
            null,
            null,
            null);

        mediator.Send(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/announcements", new
            {
                scope = AnnouncementScope.Platform,
                title = "System Upgrade",
                description = "Platform upgrade details",
                publishImmediately = true
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AnnouncementDto>();
            Assert.NotNull(body);
            Assert.Equal(announcementId, body.Id);
            Assert.Null(body.OrganizationId);
            Assert.Equal(AnnouncementScope.Platform, body.Scope);
        }
    }

    [Fact]
    public async Task CreateAnnouncement_WorkplaceScope_Returns201Created()
    {
        var mediator = Substitute.For<IMediator>();
        var announcementId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dto = new AnnouncementDto(
            announcementId,
            orgId,
            "TechCorp",
            "Office Town Hall",
            "Town hall details",
            AnnouncementScope.Workplace,
            AnnouncementStatus.Published,
            DateTime.UtcNow,
            "hr-user",
            DateTime.UtcNow,
            "hr-user",
            null,
            null,
            null,
            null);

        mediator.Send(Arg.Any<CreateAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var (host, client) = await CreateTestServer(mediator, "HrManager", orgId);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/announcements", new
            {
                scope = AnnouncementScope.Workplace,
                title = "Office Town Hall",
                description = "Town hall details",
                publishImmediately = true
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AnnouncementDto>();
            Assert.NotNull(body);
            Assert.Equal(announcementId, body.Id);
            Assert.Equal(orgId, body.OrganizationId);
            Assert.Equal(AnnouncementScope.Workplace, body.Scope);
        }
    }

    [Fact]
    public async Task GetPlatformAnnouncements_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var paged = new PagedResult<AnnouncementDto>(
            new List<AnnouncementDto>
            {
                new(Guid.NewGuid(), null, null, "Notice 1", "Desc 1", AnnouncementScope.Platform, AnnouncementStatus.Published, DateTime.UtcNow, "admin", DateTime.UtcNow, "admin", null, null, null, null)
            }, 1, 1, 20);

        mediator.Send(Arg.Any<GetPlatformAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/announcements/platform?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetWorkplaceAnnouncements_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var paged = new PagedResult<AnnouncementDto>(
            new List<AnnouncementDto>
            {
                new(Guid.NewGuid(), orgId, "TechCorp", "Workplace Notice", "Desc", AnnouncementScope.Workplace, AnnouncementStatus.Published, DateTime.UtcNow, "hr", DateTime.UtcNow, "hr", null, null, null, null)
            }, 1, 1, 20);

        mediator.Send(Arg.Any<GetWorkplaceAnnouncementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(mediator, "Member", orgId);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/announcements/workplace?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetAnnouncementById_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var id = Guid.NewGuid();
        var dto = new AnnouncementDto(
            id,
            null,
            null,
            "Notice",
            "Desc",
            AnnouncementScope.Platform,
            AnnouncementStatus.Published,
            DateTime.UtcNow,
            "admin",
            DateTime.UtcNow,
            "admin",
            null,
            null,
            null,
            null);

        mediator.Send(Arg.Any<GetAnnouncementByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync($"/api/v1/announcements/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task PublishAnnouncement_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var id = Guid.NewGuid();
        var dto = new AnnouncementDto(
            id,
            null,
            null,
            "Notice",
            "Desc",
            AnnouncementScope.Platform,
            AnnouncementStatus.Published,
            DateTime.UtcNow,
            "admin",
            DateTime.UtcNow,
            "admin",
            null,
            null,
            null,
            null);

        mediator.Send(Arg.Any<PublishAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsync($"/api/v1/announcements/{id}/publish", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ArchiveAnnouncement_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var id = Guid.NewGuid();

        mediator.Send(Arg.Any<ArchiveAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsync($"/api/v1/announcements/{id}/archive", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task DeleteAnnouncement_Returns204NoContent()
    {
        var mediator = Substitute.For<IMediator>();
        var id = Guid.NewGuid();

        mediator.Send(Arg.Any<ArchiveAnnouncementCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.DeleteAsync($"/api/v1/announcements/{id}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetAnnouncementsDirectory_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var paged = new PagedResult<AnnouncementDto>(
            new List<AnnouncementDto>
            {
                new(Guid.NewGuid(), null, null, "Notice", "Desc", AnnouncementScope.Platform, AnnouncementStatus.Published, DateTime.UtcNow, "admin", DateTime.UtcNow, "admin", null, null, null, null)
            }, 1, 1, 20);

        mediator.Send(Arg.Any<GetAnnouncementsDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/announcements?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed record TestAuthContext(string Role, Guid? OrgId);

    private sealed class TestAnnouncementAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TestAuthContext _context;

        public TestAnnouncementAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TestAuthContext context)
            : base(options, logger, encoder)
        {
            _context = context;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test_user_001"),
                new(ClaimTypes.Name, "test@cebizpay.com"),
                new(ClaimTypes.Role, _context.Role)
            };

            if (_context.OrgId.HasValue)
            {
                claims.Add(new Claim("OrganizationId", _context.OrgId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "AnnouncementTestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "AnnouncementTestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
