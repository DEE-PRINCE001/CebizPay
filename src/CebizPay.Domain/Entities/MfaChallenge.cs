namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain entity representing a short-lived, single-use MFA authentication challenge.
/// </summary>
public class MfaChallenge
{
    /// <summary>Unique challenge identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>User ID for whom challenge was issued.</summary>
    public string UserId { get; private set; } = string.Empty;
    /// <summary>SHA-256 hash of the 6-digit MFA challenge code.</summary>
    public string CodeHash { get; private set; } = string.Empty;
    /// <summary>Challenge expiration timestamp (short-lived, e.g. 5 minutes).</summary>
    public DateTime ExpiresAtUtc { get; private set; }
    /// <summary>Flag indicating if challenge was already used.</summary>
    public bool IsUsed { get; private set; }
    /// <summary>Failed verification attempt counter (rate limited to max 3 attempts).</summary>
    public int FailedAttempts { get; private set; }
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private MfaChallenge() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="MfaChallenge"/>.
    /// </summary>
    public MfaChallenge(string userId, string codeHash, TimeSpan expiryWindow)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("CodeHash is required.", nameof(codeHash));

        Id = Guid.NewGuid();
        UserId = userId.Trim();
        CodeHash = codeHash.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(expiryWindow);
        IsUsed = false;
        FailedAttempts = 0;
    }

    /// <summary>
    /// Marks the challenge as consumed/used.
    /// </summary>
    public void MarkUsed()
    {
        IsUsed = true;
    }

    /// <summary>
    /// Increments the failed attempt counter.
    /// </summary>
    public void IncrementFailedAttempts()
    {
        FailedAttempts++;
    }
}
