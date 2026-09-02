namespace CebizPay.Domain.Enums;

/// <summary>
/// Lifecycle status of an administrative user invitation.
/// </summary>
public enum AdminInvitationStatus
{
    /// <summary>Invitation is active and awaiting redemption.</summary>
    Pending = 1,

    /// <summary>Invitation has been successfully redeemed and converted to an active admin profile.</summary>
    Redeemed = 2,

    /// <summary>Invitation was cancelled by a Super Admin before redemption.</summary>
    Cancelled = 3,

    /// <summary>Invitation has exceeded its 24-hour validity window.</summary>
    Expired = 4
}
