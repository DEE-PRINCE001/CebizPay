namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Lifecycle status of a dedicated/dynamic virtual account.
/// </summary>
public enum VirtualAccountStatus
{
    /// <summary>Account creation request submitted to provider, pending assignment.</summary>
    Pending = 1,
    /// <summary>Account is active and ready to receive inbound funding deposits.</summary>
    Active = 2,
    /// <summary>Account is temporarily suspended from receiving deposits.</summary>
    Suspended = 3,
    /// <summary>Account is permanently closed.</summary>
    Closed = 4
}
