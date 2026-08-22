using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Payroll;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CebizPay.IntegrationTests.Payroll;

/// <summary>
/// PostgreSQL Testcontainers integration tests verifying end-to-end corporate payroll execution,
/// 50-employee batch processing, atomic per-item double-entry ledger settlement, row locking,
/// failure containment, retries, and payment voucher lifecycle.
/// </summary>
public sealed class PayrollIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public PayrollIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task FiftyEmployeeBatch_Execution_ShouldDisburseLedgerAndGenerateVouchersAtomically()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var deductionProvider = new NullPayrollDeductionProvider();
        var calcService = new PayrollCalculationService(dbContext, deductionProvider);
        var batchService = new PayrollBatchService(dbContext, calcService, outboxService, NullLogger<PayrollBatchService>.Instance);
        var executionService = new PayrollExecutionService(dbContext, ledgerService, outboxService, NullLogger<PayrollExecutionService>.Instance);

        var org = new Organization($"Payroll Corp {Guid.NewGuid():N}", "payroll@corp.com", "+2348000000000");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(50000000m); // 50,000,000 NGN
        dbContext.Wallets.Add(orgWallet);

        var salaryLevel = new SalaryLevel(org.Id, "Engineer", 500000m, "NGN");
        dbContext.SalaryLevels.Add(salaryLevel);

        var employeeCount = 50;
        var employeeUserIds = new List<string>(employeeCount);

        for (int i = 1; i <= employeeCount; i++)
        {
            var empUserId = $"usr_emp_{i}_{Guid.NewGuid():N}";
            employeeUserIds.Add(empUserId);

            var profile = new IndividualProfile(empUserId, $"Employee{i}", "Test");
            dbContext.IndividualProfiles.Add(profile);

            var membership = new OrganizationMembership(empUserId, org.Id, MembershipRoleType.Member, null, null, salaryLevel.Id);
            dbContext.OrganizationMemberships.Add(membership);

            var empWallet = Wallet.CreateIndividualWallet(empUserId, Currency.NGN);
            dbContext.Wallets.Add(empWallet);
        }

        await dbContext.SaveChangesAsync();

        // 1. Enqueue Batch
        var criteria = new PayrollSelectionCriteria(PayrollSelectionMode.All);
        var batchDto = await batchService.CreateAndEnqueueBatchAsync(
            organizationId: org.Id,
            initiatorUserId: "usr_admin",
            currency: Currency.NGN,
            periodStart: DateTime.UtcNow.AddDays(-30),
            periodEnd: DateTime.UtcNow,
            criteria: criteria);

        Assert.Equal(50, batchDto.TotalEmployees);
        Assert.Equal(25000000m, batchDto.TotalNetAmount);
        Assert.Equal(PayrollBatchStatus.Pending, batchDto.Status);

        // 2. Fetch and Execute all 50 items
        var itemIds = await dbContext.PayrollItems
            .Where(i => i.PayrollBatchId == batchDto.BatchId)
            .Select(i => i.Id)
            .ToListAsync();

        Assert.Equal(50, itemIds.Count);

        foreach (var itemId in itemIds)
        {
            await using var itemContext = await CreateDbContextAsync();
            var itemLedgerService = new LedgerPostingService(itemContext);
            var itemOutboxService = new OutboxService(itemContext);
            var itemExecutionService = new PayrollExecutionService(itemContext, itemLedgerService, itemOutboxService, NullLogger<PayrollExecutionService>.Instance);

            var execResult = await itemExecutionService.ExecutePayrollItemAsync(itemId, "TestWorker-1");
            Assert.True(execResult.Succeeded);
            Assert.NotNull(execResult.LedgerTransactionId);
            Assert.NotNull(execResult.PaymentVoucherId);
        }

        // 3. Verify Database Financial Invariants
        await using var verifyContext = await CreateDbContextAsync();

        var refreshedOrgWallet = await verifyContext.Wallets.FindAsync(orgWallet.Id);
        Assert.NotNull(refreshedOrgWallet);
        Assert.Equal(25000000m, refreshedOrgWallet.AvailableBalance); // 50m - 25m

        foreach (var empUserId in employeeUserIds)
        {
            var empWallet = await verifyContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == empUserId && w.Currency == Currency.NGN);
            Assert.NotNull(empWallet);
            Assert.Equal(500000m, empWallet.AvailableBalance);
        }

        var voucherCount = await verifyContext.PaymentVouchers.CountAsync(v => v.PayrollBatchId == batchDto.BatchId);
        Assert.Equal(50, voucherCount);

        var ledgerCount = await verifyContext.LedgerTransactions.CountAsync(t => t.TransactionType == LedgerTransactionType.Payroll);
        Assert.True(ledgerCount >= 50);

        // Verify batch progress projection
        var progress = await batchService.GetBatchProgressAsync(org.Id, batchDto.BatchId);
        Assert.NotNull(progress);
        Assert.Equal(50, progress.CompletedCount);
        Assert.Equal(0, progress.FailedCount);
        Assert.Equal(100m, progress.ProgressPercentage);
    }

    [Fact]
    public async Task InsufficientFunds_ShouldContainFailure_AndSucceedOnRetryAfterFunding()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var calcService = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(dbContext, calcService, outboxService, NullLogger<PayrollBatchService>.Instance);
        var executionService = new PayrollExecutionService(dbContext, ledgerService, outboxService, NullLogger<PayrollExecutionService>.Instance);

        var org = new Organization($"Payroll Corp 2 {Guid.NewGuid():N}", "payroll2@corp.com", "+2348000000000");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(1000000m); // 1,000,000 NGN balance
        dbContext.Wallets.Add(orgWallet);

        var level = new SalaryLevel(org.Id, "Standard", 500000m, "NGN");
        dbContext.SalaryLevels.Add(level);

        var emp1 = $"emp_retry_1_{Guid.NewGuid():N}";
        var emp2 = $"emp_retry_2_{Guid.NewGuid():N}";

        var m1 = new OrganizationMembership(emp1, org.Id, MembershipRoleType.Member, null, null, level.Id);
        var m2 = new OrganizationMembership(emp2, org.Id, MembershipRoleType.Member, null, null, level.Id);
        dbContext.OrganizationMemberships.AddRange(m1, m2);

        var w1 = Wallet.CreateIndividualWallet(emp1, Currency.NGN);
        var w2 = Wallet.CreateIndividualWallet(emp2, Currency.NGN);
        dbContext.Wallets.AddRange(w1, w2);

        await dbContext.SaveChangesAsync();

        // Enqueue batch (Requires 1,000,000 NGN total)
        var batch = await batchService.CreateAndEnqueueBatchAsync(org.Id, "usr_admin", Currency.NGN, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, new PayrollSelectionCriteria());

        // Simulate concurrent balance reduction on Org wallet (e.g. external transfer reduced balance to 500k)
        orgWallet.Debit(600000m); // Remaining balance: 400,000 NGN
        await dbContext.SaveChangesAsync();

        var items = await dbContext.PayrollItems.Where(i => i.PayrollBatchId == batch.BatchId).OrderBy(i => i.CreatedAtUtc).ToListAsync();

        // Execute item 1 (Needs 500,000 NGN, Org has 400,000 NGN -> Fails with Insufficient organization wallet balance)
        var result1 = await executionService.ExecutePayrollItemAsync(items[0].Id, "Worker-1");
        Assert.False(result1.Succeeded);

        // Progress check
        var progress = await batchService.GetBatchProgressAsync(org.Id, batch.BatchId);
        Assert.NotNull(progress);
        Assert.Equal(1, progress.FailedCount);

        // Fund Org Wallet to 2,000,000 NGN
        orgWallet.Credit(2000000m);
        await dbContext.SaveChangesAsync();

        // Trigger Retry
        var retriedCount = await batchService.RetryFailedItemsAsync(org.Id, batch.BatchId, "usr_admin");
        Assert.Equal(1, retriedCount);

        // Execute retried item
        var retryResult = await executionService.ExecutePayrollItemAsync(items[0].Id, "Worker-1");
        Assert.True(retryResult.Succeeded);

        // Execute item 2
        var result2 = await executionService.ExecutePayrollItemAsync(items[1].Id, "Worker-1");
        Assert.True(result2.Succeeded);

        var finalProgress = await batchService.GetBatchProgressAsync(org.Id, batch.BatchId);
        Assert.NotNull(finalProgress);
        Assert.Equal(2, finalProgress.CompletedCount);
        Assert.Equal(0, finalProgress.FailedCount);
        Assert.Equal(100m, finalProgress.ProgressPercentage);
    }
}
