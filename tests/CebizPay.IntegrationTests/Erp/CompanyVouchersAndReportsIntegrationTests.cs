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
/// End-to-end integration tests for Phase 5E Company Vouchers and ERP Reports against PostgreSQL.
/// </summary>
public sealed class CompanyVouchersAndReportsIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public CompanyVouchersAndReportsIntegrationTests(InfrastructureFixture fixture)
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
    public async Task CompanyVoucher_EndToEnd_PersistsAndSettlesCorrectly()
    {
        await using var dbContext = await CreateDbContextAsync();

        var org = new Organization("CV Org", $"cv_{Guid.NewGuid():N}@test.com", "+2348011113344");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var voucher = new CompanyVoucher(
            org.Id,
            $"CV-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            "Logistics Provider",
            "Warehouse transport",
            65000m,
            "usr_accountant",
            Currency.NGN,
            CompanyVoucherPaymentMethod.Manual,
            payeeDetails: "Bank details here",
            notes: "Immediate processing");

        dbContext.CompanyVouchers.Add(voucher);
        await dbContext.SaveChangesAsync();

        var loaded = await dbContext.CompanyVouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(CompanyVoucherStatus.Draft, loaded.Status);

        loaded.Approve("usr_manager", DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var approved = await dbContext.CompanyVouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(CompanyVoucherStatus.Approved, approved.Status);

        approved.MarkPaid(DateTime.UtcNow, reference: "MANUAL-CHQ-55");
        await dbContext.SaveChangesAsync();

        var paid = await dbContext.CompanyVouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(CompanyVoucherStatus.Paid, paid.Status);
        Assert.Equal("MANUAL-CHQ-55", paid.Reference);
    }

    [Fact]
    public async Task ErpReports_EndToEnd_MultiCurrencyDataCorrectlyAggregated()
    {
        await using var dbContext = await CreateDbContextAsync();

        var org = new Organization("Reports Org", $"rep_{Guid.NewGuid():N}@test.com", "+2348011113355");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var custNgn = new Customer(org.Id, "CUST-NGN-01", "Nigerian Client");
        var custUsd = new Customer(org.Id, "CUST-USD-01", "International Client");
        dbContext.Customers.AddRange(custNgn, custUsd);

        // Sales Orders
        var soNgn = new SalesOrder(org.Id, $"SO-{Guid.NewGuid():N}"[..18].ToUpperInvariant(), custNgn.Id, "usr", DateTime.UtcNow, null, Currency.NGN);
        soNgn.AddItem("NGN Goods", 10, 10000m); // 100,000 NGN
        soNgn.Confirm();

        var soUsd = new SalesOrder(org.Id, $"SO-{Guid.NewGuid():N}"[..18].ToUpperInvariant(), custUsd.Id, "usr", DateTime.UtcNow, null, Currency.USD);
        soUsd.AddItem("USD Software", 1, 500m); // 500 USD
        soUsd.Confirm();

        dbContext.SalesOrders.AddRange(soNgn, soUsd);

        // Operating Expenses
        var expNgn = new OperatingExpense(org.Id, $"EXP-{Guid.NewGuid():N}"[..18].ToUpperInvariant(), ExpenseCategory.Utilities, "Power", 20000m, DateTime.UtcNow, "usr", ExpensePaymentMethod.Manual);
        expNgn.Approve("mgr", DateTime.UtcNow);
        expNgn.MarkPaid(DateTime.UtcNow);

        var expUsd = new OperatingExpense(org.Id, $"EXP-{Guid.NewGuid():N}"[..18].ToUpperInvariant(), ExpenseCategory.Marketing, "Ads", 100m, DateTime.UtcNow, "usr", ExpensePaymentMethod.Manual, currency: Currency.USD);
        expUsd.Approve("mgr", DateTime.UtcNow);
        expUsd.MarkPaid(DateTime.UtcNow);

        dbContext.OperatingExpenses.AddRange(expNgn, expUsd);

        await dbContext.SaveChangesAsync();

        // Query directly from PostgreSQL to verify multi-currency separation
        var sales = await dbContext.SalesOrders
            .Where(s => s.OrganizationId == org.Id)
            .GroupBy(s => s.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(s => s.TotalAmount) })
            .ToListAsync();

        Assert.Equal(2, sales.Count);
        Assert.Equal(100000m, sales.First(s => s.Currency == Currency.NGN).Total);
        Assert.Equal(500m, sales.First(s => s.Currency == Currency.USD).Total);

        var expenses = await dbContext.OperatingExpenses
            .Where(e => e.OrganizationId == org.Id && e.Status == ExpenseStatus.Paid)
            .GroupBy(e => e.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync();

        Assert.Equal(2, expenses.Count);
        Assert.Equal(20000m, expenses.First(e => e.Currency == Currency.NGN).Total);
        Assert.Equal(100m, expenses.First(e => e.Currency == Currency.USD).Total);
    }
}
