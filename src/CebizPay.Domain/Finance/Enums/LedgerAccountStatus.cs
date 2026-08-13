namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Lifecycle status of a ledger account.
/// </summary>
public enum LedgerAccountStatus
{
    /// <summary>Active ledger account.</summary>
    Active = 1,
    /// <summary>Frozen ledger account.</summary>
    Frozen = 2,
    /// <summary>Closed ledger account.</summary>
    Closed = 3
}
