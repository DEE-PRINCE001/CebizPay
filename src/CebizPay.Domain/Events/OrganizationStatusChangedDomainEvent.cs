using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when an organization status changes.
/// </summary>
/// <param name="OrganizationId">Unique ID of the organization.</param>
/// <param name="PreviousStatus">Previous status.</param>
/// <param name="NewStatus">New status.</param>
/// <param name="Reason">Reason for transition.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record OrganizationStatusChangedDomainEvent(
    Guid OrganizationId,
    OrganizationStatus PreviousStatus,
    OrganizationStatus NewStatus,
    string? Reason,
    DateTime OccurredOnUtc);
