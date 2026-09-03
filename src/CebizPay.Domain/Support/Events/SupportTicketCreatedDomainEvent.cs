using CebizPay.Domain.Support.Enums;

namespace CebizPay.Domain.Support.Events;

/// <summary>
/// Domain event emitted when a new customer support ticket is opened.
/// </summary>
public sealed record SupportTicketCreatedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    string UserId,
    Guid? OrganizationId,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    DateTime OccurredOnUtc);
