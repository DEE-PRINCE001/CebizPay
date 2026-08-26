#pragma warning disable CS1591, CA1826
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

/// <summary>
/// Application use cases unit tests for Phase 5E ERP Financial and Operational Reports.
/// </summary>
public sealed class ErpReportsUseCasesTests
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
    public async Task GetSalesReport_AggregatesOrdersAndCalculatesMetricsCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("SalesOrg", "info@sales.com", "+2348000000001");
        var customer1 = new Customer(org.Id, "CUST-01", "Acme Ltd");
        var customer2 = new Customer(org.Id, "CUST-02", "Global Corp");

        db.Organizations.Add(org);
        db.Customers.AddRange(customer1, customer2);

        var so1 = new SalesOrder(org.Id, "SO-001", customer1.Id, "user-1", DateTime.UtcNow, null, Currency.NGN);
        so1.AddItem("Widget A", 10, 5000m); // 50,000 NGN
        so1.Confirm();
        so1.FulfillItemQuantity(so1.Items.First().Id, 10);

        var so2 = new SalesOrder(org.Id, "SO-002", customer2.Id, "user-1", DateTime.UtcNow, null, Currency.NGN);
        so2.AddItem("Widget B", 5, 10000m); // 50,000 NGN
        so2.Confirm();

        var so3 = new SalesOrder(org.Id, "SO-003", customer1.Id, "user-1", DateTime.UtcNow, null, Currency.NGN);
        so3.AddItem("Widget C", 2, 25000m); // 50,000 NGN
        so3.Cancel();

        db.SalesOrders.AddRange(so1, so2, so3);
        db.SalesOrderItems.AddRange(so1.Items.Concat(so2.Items).Concat(so3.Items));
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetSalesReportQueryHandler(db, orgContext);
        var report = await handler.Handle(new GetSalesReportQuery(org.Id), CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(3, report.TotalOrdersCount);
        Assert.Equal(1, report.FulfilledOrdersCount);
        Assert.Equal(1, report.ConfirmedOrdersCount);
        Assert.Equal(1, report.CancelledOrdersCount);

        var ngnSummary = report.CurrencySummaries.FirstOrDefault(c => c.Currency == Currency.NGN);
        Assert.NotNull(ngnSummary);
        Assert.Equal(100000m, ngnSummary.TotalGrossSales); // Excluding cancelled (50k + 50k)
        Assert.Equal(2, ngnSummary.OrderCount);
        Assert.Equal(2, report.TopCustomers.Count);
    }

    [Fact]
    public async Task GetPurchaseReport_AggregatesPurchasesAndSuppliersCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("PurchaseOrg", "info@purchase.com", "+2348000000002");
        var supplier = new Supplier(org.Id, "SUP-01", "Raw Materials Inc");

        db.Organizations.Add(org);
        db.Suppliers.Add(supplier);

        var po1 = new PurchaseOrder(org.Id, "PO-001", supplier.Id, "user-1", DateTime.UtcNow, null, Currency.NGN);
        po1.AddItem("Steel Plate", 20, 15000m); // 300,000 NGN
        po1.Confirm();
        po1.ReceiveItemQuantity(po1.Items.First().Id, 20);

        db.PurchaseOrders.Add(po1);
        db.PurchaseOrderItems.AddRange(po1.Items);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetPurchaseReportQueryHandler(db, orgContext);
        var report = await handler.Handle(new GetPurchaseReportQuery(org.Id), CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(1, report.TotalOrdersCount);
        Assert.Equal(1, report.ReceivedOrdersCount);
        Assert.Single(report.CurrencySummaries);
        Assert.Equal(300000m, report.CurrencySummaries[0].TotalPurchasesAmount);
        Assert.Single(report.TopSuppliers);
        Assert.Equal("Raw Materials Inc", report.TopSuppliers[0].SupplierName);
    }

    [Fact]
    public async Task GetSettlementReport_AggregatesAllSettlementChannels()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("SettlementOrg", "info@settle.com", "+2348000000003");
        var customer = new Customer(org.Id, "CUST-01", "Client A");

        // 1. Paid Invoice
        var invoice = new ErpInvoice(org.Id, "INV-001", customer.Id, "admin", DateTime.UtcNow, DateTime.UtcNow.AddDays(7), false);
        invoice.AddItem("Service", 1, 80000m);
        invoice.Issue(DateTime.UtcNow);
        invoice.RecordPayment(80000m, InvoiceSettlementMethod.Wallet, DateTime.UtcNow);

        // 2. Paid Operating Expense
        var expense = new OperatingExpense(org.Id, "EXP-001", ExpenseCategory.Utilities, "Electricity", 30000m, DateTime.UtcNow, "admin", ExpensePaymentMethod.Manual);
        expense.Approve("manager", DateTime.UtcNow);
        expense.MarkPaid(DateTime.UtcNow, reference: "MANUAL-REF-99");

        // 3. Paid Company Voucher
        var voucher = new CompanyVoucher(org.Id, "CV-001", "Vendor X", "Equipment Repair", 45000m, "admin", Currency.NGN, CompanyVoucherPaymentMethod.Wallet);
        voucher.Approve("manager", DateTime.UtcNow);
        voucher.MarkPaid(DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "WALLET-TX-01");

        db.Organizations.Add(org);
        db.Customers.Add(customer);
        db.ErpInvoices.Add(invoice);
        db.OperatingExpenses.Add(expense);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetSettlementReportQueryHandler(db, orgContext);
        var report = await handler.Handle(new GetSettlementReportQuery(org.Id), CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(3, report.Settlements.TotalCount);

        var ngnSummary = report.CurrencySummaries.First(c => c.Currency == Currency.NGN);
        Assert.Equal(125000m, ngnSummary.TotalWalletSettlements); // 80k invoice + 45k voucher
        Assert.Equal(30000m, ngnSummary.TotalManualSettlements); // 30k expense
        Assert.Equal(155000m, ngnSummary.GrandTotal);
    }

    [Fact]
    public async Task GetProfitLossReport_CalculatesRevenueCogsAndNetProfitCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("PlOrg", "info@pl.com", "+2348000000004");
        var customer = new Customer(org.Id, "CUST-01", "Client A");

        // Revenue: Invoice with 200,000 NGN subtotal
        var invoice = new ErpInvoice(org.Id, "INV-001", customer.Id, "admin", DateTime.UtcNow, DateTime.UtcNow.AddDays(7), false);
        invoice.AddItem("Goods delivery", 1, 200000m);
        invoice.Issue(DateTime.UtcNow);

        // COGS: 50,000 NGN from historical Phase 5C StockOut movement
        var item = new InventoryItem(org.Id, "SKU-1", "Product A", "pcs", 2000m);
        var stockOut = new StockMovement(org.Id, item.Id, StockMovementType.StockOut, 50, "REF-OUT", ValuationMethod.Fifo, 1, "admin", 1000m, 50000m);

        // Operating Expenses: 30,000 NGN
        var expense = new OperatingExpense(org.Id, "EXP-001", ExpenseCategory.Rent, "Office rent", 30000m, DateTime.UtcNow, "admin", ExpensePaymentMethod.Manual);
        expense.Approve("manager", DateTime.UtcNow);
        expense.MarkPaid(DateTime.UtcNow);

        // Company Voucher: 20,000 NGN
        var voucher = new CompanyVoucher(org.Id, "CV-001", "Contractor", "Maintenance", 20000m, "admin", Currency.NGN, CompanyVoucherPaymentMethod.Wallet);
        voucher.Approve("manager", DateTime.UtcNow);
        voucher.MarkPaid(DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "TX-01");

        db.Organizations.Add(org);
        db.Customers.Add(customer);
        db.ErpInvoices.Add(invoice);
        db.InventoryItems.Add(item);
        db.StockMovements.Add(stockOut);
        db.OperatingExpenses.Add(expense);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetProfitLossReportQueryHandler(db, orgContext);
        var report = await handler.Handle(new GetProfitLossReportQuery(org.Id), CancellationToken.None);

        Assert.NotNull(report);
        var summary = report.CurrencySummaries.First(c => c.Currency == Currency.NGN);
        Assert.Equal(200000m, summary.TotalRevenue);
        Assert.Equal(50000m, summary.TotalCogs);
        Assert.Equal(150000m, summary.GrossProfit); // 200,000 - 50,000 = 150,000
        Assert.Equal(30000m, summary.OperatingExpenses);
        Assert.Equal(20000m, summary.CompanyVoucherDisbursements);
        Assert.Equal(50000m, summary.TotalExpenses); // 30,000 + 20,000 = 50,000
        Assert.Equal(100000m, summary.NetProfitLoss); // 150,000 - 50,000 = 100,000
    }

    [Fact]
    public async Task Reports_TenantIsolation_DoesNotIncludeOtherOrganizationData()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var orgA = new Organization("OrgA", "a@test.com", "+2348000000001");
        var orgB = new Organization("OrgB", "b@test.com", "+2348000000002");
        var custA = new Customer(orgA.Id, "CUST-A", "Customer A");
        var custB = new Customer(orgB.Id, "CUST-B", "Customer B");

        var soA = new SalesOrder(orgA.Id, "SO-A", custA.Id, "user-1", DateTime.UtcNow, null, Currency.NGN);
        soA.AddItem("Item A", 1, 10000m);
        soA.Confirm();

        var soB = new SalesOrder(orgB.Id, "SO-B", custB.Id, "user-2", DateTime.UtcNow, null, Currency.NGN);
        soB.AddItem("Item B", 1, 500000m);
        soB.Confirm();

        db.Organizations.AddRange(orgA, orgB);
        db.Customers.AddRange(custA, custB);
        db.SalesOrders.AddRange(soA, soB);
        db.SalesOrderItems.AddRange(soA.Items.Concat(soB.Items));
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(orgA.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetSalesReportQueryHandler(db, orgContext);
        var report = await handler.Handle(new GetSalesReportQuery(orgA.Id), CancellationToken.None);

        Assert.Equal(1, report.TotalOrdersCount);
        Assert.Equal(10000m, report.CurrencySummaries[0].TotalGrossSales);
    }
}
