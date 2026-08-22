namespace CebizPay.Domain.Loans.Enums;

/// <summary>
/// Status of an individual scheduled repayment installment.
/// </summary>
public enum LoanRepaymentStatus
{
    /// <summary>Scheduled installment awaiting future due date.</summary>
    Pending = 1,
    /// <summary>Installment currently due for payroll auto-deduction or payment.</summary>
    Due = 2,
    /// <summary>Installment fully paid and settled.</summary>
    Paid = 3,
    /// <summary>Installment due date passed without full payment.</summary>
    Missed = 4,
    /// <summary>Installment explicitly waived per authorized policy.</summary>
    Waived = 5
}
