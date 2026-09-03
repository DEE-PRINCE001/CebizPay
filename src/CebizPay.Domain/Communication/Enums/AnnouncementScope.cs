namespace CebizPay.Domain.Communication.Enums;

/// <summary>
/// Defines the target audience scope for an announcement.
/// </summary>
public enum AnnouncementScope
{
    /// <summary>Global platform-wide announcement broadcast to all platform users.</summary>
    Platform = 1,

    /// <summary>Tenant-isolated announcement broadcast strictly within a specific organization workplace.</summary>
    Workplace = 2
}
