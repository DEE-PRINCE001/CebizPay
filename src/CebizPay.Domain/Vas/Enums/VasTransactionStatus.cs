namespace CebizPay.Domain.Vas.Enums;

/// <summary>
/// Lifecycle status of a VAS transaction.
/// </summary>
public enum VasTransactionStatus
{
    /// <summary>Transaction created, pending processing.</summary>
    Pending = 1,

    /// <summary>Fulfillment is actively processing with external provider.</summary>
    Processing = 2,

    /// <summary>Transaction completed and fulfilled successfully.</summary>
    Succeeded = 3,

    /// <summary>Transaction failed at provider or validation level.</summary>
    Failed = 4,

    /// <summary>Fulfillment outcome is indeterminate / timed out (requires reconciliation).</summary>
    Unknown = 5,

    /// <summary>Financial debit has been reversed back to the customer's wallet.</summary>
    Reversed = 6
}
