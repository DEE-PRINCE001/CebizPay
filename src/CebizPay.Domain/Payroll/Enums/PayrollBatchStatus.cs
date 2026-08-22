namespace CebizPay.Domain.Payroll.Enums;

/// <summary>
/// Lifecycle state of a payroll batch orchestration container.
/// </summary>
public enum PayrollBatchStatus
{
    /// <summary>Batch created; waiting for background worker claiming.</summary>
    Pending = 1,

    /// <summary>Worker is actively processing items.</summary>
    Processing = 2,

    /// <summary>All eligible items completed successfully.</summary>
    Completed = 3,

    /// <summary>Some items completed; one or more items failed/unresolved.</summary>
    PartiallyCompleted = 4,

    /// <summary>Zero items succeeded; entire batch encountered terminal failure.</summary>
    Failed = 5,

    /// <summary>Batch was cancelled before any item financial execution began.</summary>
    Cancelled = 6
}
