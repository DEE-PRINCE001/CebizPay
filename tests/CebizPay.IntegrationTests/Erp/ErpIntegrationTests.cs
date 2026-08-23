using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Erp;

/// <summary>
/// End-to-end integration tests for Phase 5C ERP features against PostgreSQL Testcontainers.
/// </summary>
public sealed class ErpIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public ErpIntegrationTests(InfrastructureFixture fixture)
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
    public async Task Erp_ValuationPolicy_VersionSwitch_PreservesHistoricalWacAndAppliesFifo()
    {
        await using var dbContext = await CreateDbContextAsync();

        // 1. Create Organization
        var org = new Organization("Enterprise Org", $"ent_{Guid.NewGuid():N}@test.com", "+2348011112233");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        // 2. Initial WAC Policy V1
        var now = DateTime.UtcNow;
        var policyV1 = InventoryValuationPolicy.CreateInitialDefault(org.Id, "usr_admin", now);
        dbContext.InventoryValuationPolicies.Add(policyV1);

        // 3. Create Item with initial 100 @ 100 (WAC)
        var item = new InventoryItem(org.Id, "SKU-VAL-SWITCH", "Switching Item", "pcs", 300m, initialQuantity: 100, initialUnitCost: 100m);
        dbContext.InventoryItems.Add(item);

        var initMovement = new StockMovement(
            org.Id,
            item.Id,
            StockMovementType.StockIn,
            100m,
            $"INIT-{Guid.NewGuid():N}"[..20],
            ValuationMethod.Wac,
            1,
            "usr_admin",
            100m,
            10000m);
        dbContext.StockMovements.Add(initMovement);

        await dbContext.SaveChangesAsync();

        // 4. Switch Valuation Method from WAC (V1) to FIFO (V2)
        var switchTime = DateTime.UtcNow;
        policyV1.Deactivate(switchTime);
        var policyV2 = InventoryValuationPolicy.CreateNextVersion(org.Id, ValuationMethod.Fifo, 2, "usr_admin", switchTime);
        dbContext.InventoryValuationPolicies.Add(policyV2);

        // 5. Receive 50 units @ 120 under FIFO
        item.ApplyStockIn(50m, 120m);
        var fifoMovement = new StockMovement(
            org.Id,
            item.Id,
            StockMovementType.StockIn,
            50m,
            $"IN-{Guid.NewGuid():N}"[..20],
            ValuationMethod.Fifo,
            2,
            "usr_admin",
            120m,
            6000m);
        dbContext.StockMovements.Add(fifoMovement);

        // Add FIFO cost layers for remaining stock (100 @ 100 and 50 @ 120)
        var layer1 = new InventoryCostLayer(org.Id, item.Id, initMovement.Id, 100m, 100m, now);
        var layer2 = new InventoryCostLayer(org.Id, item.Id, fifoMovement.Id, 50m, 120m, switchTime);
        dbContext.InventoryCostLayers.AddRange(layer1, layer2);

        await dbContext.SaveChangesAsync();

        // 6. Issue 120 units under FIFO (consumes 100 from Layer 1, 20 from Layer 2)
        layer1.Consume(100m);
        layer2.Consume(20m);
        item.ApplyStockOut(120m);

        var outMovement = new StockMovement(
            org.Id,
            item.Id,
            StockMovementType.StockOut,
            120m,
            $"OUT-{Guid.NewGuid():N}"[..20],
            ValuationMethod.Fifo,
            2,
            "usr_admin",
            unitCost: 103.3333m,
            totalCost: 12400m);
        dbContext.StockMovements.Add(outMovement);

        await dbContext.SaveChangesAsync();

        // 7. Verify PostgreSQL state
        var reloadedItem = await dbContext.InventoryItems.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(30m, reloadedItem.CurrentQuantity);

        var reloadedLayer1 = await dbContext.InventoryCostLayers.FirstAsync(l => l.Id == layer1.Id);
        Assert.Equal(0m, reloadedLayer1.RemainingQuantity);

        var reloadedLayer2 = await dbContext.InventoryCostLayers.FirstAsync(l => l.Id == layer2.Id);
        Assert.Equal(30m, reloadedLayer2.RemainingQuantity);

        var movements = await dbContext.StockMovements.Where(m => m.InventoryItemId == item.Id).OrderBy(m => m.CreatedAtUtc).ToListAsync();
        Assert.Equal(3, movements.Count);
        Assert.Equal(ValuationMethod.Wac, movements[0].ValuationMethod);
        Assert.Equal(1, movements[0].ValuationPolicyVersion);
        Assert.Equal(ValuationMethod.Fifo, movements[1].ValuationMethod);
        Assert.Equal(2, movements[1].ValuationPolicyVersion);
        Assert.Equal(ValuationMethod.Fifo, movements[2].ValuationMethod);
        Assert.Equal(2, movements[2].ValuationPolicyVersion);
    }

    [Fact]
    public async Task Erp_TenantIsolation_CatalogsAreStrictlyIsolatedBetweenOrganizations()
    {
        await using var dbContext = await CreateDbContextAsync();

        var orgA = new Organization("Org Alpha", $"orga_{Guid.NewGuid():N}@test.com", "+2348011110001");
        orgA.TransitionStatus(OrganizationStatus.Verified);
        var orgB = new Organization("Org Beta", $"orgb_{Guid.NewGuid():N}@test.com", "+2348011110002");
        orgB.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.AddRange(orgA, orgB);

        // Same SKU in different organizations must be allowed
        var itemA = new InventoryItem(orgA.Id, "SHARED-SKU-01", "Item in A", "pcs", 100m);
        var itemB = new InventoryItem(orgB.Id, "SHARED-SKU-01", "Item in B", "pcs", 120m);
        dbContext.InventoryItems.AddRange(itemA, itemB);

        // Services
        var srvA = new ErpService(orgA.Id, "SRV-01", "Service in A", 50000m);
        var srvB = new ErpService(orgB.Id, "SRV-01", "Service in B", 75000m);
        dbContext.ErpServices.AddRange(srvA, srvB);

        // Suppliers
        var supA = new Supplier(orgA.Id, "SUP-01", "Supplier in A", "supA@test.com");
        var supB = new Supplier(orgB.Id, "SUP-01", "Supplier in B", "supB@test.com");
        dbContext.Suppliers.AddRange(supA, supB);

        // Customers
        var custA = new Customer(orgA.Id, "CUST-01", "Customer in A", "custA@test.com");
        var custB = new Customer(orgB.Id, "CUST-01", "Customer in B", "custB@test.com");
        dbContext.Customers.AddRange(custA, custB);

        await dbContext.SaveChangesAsync();

        // Verify Org A only sees Org A items
        var itemsA = await dbContext.InventoryItems.Where(i => i.OrganizationId == orgA.Id).ToListAsync();
        Assert.Single(itemsA);
        Assert.Equal("Item in A", itemsA[0].Name);

        var srvsA = await dbContext.ErpServices.Where(s => s.OrganizationId == orgA.Id).ToListAsync();
        Assert.Single(srvsA);
        Assert.Equal("Service in A", srvsA[0].Name);

        var supsA = await dbContext.Suppliers.Where(s => s.OrganizationId == orgA.Id).ToListAsync();
        Assert.Single(supsA);
        Assert.Equal("Supplier in A", supsA[0].Name);

        var custsA = await dbContext.Customers.Where(c => c.OrganizationId == orgA.Id).ToListAsync();
        Assert.Single(custsA);
        Assert.Equal("Customer in A", custsA[0].Name);
    }
}
