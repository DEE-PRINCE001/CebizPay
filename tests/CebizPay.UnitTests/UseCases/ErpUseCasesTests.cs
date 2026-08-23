using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

/// <summary>
/// Application use cases unit tests for Phase 5C ERP features.
/// </summary>
public sealed class ErpUseCasesTests
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
    public async Task GetValuationPolicy_WhenNoneExists_SeedsInitialWacPolicy()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("admin-1");

        var handler = new GetValuationPolicyQueryHandler(dbContext, orgContext, userContext);
        var result = await handler.Handle(new GetValuationPolicyQuery(org.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ValuationMethod.Wac, result.Method);
        Assert.Equal(1, result.Version);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task SetValuationPolicy_WhenChangedToFifo_CreatesNewVersionAndDeactivatesOld()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);

        var policyV1 = InventoryValuationPolicy.CreateInitialDefault(org.Id, "admin-1", DateTime.UtcNow);
        dbContext.InventoryValuationPolicies.Add(policyV1);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("admin-1");

        var handler = new SetValuationPolicyCommandHandler(dbContext, orgContext, userContext, outbox);
        var result = await handler.Handle(new SetValuationPolicyCommand(org.Id, ValuationMethod.Fifo), CancellationToken.None);

        Assert.Equal(ValuationMethod.Fifo, result.Method);
        Assert.Equal(2, result.Version);
        Assert.True(result.IsActive);

        var reloadedV1 = await dbContext.InventoryValuationPolicies.FirstAsync(p => p.Id == policyV1.Id);
        Assert.False(reloadedV1.IsActive);
        Assert.NotNull(reloadedV1.DeactivatedAtUtc);

        outbox.Received(1).Write(Arg.Any<InventoryValuationPolicyChangedDomainEvent>());
    }

    [Fact]
    public async Task CreateInventoryItem_WithInitialQuantity_PersistsItemMovementAndCostLayerIfFifo()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);

        var policy = InventoryValuationPolicy.CreateNextVersion(org.Id, ValuationMethod.Fifo, 2, "admin-1", DateTime.UtcNow);
        dbContext.InventoryValuationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("admin-1");

        var handler = new CreateInventoryItemCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateInventoryItemCommand(
            org.Id,
            "SKU-FIFO-1",
            "Widget A",
            "pcs",
            500m,
            null,
            null,
            0,
            Currency.NGN,
            100m,
            250m);

        var itemId = await handler.Handle(command, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, itemId);

        var item = await dbContext.InventoryItems.FirstAsync(i => i.Id == itemId);
        Assert.Equal(100m, item.CurrentQuantity);
        Assert.Equal(250m, item.CurrentAverageCost);

        var movement = await dbContext.StockMovements.FirstAsync(m => m.InventoryItemId == itemId);
        Assert.Equal(100m, movement.Quantity);
        Assert.Equal(250m, movement.UnitCost);
        Assert.Equal(ValuationMethod.Fifo, movement.ValuationMethod);

        var costLayer = await dbContext.InventoryCostLayers.FirstAsync(l => l.InventoryItemId == itemId);
        Assert.Equal(100m, costLayer.OriginalQuantity);
        Assert.Equal(100m, costLayer.RemainingQuantity);
        Assert.Equal(250m, costLayer.UnitCost);

        outbox.Received(1).Write(Arg.Any<InventoryItemCreatedDomainEvent>());
    }

    [Fact]
    public async Task StockIn_UnderWac_RecalculatesAverageCostAndAudits()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);

        var item = new InventoryItem(org.Id, "SKU-WAC", "Product WAC", "pcs", 300m, initialQuantity: 100, initialUnitCost: 100m);
        dbContext.InventoryItems.Add(item);

        var policy = InventoryValuationPolicy.CreateInitialDefault(org.Id, "admin-1", DateTime.UtcNow);
        dbContext.InventoryValuationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("operator-1");

        var handler = new StockInCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new StockInCommand(item.Id, org.Id, 50m, 120m, "PO-001", "Batch stock in");

        var movementId = await handler.Handle(command, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, movementId);

        var reloaded = await dbContext.InventoryItems.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(150m, reloaded.CurrentQuantity);
        Assert.Equal(106.6667m, reloaded.CurrentAverageCost);

        outbox.Received(1).Write(Arg.Any<StockReceivedDomainEvent>());
    }

    [Fact]
    public async Task StockOut_UnderFifo_ConsumesOldestCostLayersFirst()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);

        var item = new InventoryItem(org.Id, "SKU-FIFO-OUT", "Product FIFO", "pcs", 300m, initialQuantity: 150, initialUnitCost: 100m);
        dbContext.InventoryItems.Add(item);

        var policy = InventoryValuationPolicy.CreateNextVersion(org.Id, ValuationMethod.Fifo, 2, "admin-1", DateTime.UtcNow);
        dbContext.InventoryValuationPolicies.Add(policy);

        var now = DateTime.UtcNow;
        // Layer 1: 100 @ 100
        var layer1 = new InventoryCostLayer(org.Id, item.Id, Guid.NewGuid(), 100m, 100m, now.AddHours(-2));
        // Layer 2: 50 @ 120
        var layer2 = new InventoryCostLayer(org.Id, item.Id, Guid.NewGuid(), 50m, 120m, now.AddHours(-1));
        dbContext.InventoryCostLayers.AddRange(layer1, layer2);

        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("operator-1");

        var handler = new StockOutCommandHandler(dbContext, orgContext, userContext, outbox);
        // Issue 120 units (100 from layer 1, 20 from layer 2)
        // Total cost = (100 * 100) + (20 * 120) = 10000 + 2400 = 12400
        // Unit cost = 12400 / 120 = 103.3333
        var command = new StockOutCommand(item.Id, org.Id, 120m, "SO-001", "Sales order fulfillment");

        var movementId = await handler.Handle(command, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, movementId);

        var reloadedLayer1 = await dbContext.InventoryCostLayers.FirstAsync(l => l.Id == layer1.Id);
        Assert.Equal(0m, reloadedLayer1.RemainingQuantity);

        var reloadedLayer2 = await dbContext.InventoryCostLayers.FirstAsync(l => l.Id == layer2.Id);
        Assert.Equal(30m, reloadedLayer2.RemainingQuantity);

        var reloadedItem = await dbContext.InventoryItems.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(30m, reloadedItem.CurrentQuantity);

        var movement = await dbContext.StockMovements.FirstAsync(m => m.Id == movementId);
        Assert.Equal(12400m, movement.TotalCost);
        Assert.Equal(103.3333m, movement.UnitCost);

        outbox.Received(1).Write(Arg.Any<StockIssuedDomainEvent>());
    }

    [Fact]
    public async Task StockAdjustment_Negative_ReducesQuantityAndValidates()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);

        var item = new InventoryItem(org.Id, "SKU-ADJ", "Product ADJ", "pcs", 300m, initialQuantity: 50, initialUnitCost: 100m);
        dbContext.InventoryItems.Add(item);

        var policy = InventoryValuationPolicy.CreateInitialDefault(org.Id, "admin-1", DateTime.UtcNow);
        dbContext.InventoryValuationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("operator-1");

        var handler = new StockAdjustmentCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new StockAdjustmentCommand(item.Id, org.Id, -10m, "ADJ-001", "Damaged goods");

        var movementId = await handler.Handle(command, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, movementId);

        var reloaded = await dbContext.InventoryItems.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(40m, reloaded.CurrentQuantity);

        outbox.Received(1).Write(Arg.Any<StockAdjustedDomainEvent>());
    }

    [Fact]
    public async Task ErpService_Supplier_Customer_Crud_ShouldWorkEndToEnd()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("admin-1");

        // Service
        var createServiceHandler = new CreateErpServiceCommandHandler(dbContext, orgContext, userContext, outbox);
        var serviceId = await createServiceHandler.Handle(new CreateErpServiceCommand(org.Id, "SRV-01", "Consulting", 150000m), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, serviceId);

        // Supplier
        var createSupplierHandler = new CreateSupplierCommandHandler(dbContext, orgContext, userContext, outbox);
        var supplierId = await createSupplierHandler.Handle(new CreateSupplierCommand(org.Id, "SUP-01", "Acme Vendor", "vendor@acme.com"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, supplierId);

        // Customer
        var createCustomerHandler = new CreateCustomerCommandHandler(dbContext, orgContext, userContext, outbox);
        var customerId = await createCustomerHandler.Handle(new CreateCustomerCommand(org.Id, "CUST-01", "Global Corp", "corp@global.com"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, customerId);

        Assert.Equal(1, await dbContext.ErpServices.CountAsync());
        Assert.Equal(1, await dbContext.Suppliers.CountAsync());
        Assert.Equal(1, await dbContext.Customers.CountAsync());
    }
}
