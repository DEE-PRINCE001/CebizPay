namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Status lifecycle of a ledger transaction.
/// </summary>
public enum LedgerTransactionStatus
{
    /// <summary>Pending transaction.</summary>
    Pending = 1,
    /// <summary>Processing transaction.</summary>
    Processing = 2,
    /// <summary>Completed transaction.</summary>
    Completed = 3,
    /// <summary>Failed transaction.</summary>
    Failed = 4,
    /// <summary>Reversed transaction.</summary>
    Reversed = 5
}
