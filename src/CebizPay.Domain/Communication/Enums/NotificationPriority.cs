namespace CebizPay.Domain.Communication.Enums;

/// <summary>
/// Priority level determining dispatch channels and delivery urgency.
/// </summary>
public enum NotificationPriority
{
    /// <summary>Low priority notifications (e.g. general updates, marketing).</summary>
    Low = 1,

    /// <summary>Normal operational notifications (e.g. peer transfers, announcements).</summary>
    Normal = 2,

    /// <summary>High priority transactional notifications (e.g. loan approvals, payroll payouts).</summary>
    High = 3,

    /// <summary>Critical urgency notifications (e.g. organization suspension, security alerts).</summary>
    Critical = 4
}
