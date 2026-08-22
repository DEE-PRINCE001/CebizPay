using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Infrastructure.Payroll;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.UnitTests.Loans;

public class PayrollLoanDeductionProviderTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetDeductionsForEmployeeAsync_WithActiveCorporateLoan_ReturnsEarliestPendingInstallment()
    {
        using var dbContext = CreateInMemoryDbContext();
        var provider = new PayrollLoanDeductionProvider(dbContext);

        var orgId = Guid.NewGuid();
        var employeeUserId = "emp-007";

        var app = LoanApplication.Create(
            orgId, Guid.NewGuid(), employeeUserId, "James Bond", 600_000m, 0.10m, 6,
            105_000m, 30_000m, 630_000m, 800_000m, 0m, 105_000m, 105_000m, 0.13m, true);
        app.Approve("admin-001");
        var contract = LoanContract.CreateFromApplication(app);

        var item1 = LoanRepaymentScheduleItem.Create(contract.Id, 1, DateTime.UtcNow.AddDays(5), 105_000m, 100_000m, 5_000m);
        var item2 = LoanRepaymentScheduleItem.Create(contract.Id, 2, DateTime.UtcNow.AddMonths(1), 105_000m, 100_000m, 5_000m);
        contract.AddScheduleItem(item1);
        contract.AddScheduleItem(item2);

        dbContext.LoanContracts.Add(contract);
        await dbContext.SaveChangesAsync();

        // Act
        var deductions = await provider.GetDeductionsForEmployeeAsync(
            orgId, employeeUserId, 800_000m, Currency.NGN);

        // Assert
        Assert.Single(deductions);
        var deduction = deductions[0];
        Assert.Equal("CORPORATE_LOAN_REPAYMENT", deduction.DeductionType);
        Assert.Equal(105_000m, deduction.Amount);
        Assert.Equal(item1.Id.ToString(), deduction.Reference);
        Assert.Contains("Installment #1", deduction.Description);
    }

    [Fact]
    public async Task GetDeductionsForEmployeeAsync_WhenLoanIsConvertedToIndividual_ReturnsNoDeductions()
    {
        using var dbContext = CreateInMemoryDbContext();
        var provider = new PayrollLoanDeductionProvider(dbContext);

        var orgId = Guid.NewGuid();
        var employeeUserId = "emp-008";

        var app = LoanApplication.Create(
            orgId, Guid.NewGuid(), employeeUserId, "Departing Staff", 600_000m, 0.10m, 6,
            105_000m, 30_000m, 630_000m, 800_000m, 0m, 105_000m, 105_000m, 0.13m, true);
        app.Approve("admin-001");
        var contract = LoanContract.CreateFromApplication(app);
        var item1 = LoanRepaymentScheduleItem.Create(contract.Id, 1, DateTime.UtcNow.AddDays(5), 105_000m, 100_000m, 5_000m);
        contract.AddScheduleItem(item1);

        // Offboarding conversion
        contract.ConvertToIndividual(Guid.NewGuid(), "Offboarding");

        dbContext.LoanContracts.Add(contract);
        await dbContext.SaveChangesAsync();

        // Act
        var deductions = await provider.GetDeductionsForEmployeeAsync(
            orgId, employeeUserId, 800_000m, Currency.NGN);

        // Assert: No payroll deductions should be generated for converted or non-active corporate loans
        Assert.Empty(deductions);
    }
}
