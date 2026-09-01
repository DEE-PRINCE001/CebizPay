#pragma warning disable CS1591
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain entity tracking the lifecycle, deduplication, and reconciliation of provider webhook events.
/// Enforces idempotent ingestion, worker claiming with concurrency safety, and prevents duplicate financial side effects.
/// </summary>
public sealed class WebhookEvent
{
    public const int DefaultMaxAttempts = 5;

    private WebhookEvent() { } // EF Core constructor

    /// <summary>Unique webhook event identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>External payment provider that issued the webhook.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Provider's unique event identifier or deterministic event fingerprint.</summary>
    public string ProviderEventId { get; private set; } = string.Empty;

    /// <summary>Provider event type (e.g., "charge.completed", "transfer.success").</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Cryptographic hash (SHA256) of raw payload for integrity and duplicate audit.</summary>
    public string? PayloadHash { get; private set; }

    /// <summary>UTC timestamp when the webhook was received at the ingestion gateway.</summary>
    public DateTime ReceivedAtUtc { get; private set; }

    /// <summary>UTC timestamp when the webhook was processed / reconciled.</summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>Current processing status of the webhook event.</summary>
    public WebhookEventStatus Status { get; private set; }

    /// <summary>Associated PaymentAttempt identifier if resolved.</summary>
    public Guid? PaymentAttemptId { get; private set; }

    /// <summary>Internal correlation reference (e.g. RequestReference, TransferReference, AccountNumber) if resolved.</summary>
    public string? CorrelationReference { get; private set; }

    /// <summary>Processing error details if failed.</summary>
    public string? ProcessingError { get; private set; }

    /// <summary>Sanitized, secret-free metadata associated with the event.</summary>
    public string? SafeMetadata { get; private set; }

    /// <summary>Number of processing attempts executed by workers.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Maximum number of processing retries before moving to dead-letter.</summary>
    public int MaxAttempts { get; private set; } = DefaultMaxAttempts;

    /// <summary>Next scheduled retry timestamp for transient failures.</summary>
    public DateTime? NextRetryAtUtc { get; private set; }

    /// <summary>Worker lock expiration timestamp for distributed claiming.</summary>
    public DateTime? LockedUntilUtc { get; private set; }

    /// <summary>Identity of the worker instance that claimed the event.</summary>
    public string? LockedBy { get; private set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>UTC last update timestamp.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Factory method to create a newly ingested webhook event in <see cref="WebhookEventStatus.Received"/> status.
    /// </summary>
    public static WebhookEvent Create(
        PaymentProvider provider,
        string providerEventId,
        string eventType,
        string? payloadHash = null,
        string? safeMetadata = null,
        string? correlationReference = null)
    {
        if (string.IsNullOrWhiteSpace(providerEventId))
            throw new ArgumentException("ProviderEventId is required.", nameof(providerEventId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType is required.", nameof(eventType));

        var now = DateTime.UtcNow;
        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ProviderEventId = providerEventId.Trim(),
            EventType = eventType.Trim(),
            PayloadHash = payloadHash?.Trim(),
            SafeMetadata = safeMetadata,
            CorrelationReference = correlationReference?.Trim(),
            ReceivedAtUtc = now,
            Status = WebhookEventStatus.Received,
            AttemptCount = 0,
            MaxAttempts = DefaultMaxAttempts,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Claims the webhook event for asynchronous processing by a worker instance.
    /// </summary>
    public void Claim(string workerId, TimeSpan lockDuration)
    {
        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.Processing;
        LockedBy = workerId;
        LockedUntilUtc = now.Add(lockDuration);
        AttemptCount++;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Releases the claim following a transient error and schedules exponential backoff retry.
    /// </summary>
    public void ReleaseClaim(string? errorMessage, TimeSpan retryDelay)
    {
        var now = DateTime.UtcNow;
        LockedBy = null;
        LockedUntilUtc = null;
        ProcessingError = errorMessage?.Trim();

        if (AttemptCount >= MaxAttempts)
        {
            Status = WebhookEventStatus.DeadLetter;
            NextRetryAtUtc = null;
        }
        else
        {
            Status = WebhookEventStatus.Received;
            NextRetryAtUtc = now.Add(retryDelay);
        }

        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the webhook event as successfully processed and reconciled.
    /// </summary>
    public void MarkProcessed(Guid? paymentAttemptId = null, string? safeMetadata = null, string? correlationReference = null)
    {
        if (Status == WebhookEventStatus.Processed)
            return; // Idempotent

        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.Processed;
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;

        if (paymentAttemptId.HasValue)
            PaymentAttemptId = paymentAttemptId.Value;

        if (!string.IsNullOrWhiteSpace(correlationReference))
            CorrelationReference = correlationReference.Trim();

        if (!string.IsNullOrWhiteSpace(safeMetadata))
            SafeMetadata = safeMetadata;
    }

    /// <summary>
    /// Marks the webhook event as duplicate.
    /// </summary>
    public void MarkDuplicate(string? reason = null)
    {
        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.Duplicate;
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;

        if (!string.IsNullOrWhiteSpace(reason))
            ProcessingError = reason;
    }

    /// <summary>
    /// Marks the webhook event processing as failed.
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("ErrorMessage is required.", nameof(errorMessage));

        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.Failed;
        ProcessingError = errorMessage.Trim();
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the webhook event as safely ignored (e.g. unhandled event type or stale out-of-order event).
    /// </summary>
    public void MarkIgnored(string reason)
    {
        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.Ignored;
        ProcessingError = reason?.Trim();
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the webhook event as requiring status reconciliation query.
    /// </summary>
    public void RequiresReconciliation(string reason)
    {
        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.RequiresReconciliation;
        ProcessingError = reason?.Trim();
        LockedBy = null;
        LockedUntilUtc = null;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the webhook event for manual review due to discrepancy.
    /// </summary>
    public void MarkForManualReview(string reason)
    {
        var now = DateTime.UtcNow;
        Status = WebhookEventStatus.ManualReview;
        ProcessingError = reason?.Trim();
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;
    }
}
