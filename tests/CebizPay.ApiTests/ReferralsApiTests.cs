using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.UseCases.Referrals;
using CebizPay.Domain.Referrals.Enums;
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

public sealed class ReferralsApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        IMediator mediator,
        bool authenticated = true,
        string role = "User")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers()
                        .AddApplicationPart(typeof(ProfileReferralsController).Assembly);

                    services.AddAuthentication("ReferralTestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestReferralAuthHandler>("ReferralTestScheme", _ => { });

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
                    services.AddSingleton(new TestReferralAuthContext(authenticated, role));
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
    public async Task GetDashboard_AuthenticatedUser_ReturnsOkWithDashboard()
    {
        var mediator = Substitute.For<IMediator>();
        var dashboardDto = new ReferralDashboardDto(
            ReferralCode: "CBZTEST123",
            TotalReferrals: 5,
            QualifiedReferrals: 3,
            RemainingCapacity: 7,
            ConfiguredRewardAmount: 500m,
            PendingRewardAmount: 1000m,
            EligibleRewardAmount: 1500m,
            Referrals: new List<ReferralItemDto>());

        mediator.Send(Arg.Any<GetReferralDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(dashboardDto);

        var (host, client) = await CreateTestServer(mediator, authenticated: true);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/profile/referrals");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<ReferralDashboardDto>();
            Assert.NotNull(body);
            Assert.Equal("CBZTEST123", body.ReferralCode);
            Assert.Equal(5, body.TotalReferrals);
            Assert.Equal(3, body.QualifiedReferrals);
        }
    }

    [Fact]
    public async Task GetDashboard_Unauthenticated_ReturnsUnauthorized()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: false);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/profile/referrals");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetOrCreateCode_AuthenticatedUser_ReturnsOkWithCode()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetOrCreateReferralCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns("CBZMYCODE99");

        var (host, client) = await CreateTestServer(mediator, authenticated: true);
        using (host)
        using (client)
        {
            var response = await client.PostAsync("/api/v1/profile/referrals/code", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<CodeResponse>();
            Assert.NotNull(result);
            Assert.Equal("CBZMYCODE99", result.ReferralCode);
        }
    }

    [Fact]
    public async Task ClaimCode_ValidPayload_ReturnsOkWithRelationshipId()
    {
        var mediator = Substitute.For<IMediator>();
        var relId = Guid.NewGuid();
        mediator.Send(Arg.Any<ClaimReferralCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(relId);

        var (host, client) = await CreateTestServer(mediator, authenticated: true);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/profile/referrals/claim", new ClaimReferralCodeRequest("CBZPARTNER1"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ClaimResponse>();
            Assert.NotNull(result);
            Assert.Equal(relId, result.RelationshipId);
        }
    }

    [Fact]
    public async Task GetSettings_SuperAdmin_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var settingDto = new ReferralSettingDto(500m, 10, true, 1, DateTime.UtcNow, "super_admin");
        mediator.Send(Arg.Any<GetReferralSettingQuery>(), Arg.Any<CancellationToken>())
            .Returns(settingDto);

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/referrals/settings");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<ReferralSettingDto>();
            Assert.NotNull(body);
            Assert.Equal(500m, body.RewardAmountPerSuccessfulReferral);
            Assert.Equal(10, body.MaximumSuccessfulReferralsPerUser);
        }
    }

    [Fact]
    public async Task GetSettings_Auditor_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var settingDto = new ReferralSettingDto(500m, 10, true, 1, DateTime.UtcNow, "super_admin");
        mediator.Send(Arg.Any<GetReferralSettingQuery>(), Arg.Any<CancellationToken>())
            .Returns(settingDto);

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "Auditor");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/referrals/settings");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetSettings_RegularUser_ReturnsForbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "User");
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/admin/referrals/settings");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task UpdateSettings_SuperAdmin_ReturnsOk()
    {
        var mediator = Substitute.For<IMediator>();
        var settingDto = new ReferralSettingDto(1000m, 20, true, 2, DateTime.UtcNow, "super_admin");
        mediator.Send(Arg.Any<UpdateReferralSettingCommand>(), Arg.Any<CancellationToken>())
            .Returns(settingDto);

        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "SuperAdmin");
        using (host)
        using (client)
        {
            var response = await client.PutAsJsonAsync("/api/v1/admin/referrals/settings",
                new UpdateReferralSettingRequest(1000m, 20, true));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task UpdateSettings_Auditor_ReturnsForbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "Auditor");
        using (host)
        using (client)
        {
            var response = await client.PutAsJsonAsync("/api/v1/admin/referrals/settings",
                new UpdateReferralSettingRequest(1000m, 20, true));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task UpdateSettings_RegularUser_ReturnsForbidden()
    {
        var mediator = Substitute.For<IMediator>();
        var (host, client) = await CreateTestServer(mediator, authenticated: true, role: "User");
        using (host)
        using (client)
        {
            var response = await client.PutAsJsonAsync("/api/v1/admin/referrals/settings",
                new UpdateReferralSettingRequest(1000m, 20, true));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private sealed record CodeResponse(string ReferralCode);
    private sealed record ClaimResponse(Guid RelationshipId);
}

internal sealed record TestReferralAuthContext(bool Authenticated, string Role = "User");

internal sealed class TestReferralAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestReferralAuthContext _context;

    public TestReferralAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestReferralAuthContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_context.Authenticated)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthenticated"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test_user_referral_001"),
            new Claim(ClaimTypes.Email, "testuser@cebizpay.com"),
            new Claim(ClaimTypes.Role, _context.Role)
        };

        var identity = new ClaimsIdentity(claims, "ReferralTestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "ReferralTestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
