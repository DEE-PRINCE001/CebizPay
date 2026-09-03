namespace CebizPay.Domain.Referrals.Enums;

/// <summary>
/// Lifecycle qualification status of a referred user relationship.
/// </summary>
public enum ReferralQualificationStatus
{
    /// <summary>Pending completion of KYC Tier 1 and minimum qualifying deposit.</summary>
    Pending = 1,

    /// <summary>Successfully satisfied all mandatory qualification milestones.</summary>
    Qualified = 2,

    /// <summary>Disqualified due to policy violation or fraudulent association.</summary>
    Disqualified = 3
}
