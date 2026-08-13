using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Events;

/// <summary>
/// Event emitted when an individual user's KYC status changes.
/// </summary>
/// <param name="UserId">Unique user ID.</param>
/// <param name="PreviousStatus">Previous KYC status.</param>
/// <param name="NewStatus">New KYC status.</param>
/// <param name="Reason">Reason for change.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record KycStatusChangedDomainEvent(
    string UserId,
    KycStatus PreviousStatus,
    KycStatus NewStatus,
    string? Reason,
    DateTime OccurredOnUtc);
