using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Domain.Loans.Entities;

/// <summary>
/// Domain entity representing a single scheduled repayment installment within a loan contract.
/// </summary>
public class LoanRepaymentScheduleItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent loan contract ID.</summary>
    public Guid LoanContractId { get; private set; }

    /// <summary>Sequential installment number (1, 2, 3...).</summary>
    public int InstallmentNumber { get; private set; }

    /// <summary>Due date for this installment.</summary>
    public DateTime DueDate { get; private set; }

    /// <summary>Total scheduled installment amount due.</summary>
    public decimal ScheduledAmount { get; private set; }

    /// <summary>Portion of installment covering loan principal.</summary>
    public decimal PrincipalComponent { get; private set; }

    /// <summary>Portion of installment covering interest.</summary>
    public decimal InterestComponent { get; private set; }

    /// <summary>Total amount successfully paid toward this installment.</summary>
    public decimal PaidAmount { get; private set; }

    /// <summary>Lifecycle status of this installment.</summary>
    public LoanRepaymentStatus Status { get; private set; } = LoanRepaymentStatus.Pending;

    /// <summary>Timestamp when installment was settled.</summary>
    public DateTime? PaidAtUtc { get; private set; }

    /// <summary>Timestamp when installment was marked missed.</summary>
    public DateTime? MissedAtUtc { get; private set; }

    /// <summary>Optional linked payroll line item ID if settled via corporate payroll auto-deduction.</summary>
    public Guid? PayrollItemId { get; private set; }

    /// <summary>Optional linked central double-entry ledger transaction ID.</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last state update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private LoanRepaymentScheduleItem() { } // EF Core

    /// <summary>
    /// Creates a scheduled repayment installment item.
    /// </summary>
    public static LoanRepaymentScheduleItem Create(
        Guid loanContractId,
        int installmentNumber,
        DateTime dueDate,
        decimal scheduledAmount,
        decimal principalComponent,
        decimal interestComponent)
    {
        if (loanContractId == Guid.Empty)
            throw new ArgumentException("LoanContractId is required.", nameof(loanContractId));
        if (installmentNumber <= 0)
            throw new ArgumentException("InstallmentNumber must be positive.", nameof(installmentNumber));
        if (scheduledAmount <= 0)
            throw new ArgumentException("ScheduledAmount must be positive.", nameof(scheduledAmount));

        return new LoanRepaymentScheduleItem
        {
            Id = Guid.NewGuid(),
            LoanContractId = loanContractId,
            InstallmentNumber = installmentNumber,
            DueDate = dueDate,
            ScheduledAmount = scheduledAmount,
            PrincipalComponent = principalComponent,
            InterestComponent = interestComponent,
            PaidAmount = 0m,
            Status = LoanRepaymentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the installment as due for processing.
    /// </summary>
    public void MarkDue()
    {
        if (Status == LoanRepaymentStatus.Paid || Status == LoanRepaymentStatus.Waived)
            return;

        Status = LoanRepaymentStatus.Due;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the installment as fully or partially settled.
    /// </summary>
    public void MarkPaid(decimal amount, Guid? payrollItemId = null, Guid? ledgerTransactionId = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        PaidAmount += amount;
        PayrollItemId = payrollItemId ?? PayrollItemId;
        LedgerTransactionId = ledgerTransactionId ?? LedgerTransactionId;
        PaidAtUtc = DateTime.UtcNow;

        if (PaidAmount >= ScheduledAmount)
        {
            Status = LoanRepaymentStatus.Paid;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the installment as missed when overdue.
    /// </summary>
    public void MarkMissed()
    {
        if (Status == LoanRepaymentStatus.Paid || Status == LoanRepaymentStatus.Waived)
            return;

        Status = LoanRepaymentStatus.Missed;
        MissedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the installment as waived.
    /// </summary>
    public void MarkWaived()
    {
        Status = LoanRepaymentStatus.Waived;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
