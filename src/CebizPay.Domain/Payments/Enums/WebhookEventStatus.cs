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
    Ignored = 5
}
