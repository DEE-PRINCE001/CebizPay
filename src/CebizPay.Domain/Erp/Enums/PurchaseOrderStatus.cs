namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Status of a purchase order.
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>Draft purchase order.</summary>
    Draft = 0,

    /// <summary>Confirmed and awaiting vendor shipment/delivery.</summary>
    Confirmed = 1,

    /// <summary>Partially received into inventory.</summary>
    PartiallyReceived = 2,

    /// <summary>Fully received into inventory.</summary>
    Received = 3,

    /// <summary>Cancelled.</summary>
    Cancelled = 4
}
