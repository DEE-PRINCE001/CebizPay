using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure service implementing ASP.NET Core Identity operations, password history policies, MFA verification, and JWT token issuing.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMfaService _mfaService;
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Initializes a new instance of <see cref="IdentityService"/>.
    /// </summary>
    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IMfaService mfaService,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _mfaService = mfaService;
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> RegisterUserAsync(
        string email,
        string password,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = phoneNumber,
            CreatedAtUtc = DateTime.UtcNow
        };

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

        var (accessToken, refreshToken) = GenerateTokens(user);

        return (true, user.Id, accessToken, refreshToken, false, null, Array.Empty<string>());
    }

    /// <inheritdoc/>
    public async Task<(string AccessToken, string RefreshToken)> IssueTokensForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User with ID {userId} not found.");

        return GenerateTokens(user);
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

    private (string AccessToken, string RefreshToken) GenerateTokens(ApplicationUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtOptions.Secret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Access token: 15 minutes as per PRD/Engineering specs
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Refresh token: 30-day sliding window
        var refreshToken = Guid.NewGuid().ToString("N");

        return (accessToken, refreshToken);
    }
}
