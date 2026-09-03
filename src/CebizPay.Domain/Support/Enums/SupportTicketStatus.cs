namespace CebizPay.Domain.Support.Enums;

/// <summary>
/// Lifecycle status of a customer support ticket.
/// </summary>
public enum SupportTicketStatus
{
    /// <summary>Ticket is open and awaiting operator review or initial response.</summary>
    Open = 1,

    /// <summary>Ticket has been explicitly escalated by Kola or user due to complexity or severity.</summary>
    Escalated = 2,

    /// <summary>Ticket is currently undergoing administrative investigation.</summary>
    InReview = 3,

    /// <summary>Ticket inquiry has been resolved by operator with documented resolution.</summary>
    Resolved = 4,

    /// <summary>Ticket is permanently closed.</summary>
    Closed = 5,

    /// <summary>Ticket was cancelled by requester or administrative operator.</summary>
    Cancelled = 6
}
