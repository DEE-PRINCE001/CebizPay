namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when a suspended individual profile is administratively reactivated.
/// </summary>
/// <param name="ProfileId">Unique ID of individual profile.</param>
/// <param name="UserId">User ID matching identity store.</param>
/// <param name="Reason">Administrative reason for reactivation.</param>
/// <param name="AdminUserId">Admin actor ID who initiated the reactivation.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record IndividualReactivatedDomainEvent(
    Guid ProfileId,
    string UserId,
    string Reason,
    string AdminUserId,
    DateTime OccurredOnUtc);
