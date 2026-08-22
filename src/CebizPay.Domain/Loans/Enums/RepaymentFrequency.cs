namespace CebizPay.Domain.Loans.Enums;

/// <summary>
/// Frequency of periodic loan repayment installments.
/// </summary>
public enum RepaymentFrequency
{
    /// <summary>Monthly repayment installment (standard corporate payroll frequency).</summary>
    Monthly = 1,
    /// <summary>Weekly repayment installment.</summary>
    Weekly = 2,
    /// <summary>Bi-weekly repayment installment.</summary>
    BiWeekly = 3
}
