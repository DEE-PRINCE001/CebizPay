namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the KYC status of an individual user.
/// </summary>
public enum KycStatus
{
    /// <summary>Pending verification.</summary>
    Pending = 1,
    /// <summary>Verified KYC status.</summary>
    Verified = 2,
    /// <summary>Rejected KYC status.</summary>
    Rejected = 3
}
