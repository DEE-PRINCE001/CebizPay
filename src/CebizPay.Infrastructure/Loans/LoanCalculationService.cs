using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Domain.Loans.Entities;

namespace CebizPay.Infrastructure.Loans;

/// <summary>
/// Deterministic calculation service implementing the locked Phase 4B flat/simple interest loan formula:
/// TotalInterest = Principal * AnnualRate * (DurationMonths / 12)
/// TotalRepayment = Principal + TotalInterest
/// MonthlyPayment = TotalRepayment / DurationMonths
/// </summary>
public sealed class LoanCalculationService : ILoanCalculationService
{
    /// <inheritdoc/>
    public (decimal MonthlyPayment, decimal TotalInterest, decimal TotalRepayment) CalculateFlatTerms(
        decimal principal,
        decimal annualInterestRate,
        int durationMonths)
    {
        if (principal <= 0)
            throw new ArgumentException("Principal must be positive.", nameof(principal));
        if (annualInterestRate < 0)
            throw new ArgumentException("AnnualInterestRate cannot be negative.", nameof(annualInterestRate));
        if (durationMonths <= 0)
            throw new ArgumentException("DurationMonths must be positive.", nameof(durationMonths));

        // Exact decimal arithmetic
        var durationInYears = (decimal)durationMonths / 12m;
        var totalInterest = Math.Round(principal * annualInterestRate * durationInYears, 2, MidpointRounding.AwayFromZero);
        var totalRepayment = principal + totalInterest;
        var monthlyPayment = Math.Round(totalRepayment / durationMonths, 2, MidpointRounding.AwayFromZero);

        return (monthlyPayment, totalInterest, totalRepayment);
    }

    /// <inheritdoc/>
    public LoanCalculationPreviewDto CalculatePreview(
        CorporateLoanPlan plan,
        decimal requestedAmount,
        int durationMonths,
        decimal verifiedSalary,
        decimal existingMonthlyDebt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var (isValid, errorMessage) = plan.ValidateEligibility(requestedAmount, durationMonths, verifiedSalary);

        var (monthlyPayment, totalInterest, totalRepayment) = CalculateFlatTerms(
            requestedAmount, plan.InterestRate, durationMonths);

        var proposedMonthlyPayment = monthlyPayment;
        var totalMonthlyDebt = existingMonthlyDebt + proposedMonthlyPayment;
        var maxAllowedMonthlyDebt = Math.Round(0.33m * verifiedSalary, 2, MidpointRounding.AwayFromZero);
        var dtiRatio = verifiedSalary > 0 ? Math.Round(totalMonthlyDebt / verifiedSalary, 4, MidpointRounding.AwayFromZero) : 1.0m;
        var isDtiCompliant = totalMonthlyDebt <= maxAllowedMonthlyDebt && verifiedSalary > 0;

        var isEligible = isValid && isDtiCompliant;
        var ineligibilityReason = !isValid ? errorMessage : (!isDtiCompliant ? $"Total monthly debt obligation ({totalMonthlyDebt:N2}) exceeds 33% debt-to-income ceiling ({maxAllowedMonthlyDebt:N2})." : null);

        return new LoanCalculationPreviewDto(
            RequestedAmount: requestedAmount,
            AnnualInterestRate: plan.InterestRate,
            DurationMonths: durationMonths,
            MonthlyPayment: monthlyPayment,
            TotalInterest: totalInterest,
            TotalRepayment: totalRepayment,
            VerifiedSalary: verifiedSalary,
            ExistingMonthlyDebt: existingMonthlyDebt,
            ProposedMonthlyPayment: proposedMonthlyPayment,
            TotalMonthlyDebt: totalMonthlyDebt,
            DebtToIncomeRatio: dtiRatio,
            MaxAllowedMonthlyDebt: maxAllowedMonthlyDebt,
            IsDtiCompliant: isDtiCompliant,
            IsEligible: isEligible,
            IneligibilityReason: ineligibilityReason);
    }
}
