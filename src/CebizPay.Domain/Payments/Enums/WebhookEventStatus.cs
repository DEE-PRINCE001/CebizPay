#pragma warning disable CS1591
namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Status of an ingested provider webhook event in the deduplication and reconciliation pipeline.
/// </summary>
public enum WebhookEventStatus
{
    /// <summary>Webhook event received and awaiting processing.</summary>
    Received = 1,

    /// <summary>Webhook event successfully verified, processed, and reconciled.</summary>
    Processed = 2,

    /// <summary>Duplicate webhook event detected and safely acknowledged without re-applying financial effects.</summary>
    Duplicate = 3,

    /// <summary>Webhook event processing failed due to error or invalid state.</summary>
    Failed = 4,

    /// <summary>Webhook event safely ignored (e.g. unhandled event type or stale out-of-order notification).</summary>
    Ignored = 5,

    /// <summary>Event actively claimed and being processed by an asynchronous worker.</summary>
    Processing = 6,

    /// <summary>Event payload indicates ambiguous provider state; scheduled for status reconciliation.</summary>
    RequiresReconciliation = 7,

    /// <summary>Discrepancy detected (e.g., amount mismatch, unauthorized reference); requires manual operations review.</summary>
    ManualReview = 8,

    /// <summary>Maximum processing retries exceeded; moved to dead-letter queue for investigation.</summary>
    DeadLetter = 9
}
