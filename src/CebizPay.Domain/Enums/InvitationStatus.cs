namespace CebizPay.Domain.Enums;

/// <summary>
/// Status of a staff invitation.
/// </summary>
public enum InvitationStatus
{
    /// <summary>Pending invitation.</summary>
    Pending = 1,
    /// <summary>Accepted invitation.</summary>
    Accepted = 2,
    /// <summary>Rejected invitation.</summary>
    Rejected = 3,
    /// <summary>Cancelled invitation.</summary>
    Cancelled = 4
}
