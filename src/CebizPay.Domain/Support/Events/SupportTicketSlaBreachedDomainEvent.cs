namespace CebizPay.Domain.Support.Events;

/// <summary>
/// Domain event emitted when a support ticket breaches its 12-hour review SLA.
/// </summary>
public sealed record SupportTicketSlaBreachedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    string UserId,
    DateTime SlaDueAtUtc,
    DateTime OccurredOnUtc);
