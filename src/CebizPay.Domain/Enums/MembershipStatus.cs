namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the status of a user's membership within an organization.
/// </summary>
public enum MembershipStatus
{
    /// <summary>Active workplace member.</summary>
    Active = 1,
    /// <summary>Suspended workplace member.</summary>
    Suspended = 2,
    /// <summary>Terminated / offboarded workplace member.</summary>
    Terminated = 3
}
