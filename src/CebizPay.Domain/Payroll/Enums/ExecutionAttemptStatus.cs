namespace CebizPay.Domain.Payroll.Enums;

/// <summary>
/// Execution attempt status for a payroll item financial operation.
/// </summary>
public enum ExecutionAttemptStatus
{
    /// <summary>Attempt started by worker.</summary>
    Started = 1,

    /// <summary>Attempt completed successfully.</summary>
    Completed = 2,

    /// <summary>Attempt failed.</summary>
    Failed = 3
}
