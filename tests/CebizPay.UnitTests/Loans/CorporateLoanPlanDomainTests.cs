using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using Xunit;

namespace CebizPay.UnitTests.Loans;

public class CorporateLoanPlanDomainTests
{
    [Fact]
    public void Create_ValidPlan_InitializesPropertiesCorrectly()
    {
        var orgId = Guid.NewGuid();
        var plan = CorporateLoanPlan.Create(
            orgId,
            "Staff Standard Loan",
            "Annual low-rate loan",
            50_000m,
            1_000_000m,
            0.08m,
            6,
            24,
            150_000m,
            RepaymentFrequency.Monthly);

        Assert.Equal(orgId, plan.OrganizationId);
        Assert.Equal("Staff Standard Loan", plan.Name);
        Assert.Equal(50_000m, plan.MinimumAmount);
        Assert.Equal(1_000_000m, plan.MaximumAmount);
        Assert.Equal(0.08m, plan.InterestRate);
        Assert.Equal(6, plan.MinimumDurationMonths);
        Assert.Equal(24, plan.MaximumDurationMonths);
        Assert.Equal(RepaymentFrequency.Monthly, plan.RepaymentFrequency);
        Assert.Equal(150_000m, plan.MinimumMonthlySalary);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void ValidateEligibility_AmountOutsideBounds_FailsValidation()
    {
        var plan = CorporateLoanPlan.Create(
            Guid.NewGuid(), "Plan", "Desc", 100_000m, 500_000m, 0.10m, 3, 12, 200_000m, RepaymentFrequency.Monthly);

        var (isValidLow, errorLow) = plan.ValidateEligibility(50_000m, 6, 250_000m);
        var (isValidHigh, errorHigh) = plan.ValidateEligibility(600_000m, 6, 250_000m);

        Assert.False(isValidLow);
        Assert.Contains("must be between", errorLow);

        Assert.False(isValidHigh);
        Assert.Contains("must be between", errorHigh);
    }

    [Fact]
    public void ValidateEligibility_DurationOutsideBounds_FailsValidation()
    {
        var plan = CorporateLoanPlan.Create(
            Guid.NewGuid(), "Plan", "Desc", 100_000m, 500_000m, 0.10m, 6, 12, 200_000m, RepaymentFrequency.Monthly);

        var (isValidShort, errorShort) = plan.ValidateEligibility(200_000m, 3, 250_000m);
        var (isValidLong, errorLong) = plan.ValidateEligibility(200_000m, 18, 250_000m);

        Assert.False(isValidShort);
        Assert.Contains("must be between", errorShort);

        Assert.False(isValidLong);
        Assert.Contains("must be between", errorLong);
    }

    [Fact]
    public void ValidateEligibility_SalaryBelowThreshold_FailsValidation()
    {
        var plan = CorporateLoanPlan.Create(
            Guid.NewGuid(), "Plan", "Desc", 100_000m, 500_000m, 0.10m, 6, 12, 300_000m, RepaymentFrequency.Monthly);

        var (isValid, error) = plan.ValidateEligibility(200_000m, 6, 250_000m);

        Assert.False(isValid);
        Assert.Contains("below the required plan threshold", error);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var plan = CorporateLoanPlan.Create(
            Guid.NewGuid(), "Plan", "Desc", 100_000m, 500_000m, 0.10m, 6, 12, 100_000m, RepaymentFrequency.Monthly);

        plan.Deactivate();

        Assert.False(plan.IsActive);
    }
}
