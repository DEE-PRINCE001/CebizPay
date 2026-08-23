using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for Phase 5C ERP entities (Inventory, Valuation Policy, Cost Layers, Movements, Services, Suppliers, Customers).
/// </summary>
public sealed class ErpEntitiesTests
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly string TestUserId = "user-123";

    [Fact]
    public void InventoryValuationPolicy_CreateInitialDefault_ShouldBeActiveWacVersion1()
    {
        var now = DateTime.UtcNow;
        var policy = InventoryValuationPolicy.CreateInitialDefault(OrgId, TestUserId, now);

        Assert.Equal(OrgId, policy.OrganizationId);
        Assert.Equal(ValuationMethod.Wac, policy.Method);
        Assert.Equal(1, policy.Version);
        Assert.True(policy.IsActive);
        Assert.Equal(now, policy.EffectiveFromUtc);
        Assert.Null(policy.DeactivatedAtUtc);
        Assert.Equal(TestUserId, policy.CreatedByUserId);
    }

    [Fact]
    public void InventoryValuationPolicy_CreateNextVersion_AndDeactivate_ShouldTransitionCorrectly()
    {
        var now = DateTime.UtcNow;
        var policyV1 = InventoryValuationPolicy.CreateInitialDefault(OrgId, TestUserId, now);

        var switchTime = now.AddHours(2);
        policyV1.Deactivate(switchTime);
        Assert.False(policyV1.IsActive);
        Assert.Equal(switchTime, policyV1.DeactivatedAtUtc);

        var policyV2 = InventoryValuationPolicy.CreateNextVersion(OrgId, ValuationMethod.Fifo, 2, TestUserId, switchTime);
        Assert.Equal(ValuationMethod.Fifo, policyV2.Method);
        Assert.Equal(2, policyV2.Version);
        Assert.True(policyV2.IsActive);
        Assert.Equal(switchTime, policyV2.EffectiveFromUtc);
    }

    [Fact]
    public void InventoryItem_Create_ShouldInitializeWithCorrectValues()
    {
        var item = new InventoryItem(
            OrgId,
            "sku-100",
            "Laptop Dell XPS 15",
            "pcs",
            sellingPrice: 1500000m,
            description: "High-end laptop",
            category: "Computers",
            reorderLevel: 5,
            currency: Currency.NGN,
            initialQuantity: 10,
            initialUnitCost: 1200000m);

        Assert.Equal(OrgId, item.OrganizationId);
        Assert.Equal("SKU-100", item.Sku);
        Assert.Equal("Laptop Dell XPS 15", item.Name);
        Assert.Equal("pcs", item.UnitOfMeasure);
        Assert.Equal(1500000m, item.SellingPrice);
        Assert.Equal(10, item.CurrentQuantity);
        Assert.Equal(1200000m, item.CurrentAverageCost);
        Assert.Equal(InventoryItemStatus.Active, item.Status);
        Assert.Equal(StockStatus.InStock, item.GetStockStatus());
        Assert.Equal(12000000m, item.GetTotalWacValuation());
    }

    [Fact]
    public void InventoryItem_ApplyStockIn_ShouldRecalculateWeightedAverageCostCorrectly()
    {
        // 100 units @ 100 = 10,000
        var item = new InventoryItem(
            OrgId,
            "WAC-TEST",
            "Item A",
            "pcs",
            sellingPrice: 200m,
            initialQuantity: 100,
            initialUnitCost: 100m);

        // Receive 50 units @ 120 = 6,000
        // Total value = 16,000; Total qty = 150; New Avg Cost = 16,000 / 150 = 106.6667
        item.ApplyStockIn(50, 120m);

        Assert.Equal(150m, item.CurrentQuantity);
        Assert.Equal(106.6667m, item.CurrentAverageCost);
    }

    [Fact]
    public void InventoryItem_ApplyStockOut_ShouldDecreaseQuantityWithoutChangingAverageCost()
    {
        var item = new InventoryItem(
            OrgId,
            "WAC-OUT",
            "Item B",
            "pcs",
            sellingPrice: 200m,
            initialQuantity: 150,
            initialUnitCost: 106.6667m);

        item.ApplyStockOut(30);

        Assert.Equal(120m, item.CurrentQuantity);
        Assert.Equal(106.6667m, item.CurrentAverageCost);
    }

    [Fact]
    public void InventoryItem_ApplyStockOut_WhenQuantityInsufficient_ShouldThrow()
    {
        var item = new InventoryItem(
            OrgId,
            "WAC-UNDERFLOW",
            "Item C",
            "pcs",
            sellingPrice: 200m,
            initialQuantity: 10,
            initialUnitCost: 50m);

        Assert.Throws<InvalidOperationException>(() => item.ApplyStockOut(15));
    }

    [Fact]
    public void InventoryItem_ApplyStockAdjustment_ShouldModifyQuantityAndRespectUnderflow()
    {
        var item = new InventoryItem(
            OrgId,
            "ADJUST-TEST",
            "Item D",
            "pcs",
            sellingPrice: 100m,
            initialQuantity: 20,
            initialUnitCost: 50m);

        // Positive adjustment
        item.ApplyStockAdjustment(5);
        Assert.Equal(25m, item.CurrentQuantity);

        // Negative adjustment
        item.ApplyStockAdjustment(-10);
        Assert.Equal(15m, item.CurrentQuantity);

        // Underflow adjustment
        Assert.Throws<InvalidOperationException>(() => item.ApplyStockAdjustment(-20));
    }

    [Fact]
    public void InventoryItem_StockStatus_Derivation_ShouldBeAccurate()
    {
        var item = new InventoryItem(
            OrgId,
            "STATUS-TEST",
            "Item E",
            "pcs",
            sellingPrice: 100m,
            reorderLevel: 10,
            initialQuantity: 15);

        Assert.Equal(StockStatus.InStock, item.GetStockStatus());

        item.ApplyStockOut(5); // Qty = 10 (at reorder level)
        Assert.Equal(StockStatus.LowStock, item.GetStockStatus());

        item.ApplyStockOut(10); // Qty = 0
        Assert.Equal(StockStatus.OutOfStock, item.GetStockStatus());
    }

    [Fact]
    public void InventoryCostLayer_Consume_ShouldConsumeChronologicallyAndReturnConsumedAmount()
    {
        var layer = new InventoryCostLayer(
            OrgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            quantity: 100m,
            unitCost: 150m,
            DateTime.UtcNow);

        Assert.Equal(100m, layer.RemainingQuantity);

        var consumed1 = layer.Consume(40m);
        Assert.Equal(40m, consumed1);
        Assert.Equal(60m, layer.RemainingQuantity);

        var consumed2 = layer.Consume(80m); // exceeds remaining 60
        Assert.Equal(60m, consumed2);
        Assert.Equal(0m, layer.RemainingQuantity);
    }

    [Fact]
    public void ErpService_CreateAndUpdate_ShouldWorkCorrectly()
    {
        var service = new ErpService(
            OrgId,
            "srv-audit",
            "Annual Financial Audit",
            unitPrice: 500000m,
            description: "Statutory audit service");

        Assert.Equal("SRV-AUDIT", service.Code);
        Assert.Equal("Annual Financial Audit", service.Name);
        Assert.Equal(500000m, service.UnitPrice);
        Assert.Equal(ErpServiceStatus.Active, service.Status);

        service.Update("Full Statutory Audit", "Updated description", 600000m);
        Assert.Equal("Full Statutory Audit", service.Name);
        Assert.Equal(600000m, service.UnitPrice);

        service.Deactivate();
        Assert.Equal(ErpServiceStatus.Inactive, service.Status);

        service.Activate();
        Assert.Equal(ErpServiceStatus.Active, service.Status);

        service.SoftDelete();
        Assert.True(service.IsDeleted);
    }

    [Fact]
    public void Supplier_CreateAndUpdate_ShouldWorkCorrectly()
    {
        var supplier = new Supplier(
            OrgId,
            "sup-001",
            "Dell Nigeria Ltd",
            "sales@dell.ng",
            "+2348011223344",
            "Victoria Island, Lagos",
            "TIN-12345678");

        Assert.Equal("SUP-001", supplier.Reference);
        Assert.Equal("Dell Nigeria Ltd", supplier.Name);
        Assert.Equal("sales@dell.ng", supplier.Email);
        Assert.Equal("TIN-12345678", supplier.TaxIdentifier);
        Assert.Equal(SupplierStatus.Active, supplier.Status);

        supplier.Update("Dell Technologies West Africa", "enterprise@dell.ng", "+2348099887766", "Ikoyi, Lagos", "TIN-87654321");
        Assert.Equal("Dell Technologies West Africa", supplier.Name);
        Assert.Equal("enterprise@dell.ng", supplier.Email);

        supplier.SoftDelete();
        Assert.True(supplier.IsDeleted);
    }

    [Fact]
    public void Customer_CreateAndUpdate_ShouldWorkCorrectly()
    {
        var customer = new Customer(
            OrgId,
            "cust-001",
            "Dangote Industries Ltd",
            "procurement@dangote.com",
            "+23412345678",
            "Lagos, Nigeria");

        Assert.Equal("CUST-001", customer.Reference);
        Assert.Equal("Dangote Industries Ltd", customer.Name);
        Assert.Equal("procurement@dangote.com", customer.Email);
        Assert.Equal(CustomerStatus.Active, customer.Status);

        customer.Update("Dangote Group", "invoices@dangote.com", "+23498765432", "Ikoyi, Lagos");
        Assert.Equal("Dangote Group", customer.Name);
        Assert.Equal("invoices@dangote.com", customer.Email);

        customer.SoftDelete();
        Assert.True(customer.IsDeleted);
    }
}
