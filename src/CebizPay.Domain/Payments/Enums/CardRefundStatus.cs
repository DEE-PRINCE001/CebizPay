namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Execution status of a card refund operation.
/// </summary>
public enum CardRefundStatus
{
    /// <summary>Refund initiated and awaiting provider execution.</summary>
    Pending = 1,

    /// <summary>Refund confirmed successful and double-entry ledger reversed.</summary>
    Succeeded = 2,

    /// <summary>Refund rejected by provider.</summary>
    Failed = 3,

    /// <summary>Provider processed refund but customer wallet balance was insufficient for full immediate reversal.</summary>
    RecoveryOutstanding = 4
}
