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
    Failed = 3
}
