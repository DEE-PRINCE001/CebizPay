namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Distinct external payment and banking capability rails.
/// </summary>
public enum PaymentCapability
{
    /// <summary>Dedicated or dynamic virtual account issuance and management.</summary>
    VirtualAccount = 1,

    /// <summary>Hosted / tokenized card payment processing and collection.</summary>
    CardFunding = 2,

    /// <summary>Outbound bank transfers / payouts to external financial institutions.</summary>
    BankTransfer = 3,

    /// <summary>Destination bank account name resolution / inquiry.</summary>
    BankAccountResolution = 4
}
