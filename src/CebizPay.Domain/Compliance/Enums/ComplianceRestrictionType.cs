namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Types of granular compliance restrictions applied to a subject's financial activity.
/// </summary>
public enum ComplianceRestrictionType
{
    /// <summary>Blocks all outbound money movement (transfers, payouts, disbursements).</summary>
    BlockAllOutbound = 1,
    /// <summary>Blocks external NIP bank transfers / payouts only.</summary>
    BlockBankTransfer = 2,
    /// <summary>Blocks inbound card funding operations.</summary>
    BlockCardFunding = 3,
    /// <summary>Blocks dedicated virtual account funding.</summary>
    BlockVirtualAccount = 4,
    /// <summary>Enforces a maximum cumulative daily transaction volume cap.</summary>
    CapDailyVolume = 5,
    /// <summary>Enforces a maximum single transaction amount cap.</summary>
    CapSingleTransaction = 6,
    /// <summary>Full account freeze — both inbound and outbound transactions blocked.</summary>
    FullAccountSuspension = 7
}
