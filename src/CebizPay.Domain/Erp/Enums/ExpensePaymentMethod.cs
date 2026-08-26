namespace CebizPay.Domain.Erp.Enums;

/// <summary>
/// Method of payment for an operating expense.
/// </summary>
public enum ExpensePaymentMethod
{
    /// <summary>Manual / external payment (cash, company cheque, outside bank transfer).</summary>
    Manual = 0,

    /// <summary>Direct payment from organization CebizPay wallet.</summary>
    Wallet = 1
}
