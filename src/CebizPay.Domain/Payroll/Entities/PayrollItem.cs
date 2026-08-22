using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;

namespace CebizPay.Domain.Payroll.Entities;

/// <summary>
/// Domain entity representing an individual employee's salary payment line item within a payroll batch.
/// Functions as an independent atomic financial unit.
/// </summary>
public class PayrollItem
{
    private readonly List<PayrollExecutionAttempt> _attempts = new();

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent payroll batch ID.</summary>
    public Guid PayrollBatchId { get; private set; }

    /// <summary>Owning organization tenant ID.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Recipient employee Identity User ID.</summary>
    public string EmployeeUserId { get; private set; } = string.Empty;

    /// <summary>Snapshot of employee display name at calculation time.</summary>
    public string EmployeeName { get; private set; } = string.Empty;

    /// <summary>Snapshot of employee email at calculation time.</summary>
    public string EmployeeEmail { get; private set; } = string.Empty;

    /// <summary>Snapshot of employee's assigned department ID.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Snapshot of employee's workforce job role ID.</summary>
    public Guid? WorkforceRoleId { get; private set; }

    /// <summary>Snapshot of employee's salary level ID.</summary>
    public Guid? SalaryLevelId { get; private set; }

    /// <summary>Payment currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Base gross salary amount.</summary>
    public decimal GrossPay { get; private set; }

    /// <summary>Total deductions amount (loans, withholdings).</summary>
    public decimal TotalDeductions { get; private set; }

    /// <summary>Net salary payout (GrossPay - TotalDeductions >= 0).</summary>
    public decimal NetPay { get; private set; }

    /// <summary>JSON snapshot of individual deduction components.</summary>
    public string? DeductionsDetailJson { get; private set; }

    /// <summary>Item lifecycle status.</summary>
    public PayrollItemStatus Status { get; private set; } = PayrollItemStatus.Pending;

    /// <summary>Worker instance ID currently claiming this item.</summary>
    public string? ClaimedByWorkerId { get; private set; }

    /// <summary>Timestamp when claimed by worker.</summary>
    public DateTime? ClaimedAtUtc { get; private set; }

    /// <summary>Total execution attempts count.</summary>
    public int CurrentAttemptNumber { get; private set; }

    /// <summary>Linked central ledger transaction ID upon successful execution.</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Linked generated Payment Voucher ID upon successful execution.</summary>
    public Guid? PaymentVoucherId { get; private set; }

    /// <summary>Code of the most recent failure.</summary>
    public string? LastFailureCode { get; private set; }

    /// <summary>Safe description of the most recent failure.</summary>
    public string? LastFailureReason { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Execution attempts history.</summary>
    public virtual IReadOnlyCollection<PayrollExecutionAttempt> Attempts => _attempts.AsReadOnly();

    private PayrollItem() { } // EF Core

    /// <summary>
    /// Creates a new payroll item calculation snapshot.
    /// </summary>
    public static PayrollItem Create(
        Guid payrollBatchId,
        Guid organizationId,
        string employeeUserId,
        string employeeName,
        string employeeEmail,
        Currency currency,
        decimal grossPay,
        decimal totalDeductions,
        Guid? departmentId = null,
        Guid? workforceRoleId = null,
        Guid? salaryLevelId = null,
        string? deductionsDetailJson = null)
    {
        if (payrollBatchId == Guid.Empty)
            throw new ArgumentException("PayrollBatchId is required.", nameof(payrollBatchId));
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(employeeUserId))
            throw new ArgumentException("EmployeeUserId is required.", nameof(employeeUserId));
        if (grossPay < 0)
            throw new ArgumentException("GrossPay cannot be negative.", nameof(grossPay));
        if (totalDeductions < 0)
            throw new ArgumentException("TotalDeductions cannot be negative.", nameof(totalDeductions));

        var netPay = grossPay - totalDeductions;
        if (netPay < 0)
            throw new InvalidOperationException($"Total deductions ({totalDeductions}) cannot exceed gross salary ({grossPay}). Net pay would be negative.");

        currency.EnsureTransactionalV1();

        return new PayrollItem
        {
            Id = Guid.NewGuid(),
            PayrollBatchId = payrollBatchId,
            OrganizationId = organizationId,
            EmployeeUserId = employeeUserId,
            EmployeeName = employeeName.Trim(),
            EmployeeEmail = employeeEmail.Trim().ToLowerInvariant(),
            DepartmentId = departmentId,
            WorkforceRoleId = workforceRoleId,
            SalaryLevelId = salaryLevelId,
            Currency = currency,
            GrossPay = grossPay,
            TotalDeductions = totalDeductions,
            NetPay = netPay,
            DeductionsDetailJson = deductionsDetailJson,
            Status = PayrollItemStatus.Pending,
            CurrentAttemptNumber = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Claims the item for processing by a specific worker instance.
    /// </summary>
    public PayrollExecutionAttempt Claim(string workerId)
    {
        if (Status == PayrollItemStatus.Completed)
            throw new InvalidOperationException("Cannot claim already completed payroll item.");

        CurrentAttemptNumber++;
        ClaimedByWorkerId = workerId;
        ClaimedAtUtc = DateTime.UtcNow;
        Status = PayrollItemStatus.Processing;
        UpdatedAtUtc = DateTime.UtcNow;

        var attempt = PayrollExecutionAttempt.Create(Id, CurrentAttemptNumber, workerId);
        _attempts.Add(attempt);
        return attempt;
    }

    /// <summary>
    /// Marks the payroll item as successfully completed and settled.
    /// </summary>
    public void MarkCompleted(Guid ledgerTransactionId, Guid paymentVoucherId)
    {
        if (Status == PayrollItemStatus.Completed)
            return; // Idempotent

        Status = PayrollItemStatus.Completed;
        LedgerTransactionId = ledgerTransactionId;
        PaymentVoucherId = paymentVoucherId;
        ClaimedByWorkerId = null;
        LastFailureCode = null;
        LastFailureReason = null;
        UpdatedAtUtc = DateTime.UtcNow;

        var currentAttempt = _attempts.Find(a => a.AttemptNumber == CurrentAttemptNumber);
        currentAttempt?.MarkCompleted();
    }

    /// <summary>
    /// Marks the payroll item as failed.
    /// </summary>
    public void MarkFailed(string failureCode, string failureReason)
    {
        if (Status == PayrollItemStatus.Completed)
            throw new InvalidOperationException("Cannot mark completed payroll item as failed.");

        Status = PayrollItemStatus.Failed;
        ClaimedByWorkerId = null;
        LastFailureCode = failureCode;
        LastFailureReason = failureReason;
        UpdatedAtUtc = DateTime.UtcNow;

        var currentAttempt = _attempts.Find(a => a.AttemptNumber == CurrentAttemptNumber);
        currentAttempt?.MarkFailed(failureCode, failureReason);
    }

    /// <summary>
    /// Queues a previously failed payroll item for worker retry.
    /// </summary>
    public void QueueForRetry()
    {
        if (Status != PayrollItemStatus.Failed)
            throw new InvalidOperationException($"Only Failed payroll items can be queued for retry. Current status: '{Status}'.");

        Status = PayrollItemStatus.RetryPending;
        ClaimedByWorkerId = null;
        ClaimedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
