namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Status of an ERP invoice.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Draft invoice.</summary>
    Draft = 0,

    /// <summary>Issued to customer and pending payment.</summary>
    Issued = 1,

    /// <summary>Partially paid.</summary>
    PartiallyPaid = 2,

    /// <summary>Fully paid and closed.</summary>
    Paid = 3,

    /// <summary>Past due date.</summary>
    Overdue = 4,

    /// <summary>Cancelled / voided.</summary>
    Cancelled = 5
}
