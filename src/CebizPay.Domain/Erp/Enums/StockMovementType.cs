namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Type of inventory stock movement.
/// </summary>
public enum StockMovementType
{
    /// <summary>Incoming stock receipt.</summary>
    StockIn = 0,

    /// <summary>Outgoing stock issue or fulfillment.</summary>
    StockOut = 1,

    /// <summary>Manual stock adjustment (reconciliation / count difference).</summary>
    Adjustment = 2
}
