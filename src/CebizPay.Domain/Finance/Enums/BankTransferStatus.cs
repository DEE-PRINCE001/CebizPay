namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Status lifecycle of an outbound bank transfer financial operation.
///
/// State Machine:
///   Pending -> Processing -> Completed | Failed | Unknown
///   Pending -> Failed | Unknown
///   Processing -> Completed | Failed | Unknown
///   Unknown -> Completed | Failed
/// </summary>
public enum BankTransferStatus
{
    /// <summary>
    /// Financial transaction accepted by CebizPay and funds immediately debited from sender wallet into clearing account.
    /// External provider execution has not yet reached a final state.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// External execution is actively in-flight with the external provider/network.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// External bank transfer confirmed successful. Terminal state.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// External bank transfer definitively unsuccessful. Terminal state.
    /// Funds are restored to sender wallet via an atomic reversal transaction.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// External outcome cannot currently be established (e.g. provider timeout/unreachable).
    /// Funds remain debited in clearing account until explicit reconciliation. No automatic reversal.
    /// </summary>
    Unknown = 5
}
