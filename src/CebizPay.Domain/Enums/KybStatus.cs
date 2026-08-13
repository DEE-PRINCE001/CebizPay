namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the KYB status of an organization.
/// </summary>
public enum KybStatus
{
    /// <summary>Pending registration.</summary>
    Pending = 1,
    /// <summary>Step 1 completed.</summary>
    Step1Completed = 2,
    /// <summary>Step 2 completed.</summary>
    Step2Completed = 3,
    /// <summary>Verified KYB status.</summary>
    Verified = 4,
    /// <summary>Rejected KYB status.</summary>
    Rejected = 5,
    /// <summary>Suspended KYB status.</summary>
    Suspended = 6
}
