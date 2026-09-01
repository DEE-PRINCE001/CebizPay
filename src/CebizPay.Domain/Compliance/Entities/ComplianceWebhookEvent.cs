#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Domain entity tracking inbound compliance webhook events from external providers (Dojah, Smile ID, Ninja).
/// Enforces idempotent processing, worker claiming, and protects against replay attacks.
/// </summary>
public class ComplianceWebhookEvent
{
    public const int DefaultMaxAttempts = 5;

    private ComplianceWebhookEvent() { } // EF Core

    /// <summary>Unique webhook event identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>External compliance provider that issued the webhook.</summary>
    public VerificationProvider Provider { get; private set; }

    /// <summary>Provider's unique event/job identifier or deterministic event fingerprint.</summary>
    public string ProviderEventId { get; private set; } = string.Empty;

    /// <summary>Provider event type.</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Cryptographic hash (SHA256) of raw payload for integrity and duplicate audit.</summary>
    public string? PayloadHash { get; private set; }

    /// <summary>UTC timestamp when the webhook was received.</summary>
    public DateTime ReceivedAtUtc { get; private set; }

    /// <summary>UTC timestamp when the webhook was processed.</summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>Current processing status.</summary>
    public ComplianceWebhookEventStatus Status { get; private set; }

    /// <summary>Associated VerificationOperation identifier if resolved.</summary>
    public Guid? VerificationOperationId { get; private set; }

    /// <summary>Internal correlation reference (e.g. VerificationReference, JobId).</summary>
    public string? CorrelationReference { get; private set; }

    /// <summary>Processing error details if failed.</summary>
    public string? ProcessingError { get; private set; }

    /// <summary>Sanitized metadata associated with the event.</summary>
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
    /// Creates a newly ingested compliance webhook event in <see cref="ComplianceWebhookEventStatus.Received"/> status.
    /// </summary>
    public static ComplianceWebhookEvent Create(
        VerificationProvider provider,
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
        return new ComplianceWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ProviderEventId = providerEventId.Trim(),
            EventType = eventType.Trim(),
            PayloadHash = payloadHash?.Trim(),
            SafeMetadata = safeMetadata,
            CorrelationReference = correlationReference?.Trim(),
            ReceivedAtUtc = now,
            Status = ComplianceWebhookEventStatus.Received,
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
        Status = ComplianceWebhookEventStatus.Processing;
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
            Status = ComplianceWebhookEventStatus.DeadLetter;
            NextRetryAtUtc = null;
        }
        else
        {
            Status = ComplianceWebhookEventStatus.Received;
            NextRetryAtUtc = now.Add(retryDelay);
        }

        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the webhook event as successfully processed.
    /// </summary>
    public void MarkProcessed(Guid? verificationOperationId = null, string? safeMetadata = null, string? correlationReference = null)
    {
        if (Status == ComplianceWebhookEventStatus.Processed)
            return;

        var now = DateTime.UtcNow;
        Status = ComplianceWebhookEventStatus.Processed;
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;

        if (verificationOperationId.HasValue)
            VerificationOperationId = verificationOperationId.Value;

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
        Status = ComplianceWebhookEventStatus.Duplicate;
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
        Status = ComplianceWebhookEventStatus.Failed;
        ProcessingError = errorMessage.Trim();
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the webhook event as safely ignored.
    /// </summary>
    public void MarkIgnored(string reason)
    {
        var now = DateTime.UtcNow;
        Status = ComplianceWebhookEventStatus.Ignored;
        ProcessingError = reason?.Trim();
        ProcessedAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the event as requiring reconciliation.
    /// </summary>
    public void RequiresReconciliation(string reason)
    {
        var now = DateTime.UtcNow;
        Status = ComplianceWebhookEventStatus.RequiresReconciliation;
        ProcessingError = reason?.Trim();
        LockedBy = null;
        LockedUntilUtc = null;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Marks the event for manual compliance officer review.
    /// </summary>
    public void MarkForManualReview(string reason)
    {
        var now = DateTime.UtcNow;
        Status = ComplianceWebhookEventStatus.ManualReview;
        ProcessingError = reason?.Trim();
        LockedBy = null;
        LockedUntilUtc = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = now;
    }
}
