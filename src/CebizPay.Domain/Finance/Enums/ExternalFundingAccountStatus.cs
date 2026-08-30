namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Lifecycle status for an external funding account attached to a wallet.
/// </summary>
public enum ExternalFundingAccountStatus
{
    /// <summary>Account is active and eligible for receiving external funding or being primary.</summary>
    Active = 1,

    /// <summary>Account is temporarily suspended.</summary>
    Suspended = 2,

    /// <summary>Account is permanently closed.</summary>
    Closed = 3
}
