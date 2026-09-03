namespace CebizPay.Domain.Referrals.Enums;

/// <summary>
/// State lifecycle for future referral reward entitlements.
/// Financial activation is strictly disabled in Phase 6D.
/// </summary>
public enum ReferralRewardStatus
{
    /// <summary>Reward entitlement pending qualification.</summary>
    Pending = 1,

    /// <summary>Reward entitlement validated and eligible; awaiting future financial activation.</summary>
    Eligible = 2,

    /// <summary>Reward entitlement held pending anti-abuse or risk investigation.</summary>
    HeldForRiskReview = 3,

    /// <summary>Reward entitlement prepared for financial settlement batch in a future phase.</summary>
    ReadyForActivation = 4,

    /// <summary>Reward financially activated and credited (UNREACHABLE in Phase 6D).</summary>
    Activated = 5,

    /// <summary>Reward entitlement rejected by compliance or fraud operations.</summary>
    Rejected = 6
}
