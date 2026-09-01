#pragma warning disable CS1591
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Compliance;
using CebizPay.Domain.Compliance.Enums;
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

public sealed class ComplianceApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IMediator mediator)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(ComplianceController).Assembly);
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

        var client = host.GetTestClient();
        return (host, client);
    }

    [Fact]
    public async Task VerifyBvn_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var responseDto = new VerificationOperationResponse(
            Guid.NewGuid(),
            "CBZKYC-20260830120000-A1B2C3D4",
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            VerificationStatus.Completed,
            VerificationProvider.Dojah,
            VerificationProvider.Dojah,
            false,
            VerificationResultStatus.Match,
            100m,
            "BVN verified.",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            new List<VerificationEvidenceSummaryDto>());

        mediator.Send(Arg.Any<VerifyBvnCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var request = new VerifyBvnRequest("22222222222", "Emeka", "Okonkwo", new DateTime(1990, 1, 15));
            var response = await client.PostAsJsonAsync("/api/v1/compliance/kyc/bvn", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<VerificationOperationResponse>();
            Assert.NotNull(result);
            Assert.Equal("CBZKYC-20260830120000-A1B2C3D4", result.Reference);
            Assert.Equal(VerificationStatus.Completed, result.Status);
        }
    }

    [Fact]
    public async Task VerifyNin_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var responseDto = new VerificationOperationResponse(
            Guid.NewGuid(),
            "CBZKYC-20260830120000-B2C3D4E5",
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            VerificationStatus.Completed,
            VerificationProvider.Dojah,
            VerificationProvider.Dojah,
            false,
            VerificationResultStatus.Match,
            100m,
            "NIN verified.",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            new List<VerificationEvidenceSummaryDto>());

        mediator.Send(Arg.Any<VerifyNinCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var request = new VerifyNinRequest("12345678901", "Ada", "Eze", new DateTime(1995, 3, 10));
            var response = await client.PostAsJsonAsync("/api/v1/compliance/kyc/nin", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task VerifyBiometrics_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var responseDto = new VerificationOperationResponse(
            Guid.NewGuid(),
            "CBZKYC-20260830120000-C3D4E5F6",
            VerificationType.IndividualKyc,
            VerificationCapability.Biometrics,
            VerificationStatus.Completed,
            VerificationProvider.SmileId,
            VerificationProvider.SmileId,
            false,
            VerificationResultStatus.Match,
            98m,
            "Liveness confirmed.",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            new List<VerificationEvidenceSummaryDto>());

        mediator.Send(Arg.Any<VerifyBiometricsCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var request = new VerifyBiometricsRequest("dGVzdF9zZWxmaWU=");
            var response = await client.PostAsJsonAsync("/api/v1/compliance/kyc/biometrics", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task VerifyBusiness_ValidRequest_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var orgId = Guid.NewGuid();
        var responseDto = new VerificationOperationResponse(
            Guid.NewGuid(),
            "CBZKYB-20260830120000-D4E5F6A7",
            VerificationType.OrganizationKyb,
            VerificationCapability.Business,
            VerificationStatus.Completed,
            VerificationProvider.Dojah,
            VerificationProvider.Dojah,
            false,
            VerificationResultStatus.Match,
            100m,
            "CAC verified.",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            new List<VerificationEvidenceSummaryDto>());

        mediator.Send(Arg.Any<VerifyBusinessCommand>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var request = new VerifyBusinessRequest(orgId, "RC-123456", "Acme Global Limited");
            var response = await client.PostAsJsonAsync("/api/v1/compliance/kyb/business", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetOperationByReference_Existing_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var responseDto = new VerificationOperationResponse(
            Guid.NewGuid(),
            "CBZKYC-20260830120000-REF123",
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            VerificationStatus.Completed,
            VerificationProvider.Dojah,
            VerificationProvider.Dojah,
            false,
            VerificationResultStatus.Match,
            100m,
            "Evidence captured.",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            new List<VerificationEvidenceSummaryDto>());

        mediator.Send(Arg.Any<GetVerificationOperationByReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(responseDto);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/compliance/operations/CBZKYC-20260830120000-REF123");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetEvidence_ValidQuery_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var pagedResult = new PagedResult<VerificationEvidenceSummaryDto>(
            new List<VerificationEvidenceSummaryDto>(),
            0,
            1,
            20);

        mediator.Send(Arg.Any<GetVerificationEvidenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/compliance/evidence?pageNumber=1&pageSize=20");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetProfile_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var profileResponse = new ComplianceProfileResponse(
            new CddProfileDto(Guid.NewGuid(), RiskSubjectType.Individual, "usr_test_123", null, CddStatus.Completed, RiskRating.Low, CddLevel.Basic, 1, null, DateTime.UtcNow, DateTime.UtcNow, null),
            new ComplianceDecisionDto(Guid.NewGuid(), RiskSubjectType.Individual, "usr_test_123", null, ComplianceDecisionType.Approved, RiskRating.Low, CddLevel.Basic, null, "All clear", "2026.1", "System", false, null, DateTime.UtcNow, null, true),
            new List<ComplianceRestrictionDto>());

        mediator.Send(Arg.Any<GetComplianceProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(profileResponse);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.GetAsync("/api/v1/compliance/profile");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task CheckEligibility_Returns200Ok()
    {
        var mediator = Substitute.For<IMediator>();
        var eligibilityResult = TransactionEligibilityResult.Allowed();

        mediator.Send(Arg.Any<CheckTransactionEligibilityQuery>(), Arg.Any<CancellationToken>())
            .Returns(eligibilityResult);

        var (host, client) = await CreateTestServer(mediator);
        using (host)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/compliance/eligibility/check", new
            {
                operationType = ComplianceOperationType.BankTransferPayout,
                amount = 25000m,
                currency = CebizPay.Domain.Finance.Enums.Currency.NGN
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
