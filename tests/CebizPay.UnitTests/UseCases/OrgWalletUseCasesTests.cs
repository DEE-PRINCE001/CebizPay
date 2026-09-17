using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Wallet;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

public sealed class OrgWalletUseCasesTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetOrgWalletOverview_WhenAuthorized_ComputesCumulativeMetricsCorrectly()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("Cebis Tech", "finance@cebistech.com", "+2348012345678");
        dbContext.Organizations.Add(org);

        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        wallet.Credit(150000m);
        dbContext.Wallets.Add(wallet);

        var virtualAccount = VirtualAccount.CreateOrganization(
            org.Id,
            PaymentProvider.Paystack,
            "0123456789",
            "Cebis Tech",
            "035",
            "Wema Bank / CebizPay",
            Currency.NGN);
        dbContext.VirtualAccounts.Add(virtualAccount);

        var batch = PayrollBatch.Create(org.Id, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow, "admin-1");
        dbContext.PayrollBatches.Add(batch);

        var completedPayrollItem = PayrollItem.Create(
            batch.Id,
            org.Id,
            "staff-1",
            "Emp 1",
            "emp1@cebistech.com",
            Currency.NGN,
            50000m,
            5000m);
        completedPayrollItem.Claim("worker-1");
        completedPayrollItem.MarkCompleted(Guid.NewGuid(), Guid.NewGuid());
        dbContext.PayrollItems.Add(completedPayrollItem);

        var pendingPayrollItem = PayrollItem.Create(
            batch.Id,
            org.Id,
            "staff-2",
            "Emp 2",
            "emp2@cebistech.com",
            Currency.NGN,
            30000m,
            3000m);
        dbContext.PayrollItems.Add(pendingPayrollItem);

        var loanApp1 = LoanApplication.Create(
            org.Id,
            Guid.NewGuid(),
            "staff-1",
            "Emp 1",
            80000m,
            0.05m,
            6,
            14000m,
            4000m,
            84000m,
            50000m,
            0m,
            14000m,
            14000m,
            0.034m,
            true,
            "Auto approved",
            RepaymentFrequency.Monthly,
            true);
        loanApp1.Approve("admin-1");
        var activeLoan = LoanContract.CreateFromApplication(loanApp1);
        dbContext.LoanContracts.Add(activeLoan);

        var loanApp2 = LoanApplication.Create(
            org.Id,
            Guid.NewGuid(),
            "staff-2",
            "Emp 2",
            50000m,
            0.05m,
            6,
            9000m,
            2500m,
            52500m,
            50000m,
            0m,
            9000m,
            9000m,
            0.034m,
            true,
            "Auto approved",
            RepaymentFrequency.Monthly,
            true);
        loanApp2.Approve("admin-1");
        var cancelledLoan = LoanContract.CreateFromApplication(loanApp2);
        dbContext.LoanContracts.Add(cancelledLoan);
        dbContext.Entry(cancelledLoan).Property(x => x.Status).CurrentValue = LoanContractStatus.Cancelled;

        var activeSavings = SavingsAccount.CreateFixedLockAccount(
            Guid.NewGuid(),
            "owner-1",
            org.Id,
            Currency.NGN,
            12m,
            1,
            90,
            DateTime.UtcNow);
        activeSavings.RecordContribution(65000m, Guid.NewGuid(), "idemp-sav-1");
        dbContext.SavingsAccounts.Add(activeSavings);

        var withdrawnSavings = SavingsAccount.CreateFixedLockAccount(
            Guid.NewGuid(),
            "owner-2",
            org.Id,
            Currency.NGN,
            12m,
            1,
            90,
            DateTime.UtcNow.AddDays(-100));
        withdrawnSavings.RecordContribution(30000m, Guid.NewGuid(), "idemp-sav-2");
        withdrawnSavings.ExecuteWithdrawal(30000m, 0m, 0m, Guid.NewGuid(), DateTime.UtcNow);
        dbContext.SavingsAccounts.Add(withdrawnSavings);

        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetOrgWalletOverviewQueryHandler(dbContext, orgContext);
        var query = new GetOrgWalletOverviewQuery(org.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(wallet.Id, result.WalletId);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(150000m, result.AvailableBalance);
        Assert.Equal("0123456789", result.AccountNumber);
        Assert.Equal("Cebis Tech", result.AccountName);
        Assert.Equal(45000m, result.TotalSalaryPaid);
        Assert.Equal(80000m, result.TotalLoanFund);
        Assert.Equal(65000m, result.TotalSavingMoney);
    }

    [Fact]
    public async Task GetOrgWalletOverview_WhenUnauthorized_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new GetOrgWalletOverviewQueryHandler(dbContext, orgContext);
        var query = new GetOrgWalletOverviewQuery(orgId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrgWalletOverview_WhenWalletDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("No Wallet Org", "nowallet@org.com", "+2348000000000");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetOrgWalletOverviewQueryHandler(dbContext, orgContext);
        var query = new GetOrgWalletOverviewQuery(org.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
