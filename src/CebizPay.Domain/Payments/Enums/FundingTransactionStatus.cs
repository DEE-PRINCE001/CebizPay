namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Status of a wallet funding transaction.
/// </summary>
public enum FundingTransactionStatus
{
    /// <summary>Funding initiated / payment pending provider confirmation.</summary>
    Pending = 1,
    /// <summary>Funding confirmed and credited through the central ledger.</summary>
    Completed = 2,
    /// <summary>Funding failed or rejected by provider.</summary>
    Failed = 3,
    /// <summary>Funding is currently processing / locking resources.</summary>
    Processing = 4,
    /// <summary>Funding outcome is unknown / pending provider reconciliation.</summary>
    Unknown = 5,
    /// <summary>Funding transaction was reversed / charged back.</summary>
    Reversed = 6
}
