namespace CebizPay.Domain.Communication.Enums;

/// <summary>
/// Delivery channels supported by the notification platform.
/// </summary>
public enum NotificationChannel
{
    /// <summary>In-app notification persisted to the user's notification center.</summary>
    InApp = 1,

    /// <summary>Push notification dispatched via Firebase Cloud Messaging (FCM).</summary>
    Push = 2,

    /// <summary>Transactional email dispatched via email infrastructure.</summary>
    Email = 3,

    /// <summary>SMS message dispatched via SMS infrastructure (critical events only).</summary>
    Sms = 4
}
