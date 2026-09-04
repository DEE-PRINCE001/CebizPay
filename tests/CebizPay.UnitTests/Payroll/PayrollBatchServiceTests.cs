using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Infrastructure.Payroll;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payroll;

/// <summary>
/// Unit tests for <see cref="PayrollBatchService"/> operations, progress querying, retries, voucher editing, and analytics.
/// </summary>
public sealed class PayrollBatchServiceTests
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
    public async Task CreateAndEnqueueBatch_WhenOrgNotVerified_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Test Org", "test@org.com", "+2348000000000");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        var calcService = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(dbContext, calcService, outbox, NullLogger<PayrollBatchService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => batchService.CreateAndEnqueueBatchAsync(
            org.Id, "usr_1", Currency.NGN, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, new PayrollSelectionCriteria()));
    }

    [Fact]
    public async Task CreateAndEnqueueBatch_WhenInsufficientBalance_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Test Org", "test@org.com", "+2348000000000");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var level = new SalaryLevel(org.Id, "Standard", 500000m, "NGN");
        dbContext.SalaryLevels.Add(level);

        var member = new OrganizationMembership("emp_1", org.Id, MembershipRoleType.Member, null, null, level.Id);
        var initiator = new OrganizationMembership("usr_1", org.Id, MembershipRoleType.PayrollManager);
        dbContext.OrganizationMemberships.AddRange(member, initiator);

        // Org wallet with only 100,000 NGN balance (needed 500,000 NGN)
        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        wallet.Credit(100000m);
        dbContext.Wallets.Add(wallet);

        await dbContext.SaveChangesAsync();

        var calcService = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(dbContext, calcService, outbox, NullLogger<PayrollBatchService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => batchService.CreateAndEnqueueBatchAsync(
            org.Id, "usr_1", Currency.NGN, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, new PayrollSelectionCriteria()));
    }

    [Fact]
    public async Task GetBatchProgress_ComputesPercentagesAndAggregatesCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var batch = PayrollBatch.Create(
            orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "usr_1");

        var item1 = PayrollItem.Create(batch.Id, orgId, "emp_1", "Alice", "alice@example.com", Currency.NGN, 100000m, 0m);
        item1.Claim("worker-1");
        item1.MarkCompleted(Guid.NewGuid(), Guid.NewGuid());

        var item2 = PayrollItem.Create(batch.Id, orgId, "emp_2", "Bob", "bob@example.com", Currency.NGN, 100000m, 0m);
        item2.Claim("worker-1");
        item2.MarkFailed("ERROR", "Something broke");

        var item3 = PayrollItem.Create(batch.Id, orgId, "emp_3", "Charlie", "charlie@example.com", Currency.NGN, 100000m, 0m);

        batch.AddItem(item1);
        batch.AddItem(item2);
        batch.AddItem(item3);

        dbContext.PayrollBatches.Add(batch);
        await dbContext.SaveChangesAsync();

        var calcService = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(dbContext, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        var progress = await batchService.GetBatchProgressAsync(orgId, batch.Id, 1, 10);

        Assert.NotNull(progress);
        Assert.Equal(3, progress.TotalEmployees);
        Assert.Equal(1, progress.CompletedCount);
        Assert.Equal(1, progress.FailedCount);
        Assert.Equal(1, progress.PendingCount);
        Assert.Equal(33.33m, progress.ProgressPercentage);
        Assert.Equal(3, progress.Items.Count);
    }

    [Fact]
    public async Task RetryFailedItems_ReopensFailedItemsAndBatchState()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var batch = PayrollBatch.Create(
            orgId, Currency.NGN, PayrollSelectionMode.All, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "usr_1");

        var item = PayrollItem.Create(batch.Id, orgId, "emp_1", "Alice", "alice@example.com", Currency.NGN, 100000m, 0m);
        item.Claim("worker-1");
        item.MarkFailed("ERR", "Failed");

        batch.AddItem(item);
        batch.MarkPartiallyCompleted();

        dbContext.PayrollBatches.Add(batch);
        await dbContext.SaveChangesAsync();

        var calcService = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());
        var batchService = new PayrollBatchService(dbContext, calcService, Substitute.For<IOutboxService>(), NullLogger<PayrollBatchService>.Instance);

        var retriedCount = await batchService.RetryFailedItemsAsync(orgId, batch.Id, "usr_1");

        Assert.Equal(1, retriedCount);
        Assert.Equal(PayrollItemStatus.RetryPending, item.Status);
        Assert.Equal(PayrollBatchStatus.Processing, batch.Status);
    }
}
