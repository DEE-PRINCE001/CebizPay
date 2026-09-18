namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when an individual profile is administratively suspended.
/// </summary>
/// <param name="ProfileId">Unique ID of individual profile.</param>
/// <param name="UserId">User ID matching identity store.</param>
/// <param name="Reason">Administrative reason for suspension.</param>
/// <param name="AdminUserId">Admin actor ID who initiated the suspension.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record IndividualSuspendedDomainEvent(
    Guid ProfileId,
    string UserId,
    string Reason,
    string AdminUserId,
    DateTime OccurredOnUtc);
