namespace CebizPay.Domain.Referrals.Events;

/// <summary>
/// Domain event emitted when a new referral relationship is registered.
/// </summary>
public sealed record ReferralRelationshipCreatedDomainEvent(
    Guid RelationshipId,
    string ReferrerUserId,
    string ReferredUserId,
    string ReferralCode,
    DateTime OccurredOnUtc);
