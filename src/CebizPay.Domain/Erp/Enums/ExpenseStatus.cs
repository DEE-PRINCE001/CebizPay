namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Lifecycle status of an operating expense.
/// </summary>
public enum ExpenseStatus
{
    /// <summary>Draft expense.</summary>
    Draft = 0,

    /// <summary>Approved by authorized manager.</summary>
    Approved = 1,

    /// <summary>Paid and settled.</summary>
    Paid = 2,

    /// <summary>Cancelled / voided.</summary>
    Cancelled = 3
}
