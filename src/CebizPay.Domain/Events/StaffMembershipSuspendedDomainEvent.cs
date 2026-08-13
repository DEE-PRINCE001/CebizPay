namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when a staff organization membership is suspended.
/// </summary>
/// <param name="MembershipId">Unique ID of membership.</param>
/// <param name="OrganizationId">Target organization ID.</param>
/// <param name="UserId">User ID of suspended staff.</param>
/// <param name="Reason">Reason for suspension.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record StaffMembershipSuspendedDomainEvent(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    string Reason,
    DateTime OccurredOnUtc);
