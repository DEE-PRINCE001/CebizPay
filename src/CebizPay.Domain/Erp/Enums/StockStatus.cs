namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Derived stock availability status.
/// </summary>
public enum StockStatus
{
    /// <summary>Item is in stock above reorder level.</summary>
    InStock = 0,

    /// <summary>Item quantity is at or below reorder level.</summary>
    LowStock = 1,

    /// <summary>Item has zero or negative quantity.</summary>
    OutOfStock = 2
}
