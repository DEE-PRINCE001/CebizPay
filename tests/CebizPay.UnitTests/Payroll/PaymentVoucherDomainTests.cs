using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using Xunit;

namespace CebizPay.UnitTests.Payroll;

/// <summary>
/// Domain unit tests for <see cref="PaymentVoucher"/> issuance and non-financial metadata editing.
/// </summary>
public sealed class PaymentVoucherDomainTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesGeneratedStatus()
    {
        var batchId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var ledgerTxnId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var voucher = PaymentVoucher.Create(
            payrollBatchId: batchId,
            payrollItemId: itemId,
            ledgerTransactionId: ledgerTxnId,
            organizationId: orgId,
            employeeUserId: "emp_1",
            employeeName: "Alice Smith",
            grossPay: 500000m,
            deductions: 50000m,
            netPay: 450000m,
            currency: Currency.NGN,
            bankName: "First Bank",
            remarks: "August 2026 Salary");

        Assert.NotEqual(Guid.Empty, voucher.Id);
        Assert.StartsWith($"PV-{DateTime.UtcNow:yyyyMM}-", voucher.VoucherReference);
        Assert.Equal(batchId, voucher.PayrollBatchId);
        Assert.Equal(itemId, voucher.PayrollItemId);
        Assert.Equal(ledgerTxnId, voucher.LedgerTransactionId);
        Assert.Equal(orgId, voucher.OrganizationId);
        Assert.Equal("emp_1", voucher.EmployeeUserId);
        Assert.Equal("Alice Smith", voucher.EmployeeName);
        Assert.Equal(500000m, voucher.GrossPay);
        Assert.Equal(50000m, voucher.Deductions);
        Assert.Equal(450000m, voucher.NetPay);
        Assert.Equal(Currency.NGN, voucher.Currency);
        Assert.Equal(VoucherStatus.Generated, voucher.Status);
        Assert.Equal("First Bank", voucher.BankName);
        Assert.Equal("August 2026 Salary", voucher.Remarks);
    }

    [Fact]
    public void UpdateMetadata_UpdatesNonFinancialFieldsWithoutMutatingFinancialNumbers()
    {
        var voucher = PaymentVoucher.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "emp_1", "Alice", 500000m, 50000m, 450000m, Currency.NGN);

        voucher.UpdateMetadata("GTBank", "Updated remarks", "Updated description");

        Assert.Equal("GTBank", voucher.BankName);
        Assert.Equal("Updated remarks", voucher.Remarks);
        Assert.Equal("Updated description", voucher.Description);
        Assert.NotNull(voucher.UpdatedAtUtc);

        // Financial immutability checks
        Assert.Equal(500000m, voucher.GrossPay);
        Assert.Equal(50000m, voucher.Deductions);
        Assert.Equal(450000m, voucher.NetPay);
        Assert.Equal(Currency.NGN, voucher.Currency);
    }
}
