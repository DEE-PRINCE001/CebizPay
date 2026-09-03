namespace CebizPay.Domain.Support.Events;

/// <summary>
/// Domain event emitted when a support ticket inquiry is resolved.
/// </summary>
public sealed record SupportTicketResolvedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    string UserId,
    string ResolutionSummary,
    DateTime OccurredOnUtc);
