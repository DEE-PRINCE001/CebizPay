using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Infrastructure.Loans;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CebizPay.UnitTests.Loans;

public class LoanUnderwritingServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UnderwriteApplicationAsync_CompliantDti_ReturnsEligibleResult()
    {
        using var dbContext = CreateInMemoryDbContext();
        var calcService = new LoanCalculationService();
        var underwritingService = new LoanUnderwritingService(dbContext, calcService, NullLogger<LoanUnderwritingService>.Instance);

        var orgId = Guid.NewGuid();
        var userId = "emp-001";

        // Seed salary level: 600,000 NGN base
        var salaryLevel = new SalaryLevel(orgId, "Senior Engineer", 600_000m, "NGN");
        dbContext.SalaryLevels.Add(salaryLevel);

        // Seed membership
        var membership = new OrganizationMembership(userId, orgId, MembershipRoleType.Member, null, null, salaryLevel.Id);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        // Act: Request 1,200,000 at 10% for 12 months (Monthly = 110,000 <= 33% of 600k = 198,000)
        var result = await underwritingService.UnderwriteApplicationAsync(
            orgId, userId, 1_200_000m, 0.10m, 12);

        // Assert
        Assert.True(result.Eligible);
        Assert.True(result.IsDtiCompliant);
        Assert.Equal(600_000m, result.VerifiedSalary);
        Assert.Equal(0m, result.ExistingMonthlyDebt);
        Assert.Equal(110_000m, result.ProposedMonthlyPayment);
        Assert.Equal(110_000m, result.TotalMonthlyDebt);
        Assert.Equal(198_000m, result.MaxAllowedDebt);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task UnderwriteApplicationAsync_ExceedingDtiWithExistingActiveLoans_RejectsUnderwriting()
    {
        using var dbContext = CreateInMemoryDbContext();
        var calcService = new LoanCalculationService();
        var underwritingService = new LoanUnderwritingService(dbContext, calcService, NullLogger<LoanUnderwritingService>.Instance);

        var orgId = Guid.NewGuid();
        var userId = "emp-002";

        // Seed salary level: 300,000 NGN base (Max allowed debt = 33% * 300k = 99,000)
        var salaryLevel = new SalaryLevel(orgId, "Mid Engineer", 300_000m, "NGN");
        dbContext.SalaryLevels.Add(salaryLevel);

        var membership = new OrganizationMembership(userId, orgId, MembershipRoleType.Member, null, null, salaryLevel.Id);
        dbContext.OrganizationMemberships.Add(membership);

        // Seed an existing active loan with 52,500 NGN monthly installment
        var plan = CorporateLoanPlan.Create(orgId, "Plan", "Desc", 100_000m, 1_000_000m, 0.10m, 6, 12, 100_000m, RepaymentFrequency.Monthly);
        var app = LoanApplication.Create(orgId, plan.Id, userId, "Mid Staff", 300_000m, 0.10m, 6, 52_500m, 15_000m, 315_000m, 300_000m, 0m, 52_500m, 52_500m, 0.175m, true);
        app.Approve("admin-001");
        var existingContract = LoanContract.CreateFromApplication(app);
        dbContext.LoanContracts.Add(existingContract);

        await dbContext.SaveChangesAsync();

        // Act: Apply for a new loan with 55,000 monthly installment (Total debt = 52,500 + 55,000 = 107,500 > 99,000 max allowed)
        var result = await underwritingService.UnderwriteApplicationAsync(
            orgId, userId, 600_000m, 0.10m, 12);

        // Assert
        Assert.False(result.Eligible);
        Assert.False(result.IsDtiCompliant);
        Assert.Equal(300_000m, result.VerifiedSalary);
        Assert.Equal(52_500m, result.ExistingMonthlyDebt);
        Assert.Equal(55_000m, result.ProposedMonthlyPayment);
        Assert.Equal(107_500m, result.TotalMonthlyDebt);
        Assert.Equal(99_000m, result.MaxAllowedDebt);
        Assert.Contains("exceeds the 33% debt-to-income ceiling", result.Reason);
    }
}
