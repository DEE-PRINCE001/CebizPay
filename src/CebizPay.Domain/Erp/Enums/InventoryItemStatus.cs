namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Status of an inventory item.
/// </summary>
public enum InventoryItemStatus
{
    /// <summary>Active and available for stock transactions.</summary>
    Active = 0,

    /// <summary>Inactive and disabled for regular stock operations.</summary>
    Inactive = 1,

    /// <summary>Discontinued.</summary>
    Discontinued = 2
}
