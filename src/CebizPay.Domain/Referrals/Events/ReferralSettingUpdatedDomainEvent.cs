namespace CebizPay.Domain.Referrals.Events;

/// <summary>
/// Domain event emitted when the global referral settings are modified by an administrator.
/// </summary>
public sealed record ReferralSettingUpdatedDomainEvent(
    Guid SettingId,
    decimal RewardAmount,
    int MaximumSuccessfulReferrals,
    string UpdatedBy,
    DateTime OccurredOnUtc);
