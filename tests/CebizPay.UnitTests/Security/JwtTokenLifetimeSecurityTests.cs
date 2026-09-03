using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

/// <summary>
/// Verifies that JWT access token lifetime strictly adheres to the 15-minute standard
/// mandated by PRD §4.1 and Engineering Spec §9 across options, services, and token generation.
/// </summary>
public sealed class JwtTokenLifetimeSecurityTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMfaService _mfaService;

    public JwtTokenLifetimeSecurityTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(dbOptions);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(userStore, null, null, null, null, null, null, null, null);

        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        var userPrincipalFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(_userManager, contextAccessor, userPrincipalFactory, null, null, null, null);

        _mfaService = Substitute.For<IMfaService>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void JwtOptions_DefaultExpirationInMinutes_ShouldBe15Minutes()
    {
        // Act
        var options = new JwtOptions();

        // Assert: PRD §4.1 mandates 15-minute access token lifetime
        Assert.Equal(15, options.ExpirationInMinutes);
    }

    [Fact]
    public async Task IssueTokensForUserAsync_WithDefault15Minutes_ShouldGenerateTokenExpiringIn15Minutes()
    {
        // Arrange
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = "super_secret_jwt_key_at_least_32_characters_long_1234567890",
            Issuer = "CebizPay.Test",
            Audience = "CebizPay.ClientTest",
            ExpirationInMinutes = 15
        });

        var identityService = new IdentityService(
            _userManager,
            _signInManager,
            _mfaService,
            jwtOptions,
            _dbContext,
            NullLogger<IdentityService>.Instance);

        var user = new ApplicationUser
        {
            Id = "test-user-15min",
            UserName = "test@example.com",
            Email = "test@example.com"
        };
        _userManager.FindByIdAsync(user.Id).Returns(Task.FromResult<ApplicationUser?>(user));

        var beforeCall = DateTime.UtcNow;

        // Act
        var (accessToken, refreshToken) = await identityService.IssueTokensForUserAsync(user.Id);

        var afterCall = DateTime.UtcNow;

        // Assert
        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(accessToken);

        Assert.NotNull(jwtToken);
        var expiration = jwtToken.ValidTo; // Expiration in UTC

        // Expected expiration is roughly beforeCall + 15 minutes
        var expectedMin = beforeCall.AddMinutes(14.8);
        var expectedMax = afterCall.AddMinutes(15.2);

        Assert.True(expiration >= expectedMin, $"Token expiration {expiration} was earlier than {expectedMin}");
        Assert.True(expiration <= expectedMax, $"Token expiration {expiration} was later than {expectedMax}");
    }

    [Fact]
    public async Task IssueTokensForUserAsync_WithCustomExpiration_ShouldHonorConfiguredMinutes()
    {
        // Arrange: Custom deployment override (e.g. 20 minutes)
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = "super_secret_jwt_key_at_least_32_characters_long_1234567890",
            Issuer = "CebizPay.Test",
            Audience = "CebizPay.ClientTest",
            ExpirationInMinutes = 20
        });

        var identityService = new IdentityService(
            _userManager,
            _signInManager,
            _mfaService,
            jwtOptions,
            _dbContext,
            NullLogger<IdentityService>.Instance);

        var user = new ApplicationUser
        {
            Id = "test-user-custom",
            UserName = "custom@example.com",
            Email = "custom@example.com"
        };
        _userManager.FindByIdAsync(user.Id).Returns(Task.FromResult<ApplicationUser?>(user));

        var beforeCall = DateTime.UtcNow;

        // Act
        var (accessToken, _) = await identityService.IssueTokensForUserAsync(user.Id);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(accessToken);

        var expectedMin = beforeCall.AddMinutes(19.8);
        Assert.True(jwtToken.ValidTo >= expectedMin);
    }

    [Fact]
    public async Task IssueTokensForUserAsync_WithInvalidZeroOrNegativeExpiration_ShouldFallbackToSafe15Minutes()
    {
        // Arrange: Misconfigured options with 0 or negative
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = "super_secret_jwt_key_at_least_32_characters_long_1234567890",
            Issuer = "CebizPay.Test",
            Audience = "CebizPay.ClientTest",
            ExpirationInMinutes = 0
        });

        var identityService = new IdentityService(
            _userManager,
            _signInManager,
            _mfaService,
            jwtOptions,
            _dbContext,
            NullLogger<IdentityService>.Instance);

        var user = new ApplicationUser
        {
            Id = "test-user-fallback",
            UserName = "fallback@example.com",
            Email = "fallback@example.com"
        };
        _userManager.FindByIdAsync(user.Id).Returns(Task.FromResult<ApplicationUser?>(user));

        var beforeCall = DateTime.UtcNow;

        // Act
        var (accessToken, _) = await identityService.IssueTokensForUserAsync(user.Id);

        // Assert: Must fallback to authoritative 15 minutes, never 0
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(accessToken);

        var expectedMin = beforeCall.AddMinutes(14.5);
        Assert.True(jwtToken.ValidTo >= expectedMin);
    }

    [Fact]
    public void ConfigurationBinding_WithAppSettingsJson_ShouldBind15Minutes()
    {
        // Arrange: Simulate appsettings.json binding
        var json = @"
        {
            ""Jwt"": {
                ""Secret"": ""a_very_secure_and_sufficiently_long_secret_key_256bit!"",
                ""Issuer"": ""CebizPay.Api"",
                ""Audience"": ""CebizPay.Clients"",
                ""ExpirationInMinutes"": 15
            }
        }";

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        var options = new JwtOptions();
        configuration.GetSection(JwtOptions.SectionName).Bind(options);

        // Assert
        Assert.Equal(15, options.ExpirationInMinutes);
        Assert.Equal("CebizPay.Api", options.Issuer);
        Assert.Equal("CebizPay.Clients", options.Audience);
    }
}
