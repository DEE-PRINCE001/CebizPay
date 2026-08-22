using CebizPay.Domain.Payroll.Enums;

namespace CebizPay.Domain.Payroll.Entities;

/// <summary>
/// Domain entity recording an individual worker execution attempt for a payroll item.
/// Preserves complete historical failure reasons and timing without overwriting past attempts.
/// </summary>
public class PayrollExecutionAttempt
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Linked payroll line item ID.</summary>
    public Guid PayrollItemId { get; private set; }

    /// <summary>Monotonically increasing attempt number (1, 2, 3...).</summary>
    public int AttemptNumber { get; private set; }

    /// <summary>Identifier of the background worker instance that executed this attempt.</summary>
    public string WorkerId { get; private set; } = string.Empty;

    /// <summary>Execution status of this attempt.</summary>
    public ExecutionAttemptStatus Status { get; private set; } = ExecutionAttemptStatus.Started;

    /// <summary>Timestamp when attempt started.</summary>
    public DateTime StartedAtUtc { get; private set; }

    /// <summary>Timestamp when attempt concluded.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Failure classification code if attempt failed.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Safe failure reason description if attempt failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private PayrollExecutionAttempt() { } // EF Core

    /// <summary>
    /// Creates a new execution attempt record in Started status.
    /// </summary>
    public static PayrollExecutionAttempt Create(Guid payrollItemId, int attemptNumber, string workerId)
    {
        if (payrollItemId == Guid.Empty)
            throw new ArgumentException("PayrollItemId is required.", nameof(payrollItemId));
        if (attemptNumber <= 0)
            throw new ArgumentException("AttemptNumber must be positive.", nameof(attemptNumber));
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("WorkerId is required.", nameof(workerId));

        return new PayrollExecutionAttempt
        {
            Id = Guid.NewGuid(),
            PayrollItemId = payrollItemId,
            AttemptNumber = attemptNumber,
            WorkerId = workerId.Trim(),
            Status = ExecutionAttemptStatus.Started,
            StartedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the attempt as successfully completed.
    /// </summary>
    public void MarkCompleted()
    {
        Status = ExecutionAttemptStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        FailureCode = null;
        FailureReason = null;
    }

    /// <summary>
    /// Marks the attempt as failed with diagnostic codes.
    /// </summary>
    public void MarkFailed(string failureCode, string failureReason)
    {
        Status = ExecutionAttemptStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        FailureCode = failureCode;
        FailureReason = failureReason;
    }
}
