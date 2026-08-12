namespace CebizPay.Infrastructure.Persistence.Outbox;

/// <summary>
/// Represents a persisted Outbox message for reliable domain event publishing.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Gets or sets the unique identifier of the outbox message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified name or type of the event.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON-serialized event payload.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the message was created.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the message was successfully processed/published.
    /// </summary>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets any error message encountered during processing.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the number of processing retry attempts.
    /// </summary>
    public int RetryCount { get; set; }
}
