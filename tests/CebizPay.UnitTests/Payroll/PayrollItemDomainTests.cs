using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using Xunit;

namespace CebizPay.UnitTests.Payroll;

/// <summary>
/// Domain unit tests for <see cref="PayrollItem"/> creation, calculations, and worker execution attempt lifecycle.
/// </summary>
public sealed class PayrollItemDomainTests
{
    [Fact]
    public void Create_WithValidParameters_CalculatesNetPayAndInitializesPending()
    {
        var batchId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var item = PayrollItem.Create(
            payrollBatchId: batchId,
            organizationId: orgId,
            employeeUserId: "emp_1",
            employeeName: "Alice Smith",
            employeeEmail: "alice@cebizpay.internal",
            currency: Currency.NGN,
            grossPay: 350000m,
            totalDeductions: 50000m);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(batchId, item.PayrollBatchId);
        Assert.Equal(orgId, item.OrganizationId);
        Assert.Equal("emp_1", item.EmployeeUserId);
        Assert.Equal("Alice Smith", item.EmployeeName);
        Assert.Equal(350000m, item.GrossPay);
        Assert.Equal(50000m, item.TotalDeductions);
        Assert.Equal(300000m, item.NetPay);
        Assert.Equal(PayrollItemStatus.Pending, item.Status);
        Assert.Equal(0, item.CurrentAttemptNumber);
    }

    [Fact]
    public void Create_WithReportingCurrency_ThrowsArgumentException()
    {
        var batchId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => PayrollItem.Create(
            batchId, orgId, "emp_1", "Alice", "alice@example.com", Currency.USD, 1000m, 0m));
    }

    [Fact]
    public void Create_WithDeductionsExceedingGross_ThrowsInvalidOperationException()
    {
        var batchId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => PayrollItem.Create(
            batchId, orgId, "emp_1", "Alice", "alice@example.com", Currency.NGN, 50000m, 60000m));
    }

    [Fact]
    public void Claim_IncrementsAttemptNumberAndSetsProcessing()
    {
        var item = PayrollItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "emp_1", "Alice", "alice@example.com", Currency.NGN, 100000m, 0m);

        item.Claim("Worker-1");

        Assert.Equal(PayrollItemStatus.Processing, item.Status);
        Assert.Equal("Worker-1", item.ClaimedByWorkerId);
        Assert.NotNull(item.ClaimedAtUtc);
        Assert.Equal(1, item.CurrentAttemptNumber);
        Assert.Single(item.Attempts);
        Assert.Equal(ExecutionAttemptStatus.Started, item.Attempts.First().Status);
    }

    [Fact]
    public void MarkCompleted_SetsCompletedStatusAndLinksLedgerAndVoucher()
    {
        var item = PayrollItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "emp_1", "Alice", "alice@example.com", Currency.NGN, 100000m, 0m);
        item.Claim("Worker-1");

        var ledgerTxnId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        item.MarkCompleted(ledgerTxnId, voucherId);

        Assert.Equal(PayrollItemStatus.Completed, item.Status);
        Assert.Equal(ledgerTxnId, item.LedgerTransactionId);
        Assert.Equal(voucherId, item.PaymentVoucherId);
        Assert.Null(item.ClaimedByWorkerId);
        Assert.Equal(ExecutionAttemptStatus.Completed, item.Attempts.First().Status);
    }

    [Fact]
    public void MarkFailed_AndQueueForRetry_ManagesAttemptTransitions()
    {
        var item = PayrollItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "emp_1", "Alice", "alice@example.com", Currency.NGN, 100000m, 0m);
        item.Claim("Worker-1");

        item.MarkFailed("INSUFFICIENT_FUNDS", "Org balance depleted");

        Assert.Equal(PayrollItemStatus.Failed, item.Status);
        Assert.Equal("INSUFFICIENT_FUNDS", item.LastFailureCode);
        Assert.Equal("Org balance depleted", item.LastFailureReason);
        Assert.Equal(ExecutionAttemptStatus.Failed, item.Attempts.First().Status);

        item.QueueForRetry();
        Assert.Equal(PayrollItemStatus.RetryPending, item.Status);
    }
}
