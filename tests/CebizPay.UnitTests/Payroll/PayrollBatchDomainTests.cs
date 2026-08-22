using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using Xunit;

namespace CebizPay.UnitTests.Payroll;

/// <summary>
/// Domain unit tests for <see cref="PayrollBatch"/> lifecycle, state machine transitions, and financial aggregates.
/// </summary>
public sealed class PayrollBatchDomainTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesInPendingStatus()
    {
        var orgId = Guid.NewGuid();
        var periodStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc);

        var batch = PayrollBatch.Create(
            organizationId: orgId,
            currency: Currency.NGN,
            selectionMode: PayrollSelectionMode.All,
            periodStart: periodStart,
            periodEnd: periodEnd,
            createdByUserId: "usr_ceo_1");

        Assert.NotEqual(Guid.Empty, batch.Id);
        Assert.StartsWith("PB-202608-", batch.BatchReference);
        Assert.Equal(orgId, batch.OrganizationId);
        Assert.Equal(Currency.NGN, batch.Currency);
        Assert.Equal(PayrollSelectionMode.All, batch.SelectionMode);
        Assert.Equal(PayrollBatchStatus.Pending, batch.Status);
        Assert.Equal(0, batch.TotalEmployees);
        Assert.Equal(0m, batch.TotalGrossAmount);
        Assert.Equal(0m, batch.TotalNetAmount);
    }

    [Fact]
    public void AddItem_IncrementsCountersAndAggregates()
    {
        var orgId = Guid.NewGuid();
        var batch = PayrollBatch.Create(
            orgId,
            Currency.NGN,
            PayrollSelectionMode.All,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            "usr_ceo_1");

        var item1 = PayrollItem.Create(batch.Id, orgId, "emp_1", "Alice", "alice@example.com", Currency.NGN, grossPay: 100000m, totalDeductions: 10000m);
        var item2 = PayrollItem.Create(batch.Id, orgId, "emp_2", "Bob", "bob@example.com", Currency.NGN, grossPay: 150000m, totalDeductions: 0m);

        batch.AddItem(item1);
        batch.AddItem(item2);

        Assert.Equal(2, batch.TotalEmployees);
        Assert.Equal(250000m, batch.TotalGrossAmount);
        Assert.Equal(10000m, batch.TotalDeductionsAmount);
        Assert.Equal(240000m, batch.TotalNetAmount);
    }

    [Fact]
    public void StateTransitions_FollowControlledLifecycle()
    {
        var batch = PayrollBatch.Create(
            Guid.NewGuid(),
            Currency.NGN,
            PayrollSelectionMode.All,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            "usr_ceo_1");

        Assert.Equal(PayrollBatchStatus.Pending, batch.Status);

        batch.MarkProcessing();
        Assert.Equal(PayrollBatchStatus.Processing, batch.Status);
        Assert.NotNull(batch.StartedAtUtc);

        batch.MarkCompleted();
        Assert.Equal(PayrollBatchStatus.Completed, batch.Status);
        Assert.NotNull(batch.CompletedAtUtc);
    }

    [Fact]
    public void Cancel_WhenPending_Succeeds()
    {
        var batch = PayrollBatch.Create(
            Guid.NewGuid(),
            Currency.NGN,
            PayrollSelectionMode.All,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            "usr_ceo_1");

        batch.Cancel();
        Assert.Equal(PayrollBatchStatus.Cancelled, batch.Status);
    }

    [Fact]
    public void Cancel_WhenProcessing_ThrowsInvalidOperationException()
    {
        var batch = PayrollBatch.Create(
            Guid.NewGuid(),
            Currency.NGN,
            PayrollSelectionMode.All,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            "usr_ceo_1");

        batch.MarkProcessing();

        Assert.Throws<InvalidOperationException>(() => batch.Cancel());
    }
}
