using CebizPay.Domain.Referrals.Enums;

namespace CebizPay.Domain.Referrals.Entities;

/// <summary>
/// Domain aggregate tracking an immutable 1-to-1 association between a referring user and a referred user.
/// Strictly enforces self-referral rejection and qualification lifecycle states.
/// </summary>
public class ReferralRelationship
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Referring user identity ID.</summary>
    public string ReferrerUserId { get; private set; } = string.Empty;

    /// <summary>Referred new user identity ID.</summary>
    public string ReferredUserId { get; private set; } = string.Empty;

    /// <summary>Referral code entity ID used during association.</summary>
    public Guid ReferralCodeId { get; private set; }

    /// <summary>Snapshot of referral code string used during association.</summary>
    public string ReferralCode { get; private set; } = string.Empty;

    /// <summary>Milestone qualification status.</summary>
    public ReferralQualificationStatus QualificationStatus { get; private set; } = ReferralQualificationStatus.Pending;

    /// <summary>Reward entitlement eligibility status.</summary>
    public ReferralRewardEligibility RewardEligibility { get; private set; } = ReferralRewardEligibility.Pending;

    /// <summary>Timestamp when referred user registered or claimed the referral association.</summary>
    public DateTime RegisteredAtUtc { get; private set; }

    /// <summary>Timestamp when both KYC Tier 1 and minimum qualifying deposit were satisfied.</summary>
    public DateTime? QualifiedAtUtc { get; private set; }

    /// <summary>Transaction reference of the qualifying deposit.</summary>
    public string? QualifyingDepositReference { get; private set; }

    /// <summary>Monetary amount of the qualifying deposit in NGN.</summary>
    public decimal? QualifyingDepositAmount { get; private set; }

    /// <summary>Risk or anti-abuse review commentary.</summary>
    public string? RiskReviewNotes { get; private set; }

    /// <summary>Timestamp of relationship record creation.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp of last relationship update.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    private ReferralRelationship() { } // EF Core

    /// <summary>
    /// Creates a new referral relationship under strict self-referral prevention.
    /// </summary>
    public static ReferralRelationship Create(
        string referrerUserId,
        string referredUserId,
        Guid referralCodeId,
        string referralCode,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(referrerUserId))
        {
            throw new ArgumentException("ReferrerUserId is required.", nameof(referrerUserId));
        }

        if (string.IsNullOrWhiteSpace(referredUserId))
        {
            throw new ArgumentException("ReferredUserId is required.", nameof(referredUserId));
        }

        if (string.Equals(referrerUserId.Trim(), referredUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Self-referral is strictly forbidden.");
        }

        if (string.IsNullOrWhiteSpace(referralCode))
        {
            throw new ArgumentException("ReferralCode is required.", nameof(referralCode));
        }

        return new ReferralRelationship
        {
            Id = Guid.NewGuid(),
            ReferrerUserId = referrerUserId.Trim(),
            ReferredUserId = referredUserId.Trim(),
            ReferralCodeId = referralCodeId,
            ReferralCode = referralCode.Trim().ToUpperInvariant(),
            QualificationStatus = ReferralQualificationStatus.Pending,
            RewardEligibility = ReferralRewardEligibility.Pending,
            RegisteredAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Transitions relationship to Qualified upon satisfaction of KYC and deposit requirements.
    /// </summary>
    public void Qualify(
        decimal depositAmount,
        string depositReference,
        ReferralRewardEligibility eligibility,
        DateTime now,
        string? riskNotes = null)
    {
        if (QualificationStatus == ReferralQualificationStatus.Disqualified)
        {
            throw new InvalidOperationException("Cannot qualify a disqualified referral relationship.");
        }

        QualificationStatus = ReferralQualificationStatus.Qualified;
        RewardEligibility = eligibility;
        QualifiedAtUtc = now;
        QualifyingDepositAmount = depositAmount;
        QualifyingDepositReference = depositReference;
        RiskReviewNotes = riskNotes;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Holds the referral relationship for risk or compliance review.
    /// </summary>
    public void HoldForRisk(string riskNotes, DateTime now)
    {
        RewardEligibility = ReferralRewardEligibility.HeldForRiskReview;
        RiskReviewNotes = riskNotes;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Disqualifies the referral relationship due to fraud or policy breach.
    /// </summary>
    public void Disqualify(string reason, DateTime now)
    {
        QualificationStatus = ReferralQualificationStatus.Disqualified;
        RewardEligibility = ReferralRewardEligibility.Ineligible;
        RiskReviewNotes = reason;
        UpdatedAtUtc = now;
    }
}
