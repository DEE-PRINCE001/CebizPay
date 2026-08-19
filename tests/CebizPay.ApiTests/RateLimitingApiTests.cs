using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.UseCases.Auth.Login;
using CebizPay.Application.UseCases.Auth.RegisterPhone;
using CebizPay.Application.UseCases.Auth.VerifyMfa;
using CebizPay.Application.UseCases.Auth.VerifyOtp;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class RateLimitingApiTests
{
    private static async Task<HttpClient> CreateRateLimitedClient(ISender mediator)
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

                    services.AddRateLimiter(options =>
                    {
                        options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;
                        options.AddFixedWindowLimiter("AuthLoginPolicy", opt =>
                        {
                            opt.PermitLimit = 3;
                            opt.Window = TimeSpan.FromMinutes(1);
                            opt.QueueLimit = 0;
                        });
                        options.AddFixedWindowLimiter("OtpRequestPolicy", opt =>
                        {
                            opt.PermitLimit = 2;
                            opt.Window = TimeSpan.FromMinutes(1);
                            opt.QueueLimit = 0;
                        });
                        options.AddFixedWindowLimiter("OtpVerificationPolicy", opt =>
                        {
                            opt.PermitLimit = 2;
                            opt.Window = TimeSpan.FromMinutes(1);
                            opt.QueueLimit = 0;
                        });
                        options.AddFixedWindowLimiter("MfaVerificationPolicy", opt =>
                        {
                            opt.PermitLimit = 2;
                            opt.Window = TimeSpan.FromMinutes(1);
                            opt.QueueLimit = 0;
                        });
                        options.AddFixedWindowLimiter("FinancialTransferPolicy", opt =>
                        {
                            opt.PermitLimit = 2;
                            opt.Window = TimeSpan.FromMinutes(1);
                            opt.QueueLimit = 0;
                        });
                    });

                    services.AddSingleton(mediator);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            })
            .StartAsync();

        return host.GetTestClient();
    }

    [Fact]
    public async Task Login_ExceedingRateLimit_Returns429TooManyRequests()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new LoginResponseDto(true, "user1", "token", "refresh", null));

        using var client = await CreateRateLimitedClient(mediator);

        // Act - 3 allowed
        for (int i = 0; i < 3; i++)
        {
            var res = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand("test@example.com", "Password123!"));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 4th request -> 429
        var rejectedRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand("test@example.com", "Password123!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedRes.StatusCode);
    }

    [Fact]
    public async Task RegisterPhone_ExceedingRateLimit_Returns429TooManyRequests()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<RegisterPhoneCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RegisterPhoneResponseDto(true, "OTP sent successfully"));

        using var client = await CreateRateLimitedClient(mediator);

        // Act - 2 allowed
        for (int i = 0; i < 2; i++)
        {
            var res = await client.PostAsJsonAsync("/api/v1/auth/register/phone", new RegisterPhoneCommand("+2348000000000", "dev-123"));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 3rd request -> 429
        var rejectedRes = await client.PostAsJsonAsync("/api/v1/auth/register/phone", new RegisterPhoneCommand("+2348000000000", "dev-123"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedRes.StatusCode);
    }

    [Fact]
    public async Task MfaVerify_ExceedingRateLimit_Returns429TooManyRequests()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<VerifyMfaCommand>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyMfaResponseDto(true, "user-1", "access", "refresh", Array.Empty<string>()));

        using var client = await CreateRateLimitedClient(mediator);

        // Act - 2 allowed
        for (int i = 0; i < 2; i++)
        {
            var res = await client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new VerifyMfaCommand(Guid.NewGuid(), "123456"));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 3rd request -> 429
        var rejectedRes = await client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new VerifyMfaCommand(Guid.NewGuid(), "123456"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedRes.StatusCode);
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-id") };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
