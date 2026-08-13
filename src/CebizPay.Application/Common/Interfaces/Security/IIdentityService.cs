namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Application abstraction for Identity user management, authentication, MFA flow, and token issuing.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Registers a new user.
    /// </summary>
    Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> RegisterUserAsync(
        string email,
        string password,
        string? phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user with email and password.
    /// Supports lockout after 5 failed attempts (5-minute lock) and MFA enforcement.
    /// </summary>
    Task<(bool Succeeded, string UserId, string AccessToken, string RefreshToken, bool MfaRequired, Guid? MfaChallengeId, IEnumerable<string> Errors)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues JWT access token (15 mins) and refresh token for a user after successful authentication/MFA verification.
    /// </summary>
    Task<(string AccessToken, string RefreshToken)> IssueTokensForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes user password respecting password policy and past 3 password history rules.
    /// </summary>
    Task<(bool Succeeded, IEnumerable<string> Errors)> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        bool isMobile,
        CancellationToken cancellationToken = default);
}
