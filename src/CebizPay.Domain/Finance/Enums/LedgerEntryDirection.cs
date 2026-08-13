namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Direction of a double-entry ledger record.
/// </summary>
public enum LedgerEntryDirection
{
    /// <summary>Debit entry (money out for asset/expense accounts, money in for liabilities/equity).</summary>
    Debit = 1,
    /// <summary>Credit entry (money in for asset/expense accounts, money out for liabilities/equity).</summary>
    Credit = 2
}
