namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when a staff invitation is created.
/// </summary>
/// <param name="InvitationId">Unique ID of invitation.</param>
/// <param name="OrganizationId">Target organization ID.</param>
/// <param name="TargetEmail">Invited email.</param>
/// <param name="InvitationCode">Invitation secret token.</param>
/// <param name="ExpiresAtUtc">Expiration time.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record StaffInvitationCreatedDomainEvent(
    Guid InvitationId,
    Guid OrganizationId,
    string TargetEmail,
    string InvitationCode,
    DateTime ExpiresAtUtc,
    DateTime OccurredOnUtc);
