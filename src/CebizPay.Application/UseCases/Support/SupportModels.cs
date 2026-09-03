using CebizPay.Domain.Support.Enums;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Data transfer object representing a support ticket.
/// </summary>
public sealed record SupportTicketDto(
    Guid Id,
    string TicketNumber,
    string UserId,
    Guid? OrganizationId,
    SupportTicketCategory Category,
    string Subject,
    string Description,
    SupportTicketStatus Status,
    SupportTicketPriority Priority,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? EscalatedAtUtc,
    DateTime? FirstResponseAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime SlaDueAtUtc,
    bool IsSlaBreached,
    string? ResolutionSummary,
    List<TicketMessageDto> Messages);

/// <summary>
/// Data transfer object representing a message in a ticket thread.
/// </summary>
public sealed record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    string? SenderUserId,
    TicketMessageSenderType SenderType,
    string Content,
    DateTime CreatedAtUtc);

/// <summary>
/// Request payload to open a new support ticket (direct or offline synchronized).
/// </summary>
public sealed record CreateSupportTicketRequest(
    SupportTicketCategory Category,
    string Subject,
    string Description,
    SupportTicketPriority Priority = SupportTicketPriority.Normal,
    string? IdempotencyKey = null);

/// <summary>
/// Request payload to append a message to an existing ticket.
/// </summary>
public sealed record AddTicketMessageRequest(
    string Content);

/// <summary>
/// Request payload for resolving a support ticket.
/// </summary>
public sealed record ResolveTicketRequest(
    string ResolutionSummary);

/// <summary>
/// Request payload for operator status updates.
/// </summary>
public sealed record UpdateTicketStatusRequest(
    SupportTicketStatus Status,
    string? ResolutionSummary = null);

/// <summary>
/// Comprehensive support metrics aggregation for administrative reporting.
/// </summary>
public sealed record SupportReportsDto(
    int TotalTickets,
    int OpenTickets,
    int EscalatedTickets,
    int InReviewTickets,
    int ResolvedTickets,
    int ClosedTickets,
    int SlaBreachedTickets,
    Dictionary<string, int> TicketsByCategory,
    Dictionary<string, int> TicketsByPriority,
    DateTime FromUtc,
    DateTime ToUtc);

/// <summary>
/// Request payload to initiate a Kola chatbot session.
/// </summary>
public sealed record KolaStartSessionRequest(
    Guid? OrganizationId = null);

/// <summary>
/// Request payload to interact with Kola session.
/// </summary>
public sealed record KolaInteractRequest(
    string SessionId,
    KolaSessionState CurrentState,
    SupportTicketCategory? Category,
    int? SelectedIssueIndex,
    string Message,
    Guid? OrganizationId = null);
