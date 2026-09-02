using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.UseCases.Auth.RefreshToken;
using CebizPay.Application.UseCases.Auth.RevokeToken;
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

namespace CebizPay.ApiTests;

public sealed class RefreshTokenApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(ISender sender)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
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
                    services.AddSingleton(sender);
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
    public async Task RefreshToken_WhenSuccess_ShouldReturnOk()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RefreshTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshTokenResponseDto(true, "user-123", "access_jwt_abc", "new_refresh_xyz", null));

        var (host, client) = await CreateTestServer(sender);
        using (host)
        {
            var request = new RefreshTokenCommand("valid_token_123");

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDto>();
            Assert.NotNull(body);
            Assert.True(body.Succeeded);
            Assert.Equal("access_jwt_abc", body.AccessToken);
            Assert.Equal("new_refresh_xyz", body.RefreshToken);
        }
    }

    [Fact]
    public async Task RefreshToken_WhenInvalidOrExpired_ShouldReturnBadRequest()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RefreshTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshTokenResponseDto(false, null, null, null, "Invalid refresh token."));

        var (host, client) = await CreateTestServer(sender);
        using (host)
        {
            var request = new RefreshTokenCommand("invalid_token");

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDto>();
            Assert.NotNull(body);
            Assert.False(body.Succeeded);
            Assert.Equal("Invalid refresh token.", body.ErrorMessage);
        }
    }

    [Fact]
    public async Task RevokeToken_WhenSuccess_ShouldReturnOk()
    {
        // Arrange
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RevokeTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RevokeTokenResponseDto(true, "Token revoked successfully."));

        var (host, client) = await CreateTestServer(sender);
        using (host)
        {
            var request = new RevokeTokenCommand("token_to_revoke");

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/auth/revoke-token", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<RevokeTokenResponseDto>();
            Assert.NotNull(body);
            Assert.True(body.Succeeded);
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
