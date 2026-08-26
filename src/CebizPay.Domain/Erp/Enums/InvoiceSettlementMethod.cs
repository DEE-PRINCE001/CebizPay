namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Method of settlement for an ERP invoice.
/// </summary>
public enum InvoiceSettlementMethod
{
    /// <summary>Manual / external payment (cash, bank deposit, POS, cheque).</summary>
    Manual = 0,

    /// <summary>Wallet-to-wallet transfer / direct CebizPay wallet debit.</summary>
    Wallet = 1
}
