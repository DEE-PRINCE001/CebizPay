namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Status of a sales order.
/// </summary>
public enum SalesOrderStatus
{
    /// <summary>Draft sales order.</summary>
    Draft = 0,

    /// <summary>Confirmed sales order awaiting fulfillment.</summary>
    Confirmed = 1,

    /// <summary>Partially fulfilled from inventory.</summary>
    PartiallyFulfilled = 2,

    /// <summary>Fully fulfilled and dispatched.</summary>
    Fulfilled = 3,

    /// <summary>Cancelled.</summary>
    Cancelled = 4
}
