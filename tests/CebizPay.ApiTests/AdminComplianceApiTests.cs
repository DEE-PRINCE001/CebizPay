#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.UseCases.Compliance;
using CebizPay.Domain.Compliance.Enums;
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

public sealed class AdminComplianceApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AdminComplianceController).Assembly);
                    services.AddAuthentication("AdminTestScheme")
                            .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>("AdminTestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
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
    public async Task EvaluateRisk_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var assessmentResult = new RiskAssessmentResult(
            Guid.NewGuid(),
            RiskSubjectType.Individual,
            "usr_test_99",
            null,
            RiskRating.Low,
            CddLevel.Basic,
            false,
            false,
            "2026.1",
            DateTime.UtcNow,
            null,
            "Low risk profile",
            new List<RiskFactorDto>());

        mediator.Send(Arg.Any<EvaluateRiskCommand>(), Arg.Any<CancellationToken>())
            .Returns(assessmentResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/compliance/assessments/evaluate", new
            {
                subjectType = RiskSubjectType.Individual,
                subjectId = "usr_test_99"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ApplyComplianceOverride_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var decisionDto = new ComplianceDecisionDto(
            Guid.NewGuid(),
            RiskSubjectType.Individual,
            "usr_test_99",
            null,
            ComplianceDecisionType.Approved,
            RiskRating.Medium,
            CddLevel.Standard,
            null,
            "Manual approval by compliance officer",
            "2026.1",
            "admin_chief_1",
            true,
            "Secondary documentation verified",
            DateTime.UtcNow,
            null,
            true);

        mediator.Send(Arg.Any<ApplyComplianceOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(decisionDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/compliance/decisions/override", new
            {
                subjectType = RiskSubjectType.Individual,
                subjectId = "usr_test_99",
                newDecision = ComplianceDecisionType.Approved,
                reason = "Secondary documentation verified"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task PlaceRestriction_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var restrictionDto = new ComplianceRestrictionDto(
            Guid.NewGuid(),
            RiskSubjectType.Individual,
            "usr_test_99",
            null,
            ComplianceRestrictionType.BlockBankTransfer,
            "Suspicious transfer pattern",
            null,
            null,
            "admin_risk_1",
            DateTime.UtcNow,
            true,
            null,
            null,
            null);

        mediator.Send(Arg.Any<PlaceComplianceRestrictionCommand>(), Arg.Any<CancellationToken>())
            .Returns(restrictionDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/compliance/restrictions", new
            {
                subjectType = RiskSubjectType.Individual,
                subjectId = "usr_test_99",
                restrictionType = ComplianceRestrictionType.BlockBankTransfer,
                reason = "Suspicious transfer pattern"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed class AdminTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AdminTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin_user_001"),
                new Claim(ClaimTypes.Name, "admin@cebizpay.com"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "SuperAdmin")
            };
            var identity = new ClaimsIdentity(claims, "AdminTestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "AdminTestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
