namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Financial operation type governed by platform fee policies.
/// </summary>
public enum FeeOperationType
{
    /// <summary>Inbound dedicated/dynamic virtual account funding.</summary>
    VirtualAccountFunding = 1,

    /// <summary>Inbound card checkout funding.</summary>
    CardFunding = 2,

    /// <summary>Outbound bank transfer / payout.</summary>
    BankTransfer = 3,

    /// <summary>Internal peer-to-peer wallet transfer.</summary>
    PeerTransfer = 4
}
