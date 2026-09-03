namespace CebizPay.Domain.Referrals.Events;

/// <summary>
/// Domain event emitted when a user generates a referral code.
/// </summary>
public sealed record ReferralCodeCreatedDomainEvent(
    Guid ReferralCodeId,
    string UserId,
    string Code,
    DateTime OccurredOnUtc);
