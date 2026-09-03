namespace CebizPay.Domain.Communication.Enums;

/// <summary>
/// Status representing the delivery outcome of a notification dispatch attempt across a channel.
/// </summary>
public enum NotificationDeliveryStatus
{
    /// <summary>Pending dispatch to channel.</summary>
    Pending = 1,

    /// <summary>Successfully delivered or accepted by the provider.</summary>
    Delivered = 2,

    /// <summary>Failed due to technical or business provider error.</summary>
    Failed = 3,

    /// <summary>Throttled due to per-user rate limit protection.</summary>
    Throttled = 4,

    /// <summary>Suppressed because the user opted out via notification preferences.</summary>
    SuppressedByPreference = 5,

    /// <summary>Skipped because the message was already delivered (deduplicated).</summary>
    Duplicate = 6
}
