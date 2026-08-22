using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;

namespace CebizPay.Domain.Payroll.Entities;

/// <summary>
/// Domain aggregate root representing a corporate payroll batch run.
/// Manages the lifecycle, total financial aggregates, selection scope, and progress of payroll items.
/// </summary>
public class PayrollBatch
{
    private readonly List<PayrollItem> _items = new();

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Human-readable unique business reference (e.g. PB-202608-ABC12345).</summary>
    public string BatchReference { get; private set; } = string.Empty;

    /// <summary>Owning organization tenant ID.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Transactional currency for this payroll run.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Workforce selection mode used to compile eligible employees.</summary>
    public PayrollSelectionMode SelectionMode { get; private set; }

    /// <summary>Serialized JSON snapshot of selection filters.</summary>
    public string? SelectionCriteriaJson { get; private set; }

    /// <summary>Payroll salary period start date.</summary>
    public DateTime PeriodStart { get; private set; }

    /// <summary>Payroll salary period end date.</summary>
    public DateTime PeriodEnd { get; private set; }

    /// <summary>Current batch lifecycle status.</summary>
    public PayrollBatchStatus Status { get; private set; } = PayrollBatchStatus.Pending;

    /// <summary>Total number of employees in this batch.</summary>
    public int TotalEmployees { get; private set; }

    /// <summary>Aggregated total gross salary amount.</summary>
    public decimal TotalGrossAmount { get; private set; }

    /// <summary>Aggregated total deductions amount.</summary>
    public decimal TotalDeductionsAmount { get; private set; }

    /// <summary>Aggregated total net payout amount.</summary>
    public decimal TotalNetAmount { get; private set; }

    /// <summary>User ID of the initiator who authorized and scheduled the batch.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp when worker began processing the batch.</summary>
    public DateTime? StartedAtUtc { get; private set; }

    /// <summary>Timestamp when batch reached a terminal/completion state.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Timestamp when batch failed completely.</summary>
    public DateTime? FailedAtUtc { get; private set; }

    /// <summary>Failure reason description if batch execution failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Read-only collection of payroll item units.</summary>
    public virtual IReadOnlyCollection<PayrollItem> Items => _items.AsReadOnly();

    private PayrollBatch() { } // EF Core

    /// <summary>
    /// Creates a new payroll batch aggregate.
    /// </summary>
    public static PayrollBatch Create(
        Guid organizationId,
        Currency currency,
        PayrollSelectionMode selectionMode,
        DateTime periodStart,
        DateTime periodEnd,
        string createdByUserId,
        string? selectionCriteriaJson = null,
        string? customReference = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (periodEnd < periodStart)
            throw new ArgumentException("PeriodEnd cannot be earlier than PeriodStart.", nameof(periodEnd));

        currency.EnsureTransactionalV1();

        var refCode = customReference ?? $"PB-{periodStart:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        return new PayrollBatch
        {
            Id = Guid.NewGuid(),
            BatchReference = refCode,
            OrganizationId = organizationId,
            Currency = currency,
            SelectionMode = selectionMode,
            SelectionCriteriaJson = selectionCriteriaJson,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CreatedByUserId = createdByUserId,
            Status = PayrollBatchStatus.Pending,
            TotalEmployees = 0,
            TotalGrossAmount = 0m,
            TotalDeductionsAmount = 0m,
            TotalNetAmount = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds a calculated payroll item to the batch during preparation.
    /// </summary>
    public void AddItem(PayrollItem item)
    {
        if (Status != PayrollBatchStatus.Pending)
            throw new InvalidOperationException($"Cannot add items to batch with status {Status}.");

        _items.Add(item);
        TotalEmployees++;
        TotalGrossAmount += item.GrossPay;
        TotalDeductionsAmount += item.TotalDeductions;
        TotalNetAmount += item.NetPay;
    }

    /// <summary>
    /// Marks the batch as actively processing.
    /// </summary>
    public void MarkProcessing()
    {
        if (Status == PayrollBatchStatus.Completed || Status == PayrollBatchStatus.Cancelled)
            throw new InvalidOperationException($"Cannot move batch from {Status} to Processing.");

        if (Status == PayrollBatchStatus.Pending)
        {
            StartedAtUtc = DateTime.UtcNow;
        }

        Status = PayrollBatchStatus.Processing;
    }

    /// <summary>
    /// Marks the batch as fully completed.
    /// </summary>
    public void MarkCompleted()
    {
        Status = PayrollBatchStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the batch as partially completed when one or more items fail.
    /// </summary>
    public void MarkPartiallyCompleted()
    {
        Status = PayrollBatchStatus.PartiallyCompleted;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the entire batch as failed.
    /// </summary>
    public void MarkFailed(string reason)
    {
        Status = PayrollBatchStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason;
    }

    /// <summary>
    /// Cancels the batch before any item begins execution.
    /// </summary>
    public void Cancel()
    {
        if (Status != PayrollBatchStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel batch in status '{Status}'. Only Pending batches can be cancelled.");

        Status = PayrollBatchStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
