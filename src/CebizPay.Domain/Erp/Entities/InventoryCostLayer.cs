namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain entity representing a distinct inventory cost layer for First-In, First-Out (FIFO) valuation.
/// </summary>
public sealed class InventoryCostLayer
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Inventory item identifier.</summary>
    public Guid InventoryItemId { get; private set; }

    /// <summary>Source stock movement ID that created this layer.</summary>
    public Guid SourceMovementId { get; private set; }

    /// <summary>Original quantity received into this layer.</summary>
    public decimal OriginalQuantity { get; private set; }

    /// <summary>Remaining unconsumed quantity in this layer.</summary>
    public decimal RemainingQuantity { get; private set; }

    /// <summary>Unit purchase/acquisition cost for items in this layer.</summary>
    public decimal UnitCost { get; private set; }

    /// <summary>Creation timestamp (used for FIFO chronological ordering).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private InventoryCostLayer() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="InventoryCostLayer"/>.
    /// </summary>
    public InventoryCostLayer(
        Guid organizationId,
        Guid inventoryItemId,
        Guid sourceMovementId,
        decimal quantity,
        decimal unitCost,
        DateTime createdAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (inventoryItemId == Guid.Empty)
        {
            throw new ArgumentException("InventoryItemId cannot be empty.", nameof(inventoryItemId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Cost layer quantity must be greater than zero.");
        }

        if (unitCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        InventoryItemId = inventoryItemId;
        SourceMovementId = sourceMovementId;
        OriginalQuantity = quantity;
        RemainingQuantity = quantity;
        UnitCost = unitCost;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Consumes up to <paramref name="quantityToConsume"/> from this layer.
    /// Returns the quantity actually consumed.
    /// </summary>
    public decimal Consume(decimal quantityToConsume)
    {
        if (quantityToConsume <= 0)
        {
            return 0;
        }

        var consumed = Math.Min(RemainingQuantity, quantityToConsume);
        RemainingQuantity -= consumed;
        return consumed;
    }
}
