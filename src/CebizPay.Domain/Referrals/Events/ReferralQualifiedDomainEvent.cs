using CebizPay.Domain.Referrals.Enums;

namespace CebizPay.Domain.Referrals.Events;

/// <summary>
/// Domain event emitted when a referral relationship completes qualification milestones.
/// </summary>
public sealed record ReferralQualifiedDomainEvent(
    Guid RelationshipId,
    string ReferrerUserId,
    string ReferredUserId,
    decimal RewardAmount,
    ReferralRewardEligibility Eligibility,
    DateTime OccurredOnUtc);
