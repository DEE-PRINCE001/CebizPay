using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;
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

public sealed class ThriftApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(
        IThriftGroupService groupService)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(StaffThriftController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestThriftAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(groupService);
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
    public async Task CreateGroup_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var groupService = Substitute.For<IThriftGroupService>();
        var groupId = Guid.NewGuid();

        var groupDto = new ThriftGroupDto(
            groupId,
            null,
            "test-user-id",
            "Weekend Friends Ajo",
            "Weekly rotational savings",
            Currency.NGN,
            20_000m,
            ThriftFrequency.Weekly,
            4,
            DateTime.UtcNow.AddDays(7),
            DateTime.UtcNow.AddDays(35),
            DateTime.UtcNow.AddDays(5),
            ThriftStatus.OpenForMembers,
            1,
            1,
            DateTime.UtcNow);

        groupService.CreateGroupAsync(Arg.Any<string>(), Arg.Any<CreateThriftGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(groupDto);

        var (host, client) = await CreateTestServer(groupService);
        using (host)
        {
            var request = new CreateThriftGroupRequest(
                null,
                "Weekend Friends Ajo",
                "Weekly rotational savings",
                Currency.NGN,
                20_000m,
                ThriftFrequency.Weekly,
                4,
                DateTime.UtcNow.AddDays(7),
                DateTime.UtcNow.AddDays(5));

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/work/thrift", request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ThriftGroupDto>();
            Assert.NotNull(result);
            Assert.Equal(groupId, result.Id);
            Assert.Equal("Weekend Friends Ajo", result.Name);
            Assert.Equal(4, result.TotalPositions);
        }
    }

    [Fact]
    public async Task SelectPosition_WithValidPosition_ReturnsUpdatedMember()
    {
        // Arrange
        var groupService = Substitute.For<IThriftGroupService>();
        var groupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var memberDto = new ThriftMemberDto(
            memberId,
            groupId,
            "test-user-id",
            2,
            ThriftMemberStatus.Active,
            0,
            0m,
            0m,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null);

        groupService.SelectPositionAsync(groupId, "test-user-id", Arg.Any<SelectThriftPositionRequest>(), Arg.Any<CancellationToken>())
            .Returns(memberDto);

        var (host, client) = await CreateTestServer(groupService);
        using (host)
        {
            var request = new SelectThriftPositionRequest(2);

            // Act
            var response = await client.PostAsJsonAsync($"/api/v1/work/thrift/{groupId}/position", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ThriftMemberDto>();
            Assert.NotNull(result);
            Assert.Equal(2, result.Position);
        }
    }
}

internal sealed class TestThriftAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestThriftAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("OrganizationId", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
