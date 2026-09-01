#pragma warning disable CS1591
namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Processing lifecycle state of an inbound compliance verification webhook event.
/// </summary>
public enum ComplianceWebhookEventStatus
{
    /// <summary>Received and persisted at webhook gateway.</summary>
    Received = 1,

    /// <summary>Asynchronously processed and evidence recorded.</summary>
    Processed = 2,

    /// <summary>Duplicate delivery safely acknowledged.</summary>
    Duplicate = 3,

    /// <summary>Ignored (e.g. unhandled event type).</summary>
    Ignored = 4,

    /// <summary>Processing failed with an error.</summary>
    Failed = 5,

    /// <summary>Event actively claimed and being processed by a worker.</summary>
    Processing = 6,

    /// <summary>Callback status is ambiguous; scheduled for status re-query.</summary>
    RequiresReconciliation = 7,

    /// <summary>Discrepancy detected; requires manual compliance officer review.</summary>
    ManualReview = 8,

    /// <summary>Maximum processing retries exceeded; quarantined.</summary>
    DeadLetter = 9
}
