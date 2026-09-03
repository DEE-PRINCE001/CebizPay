namespace CebizPay.Domain.Referrals.Entities;

/// <summary>
/// Domain entity representing the active global referral program configuration.
/// Enforces positive fixed monetary rewards and positive maximum successful referral caps.
/// </summary>
public class ReferralSetting
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Fixed reward amount in NGN per successfully qualified referral.</summary>
    public decimal RewardAmountPerSuccessfulReferral { get; private set; }

    /// <summary>Maximum lifetime count of successful/eligible referrals per referring user.</summary>
    public int MaximumSuccessfulReferralsPerUser { get; private set; }

    /// <summary>Flag indicating whether the referral program is currently active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Optimistic concurrency version number.</summary>
    public int Version { get; private set; } = 1;

    /// <summary>Timestamp of creation.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp of last update.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Identity of administrator who updated the configuration.</summary>
    public string UpdatedBy { get; private set; } = string.Empty;

    private ReferralSetting() { } // EF Core

    /// <summary>
    /// Creates the authoritative default referral configuration.
    /// </summary>
    public static ReferralSetting CreateDefault(
        decimal defaultReward = 500.00m,
        int defaultMaxReferrals = 10,
        string createdBy = "System")
    {
        ValidateValues(defaultReward, defaultMaxReferrals);

        var now = DateTime.UtcNow;
        return new ReferralSetting
        {
            Id = Guid.NewGuid(),
            RewardAmountPerSuccessfulReferral = defaultReward,
            MaximumSuccessfulReferralsPerUser = defaultMaxReferrals,
            IsActive = true,
            Version = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    /// <summary>
    /// Updates the global referral parameters under administrator authorization.
    /// </summary>
    public void Update(
        decimal rewardAmount,
        int maximumReferrals,
        bool isActive,
        string updatedBy,
        DateTime now)
    {
        ValidateValues(rewardAmount, maximumReferrals);

        if (string.IsNullOrWhiteSpace(updatedBy))
        {
            throw new ArgumentException("UpdatedBy must be provided.", nameof(updatedBy));
        }

        RewardAmountPerSuccessfulReferral = rewardAmount;
        MaximumSuccessfulReferralsPerUser = maximumReferrals;
        IsActive = isActive;
        Version++;
        UpdatedAtUtc = now;
        UpdatedBy = updatedBy;
    }

    private static void ValidateValues(decimal rewardAmount, int maximumReferrals)
    {
        if (rewardAmount <= 0)
        {
            throw new ArgumentException("Reward amount per successful referral must be strictly positive.", nameof(rewardAmount));
        }

        if (maximumReferrals <= 0)
        {
            throw new ArgumentException("Maximum successful referrals per user must be strictly positive.", nameof(maximumReferrals));
        }
    }
}
