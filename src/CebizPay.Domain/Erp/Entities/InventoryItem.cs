using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an organization inventory product/stock item.
/// </summary>
public sealed class InventoryItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Stock Keeping Unit (SKU), unique per organization.</summary>
    public string Sku { get; private set; } = string.Empty;

    /// <summary>Product/Item display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Detailed description.</summary>
    public string? Description { get; private set; }

    /// <summary>Category classification (e.g., Electronics, Consumables).</summary>
    public string? Category { get; private set; }

    /// <summary>Unit of measure (e.g., pcs, kg, box, unit).</summary>
    public string UnitOfMeasure { get; private set; } = string.Empty;

    /// <summary>Currency code for valuation and selling price.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Current on-hand quantity available (cannot become negative).</summary>
    public decimal CurrentQuantity { get; private set; }

    /// <summary>Reorder threshold quantity.</summary>
    public decimal ReorderLevel { get; private set; }

    /// <summary>Current Weighted Average Cost (WAC) per unit.</summary>
    public decimal CurrentAverageCost { get; private set; }

    /// <summary>Standard selling price per unit.</summary>
    public decimal SellingPrice { get; private set; }

    /// <summary>Lifecycle status of the inventory item.</summary>
    public InventoryItemStatus Status { get; private set; } = InventoryItemStatus.Active;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Soft deleted timestamp.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    private InventoryItem() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="InventoryItem"/>.
    /// </summary>
    public InventoryItem(
        Guid organizationId,
        string sku,
        string name,
        string unitOfMeasure,
        decimal sellingPrice,
        string? description = null,
        string? category = null,
        decimal reorderLevel = 0,
        Currency currency = Currency.NGN,
        decimal initialQuantity = 0,
        decimal initialUnitCost = 0)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU is required.", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
        {
            throw new ArgumentException("Unit of measure is required.", nameof(unitOfMeasure));
        }

        if (sellingPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sellingPrice), "Selling price cannot be negative.");
        }

        if (reorderLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), "Reorder level cannot be negative.");
        }

        if (initialQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialQuantity), "Initial quantity cannot be negative.");
        }

        if (initialUnitCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialUnitCost), "Initial unit cost cannot be negative.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        UnitOfMeasure = unitOfMeasure.Trim();
        Currency = currency;
        SellingPrice = sellingPrice;
        ReorderLevel = reorderLevel;
        CurrentQuantity = initialQuantity;
        CurrentAverageCost = initialUnitCost;
        Status = InventoryItemStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates item details.
    /// </summary>
    public void Update(
        string name,
        string? description,
        string? category,
        string unitOfMeasure,
        decimal reorderLevel,
        decimal sellingPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
        {
            throw new ArgumentException("Unit of measure is required.", nameof(unitOfMeasure));
        }

        if (sellingPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sellingPrice), "Selling price cannot be negative.");
        }

        if (reorderLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), "Reorder level cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        UnitOfMeasure = unitOfMeasure.Trim();
        ReorderLevel = reorderLevel;
        SellingPrice = sellingPrice;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies an incoming stock receipt and recalculates Weighted Average Cost.
    /// </summary>
    public void ApplyStockIn(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Stock in quantity must be greater than zero.");
        }

        if (unitCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative.");
        }

        var totalQty = CurrentQuantity + quantity;
        if (totalQty > 0)
        {
            var totalValue = (CurrentQuantity * CurrentAverageCost) + (quantity * unitCost);
            CurrentAverageCost = Math.Round(totalValue / totalQty, 4);
        }
        else
        {
            CurrentAverageCost = unitCost;
        }

        CurrentQuantity = totalQty;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies an outgoing stock issue, decreasing quantity without altering WAC average cost.
    /// </summary>
    public void ApplyStockOut(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Stock out quantity must be greater than zero.");
        }

        if (CurrentQuantity < quantity)
        {
            throw new InvalidOperationException($"Insufficient inventory available. Current: {CurrentQuantity}, requested: {quantity}.");
        }

        CurrentQuantity -= quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a manual inventory quantity adjustment and optional unit cost realignment.
    /// </summary>
    public void ApplyStockAdjustment(decimal quantityDelta, decimal? newAverageCost = null)
    {
        if (CurrentQuantity + quantityDelta < 0)
        {
            throw new InvalidOperationException($"Adjustment would cause negative inventory. Current: {CurrentQuantity}, delta: {quantityDelta}.");
        }

        CurrentQuantity += quantityDelta;

        if (newAverageCost.HasValue)
        {
            if (newAverageCost.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newAverageCost), "Average cost cannot be negative.");
            }
            CurrentAverageCost = newAverageCost.Value;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Derives the current stock status.
    /// </summary>
    public StockStatus GetStockStatus()
    {
        if (CurrentQuantity <= 0)
        {
            return StockStatus.OutOfStock;
        }

        if (CurrentQuantity <= ReorderLevel)
        {
            return StockStatus.LowStock;
        }

        return StockStatus.InStock;
    }

    /// <summary>
    /// Returns total WAC valuation value.
    /// </summary>
    public decimal GetTotalWacValuation()
    {
        return Math.Round(CurrentQuantity * CurrentAverageCost, 2);
    }

    /// <summary>Deactivates the inventory item.</summary>
    public void Deactivate()
    {
        Status = InventoryItemStatus.Inactive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Activates the inventory item.</summary>
    public void Activate()
    {
        Status = InventoryItemStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Performs soft deletion.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        Status = InventoryItemStatus.Inactive;
    }
}
