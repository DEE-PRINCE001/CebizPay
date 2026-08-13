namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an organization.
/// </summary>
public enum OrganizationStatus
{
    /// <summary>Pending verification.</summary>
    Pending = 1,
    /// <summary>Verified status.</summary>
    Verified = 2,
    /// <summary>Rejected status.</summary>
    Rejected = 3,
    /// <summary>Suspended status.</summary>
    Suspended = 4
}
