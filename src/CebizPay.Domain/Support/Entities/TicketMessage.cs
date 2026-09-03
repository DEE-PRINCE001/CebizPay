using CebizPay.Domain.Support.Enums;

namespace CebizPay.Domain.Support.Entities;

/// <summary>
/// Domain entity representing a message within a support ticket thread.
/// </summary>
public class TicketMessage
{
    /// <summary>Unique message identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Associated ticket identifier.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>Sender user ID (null for system/Kola automated assistant messages).</summary>
    public string? SenderUserId { get; private set; }

    /// <summary>Origin classification of the sender.</summary>
    public TicketMessageSenderType SenderType { get; private set; }

    /// <summary>Message body text content.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private TicketMessage() { } // EF Core

    /// <summary>
    /// Factory method to create a ticket message.
    /// </summary>
    public static TicketMessage Create(
        Guid ticketId,
        string? senderUserId,
        TicketMessageSenderType senderType,
        string content,
        DateTime now)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId cannot be empty.", nameof(ticketId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderUserId = string.IsNullOrWhiteSpace(senderUserId) ? null : senderUserId.Trim(),
            SenderType = senderType,
            Content = content.Trim(),
            CreatedAtUtc = now
        };
    }
}
