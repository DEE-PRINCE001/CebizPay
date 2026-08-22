namespace CebizPay.Domain.Loans.Enums;

/// <summary>
/// Classification of loan obligation.
/// </summary>
public enum LoanType
{
    /// <summary>Corporate payroll loan repaid automatically via employer salary deductions.</summary>
    CorporatePayrollLoan = 1,
    /// <summary>Standard individual loan repaid directly by borrower wallet.</summary>
    StandardIndividualLoan = 2
}
