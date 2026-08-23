using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Recruitment;
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

public sealed class RecruitmentApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        ISender sender,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(OrgRecruitmentJobsController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestRecruitmentAuthHandler>("TestScheme", _ => { });
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
    public async Task GetOrgJobPostings_Returns200OkWithPagedResult()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        var paged = new PagedResult<JobPostingDto>(
            new[]
            {
                new JobPostingDto(
                    Guid.NewGuid(),
                    orgId,
                    "Backend Engineer",
                    "C# APIs",
                    null, null, null, null, null, null, null, null,
                    EmploymentType.FullTime,
                    "Lagos",
                    null, null, null,
                    JobPostingStatus.Published,
                    DateTime.UtcNow, null,
                    "usr_admin", DateTime.UtcNow, null, 3)
            },
            1, 1, 20);

        sender.Send(Arg.Any<GetOrgJobPostingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(paged);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var response = await client.GetAsync("/api/v1/org/recruitment/jobs");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<PagedResult<JobPostingDto>>();
            Assert.NotNull(content);
            Assert.Equal(1, content.TotalCount);
            Assert.Equal("Backend Engineer", content.Items[0].Title);
            Assert.Equal(3, content.Items[0].ApplicationCount);
        }
    }

    [Fact]
    public async Task CreateJobPosting_Returns201Created()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<CreateJobPostingCommand>(), Arg.Any<CancellationToken>())
            .Returns(jobId);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var req = new CreateJobPostingApiRequest("Data Engineer", "ETL Pipelines");
            var response = await client.PostAsJsonAsync("/api/v1/org/recruitment/jobs", req);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task PublishAndCloseJobPosting_Returns200Ok()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<PublishJobPostingCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        sender.Send(Arg.Any<CloseJobPostingCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var pubResponse = await client.PostAsync($"/api/v1/org/recruitment/jobs/{jobId}/publish", null);
            Assert.Equal(HttpStatusCode.OK, pubResponse.StatusCode);

            var closeResponse = await client.PostAsync($"/api/v1/org/recruitment/jobs/{jobId}/close", null);
            Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        }
    }

    [Fact]
    public async Task OrgApplicationReviewWorkflow_Returns200Ok()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var orgId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        orgContext.CurrentOrganizationId.Returns(orgId);

        sender.Send(Arg.Any<ReviewApplicationCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        sender.Send(Arg.Any<ShortlistApplicationCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        sender.Send(Arg.Any<AcceptApplicationCommand>(), Arg.Any<CancellationToken>()).Returns(true);

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            var reviewRes = await client.PostAsJsonAsync($"/api/v1/org/recruitment/applications/{appId}/review", new ReviewApplicationApiRequest("Good profile"));
            Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

            var shortlistRes = await client.PostAsJsonAsync($"/api/v1/org/recruitment/applications/{appId}/shortlist", new ShortlistApplicationApiRequest("Shortlisted"));
            Assert.Equal(HttpStatusCode.OK, shortlistRes.StatusCode);

            var acceptRes = await client.PostAsJsonAsync($"/api/v1/org/recruitment/applications/{appId}/accept", new AcceptApplicationApiRequest("Offer made"));
            Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);
        }
    }

    [Fact]
    public async Task PublicJobEndpoints_AllowAnonymousAccess()
    {
        var sender = Substitute.For<ISender>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userService = Substitute.For<ICurrentUserService>();

        var jobId = Guid.NewGuid();
        var paged = new PagedResult<PublicJobPostingDto>(
            new[]
            {
                new PublicJobPostingDto(
                    jobId,
                    Guid.NewGuid(),
                    "Acme Corp",
                    "Solutions Architect",
                    "Cloud architecture",
                    "IT",
                    "Architect",
                    EmploymentType.FullTime,
                    "Remote",
                    "AWS/Azure",
                    "Design systems",
                    DateTime.UtcNow.AddDays(30),
                    DateTime.UtcNow)
            },
            1, 1, 20);

        sender.Send(Arg.Any<GetPublicJobPostingsQuery>(), Arg.Any<CancellationToken>()).Returns(paged);
        sender.Send(Arg.Any<SubmitApplicationCommand>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var (host, client) = await CreateTestServer(sender, orgContext, userService);
        using (host)
        {
            // 1. Browse public jobs
            var browseRes = await client.GetAsync("/api/v1/recruitment/jobs");
            Assert.Equal(HttpStatusCode.OK, browseRes.StatusCode);

            // 2. Submit application
            var submitReq = new SubmitApplicationApiRequest("Ada Lovelace", "ada@example.com", "+2348000000000");
            var submitRes = await client.PostAsJsonAsync($"/api/v1/recruitment/jobs/{jobId}/applications", submitReq);
            Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        }
    }
}

internal sealed class TestRecruitmentAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestRecruitmentAuthHandler(
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
