namespace CebizPay.Domain.Communication.Enums;

/// <summary>
/// Publication lifecycle status for an announcement.
/// </summary>
public enum AnnouncementStatus
{
    /// <summary>Announcement created but not yet published or publicly visible.</summary>
    Draft = 1,

    /// <summary>Announcement published and publicly visible to its target audience.</summary>
    Published = 2,

    /// <summary>Announcement archived and permanently hidden from public feeds.</summary>
    Archived = 3
}
