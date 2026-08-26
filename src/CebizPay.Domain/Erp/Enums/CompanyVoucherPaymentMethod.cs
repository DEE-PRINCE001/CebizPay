namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Settlement method for an ERP company disbursement voucher.
/// </summary>
public enum CompanyVoucherPaymentMethod
{
    /// <summary>Manual or external payment (no internal wallet/ledger mutation).</summary>
    Manual = 0,

    /// <summary>Paid from organization CebizPay wallet with ledger integration.</summary>
    Wallet = 1
}
