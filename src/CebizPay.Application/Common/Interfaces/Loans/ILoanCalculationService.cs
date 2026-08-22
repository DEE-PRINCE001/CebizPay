using CebizPay.Domain.Loans.Entities;

namespace CebizPay.Application.Common.Interfaces.Loans;

/// <summary>
/// Service contract for deterministic flat/simple interest loan computations and installment schedules.
/// </summary>
public interface ILoanCalculationService
{
    /// <summary>
    /// Computes loan interest, total repayment, and monthly payment under the locked flat-interest model:
    /// Interest = Principal * AnnualRate * (DurationMonths / 12)
    /// TotalRepayment = Principal + Interest
    /// MonthlyPayment = TotalRepayment / DurationMonths
    /// </summary>
    (decimal MonthlyPayment, decimal TotalInterest, decimal TotalRepayment) CalculateFlatTerms(
        decimal principal,
        decimal annualInterestRate,
        int durationMonths);

    /// <summary>
    /// Generates a preview calculation combining plan rules, flat interest calculation, and 33% DTI ratio checks.
    /// </summary>
    LoanCalculationPreviewDto CalculatePreview(
        CorporateLoanPlan plan,
        decimal requestedAmount,
        int durationMonths,
        decimal verifiedSalary,
        decimal existingMonthlyDebt);
}
