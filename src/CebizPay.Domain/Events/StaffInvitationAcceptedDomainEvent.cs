namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when a staff invitation is accepted.
/// </summary>
/// <param name="InvitationId">Unique ID of invitation.</param>
/// <param name="OrganizationId">Target organization ID.</param>
/// <param name="UserId">ID of user who accepted invitation.</param>
/// <param name="MembershipId">ID of created organization membership.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record StaffInvitationAcceptedDomainEvent(
    Guid InvitationId,
    Guid OrganizationId,
    string UserId,
    Guid MembershipId,
    DateTime OccurredOnUtc);
