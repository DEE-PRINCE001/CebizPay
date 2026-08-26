#pragma warning disable CS1591
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Erp;

/// <summary>
/// End-to-end integration tests for Phase 5D ERP features (Orders, Expenses, Invoices, Receipts) against PostgreSQL.
/// </summary>
public sealed class ErpOrdersIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public ErpOrdersIntegrationTests(InfrastructureFixture fixture)
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
    public async Task PurchaseOrder_EndToEnd_PersistsAndReceivesCorrectly()
    {
        await using var dbContext = await CreateDbContextAsync();

        var org = new Organization("PO Org", $"po_{Guid.NewGuid():N}@test.com", "+2348011112244");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var supplier = new Supplier(org.Id, "SUP-INT-01", "Global Supplies", "Contact Name", "sales@globalsupplies.com");
        dbContext.Suppliers.Add(supplier);

        var po = new PurchaseOrder(
            org.Id,
            $"PO-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            supplier.Id,
            "usr_buyer",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            Currency.NGN,
            "Integration test PO");

        po.AddItem("Industrial Steel Plates", 50, 10000m);
        po.AddItem("Fasteners Box", 20, 2500m);
        po.Confirm();

        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync();

        var retrievedPo = await dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == po.Id);

        Assert.NotNull(retrievedPo);
        Assert.Equal(PurchaseOrderStatus.Confirmed, retrievedPo.Status);
        Assert.Equal(550000m, retrievedPo.TotalAmount);
        Assert.Equal(2, retrievedPo.Items.Count);

        // Receive first item
        var item1 = retrievedPo.Items.First(i => i.Quantity == 50);
        retrievedPo.ReceiveItemQuantity(item1.Id, 50);
        await dbContext.SaveChangesAsync();

        var partiallyReceivedPo = await dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstAsync(p => p.Id == po.Id);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, partiallyReceivedPo.Status);

        // Receive second item
        var item2 = partiallyReceivedPo.Items.First(i => i.Quantity == 20);
        partiallyReceivedPo.ReceiveItemQuantity(item2.Id, 20);
        await dbContext.SaveChangesAsync();

        var fullyReceivedPo = await dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstAsync(p => p.Id == po.Id);
        Assert.Equal(PurchaseOrderStatus.Received, fullyReceivedPo.Status);
    }

    [Fact]
    public async Task SalesOrder_EndToEnd_PersistsAndFulfillsCorrectly()
    {
        await using var dbContext = await CreateDbContextAsync();

        var org = new Organization("SO Org", $"so_{Guid.NewGuid():N}@test.com", "+2348011112255");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var customer = new Customer(org.Id, "CUST-INT-01", "Acme Retailers", "orders@acmeretail.com");
        dbContext.Customers.Add(customer);

        var so = new SalesOrder(
            org.Id,
            $"SO-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            customer.Id,
            "usr_seller",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(5),
            Currency.NGN,
            "Customer SO Integration test");

        so.AddItem("Finished Gadget Alpha", 15, 12000m);
        so.Confirm();

        dbContext.SalesOrders.Add(so);
        await dbContext.SaveChangesAsync();

        var retrievedSo = await dbContext.SalesOrders
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == so.Id);

        Assert.NotNull(retrievedSo);
        Assert.Equal(SalesOrderStatus.Confirmed, retrievedSo.Status);
        Assert.Equal(180000m, retrievedSo.TotalAmount);

        // Fulfill line
        var line = retrievedSo.Items.First();
        retrievedSo.FulfillItemQuantity(line.Id, 15);
        await dbContext.SaveChangesAsync();

        var fulfilledSo = await dbContext.SalesOrders
            .Include(s => s.Items)
            .FirstAsync(s => s.Id == so.Id);
        Assert.Equal(SalesOrderStatus.Fulfilled, fulfilledSo.Status);
    }

    [Fact]
    public async Task OperatingExpense_EndToEnd_LifecyclePersistsCorrectly()
    {
        await using var dbContext = await CreateDbContextAsync();

        var org = new Organization("EXP Org", $"exp_{Guid.NewGuid():N}@test.com", "+2348011112266");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var expense = new OperatingExpense(
            org.Id,
            $"EXP-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            ExpenseCategory.Maintenance,
            "Generator servicing and diesel fill",
            85000m,
            DateTime.UtcNow,
            "usr_admin",
            ExpensePaymentMethod.Manual);

        dbContext.OperatingExpenses.Add(expense);
        await dbContext.SaveChangesAsync();

        var loadedExpense = await dbContext.OperatingExpenses.FirstAsync(e => e.Id == expense.Id);
        Assert.Equal(ExpenseStatus.Draft, loadedExpense.Status);

        loadedExpense.Approve("usr_manager", DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var approvedExpense = await dbContext.OperatingExpenses.FirstAsync(e => e.Id == expense.Id);
        Assert.Equal(ExpenseStatus.Approved, approvedExpense.Status);

        approvedExpense.MarkPaid(DateTime.UtcNow, reference: "BANK-TRANSFER-REF-99");
        await dbContext.SaveChangesAsync();

        var paidExpense = await dbContext.OperatingExpenses.FirstAsync(e => e.Id == expense.Id);
        Assert.Equal(ExpenseStatus.Paid, paidExpense.Status);
        Assert.Equal("BANK-TRANSFER-REF-99", paidExpense.Reference);
    }

    [Fact]
    public async Task InvoiceAndReceipt_EndToEnd_PersistsAndGeneratesReceipt()
    {
        await using var dbContext = await CreateDbContextAsync();

        var org = new Organization("INV Org", $"inv_{Guid.NewGuid():N}@test.com", "+2348011112277");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var customer = new Customer(org.Id, "CUST-INV-01", "Premier Client", "finance@premier.com");
        dbContext.Customers.Add(customer);

        var invoice = new ErpInvoice(
            org.Id,
            $"INV-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            customer.Id,
            "usr_accountant",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            applyVat: true,
            salesOrderId: null,
            currency: Currency.NGN,
            notes: "Strict net-30 terms");

        invoice.AddItem("Professional Architectural Services", 1, 1000000m); // 1,000,000 + 75,000 VAT = 1,075,000
        invoice.Issue(DateTime.UtcNow);

        dbContext.ErpInvoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var retrievedInvoice = await dbContext.ErpInvoices
            .Include(i => i.Items)
            .FirstAsync(i => i.Id == invoice.Id);

        Assert.Equal(InvoiceStatus.Issued, retrievedInvoice.Status);
        Assert.Equal(1000000m, retrievedInvoice.Subtotal);
        Assert.Equal(75000m, retrievedInvoice.VatAmount);
        Assert.Equal(1075000m, retrievedInvoice.TotalAmount);

        // Record full payment and generate receipt
        var now = DateTime.UtcNow;
        retrievedInvoice.RecordPayment(1075000m, InvoiceSettlementMethod.Wallet, now);

        var receipt = new ErpReceipt(
            org.Id,
            $"REC-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            retrievedInvoice.Id,
            retrievedInvoice.CustomerId,
            retrievedInvoice.TotalAmount,
            now,
            InvoiceSettlementMethod.Wallet,
            "WALLET-SETTLE-REF-01",
            "usr_accountant",
            retrievedInvoice.Currency,
            "Paid in full via wallet");

        dbContext.ErpReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var loadedReceipt = await dbContext.ErpReceipts.FirstOrDefaultAsync(r => r.InvoiceId == retrievedInvoice.Id);
        Assert.NotNull(loadedReceipt);
        Assert.Equal(1075000m, loadedReceipt.Amount);
        Assert.Equal(InvoiceSettlementMethod.Wallet, loadedReceipt.SettlementMethod);
    }
}
