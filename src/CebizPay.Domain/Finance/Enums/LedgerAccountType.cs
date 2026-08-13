namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Type of ledger account.
/// </summary>
public enum LedgerAccountType
{
    /// <summary>Customer user/organization wallet account.</summary>
    CustomerWallet = 1,
    /// <summary>Platform FX settlement / clearing account.</summary>
    SystemSettlement = 2,
    /// <summary>Platform fee revenue account.</summary>
    FeeRevenue = 3,
    /// <summary>Platform suspense / clearing account.</summary>
    PlatformClearing = 4
}
