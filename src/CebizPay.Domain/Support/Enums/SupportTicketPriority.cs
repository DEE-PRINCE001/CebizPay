namespace CebizPay.Domain.Support.Enums;

/// <summary>
/// Urgency and business severity classification for support tickets.
/// </summary>
public enum SupportTicketPriority
{
    /// <summary>Standard operational priority.</summary>
    Normal = 1,

    /// <summary>High priority inquiry affecting core user services.</summary>
    High = 2,

    /// <summary>Critical financial discrepancy, suspected fraud, or security incident requiring immediate triage.</summary>
    Critical = 3
}
