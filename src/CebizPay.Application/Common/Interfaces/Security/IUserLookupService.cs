namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Application-layer abstraction for looking up platform users by identity attributes.
/// Keeps Application layer free of ASP.NET Core Identity dependencies.
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// Finds a user by their email address. Returns null if not found.
    /// </summary>
    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by their phone number. Returns null if not found.
    /// </summary>
    Task<UserSummary?> FindByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal safe user representation for transfer recipient resolution.
/// Does not expose hashed credentials, PIN, or sensitive authentication data.
/// </summary>
public sealed record UserSummary(
    string UserId,
    string? Email,
    string? PhoneNumber);
