#pragma warning disable CA1848, CA1873
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Entities;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure service implementing ASP.NET Core Identity operations, password history policies, MFA verification, and JWT/Refresh token lifecycle.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMfaService _mfaService;
    private readonly JwtOptions _jwtOptions;
    private readonly IApplicationDbContext? _dbContext;
    private readonly ILogger<IdentityService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="IdentityService"/>.
    /// </summary>
    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IMfaService mfaService,
        IOptions<JwtOptions> jwtOptions,
        IApplicationDbContext dbContext,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _mfaService = mfaService ?? throw new ArgumentNullException(nameof(mfaService));
        _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Backward-compatible constructor for lightweight testing without db context.
    /// </summary>
    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IMfaService mfaService,
        IOptions<JwtOptions> jwtOptions)
        : this(userManager, signInManager, mfaService, jwtOptions, null!, NullLogger<IdentityService>.Instance)
    {
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> RegisterUserAsync(
        string email,
        string password,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        string? canonicalPhone = null;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            canonicalPhone = PhoneNormalizer.NormalizeE164(phoneNumber);

            // Fast application-level check against existing users
            var phoneExists = await _userManager.Users
                .AnyAsync(u => u.PhoneNumber == canonicalPhone, cancellationToken);

            if (phoneExists)
            {
                _logger.LogWarning("Registration rejected for {Email}: phone number {Phone} is already registered.", email, canonicalPhone);
                return (false, string.Empty, ["Phone number is already registered to an account."]);
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = canonicalPhone,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return (false, string.Empty, result.Errors.Select(e => e.Description));
            }

            // Store initial password hash in password history
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                var history = new List<string> { user.PasswordHash };
                user.PasswordHistoryJson = JsonSerializer.Serialize(history);
                await _userManager.UpdateAsync(user);
            }

            return (true, user.Id, Array.Empty<string>());
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database unique constraint violation while registering user {Email} with phone {Phone}.", email, canonicalPhone);
            return (false, string.Empty, ["Phone number or email is already registered to an account."]);
        }
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, string UserId, string AccessToken, string RefreshToken, bool MfaRequired, Guid? MfaChallengeId, IEnumerable<string> Errors)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, false, null, new[] { "Invalid credentials." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return (false, string.Empty, string.Empty, string.Empty, false, null, new[] { "Account is locked due to multiple failed login attempts. Please try again in 5 minutes." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            // Web constraint: 5 failed attempts -> 5-minute lock
            if (user.AccessFailedCount >= 5)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));
            }
            return (false, string.Empty, string.Empty, string.Empty, false, null, new[] { "Invalid credentials." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // Check if MFA is required for this user profile
        var isMfaRequired = await _mfaService.IsMfaEnabledAsync(user.Id, cancellationToken);
        if (isMfaRequired)
        {
            var (challengeId, _) = await _mfaService.CreateChallengeAsync(user.Id, cancellationToken);
            // Return MfaRequired = true and MfaChallengeId, but NO tokens yet!
            return (true, user.Id, string.Empty, string.Empty, true, challengeId, Array.Empty<string>());
        }

        var (accessToken, refreshToken) = await GenerateAndSaveTokensAsync(user, null, cancellationToken);

        return (true, user.Id, accessToken, refreshToken, false, null, Array.Empty<string>());
    }

    /// <inheritdoc/>
    public async Task<(string AccessToken, string RefreshToken)> IssueTokensForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User with ID {userId} not found.");

        return await GenerateAndSaveTokensAsync(user, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, string UserId, string AccessToken, string RefreshToken, string? ErrorMessage)> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return (false, string.Empty, string.Empty, string.Empty, "Refresh token is required.");
        }

        if (_dbContext == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Database context not available.");
        }

        var tokenHash = HashToken(refreshToken);

        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid refresh token.");
        }

        // Reuse / Theft Detection: If token is already revoked, terminate all active tokens for this user
        if (token.IsRevoked)
        {
            _logger.LogWarning("Compromise detected: Attempt to reuse revoked refresh token for user {UserId}.", token.UserId);

            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == token.UserId && t.RevokedAtUtc == null && t.ExpiresAtUtc > DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var activeToken in activeTokens)
            {
                activeToken.Revoke(null, "Compromised reuse detected");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return (false, string.Empty, string.Empty, string.Empty, "Compromised or already used refresh token. All active sessions have been terminated.");
        }

        if (token.IsExpired)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Refresh token has expired. Please log in again.");
        }

        var user = await _userManager.FindByIdAsync(token.UserId);
        if (user == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "User account not found.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return (false, string.Empty, string.Empty, string.Empty, "User account is locked.");
        }

        // Rotate token: Generate new pair, revoke current token with pointer to new hash
        var (newAccessToken, newRawRefreshToken) = GenerateRawTokens(user);
        var newHashedToken = HashToken(newRawRefreshToken);

        token.Revoke(newHashedToken, "Rotated");

        var newRefreshTokenEntity = new RefreshToken(
            userId: user.Id,
            tokenHash: newHashedToken,
            expiresAtUtc: DateTime.UtcNow.AddDays(30),
            createdByIp: ipAddress);

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully rotated refresh token for user {UserId}.", user.Id);

        return (true, user.Id, newAccessToken, newRawRefreshToken, null);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || _dbContext == null)
        {
            return false;
        }

        var tokenHash = HashToken(refreshToken);

        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token == null)
        {
            return true; // Idempotent
        }

        if (!token.IsRevoked)
        {
            token.Revoke(null, "Logout");
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Revoked refresh token for user {UserId}.", token.UserId);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, IEnumerable<string> Errors)> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        bool isMobile,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, new[] { "User not found." });
        }

        // Parse password history (last 3 passwords)
        var history = string.IsNullOrEmpty(user.PasswordHistoryJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(user.PasswordHistoryJson) ?? new List<string>();

        var passwordHasher = new PasswordHasher<ApplicationUser>();

        // Check if new password matches any of the last 3 passwords
        foreach (var pastHash in history)
        {
            var verifyResult = passwordHasher.VerifyHashedPassword(user, pastHash, newPassword);
            if (verifyResult != PasswordVerificationResult.Failed)
            {
                return (false, new[] { "Cannot reuse any of your last 3 passwords." });
            }
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description));
        }

        // Add new password hash to history, keep only last 3
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            history.Add(user.PasswordHash);
            if (history.Count > 3)
            {
                history = history.Skip(history.Count - 3).ToList();
            }
            user.PasswordHistoryJson = JsonSerializer.Serialize(history);
            await _userManager.UpdateAsync(user);
        }

        return (true, Array.Empty<string>());
    }

    private async Task<(string AccessToken, string RefreshToken)> GenerateAndSaveTokensAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var (accessToken, rawRefreshToken) = GenerateRawTokens(user);

        if (_dbContext != null)
        {
            var tokenHash = HashToken(rawRefreshToken);
            var refreshTokenEntity = new RefreshToken(
                userId: user.Id,
                tokenHash: tokenHash,
                expiresAtUtc: DateTime.UtcNow.AddDays(30),
                createdByIp: ipAddress);

            _dbContext.RefreshTokens.Add(refreshTokenEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return (accessToken, rawRefreshToken);
    }

    private (string AccessToken, string RefreshToken) GenerateRawTokens(ApplicationUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtOptions.Secret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Access token: authoritative 15 minutes as per PRD §4.1 and Engineering Spec §9
        var expirationMinutes = _jwtOptions.ExpirationInMinutes > 0 ? _jwtOptions.ExpirationInMinutes : 15;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Cryptographically secure refresh token
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var rawRefreshToken = Convert.ToHexString(randomBytes).ToLowerInvariant();

        return (accessToken, rawRefreshToken);
    }

    private static string HashToken(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <inheritdoc/>
    public async Task<(bool Found, string UserId, string Email, string? PhoneNumber)> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return (false, string.Empty, string.Empty, null);
        }

        return (true, user.Id, user.Email ?? string.Empty, user.PhoneNumber);
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, (string Email, string? PhoneNumber)>> GetUserDetailsByIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var idsList = userIds.Distinct().ToList();
        if (idsList.Count == 0)
        {
            return new Dictionary<string, (string Email, string? PhoneNumber)>();
        }

        var users = await _userManager.Users
            .Where(u => idsList.Contains(u.Id))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => (u.Email ?? string.Empty, u.PhoneNumber));
    }
}
