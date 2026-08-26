namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Lifecycle status of an ERP company disbursement voucher.
/// </summary>
public enum CompanyVoucherStatus
{
    /// <summary>Voucher created as draft.</summary>
    Draft = 0,

    /// <summary>Voucher approved for payment/disbursement.</summary>
    Approved = 1,

    /// <summary>Voucher financially settled / paid.</summary>
    Paid = 2,

    /// <summary>Voucher cancelled.</summary>
    Cancelled = 3
}
