using CebizPay.Domain.Support.Enums;

namespace CebizPay.Domain.Support.Entities;

/// <summary>
/// Domain aggregate root representing a customer support ticket.
/// Enforces 12-hour review SLA, lifecycle transitions, tenant/user isolation,
/// and message thread management without requiring a dedicated SupportAgent role.
/// </summary>
public class SupportTicket
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Canonical human-readable ticket tracking number (e.g. CBZ-SUP-2026-XXXX).</summary>
    public string TicketNumber { get; private set; } = string.Empty;

    /// <summary>Requester customer user ID.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Optional organization ID if ticket was created in organization portal context.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Triage category classification.</summary>
    public SupportTicketCategory Category { get; private set; }

    /// <summary>Ticket subject summary.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Initial inquiry or problem description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    public SupportTicketStatus Status { get; private set; } = SupportTicketStatus.Open;

    /// <summary>Severity priority classification.</summary>
    public SupportTicketPriority Priority { get; private set; } = SupportTicketPriority.Normal;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last modification timestamp.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Timestamp when ticket was escalated to administrative attention.</summary>
    public DateTime? EscalatedAtUtc { get; private set; }

    /// <summary>Timestamp of first administrative response.</summary>
    public DateTime? FirstResponseAtUtc { get; private set; }

    /// <summary>Timestamp when inquiry was resolved.</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Timestamp when ticket was closed.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>Authoritative 12-hour review SLA deadline (CreatedAtUtc + 12 hours).</summary>
    public DateTime SlaDueAtUtc { get; private set; }

    /// <summary>Whether the 12-hour review SLA was breached.</summary>
    public bool IsSlaBreached { get; private set; }

    /// <summary>Timestamp when SLA breach was recorded.</summary>
    public DateTime? SlaBreachedAtUtc { get; private set; }

    /// <summary>Summary of resolution documented upon resolving ticket.</summary>
    public string? ResolutionSummary { get; private set; }

    /// <summary>Client-provided idempotency key for offline synchronization and retry deduplication.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Associated conversation messages.</summary>
    public ICollection<TicketMessage> Messages { get; private set; } = new List<TicketMessage>();

    private SupportTicket() { } // EF Core

    /// <summary>
    /// Factory method to create a new support ticket enforcing the authoritative 12-hour SLA deadline.
    /// </summary>
    public static SupportTicket Create(
        string ticketNumber,
        string userId,
        Guid? organizationId,
        SupportTicketCategory category,
        string subject,
        string description,
        SupportTicketPriority priority,
        DateTime now,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new ArgumentException("TicketNumber is required.", nameof(ticketNumber));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            TicketNumber = ticketNumber.Trim(),
            UserId = userId.Trim(),
            OrganizationId = organizationId,
            Category = category,
            Subject = subject.Trim(),
            Description = description.Trim(),
            Status = SupportTicketStatus.Open,
            Priority = priority,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SlaDueAtUtc = now.AddHours(12),
            IsSlaBreached = false,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
        };

        return ticket;
    }

    /// <summary>
    /// Appends a new message to the ticket thread.
    /// </summary>
    public TicketMessage AddMessage(
        string? senderUserId,
        TicketMessageSenderType senderType,
        string content,
        DateTime now)
    {
        if (Status == SupportTicketStatus.Closed || Status == SupportTicketStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot add messages to a ticket in '{Status}' status.");
        }

        var message = TicketMessage.Create(Id, senderUserId, senderType, content, now);
        Messages.Add(message);

        if (senderType == TicketMessageSenderType.Admin && FirstResponseAtUtc == null)
        {
            FirstResponseAtUtc = now;
        }

        UpdatedAtUtc = now;
        return message;
    }

    /// <summary>
    /// Escalates the ticket to administrative attention.
    /// </summary>
    public void Escalate(DateTime now, string? notes = null)
    {
        if (Status == SupportTicketStatus.Closed || Status == SupportTicketStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot escalate a ticket in '{Status}' status.");
        }

        Status = SupportTicketStatus.Escalated;
        EscalatedAtUtc = now;
        UpdatedAtUtc = now;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            AddMessage(null, TicketMessageSenderType.Kola, $"[Escalation Note]: {notes.Trim()}", now);
        }
    }

    /// <summary>
    /// Transitions ticket into administrative review status.
    /// </summary>
    public void MarkInReview(DateTime now)
    {
        if (Status == SupportTicketStatus.Closed || Status == SupportTicketStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot mark a ticket in '{Status}' status as in review.");
        }

        Status = SupportTicketStatus.InReview;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Resolves the ticket with documented resolution summary.
    /// </summary>
    public void Resolve(string resolutionSummary, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(resolutionSummary))
            throw new ArgumentException("ResolutionSummary is required when resolving a ticket.", nameof(resolutionSummary));

        Status = SupportTicketStatus.Resolved;
        ResolutionSummary = resolutionSummary.Trim();
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Permanently closes the ticket.
    /// </summary>
    public void Close(DateTime now)
    {
        Status = SupportTicketStatus.Closed;
        ClosedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Cancels the ticket.
    /// </summary>
    public void Cancel(DateTime now)
    {
        Status = SupportTicketStatus.Cancelled;
        ClosedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Records that the 12-hour review SLA was breached.
    /// </summary>
    public void MarkSlaBreached(DateTime now)
    {
        if (IsSlaBreached)
            return;

        IsSlaBreached = true;
        SlaBreachedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Reopens a resolved ticket if customer inquires further.
    /// </summary>
    public void Reopen(DateTime now)
    {
        if (Status != SupportTicketStatus.Resolved)
        {
            throw new InvalidOperationException($"Only resolved tickets can be reopened. Current status: '{Status}'.");
        }

        Status = SupportTicketStatus.Open;
        ResolvedAtUtc = null;
        ResolutionSummary = null;
        UpdatedAtUtc = now;
    }
}
