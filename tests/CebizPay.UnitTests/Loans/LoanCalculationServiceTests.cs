using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Infrastructure.Loans;
using Xunit;

namespace CebizPay.UnitTests.Loans;

public class LoanCalculationServiceTests
{
    private readonly LoanCalculationService _service = new();

    [Fact]
    public void CalculateFlatTerms_StandardTwelveMonthLoan_ComputesAccurateInterestAndInstallments()
    {
        // Arrange: 1,200,000 NGN at 10% annual interest for 12 months
        decimal principal = 1_200_000m;
        decimal annualRate = 0.10m;
        int durationMonths = 12;

        // Act
        var (monthlyPayment, totalInterest, totalRepayment) = _service.CalculateFlatTerms(principal, annualRate, durationMonths);

        // Assert
        // Expected Interest = 1,200,000 * 0.10 * (12/12) = 120,000
        // Total Repayment = 1,320,000
        // Monthly Payment = 1,320,000 / 12 = 110,000
        Assert.Equal(120_000m, totalInterest);
        Assert.Equal(1_320_000m, totalRepayment);
        Assert.Equal(110_000m, monthlyPayment);
    }

    [Fact]
    public void CalculateFlatTerms_SixMonthLoan_ComputesProRatedInterest()
    {
        // Arrange: 600,000 NGN at 12% annual interest for 6 months
        decimal principal = 600_000m;
        decimal annualRate = 0.12m;
        int durationMonths = 6;

        // Act
        var (monthlyPayment, totalInterest, totalRepayment) = _service.CalculateFlatTerms(principal, annualRate, durationMonths);

        // Assert
        // Interest = 600,000 * 0.12 * (6/12) = 36,000
        // Total Repayment = 636,000
        // Monthly Payment = 636,000 / 6 = 106,000
        Assert.Equal(36_000m, totalInterest);
        Assert.Equal(636_000m, totalRepayment);
        Assert.Equal(106_000m, monthlyPayment);
    }

    [Fact]
    public void CalculateFlatTerms_ZeroPrincipalOrDuration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.CalculateFlatTerms(0m, 0.10m, 12));
        Assert.Throws<ArgumentException>(() => _service.CalculateFlatTerms(100_000m, 0.10m, 0));
        Assert.Throws<ArgumentException>(() => _service.CalculateFlatTerms(100_000m, -0.05m, 12));
    }

    [Fact]
    public void PlanEligibility_WithCompliantSalaryAndBounds_ValidatesSuccessfully()
    {
        // Arrange
        var plan = CorporateLoanPlan.Create(
            Guid.NewGuid(),
            "Standard Staff Loan",
            "Test description",
            100_000m,
            2_000_000m,
            0.10m,
            3,
            24,
            300_000m,
            RepaymentFrequency.Monthly);

        // Act
        var (isValid, errorMessage) = plan.ValidateEligibility(600_000m, 12, 500_000m);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void PlanEligibility_WithSalaryBelowThreshold_FailsValidation()
    {
        // Arrange
        var plan = CorporateLoanPlan.Create(
            Guid.NewGuid(),
            "Standard Staff Loan",
            "Test description",
            100_000m,
            2_000_000m,
            0.10m,
            3,
            24,
            300_000m,
            RepaymentFrequency.Monthly);

        // Act
        var (isValid, errorMessage) = plan.ValidateEligibility(600_000m, 12, 200_000m);

        // Assert
        Assert.False(isValid);
        Assert.Contains("below the required plan threshold", errorMessage);
    }
}
