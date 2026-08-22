namespace CebizPay.Domain.Payroll.Enums;

/// <summary>
/// Status lifecycle of a generated Payment Voucher.
/// </summary>
public enum VoucherStatus
{
    /// <summary>Payment voucher generated upon successful payroll execution.</summary>
    Generated = 1,

    /// <summary>Voucher marked as paid.</summary>
    Paid = 2,

    /// <summary>Voucher voided/cancelled.</summary>
    Voided = 3
}
