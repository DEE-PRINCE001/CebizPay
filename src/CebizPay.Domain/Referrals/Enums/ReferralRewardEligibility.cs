namespace CebizPay.Domain.Referrals.Enums;

/// <summary>
/// Reward entitlement eligibility state for a qualified referral relationship.
/// </summary>
public enum ReferralRewardEligibility
{
    /// <summary>Awaiting referral qualification milestones.</summary>
    Pending = 1,

    /// <summary>Fully eligible for future reward activation when financial rewards are enabled.</summary>
    Eligible = 2,

    /// <summary>Under risk review; reward eligibility suspended pending manual or AML review.</summary>
    HeldForRiskReview = 3,

    /// <summary>Qualified referral, but referring user has reached the maximum successful referral limit.</summary>
    CapacityExceeded = 4,

    /// <summary>Ineligible for reward entitlement due to self-referral, disqualification, or abuse.</summary>
    Ineligible = 5
}
