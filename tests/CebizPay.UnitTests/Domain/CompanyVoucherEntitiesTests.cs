#pragma warning disable CS1591
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for Phase 5E CompanyVoucher entity.
/// </summary>
public sealed class CompanyVoucherEntitiesTests
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly string TestUserId = "user-123";

    [Fact]
    public void CompanyVoucher_Create_StartsInDraftStatus()
    {
        var voucher = new CompanyVoucher(
            OrgId,
            "CV-20260826-001",
            "John Doe Logistics",
            "Freight delivery for office equipment",
            75000m,
            TestUserId,
            Currency.NGN,
            CompanyVoucherPaymentMethod.Wallet,
            payeeDetails: "Account: 0123456789 (Access Bank)",
            notes: "Immediate disbursement upon delivery verification");

        Assert.Equal(OrgId, voucher.OrganizationId);
        Assert.Equal("CV-20260826-001", voucher.VoucherNumber);
        Assert.Equal("John Doe Logistics", voucher.PayeeName);
        Assert.Equal("Freight delivery for office equipment", voucher.Purpose);
        Assert.Equal(75000m, voucher.Amount);
        Assert.Equal(Currency.NGN, voucher.Currency);
        Assert.Equal(CompanyVoucherPaymentMethod.Wallet, voucher.PaymentMethod);
        Assert.Equal(CompanyVoucherStatus.Draft, voucher.Status);
        Assert.Equal(TestUserId, voucher.CreatedByUserId);
        Assert.Null(voucher.ApprovedByUserId);
        Assert.Null(voucher.PaidAtUtc);
    }

    [Fact]
    public void CompanyVoucher_ApproveAndPay_TransitionsCorrectly()
    {
        var voucher = new CompanyVoucher(
            OrgId,
            "CV-001",
            "Office Landlord",
            "Annual HQ lease rent",
            1200000m,
            TestUserId,
            Currency.NGN,
            CompanyVoucherPaymentMethod.Wallet);

        var approverId = "manager-1";
        var approveTime = DateTime.UtcNow;
        voucher.Approve(approverId, approveTime);

        Assert.Equal(CompanyVoucherStatus.Approved, voucher.Status);
        Assert.Equal(approverId, voucher.ApprovedByUserId);
        Assert.Equal(approveTime, voucher.ApprovedAtUtc);

        var payTime = DateTime.UtcNow;
        var walletId = Guid.NewGuid();
        var ledgerTxId = Guid.NewGuid();
        voucher.MarkPaid(payTime, walletId, ledgerTxId, "LEDGER-REF-001");

        Assert.Equal(CompanyVoucherStatus.Paid, voucher.Status);
        Assert.Equal(payTime, voucher.PaidAtUtc);
        Assert.Equal(walletId, voucher.WalletId);
        Assert.Equal(ledgerTxId, voucher.LedgerTransactionId);
        Assert.Equal("LEDGER-REF-001", voucher.Reference);
    }

    [Fact]
    public void CompanyVoucher_PayUnapproved_ThrowsInvalidOperationException()
    {
        var voucher = new CompanyVoucher(
            OrgId,
            "CV-002",
            "Supplier Co",
            "Material deposit",
            50000m,
            TestUserId);

        Assert.Throws<InvalidOperationException>(() => voucher.MarkPaid(DateTime.UtcNow));
    }

    [Fact]
    public void CompanyVoucher_Cancel_TransitionsToCancelled()
    {
        var voucher = new CompanyVoucher(
            OrgId,
            "CV-003",
            "Contractor X",
            "Renovation work",
            200000m,
            TestUserId);

        voucher.Cancel(DateTime.UtcNow);
        Assert.Equal(CompanyVoucherStatus.Cancelled, voucher.Status);
    }

    [Fact]
    public void CompanyVoucher_CancelPaid_ThrowsInvalidOperationException()
    {
        var voucher = new CompanyVoucher(
            OrgId,
            "CV-004",
            "Contractor Y",
            "Painting work",
            100000m,
            TestUserId);

        voucher.Approve("manager-1", DateTime.UtcNow);
        voucher.MarkPaid(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => voucher.Cancel(DateTime.UtcNow));
    }

    [Fact]
    public void CompanyVoucher_ZeroOrNegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompanyVoucher(
            OrgId,
            "CV-005",
            "Payee",
            "Purpose",
            0m,
            TestUserId));

        Assert.Throws<ArgumentOutOfRangeException>(() => new CompanyVoucher(
            OrgId,
            "CV-006",
            "Payee",
            "Purpose",
            -500m,
            TestUserId));
    }
}
