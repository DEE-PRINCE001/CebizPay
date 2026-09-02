using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CebizPay.UnitTests.Security;

public sealed class IdentityServiceRefreshTokenTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMfaService _mfaService;
    private readonly IdentityService _identityService;

    public IdentityServiceRefreshTokenTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(userStore, null, null, null, null, null, null, null, null);

        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        var userPrincipalFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(_userManager, contextAccessor, userPrincipalFactory, null, null, null, null);

        _mfaService = Substitute.For<IMfaService>();

        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Secret = "super_secret_jwt_key_at_least_32_characters_long_1234567890",
            Issuer = "CebizPay.Test",
            Audience = "CebizPay.ClientTest",
            ExpirationInMinutes = 30
        });

        _identityService = new IdentityService(
            _userManager,
            _signInManager,
            _mfaService,
            jwtOptions,
            _dbContext,
            NullLogger<IdentityService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task IssueTokensForUserAsync_ShouldPersistRefreshTokenInDatabase()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-001", Email = "test@example.com" };
        _userManager.FindByIdAsync("user-001").Returns(user);

        // Act
        var (accessToken, refreshToken) = await _identityService.IssueTokensForUserAsync("user-001");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        var tokensInDb = await _dbContext.RefreshTokens.Where(t => t.UserId == "user-001").ToListAsync();
        Assert.Single(tokensInDb);
        Assert.True(tokensInDb[0].IsActive);
        Assert.False(tokensInDb[0].IsRevoked);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsValid_ShouldRotateAndReturnNewTokens()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-002", Email = "rotate@example.com" };
        _userManager.FindByIdAsync("user-002").Returns(user);

        var (_, initialRefreshToken) = await _identityService.IssueTokensForUserAsync("user-002");

        // Act
        var (succeeded, userId, newAccessToken, newRefreshToken, errorMessage) =
            await _identityService.RefreshTokenAsync(initialRefreshToken, "192.168.1.1");

        // Assert
        Assert.True(succeeded);
        Assert.Equal("user-002", userId);
        Assert.False(string.IsNullOrWhiteSpace(newAccessToken));
        Assert.False(string.IsNullOrWhiteSpace(newRefreshToken));
        Assert.NotEqual(initialRefreshToken, newRefreshToken);
        Assert.Null(errorMessage);

        // Verify old token is revoked and linked to new token hash
        var oldToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.ReplacedByTokenHash != null);
        Assert.NotNull(oldToken);
        Assert.True(oldToken.IsRevoked);
        Assert.Equal("Rotated", oldToken.RevocationReason);

        // Verify new token is active
        var activeTokens = await _dbContext.RefreshTokens.Where(t => t.UserId == "user-002" && t.RevokedAtUtc == null).ToListAsync();
        Assert.Single(activeTokens);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsExpired_ShouldReturnFailure()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-003", Email = "expired@example.com" };
        _userManager.FindByIdAsync("user-003").Returns(user);

        // Create expired token manually
        var rawToken = "expired_token_test_123456";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var expiredToken = new RefreshToken("user-003", tokenHash, DateTime.UtcNow.AddDays(-1));
        _dbContext.RefreshTokens.Add(expiredToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var (succeeded, _, _, _, errorMessage) = await _identityService.RefreshTokenAsync(rawToken);

        // Assert
        Assert.False(succeeded);
        Assert.Equal("Refresh token has expired. Please log in again.", errorMessage);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsAlreadyRevoked_ShouldTriggerReuseDetectionAndRevokeAllActiveTokens()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-compromised", Email = "victim@example.com" };
        _userManager.FindByIdAsync("user-compromised").Returns(user);

        // Step 1: Issue token
        var (_, token1) = await _identityService.IssueTokensForUserAsync("user-compromised");

        // Step 2: Legitimately rotate token1 -> token2
        var (_, _, _, token2, _) = await _identityService.RefreshTokenAsync(token1);

        // Verify token2 is active
        var activeBeforeReplay = await _dbContext.RefreshTokens.CountAsync(t => t.UserId == "user-compromised" && t.RevokedAtUtc == null);
        Assert.Equal(1, activeBeforeReplay);

        // Step 3: Attacker replays already-used token1
        var (replaySuccess, _, _, _, replayError) = await _identityService.RefreshTokenAsync(token1);

        // Assert
        Assert.False(replaySuccess);
        Assert.Contains("Compromised or already used refresh token", replayError);

        // Verify all tokens for this user are now revoked
        var activeAfterReplay = await _dbContext.RefreshTokens.CountAsync(t => t.UserId == "user-compromised" && t.RevokedAtUtc == null);
        Assert.Equal(0, activeAfterReplay);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WhenTokenExists_ShouldRevokeSuccessfully()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-logout", Email = "logout@example.com" };
        _userManager.FindByIdAsync("user-logout").Returns(user);

        var (_, refreshToken) = await _identityService.IssueTokensForUserAsync("user-logout");

        // Act
        var result = await _identityService.RevokeRefreshTokenAsync(refreshToken);

        // Assert
        Assert.True(result);
        var token = await _dbContext.RefreshTokens.FirstAsync(t => t.UserId == "user-logout");
        Assert.True(token.IsRevoked);
        Assert.Equal("Logout", token.RevocationReason);
    }
}
