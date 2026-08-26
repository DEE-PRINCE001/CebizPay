#pragma warning disable CS1591
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

/// <summary>
/// Application use cases unit tests for Phase 5D ERP features (Orders, Expenses, Invoices, Receipts).
/// </summary>
public sealed class ErpOrdersUseCasesTests
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
    public async Task CreatePurchaseOrder_ValidRequest_CreatesPOAndOutboxEvent()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var supplier = new Supplier(org.Id, "SUP-001", "Acme Supplier", "Contact", "acme@test.com");
        db.Organizations.Add(org);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-admin");

        var handler = new CreatePurchaseOrderCommandHandler(db, orgContext, userContext, outbox);
        var command = new CreatePurchaseOrderCommand(
            org.Id,
            supplier.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            Currency.NGN,
            "Urgent restock",
            new List<PurchaseOrderItemRequest>
            {
                new("Raw Paper", 100, 250m)
            });

        var poId = await handler.Handle(command, CancellationToken.None);

        var po = await db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == poId);
        Assert.NotNull(po);
        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
        Assert.Equal(25000m, po.TotalAmount);
        Assert.Single(po.Items);
    }

    [Fact]
    public async Task ReceivePurchaseOrderItem_WithFifoPolicy_CreatesCostLayerAndStockMovement()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var supplier = new Supplier(org.Id, "SUP-001", "Acme Supplier", "Contact", "acme@test.com");
        var item = new InventoryItem(org.Id, "SKU-PAPER", "A4 Paper", "ream", 3500m);
        var fifoPolicy = InventoryValuationPolicy.CreateNextVersion(org.Id, ValuationMethod.Fifo, 1, "admin", DateTime.UtcNow);

        db.Organizations.Add(org);
        db.Suppliers.Add(supplier);
        db.InventoryItems.Add(item);
        db.InventoryValuationPolicies.Add(fifoPolicy);

        var po = new PurchaseOrder(org.Id, "PO-001", supplier.Id, "user-admin", DateTime.UtcNow, null, Currency.NGN);
        po.AddItem("A4 Paper Reams", 50, 2000m, item.Id);
        po.Confirm();
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-admin");

        var lineId = po.Items.First().Id;
        var handler = new ReceivePurchaseOrderItemCommandHandler(db, orgContext, userContext, outbox);
        await handler.Handle(new ReceivePurchaseOrderItemCommand(org.Id, po.Id, lineId, 50), CancellationToken.None);

        var updatedPo = await db.PurchaseOrders.FirstAsync(p => p.Id == po.Id);
        Assert.Equal(PurchaseOrderStatus.Received, updatedPo.Status);

        var updatedItem = await db.InventoryItems.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(50, updatedItem.CurrentQuantity);

        var costLayer = await db.InventoryCostLayers.FirstOrDefaultAsync(l => l.InventoryItemId == item.Id);
        Assert.NotNull(costLayer);
        Assert.Equal(50, costLayer.RemainingQuantity);
        Assert.Equal(2000m, costLayer.UnitCost);

        var movement = await db.StockMovements.FirstOrDefaultAsync(m => m.InventoryItemId == item.Id);
        Assert.NotNull(movement);
        Assert.Equal(StockMovementType.StockIn, movement.MovementType);
        Assert.Equal(50, movement.Quantity);
        Assert.Equal(2000m, movement.UnitCost);
    }

    [Fact]
    public async Task FulfillSalesOrderItem_WithFifoPolicy_ConsumesCostLayers()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var customer = new Customer(org.Id, "CUST-001", "Global Corp", "corp@test.com");
        var item = new InventoryItem(org.Id, "SKU-WIDGET", "Widget A", "pcs", 5000m);
        item.ApplyStockIn(20, 1000m); // Initial stock 20
        var fifoPolicy = InventoryValuationPolicy.CreateNextVersion(org.Id, ValuationMethod.Fifo, 1, "admin", DateTime.UtcNow);

        // Pre-create a cost layer of 20 units @ 1000 NGN
        var layer = new InventoryCostLayer(org.Id, item.Id, Guid.NewGuid(), 20, 1000m, DateTime.UtcNow);

        db.Organizations.Add(org);
        db.Customers.Add(customer);
        db.InventoryItems.Add(item);
        db.InventoryValuationPolicies.Add(fifoPolicy);
        db.InventoryCostLayers.Add(layer);

        var so = new SalesOrder(org.Id, "SO-001", customer.Id, "user-admin", DateTime.UtcNow, null, Currency.NGN);
        so.AddItem("Widget A", 10, 5000m, item.Id);
        so.Confirm();
        db.SalesOrders.Add(so);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-admin");

        var lineId = so.Items.First().Id;
        var handler = new FulfillSalesOrderItemCommandHandler(db, orgContext, userContext, outbox);
        await handler.Handle(new FulfillSalesOrderItemCommand(org.Id, so.Id, lineId, 10), CancellationToken.None);

        var updatedSo = await db.SalesOrders.FirstAsync(s => s.Id == so.Id);
        Assert.Equal(SalesOrderStatus.Fulfilled, updatedSo.Status);

        var updatedItem = await db.InventoryItems.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(10, updatedItem.CurrentQuantity);

        var updatedLayer = await db.InventoryCostLayers.FirstAsync(l => l.Id == layer.Id);
        Assert.Equal(10, updatedLayer.RemainingQuantity);
    }

    [Fact]
    public async Task PayOperatingExpense_WalletSettlement_DebitsOrgWalletAndMarksPaid()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(100000m); // 100,000 NGN balance

        var ledgerAccount = LedgerAccount.CreateWalletAccount(orgWallet.Id, "Org Wallet", Currency.NGN);
        var sysExpenseAccount = LedgerAccount.CreateSystemAccount("Expense Settlement", Currency.NGN, LedgerAccountType.SystemSettlement);

        var expense = new OperatingExpense(
            org.Id,
            "EXP-001",
            ExpenseCategory.Utilities,
            "Power bill",
            25000m,
            DateTime.UtcNow,
            "user-1",
            ExpensePaymentMethod.Wallet);
        expense.Approve("manager-1", DateTime.UtcNow);

        db.Organizations.Add(org);
        db.Wallets.Add(orgWallet);
        db.LedgerAccounts.AddRange(ledgerAccount, sysExpenseAccount);
        db.OperatingExpenses.Add(expense);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-1");
        pinService.VerifyPinAsync("user-1", "1234", Arg.Any<CancellationToken>()).Returns((true, false, null));
        ledgerService.GetOrCreateSystemSettlementAccountAsync(Currency.NGN, Arg.Any<CancellationToken>()).Returns(sysExpenseAccount);
        ledgerService.PostSingleCurrencyTransactionAsync(
            ledgerAccount.Id,
            sysExpenseAccount.Id,
            25000m,
            Currency.NGN,
            LedgerTransactionType.ErpExpense,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new LedgerTransaction(LedgerTransactionType.ErpExpense, "EXP-001", null, "Expense"));

        var handler = new PayOperatingExpenseCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);
        await handler.Handle(new PayOperatingExpenseCommand(org.Id, expense.Id, ExpensePaymentMethod.Wallet, "1234"), CancellationToken.None);

        var paidExpense = await db.OperatingExpenses.FirstAsync(e => e.Id == expense.Id);
        Assert.Equal(ExpenseStatus.Paid, paidExpense.Status);
        Assert.NotNull(paidExpense.PaidAtUtc);
        Assert.NotNull(paidExpense.WalletId);
    }

    [Fact]
    public async Task CreateInvoice_WithVat_Computes7Point5PercentCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var customer = new Customer(org.Id, "CUST-001", "Client X", "client@test.com");
        db.Organizations.Add(org);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-admin");

        var handler = new CreateInvoiceCommandHandler(db, orgContext, userContext, outbox);
        var command = new CreateInvoiceCommand(
            org.Id,
            customer.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            ApplyVat: true,
            SalesOrderId: null,
            Currency: Currency.NGN,
            Notes: "Payment terms 30 days",
            BillingContact: "accounts@client.com",
            Items: new List<InvoiceItemRequest>
            {
                new("Software License", 1, 200000m)
            });

        var invoiceId = await handler.Handle(command, CancellationToken.None);

        var invoice = await db.ErpInvoices.Include(i => i.Items).FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(200000m, invoice.Subtotal);
        Assert.Equal(15000m, invoice.VatAmount); // 7.5% of 200,000
        Assert.Equal(215000m, invoice.TotalAmount);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }

    [Fact]
    public async Task RecordInvoicePayment_FullPayment_GeneratesReceiptAtomically()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var customer = new Customer(org.Id, "CUST-001", "Client X", "client@test.com");

        var invoice = new ErpInvoice(
            org.Id,
            "INV-001",
            customer.Id,
            "admin-1",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(14),
            applyVat: false,
            salesOrderId: null,
            currency: Currency.NGN);
        invoice.AddItem("Service Package", 1, 50000m);
        invoice.Issue(DateTime.UtcNow);

        db.Organizations.Add(org);
        db.Customers.Add(customer);
        db.ErpInvoices.Add(invoice);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("admin-1");

        var handler = new RecordInvoicePaymentCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);
        var receiptId = await handler.Handle(new RecordInvoicePaymentCommand(
            org.Id,
            invoice.Id,
            50000m,
            InvoiceSettlementMethod.Manual,
            "MANUAL-BANK-REF-12345"), CancellationToken.None);

        Assert.NotNull(receiptId);

        var updatedInvoice = await db.ErpInvoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, updatedInvoice.Status);
        Assert.Equal(50000m, updatedInvoice.PaidAmount);

        var receipt = await db.ErpReceipts.FirstOrDefaultAsync(r => r.Id == receiptId.Value);
        Assert.NotNull(receipt);
        Assert.Equal(invoice.Id, receipt.InvoiceId);
        Assert.Equal(50000m, receipt.Amount);
        Assert.Equal("MANUAL-BANK-REF-12345", receipt.Reference);
    }

    [Fact]
    public async Task ErpOperations_WhenOrgIsSuspended_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        org.TransitionStatus(OrganizationStatus.Verified);
        org.TransitionStatus(OrganizationStatus.Suspended);
        var supplier = new Supplier(org.Id, "SUP-001", "Acme", "Contact", "acme@test.com");

        db.Organizations.Add(org);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("admin-1");

        var handler = new CreatePurchaseOrderCommandHandler(db, orgContext, userContext, outbox);
        var command = new CreatePurchaseOrderCommand(
            org.Id,
            supplier.Id,
            DateTime.UtcNow,
            null,
            Currency.NGN,
            null,
            new List<PurchaseOrderItemRequest> { new("Item", 1, 100m) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }
}
