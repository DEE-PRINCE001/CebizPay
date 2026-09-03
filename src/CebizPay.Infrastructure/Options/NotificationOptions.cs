namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Configuration options for system-wide notification limits and retention.
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications";

    /// <summary>Maximum outbound SMS messages allowed per recipient per hour.</summary>
    public int MaxSmsPerHour { get; set; } = 5;

    /// <summary>Maximum outbound emails allowed per recipient per hour.</summary>
    public int MaxEmailPerHour { get; set; } = 20;

    /// <summary>Maximum outbound push notifications allowed per recipient per hour.</summary>
    public int MaxPushPerHour { get; set; } = 30;

    /// <summary>Number of days to retain read in-app notifications before archival/purging.</summary>
    public int RetentionDays { get; set; } = 90;
}
