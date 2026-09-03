using CebizPay.Domain.Support.Enums;

namespace CebizPay.Domain.Support.Events;

/// <summary>
/// Domain event emitted when a support ticket is escalated to administrative attention.
/// </summary>
public sealed record SupportTicketEscalatedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    string UserId,
    SupportTicketPriority Priority,
    DateTime OccurredOnUtc);
