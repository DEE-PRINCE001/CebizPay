namespace CebizPay.Domain.Payroll.Enums;

/// <summary>
/// Lifecycle state of an individual payroll item financial unit.
/// </summary>
public enum PayrollItemStatus
{
    /// <summary>Pending worker pickup.</summary>
    Pending = 1,

    /// <summary>Currently claimed and processing by a worker.</summary>
    Processing = 2,

    /// <summary>Financially settled and voucher generated (Terminal state).</summary>
    Completed = 3,

    /// <summary>Financial execution failed (Eligible for retry).</summary>
    Failed = 4,

    /// <summary>Manually retried; queued for next worker claiming.</summary>
    RetryPending = 5
}
