namespace CebizPay.Domain.Referrals.Entities;

/// <summary>
/// Domain entity representing a unique, collision-resistant referral code owned by a user.
/// Safe for public sharing; contains no sensitive personally identifiable information.
/// </summary>
public class ReferralCode
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owner user ID.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Canonical referral code string (uppercase alphanumeric).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Whether this referral code is actively accepting associations.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Timestamp of code creation.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private ReferralCode() { } // EF Core

    /// <summary>
    /// Creates a new referral code for a user.
    /// </summary>
    public static ReferralCode Create(string userId, string code, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Referral code is required.", nameof(code));
        }

        return new ReferralCode
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = now
        };
    }

    /// <summary>
    /// Deactivates this referral code.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
