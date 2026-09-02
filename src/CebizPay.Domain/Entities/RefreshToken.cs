namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain aggregate entity representing a rotating refresh token with a 30-day sliding window and reuse detection.
/// </summary>
public class RefreshToken
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identity User ID associated with this token.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Cryptographic SHA-256 hash of the refresh token string.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Expiration timestamp in UTC (30-day sliding window).</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Revocation timestamp in UTC (if revoked).</summary>
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>Token hash of the replacement refresh token upon rotation.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    /// <summary>Optional IP address or device identifier from which the token was created.</summary>
    public string? CreatedByIp { get; private set; }

    /// <summary>Reason for revocation (e.g. "Rotated", "Logout", "Compromised").</summary>
    public string? RevocationReason { get; private set; }

    /// <summary>Gets a value indicating whether the token has expired.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    /// <summary>Gets a value indicating whether the token has been revoked.</summary>
    public bool IsRevoked => RevokedAtUtc.HasValue;

    /// <summary>Gets a value indicating whether the token is currently active.</summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { } // EF Core constructor

    /// <summary>
    /// Initializes a new active RefreshToken.
    /// </summary>
    public RefreshToken(string userId, string tokenHash, DateTime expiresAtUtc, string? createdByIp = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("TokenHash is required.", nameof(tokenHash));

        Id = Guid.NewGuid();
        UserId = userId.Trim();
        TokenHash = tokenHash.Trim();
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        CreatedByIp = createdByIp;
    }

    /// <summary>
    /// Revokes the current token during rotation, logout, or reuse detection.
    /// </summary>
    public void Revoke(string? replacedByTokenHash = null, string? reason = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
        RevocationReason = reason ?? "Rotated";
    }
}
