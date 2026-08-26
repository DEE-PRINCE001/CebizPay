#pragma warning disable CS1591
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for Phase 5D ERP entities (Purchase Orders, Sales Orders, Operating Expenses, Invoices, Receipts).
/// </summary>
public sealed class ErpOrdersEntitiesTests
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly string TestUserId = "user-123";

    // ==========================================
    // Purchase Order Tests
    // ==========================================

    [Fact]
    public void PurchaseOrder_CreateAndAddItems_CalculatesTotalCorrectly()
    {
        var po = new PurchaseOrder(
            OrgId,
            "PO-20260824-001",
            SupplierId,
            TestUserId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            Currency.NGN,
            "Urgent inventory restock");

        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
        Assert.Equal(0m, po.TotalAmount);

        po.AddItem("Raw Material A", 100, 500m);
        po.AddItem("Raw Material B", 50, 1000m);

        Assert.Equal(2, po.Items.Count);
        Assert.Equal(100000m, po.TotalAmount);
        Assert.Equal(100000m, po.Subtotal);
    }

    [Fact]
    public void PurchaseOrder_Confirm_TransitionsStatusToConfirmed()
    {
        var po = new PurchaseOrder(OrgId, "PO-001", SupplierId, TestUserId, DateTime.UtcNow, null, Currency.NGN);
        po.AddItem("Item A", 10, 100m);

        po.Confirm();
        Assert.Equal(PurchaseOrderStatus.Confirmed, po.Status);
    }

    [Fact]
    public void PurchaseOrder_ReceiveItemQuantity_UpdatesToPartiallyReceivedAndReceived()
    {
        var po = new PurchaseOrder(OrgId, "PO-001", SupplierId, TestUserId, DateTime.UtcNow, null, Currency.NGN);
        po.AddItem("Item A", 10, 100m);
        po.Confirm();

        var itemId = po.Items.First().Id;

        // Partial receive
        po.ReceiveItemQuantity(itemId, 4);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, po.Status);
        Assert.Equal(4, po.Items.First().ReceivedQuantity);

        // Complete receive
        po.ReceiveItemQuantity(itemId, 6);
        Assert.Equal(PurchaseOrderStatus.Received, po.Status);
        Assert.Equal(10, po.Items.First().ReceivedQuantity);
    }

    [Fact]
    public void PurchaseOrder_Cancel_TransitionsToCancelled()
    {
        var po = new PurchaseOrder(OrgId, "PO-001", SupplierId, TestUserId, DateTime.UtcNow, null, Currency.NGN);
        po.AddItem("Item A", 10, 100m);
        po.Cancel();

        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);
    }

    [Fact]
    public void PurchaseOrder_ConfirmNonDraft_ThrowsInvalidOperationException()
    {
        var po = new PurchaseOrder(OrgId, "PO-001", SupplierId, TestUserId, DateTime.UtcNow, null, Currency.NGN);
        po.AddItem("Item A", 10, 100m);
        po.Confirm();

        Assert.Throws<InvalidOperationException>(() => po.Confirm());
    }

    // ==========================================
    // Sales Order Tests
    // ==========================================

    [Fact]
    public void SalesOrder_CreateAndAddItems_CalculatesTotalCorrectly()
    {
        var so = new SalesOrder(
            OrgId,
            "SO-20260824-001",
            CustomerId,
            TestUserId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(3),
            Currency.NGN,
            "Customer bulk order");

        Assert.Equal(SalesOrderStatus.Draft, so.Status);
        Assert.Equal(0m, so.TotalAmount);

        so.AddItem("Product Widget X", 20, 2500m);
        so.AddItem("Product Widget Y", 10, 5000m);

        Assert.Equal(2, so.Items.Count);
        Assert.Equal(100000m, so.TotalAmount);
    }

    [Fact]
    public void SalesOrder_ConfirmAndFulfill_UpdatesStatusTransitions()
    {
        var so = new SalesOrder(OrgId, "SO-001", CustomerId, TestUserId, DateTime.UtcNow, null, Currency.NGN);
        so.AddItem("Product A", 10, 200m);

        so.Confirm();
        Assert.Equal(SalesOrderStatus.Confirmed, so.Status);

        var itemId = so.Items.First().Id;

        // Partial fulfillment
        so.FulfillItemQuantity(itemId, 3);
        Assert.Equal(SalesOrderStatus.PartiallyFulfilled, so.Status);
        Assert.Equal(3, so.Items.First().FulfilledQuantity);

        // Full fulfillment
        so.FulfillItemQuantity(itemId, 7);
        Assert.Equal(SalesOrderStatus.Fulfilled, so.Status);
        Assert.Equal(10, so.Items.First().FulfilledQuantity);
    }

    // ==========================================
    // Operating Expense Tests
    // ==========================================

    [Fact]
    public void OperatingExpense_Create_StartsInDraftStatus()
    {
        var expense = new OperatingExpense(
            OrgId,
            "EXP-20260824-001",
            ExpenseCategory.Utilities,
            "Electricity bill for HQ",
            45000m,
            DateTime.UtcNow,
            TestUserId,
            ExpensePaymentMethod.Manual);

        Assert.Equal(ExpenseStatus.Draft, expense.Status);
        Assert.Equal(45000m, expense.Amount);
        Assert.Equal(ExpenseCategory.Utilities, expense.Category);
        Assert.Null(expense.ApprovedByUserId);
        Assert.Null(expense.PaidAtUtc);
    }

    [Fact]
    public void OperatingExpense_ApproveAndPay_TransitionsCorrectly()
    {
        var expense = new OperatingExpense(
            OrgId,
            "EXP-001",
            ExpenseCategory.Rent,
            "Office Rent",
            500000m,
            DateTime.UtcNow,
            TestUserId,
            ExpensePaymentMethod.Wallet);

        var approverId = "manager-1";
        var approveTime = DateTime.UtcNow;
        expense.Approve(approverId, approveTime);

        Assert.Equal(ExpenseStatus.Approved, expense.Status);
        Assert.Equal(approverId, expense.ApprovedByUserId);
        Assert.Equal(approveTime, expense.ApprovedAtUtc);

        var payTime = DateTime.UtcNow;
        var walletId = Guid.NewGuid();
        var ledgerTxId = Guid.NewGuid();
        expense.MarkPaid(payTime, walletId, ledgerTxId, "EXT-PAY-99");

        Assert.Equal(ExpenseStatus.Paid, expense.Status);
        Assert.Equal(payTime, expense.PaidAtUtc);
        Assert.Equal(walletId, expense.WalletId);
        Assert.Equal(ledgerTxId, expense.LedgerTransactionId);
        Assert.Equal("EXT-PAY-99", expense.Reference);
    }

    [Fact]
    public void OperatingExpense_PayUnapproved_ThrowsInvalidOperationException()
    {
        var expense = new OperatingExpense(
            OrgId,
            "EXP-001",
            ExpenseCategory.Supplies,
            "Stationery",
            12000m,
            DateTime.UtcNow,
            TestUserId,
            ExpensePaymentMethod.Manual);

        Assert.Throws<InvalidOperationException>(() => expense.MarkPaid(DateTime.UtcNow));
    }

    // ==========================================
    // ERP Invoice Tests
    // ==========================================

    [Fact]
    public void ErpInvoice_WithVat_Calculates7Point5PercentVatCorrectly()
    {
        var invoice = new ErpInvoice(
            OrgId,
            "INV-20260824-001",
            CustomerId,
            TestUserId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(14),
            applyVat: true,
            salesOrderId: null,
            currency: Currency.NGN,
            notes: "Please pay via bank or wallet");

        invoice.AddItem("Consulting Services", 10, 10000m); // 100,000 NGN

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(100000m, invoice.Subtotal);
        Assert.Equal(0.075m, invoice.VatRate);
        Assert.Equal(7500m, invoice.VatAmount);
        Assert.Equal(107500m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.PaidAmount);
    }

    [Fact]
    public void ErpInvoice_WithoutVat_HasZeroVat()
    {
        var invoice = new ErpInvoice(
            OrgId,
            "INV-002",
            CustomerId,
            TestUserId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(14),
            applyVat: false,
            salesOrderId: null,
            currency: Currency.NGN);

        invoice.AddItem("Exempt Item", 5, 20000m); // 100,000 NGN

        Assert.Equal(100000m, invoice.Subtotal);
        Assert.Equal(0m, invoice.VatRate);
        Assert.Equal(0m, invoice.VatAmount);
        Assert.Equal(100000m, invoice.TotalAmount);
    }

    [Fact]
    public void ErpInvoice_RecordPayment_TransitionsThroughPartiallyPaidToPaid()
    {
        var invoice = new ErpInvoice(
            OrgId,
            "INV-003",
            CustomerId,
            TestUserId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(14),
            applyVat: false,
            salesOrderId: null,
            currency: Currency.NGN);

        invoice.AddItem("Item 1", 1, 10000m);
        invoice.Issue(DateTime.UtcNow);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);

        // Partial payment 4000
        invoice.RecordPayment(4000m, InvoiceSettlementMethod.Manual, DateTime.UtcNow);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(4000m, invoice.PaidAmount);

        // Remaining payment 6000
        invoice.RecordPayment(6000m, InvoiceSettlementMethod.Wallet, DateTime.UtcNow);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(10000m, invoice.PaidAmount);
    }

    [Fact]
    public void ErpInvoice_Overpaying_ThrowsInvalidOperationException()
    {
        var invoice = new ErpInvoice(
            OrgId,
            "INV-004",
            CustomerId,
            TestUserId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(14),
            applyVat: false,
            salesOrderId: null,
            currency: Currency.NGN);

        invoice.AddItem("Item", 1, 1000m);
        invoice.Issue(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(1500m, InvoiceSettlementMethod.Manual, DateTime.UtcNow));
    }

    // ==========================================
    // ERP Receipt Tests
    // ==========================================

    [Fact]
    public void ErpReceipt_Create_SetsImmutableProperties()
    {
        var invoiceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var receipt = new ErpReceipt(
            OrgId,
            "REC-20260824-001",
            invoiceId,
            CustomerId,
            107500m,
            now,
            InvoiceSettlementMethod.Wallet,
            "TX-WALLET-12345",
            TestUserId,
            Currency.NGN,
            "Payment received in full");

        Assert.Equal(OrgId, receipt.OrganizationId);
        Assert.Equal("REC-20260824-001", receipt.ReceiptNumber);
        Assert.Equal(invoiceId, receipt.InvoiceId);
        Assert.Equal(CustomerId, receipt.CustomerId);
        Assert.Equal(107500m, receipt.Amount);
        Assert.Equal(Currency.NGN, receipt.Currency);
        Assert.Equal(InvoiceSettlementMethod.Wallet, receipt.SettlementMethod);
        Assert.Equal("TX-WALLET-12345", receipt.Reference);
        Assert.Equal(TestUserId, receipt.CreatedByUserId);
        Assert.Equal(now, receipt.PaymentDate);
    }
}
