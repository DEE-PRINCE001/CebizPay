using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using Xunit;

namespace CebizPay.UnitTests.Loans;

public class LoanContractDomainTests
{
    [Fact]
    public void CreateFromApplication_ApprovedApplication_BuildsValidContract()
    {
        var orgId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var app = LoanApplication.Create(
            orgId,
            planId,
            "usr-100",
            "Alice Staff",
            1_200_000m,
            0.10m,
            12,
            110_000m,
            120_000m,
            1_320_000m,
            600_000m,
            0m,
            110_000m,
            110_000m,
            0.1833m,
            true);
        app.Approve("admin-999");

        var contract = LoanContract.CreateFromApplication(app);

        Assert.Equal(LoanType.CorporatePayrollLoan, contract.LoanType);
        Assert.Equal(LoanContractStatus.Active, contract.Status);
        Assert.Equal(1_200_000m, contract.OriginalPrincipal);
        Assert.Equal(1_200_000m, contract.OutstandingPrincipal);
        Assert.Equal(1_320_000m, contract.TotalRepayment);
        Assert.Equal(0m, contract.TotalAmountPaid);
        Assert.Equal(12, contract.NumberOfInstallments);
        Assert.Equal(110_000m, contract.MonthlyInstallmentAmount);
    }

    [Fact]
    public void ApplyRepayment_ConsecutiveInstallments_ReducesBalancesAndCompletesContract()
    {
        var orgId = Guid.NewGuid();
        var app = LoanApplication.Create(
            orgId, Guid.NewGuid(), "usr-100", "Alice Staff", 200_000m, 0.10m, 2,
            101_666.67m, 3_333.33m, 203_333.33m, 500_000m, 0m, 101_666.67m, 101_666.67m, 0.20m, true);
        app.Approve("admin-999");
        var contract = LoanContract.CreateFromApplication(app);

        var item1 = LoanRepaymentScheduleItem.Create(contract.Id, 1, DateTime.UtcNow.AddMonths(1), 101_666.67m, 100_000m, 1_666.67m);
        var item2 = LoanRepaymentScheduleItem.Create(contract.Id, 2, DateTime.UtcNow.AddMonths(2), 101_666.66m, 100_000m, 1_666.66m);
        contract.AddScheduleItem(item1);
        contract.AddScheduleItem(item2);

        // Apply first repayment
        var pItemId1 = Guid.NewGuid();
        var ledgerTx1 = Guid.NewGuid();
        contract.ApplyRepayment(1, 101_666.67m, pItemId1, ledgerTx1);

        Assert.Equal(LoanRepaymentStatus.Paid, item1.Status);
        Assert.Equal(101_666.67m, item1.PaidAmount);
        Assert.Equal(100_000m, contract.OutstandingPrincipal);
        Assert.Equal(101_666.67m, contract.TotalAmountPaid);
        Assert.Equal(LoanContractStatus.Active, contract.Status);

        // Apply second and final repayment
        var pItemId2 = Guid.NewGuid();
        var ledgerTx2 = Guid.NewGuid();
        contract.ApplyRepayment(2, 101_666.66m, pItemId2, ledgerTx2);

        Assert.Equal(LoanRepaymentStatus.Paid, item2.Status);
        Assert.Equal(0m, contract.OutstandingPrincipal);
        Assert.Equal(203_333.33m, contract.TotalAmountPaid);
        Assert.Equal(LoanContractStatus.PaidOff, contract.Status);
    }

    [Fact]
    public void CheckOverdue_WithPastDuePendingInstallments_TransitionsToOverdue()
    {
        var app = LoanApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "usr-100", "Alice Staff", 100_000m, 0.10m, 1,
            100_833.33m, 833.33m, 100_833.33m, 500_000m, 0m, 100_833.33m, 100_833.33m, 0.20m, true);
        app.Approve("admin-999");
        var contract = LoanContract.CreateFromApplication(app);

        // Installment due 5 days ago
        var item = LoanRepaymentScheduleItem.Create(contract.Id, 1, DateTime.UtcNow.AddDays(-5), 100_833.33m, 100_000m, 833.33m);
        contract.AddScheduleItem(item);

        contract.CheckOverdue(DateTime.UtcNow);

        Assert.Equal(LoanRepaymentStatus.Missed, item.Status);
        Assert.Equal(LoanContractStatus.Overdue, contract.Status);
    }

    [Fact]
    public void CreateConvertedIndividualLoan_TransitionsOriginalAndCreatesIndividualLoan()
    {
        var app = LoanApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "usr-100", "Alice Staff", 600_000m, 0.10m, 6,
            105_000m, 30_000m, 630_000m, 500_000m, 0m, 105_000m, 105_000m, 0.21m, true);
        app.Approve("admin-999");
        var originalLoan = LoanContract.CreateFromApplication(app);

        for (int i = 1; i <= 6; i++)
        {
            originalLoan.AddScheduleItem(LoanRepaymentScheduleItem.Create(
                originalLoan.Id, i, DateTime.UtcNow.AddMonths(i), 105_000m, 100_000m, 5_000m));
        }

        // Pay installment 1 & 2
        originalLoan.ApplyRepayment(1, 105_000m, Guid.NewGuid(), Guid.NewGuid());
        originalLoan.ApplyRepayment(2, 105_000m, Guid.NewGuid(), Guid.NewGuid());

        // Offboarding conversion
        var convertedLoan = LoanContract.CreateConvertedIndividualLoan(
            originalLoan, "Staff resigned and separated from organization.");
        originalLoan.ConvertToIndividual(convertedLoan.Id, "Staff resigned.");

        // Assert original
        Assert.Equal(LoanContractStatus.ConvertedToIndividual, originalLoan.Status);
        Assert.Equal(convertedLoan.Id, originalLoan.ConvertedToContractId);

        // Assert converted loan
        Assert.Equal(LoanType.StandardIndividualLoan, convertedLoan.LoanType);
        Assert.Equal(LoanContractStatus.Active, convertedLoan.Status);
        Assert.Equal(400_000m, convertedLoan.OriginalPrincipal);
        Assert.Equal(420_000m, convertedLoan.TotalRepayment);
        Assert.Equal(4, convertedLoan.NumberOfInstallments);
        Assert.Equal(105_000m, convertedLoan.MonthlyInstallmentAmount);
    }
}
