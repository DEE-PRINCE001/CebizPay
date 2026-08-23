using CebizPay.Domain.Erp.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Immutable audit record of an inventory stock transaction (IN / OUT / ADJUSTMENT).
/// </summary>
public sealed class StockMovement
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Inventory item identifier.</summary>
    public Guid InventoryItemId { get; private set; }

    /// <summary>Movement classification (StockIn, StockOut, Adjustment).</summary>
    public StockMovementType MovementType { get; private set; }

    /// <summary>Quantity moved (positive magnitude).</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Unit cost applied at transaction time (nullable for simple issue/adjustments).</summary>
    public decimal? UnitCost { get; private set; }

    /// <summary>Total cost valuation of the movement (Quantity * UnitCost).</summary>
    public decimal? TotalCost { get; private set; }

    /// <summary>Unique business reference or idempotency tracking key.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Optional business reason or adjustment notes.</summary>
    public string? Reason { get; private set; }

    /// <summary>Valuation method active at the time of movement (WAC / FIFO).</summary>
    public ValuationMethod ValuationMethod { get; private set; }

    /// <summary>Valuation policy version active at the time of movement.</summary>
    public int ValuationPolicyVersion { get; private set; }

    /// <summary>User ID of the operator who executed the stock movement.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Timestamp when the movement occurred in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private StockMovement() { } // EF Core

    /// <summary>
    /// Initializes a new immutable <see cref="StockMovement"/> record.
    /// </summary>
    public StockMovement(
        Guid organizationId,
        Guid inventoryItemId,
        StockMovementType movementType,
        decimal quantity,
        string reference,
        ValuationMethod valuationMethod,
        int valuationPolicyVersion,
        string createdByUserId,
        decimal? unitCost = null,
        decimal? totalCost = null,
        string? reason = null)
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
            throw new ArgumentOutOfRangeException(nameof(quantity), "Movement quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Reference cannot be empty.", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        InventoryItemId = inventoryItemId;
        MovementType = movementType;
        Quantity = quantity;
        Reference = reference.Trim();
        ValuationMethod = valuationMethod;
        ValuationPolicyVersion = valuationPolicyVersion;
        CreatedByUserId = createdByUserId.Trim();
        UnitCost = unitCost;
        TotalCost = totalCost ?? (unitCost.HasValue ? Math.Round(quantity * unitCost.Value, 4) : null);
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
