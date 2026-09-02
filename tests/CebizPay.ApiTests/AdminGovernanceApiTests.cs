#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Manage;
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

public sealed class AdminGovernanceApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator, string role = "SuperAdmin")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminManageController).Assembly);
                    services.AddAuthentication("AdminTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("AdminTestScheme", _ => { });
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
    public async Task GetAdminDirectory_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pageResult = new PagedResult<AdminProfileDto>(
            new List<AdminProfileDto>
            {
                new(Guid.NewGuid(), "usr_admin_1", "admin1@cebizpay.com", null, "Admin", true, false, new List<string>(), DateTime.UtcNow, null)
            }, 1, 1, 20);

        mediator.Send(Arg.Any<GetAdminDirectoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(pageResult);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/manage?pageNumber=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task InviteAdmin_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var inviteResponse = new InviteAdminResponseDto(
            Guid.NewGuid(),
            "invited@cebizpay.com",
            "Admin",
            "mock-crypto-token-64hexchars",
            DateTime.UtcNow.AddHours(24));

        mediator.Send(Arg.Any<InviteAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(inviteResponse);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/manage/invite", new
            {
                email = "invited@cebizpay.com",
                role = AdminRoleType.Admin
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<InviteAdminResponseDto>();
            Assert.NotNull(body);
            Assert.Equal("invited@cebizpay.com", body.Email);
            Assert.Equal("mock-crypto-token-64hexchars", body.InvitationToken);
        }
    }

    [Fact]
    public async Task InviteAdmin_AsAuditor_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/manage/invite", new
            {
                email = "invited@cebizpay.com",
                role = AdminRoleType.Admin
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ToggleAdminStatus_AsSuperAdmin_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var profileId = Guid.NewGuid();
        var profileDto = new AdminProfileDto(
            profileId,
            "usr_target",
            "target@cebizpay.com",
            null,
            "Admin",
            false,
            false,
            new List<string>(),
            DateTime.UtcNow,
            DateTime.UtcNow);

        mediator.Send(Arg.Any<ToggleAdminStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(profileDto);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync("/api/v1/admin/manage/toggle-status", new
            {
                adminProfileId = profileId,
                isActive = false
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ToggleAdminStatus_AsAuditor_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PatchAsJsonAsync("/api/v1/admin/manage/toggle-status", new
            {
                adminProfileId = Guid.NewGuid(),
                isActive = false
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task DeleteAdmin_AsSuperAdmin_Returns204NoContent()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(mediator, "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.DeleteAsync($"/api/v1/admin/manage/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task DeleteAdmin_AsAuditor_Returns403Forbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, "Auditor");
        using (host)
        using (client)
        {
            var response = await client.DeleteAsync($"/api/v1/admin/manage/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task RedeemAdminInvite_AnonymousRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var redeemResponse = new RedeemAdminInviteResponseDto(
            true,
            "usr_new_admin",
            "redeemed@cebizpay.com",
            "Admin",
            "mock-access-token",
            "mock-refresh-token",
            null);

        mediator.Send(Arg.Any<RedeemAdminInviteCommand>(), Arg.Any<CancellationToken>())
            .Returns(redeemResponse);

        var (host, client) = await CreateTestServer(mediator, "None");
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/manage/redeem-invite", new
            {
                invitationToken = "valid-token",
                password = "SecurePassword123!"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<RedeemAdminInviteResponseDto>();
            Assert.NotNull(body);
            Assert.True(body.Succeeded);
            Assert.Equal("mock-access-token", body.AccessToken);
        }
    }

    private sealed record TestUserRole(string Role);

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TestUserRole _role;

        public TestAuthHandler(
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
            if (_role.Role == "None")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin_user_001"),
                new Claim(ClaimTypes.Name, "admin@cebizpay.com"),
                new Claim(ClaimTypes.Role, _role.Role)
            };
            var identity = new ClaimsIdentity(claims, "AdminTestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "AdminTestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
