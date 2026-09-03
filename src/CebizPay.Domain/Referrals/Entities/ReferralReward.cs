using CebizPay.Domain.Referrals.Enums;

namespace CebizPay.Domain.Referrals.Entities;

/// <summary>
/// Domain entity representing a future referral reward entitlement.
/// Financial reward movement is strictly DISABLED in Phase 6D:
/// Contains no wallet credit, no ledger transaction, and no fund transfers.
/// </summary>
public class ReferralReward
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Associated referral relationship ID.</summary>
    public Guid ReferralRelationshipId { get; private set; }

    /// <summary>Referring beneficiary user ID.</summary>
    public string ReferrerUserId { get; private set; } = string.Empty;

    /// <summary>Referred user ID whose qualification created the entitlement.</summary>
    public string ReferredUserId { get; private set; } = string.Empty;

    /// <summary>Fixed monetary reward amount in NGN.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Three-letter ISO currency code (NGN).</summary>
    public string Currency { get; private set; } = "NGN";

    /// <summary>Reward entitlement lifecycle state.</summary>
    public ReferralRewardStatus Status { get; private set; } = ReferralRewardStatus.Pending;

    /// <summary>Timestamp when reward was marked eligible.</summary>
    public DateTime? EligibleAtUtc { get; private set; }

    /// <summary>Future ledger posting reference (null until future activation phase).</summary>
    public string? LedgerTransactionReference { get; private set; }

    /// <summary>Timestamp of reward entity creation.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp of last reward entity update.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    private ReferralReward() { } // EF Core

    /// <summary>
    /// Creates a new referral reward entitlement record.
    /// </summary>
    public static ReferralReward Create(
        Guid referralRelationshipId,
        string referrerUserId,
        string referredUserId,
        decimal amount,
        ReferralRewardStatus initialStatus,
        DateTime now)
    {
        if (referralRelationshipId == Guid.Empty)
        {
            throw new ArgumentException("ReferralRelationshipId is required.", nameof(referralRelationshipId));
        }

        if (string.IsNullOrWhiteSpace(referrerUserId))
        {
            throw new ArgumentException("ReferrerUserId is required.", nameof(referrerUserId));
        }

        if (string.IsNullOrWhiteSpace(referredUserId))
        {
            throw new ArgumentException("ReferredUserId is required.", nameof(referredUserId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Reward amount must be strictly positive.", nameof(amount));
        }

        return new ReferralReward
        {
            Id = Guid.NewGuid(),
            ReferralRelationshipId = referralRelationshipId,
            ReferrerUserId = referrerUserId.Trim(),
            ReferredUserId = referredUserId.Trim(),
            Amount = amount,
            Currency = "NGN",
            Status = initialStatus,
            EligibleAtUtc = initialStatus == ReferralRewardStatus.Eligible ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Marks the reward as eligible for future activation.
    /// </summary>
    public void MarkEligible(DateTime now)
    {
        Status = ReferralRewardStatus.Eligible;
        EligibleAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Suspends reward eligibility for compliance or risk investigation.
    /// </summary>
    public void HoldForRiskReview(DateTime now)
    {
        Status = ReferralRewardStatus.HeldForRiskReview;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Rejects the reward entitlement.
    /// </summary>
    public void Reject(string reason, DateTime now)
    {
        Status = ReferralRewardStatus.Rejected;
        UpdatedAtUtc = now;
    }
}
