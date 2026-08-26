namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Categorization of ledger transactions.
/// </summary>
public enum LedgerTransactionType
{
    /// <summary>Peer to peer transfer.</summary>
    PeerTransfer = 1,
    /// <summary>Bank transfer.</summary>
    BankTransfer = 2,
    /// <summary>Corporate payroll disbursement.</summary>
    Payroll = 3,
    /// <summary>Loan disbursement.</summary>
    LoanDisbursement = 4,
    /// <summary>Loan repayment.</summary>
    LoanRepayment = 5,
    /// <summary>Savings contribution.</summary>
    SavingsContribution = 6,
    /// <summary>Savings withdrawal.</summary>
    SavingsWithdrawal = 7,
    /// <summary>Thrift contribution.</summary>
    ThriftContribution = 8,
    /// <summary>Thrift payout.</summary>
    ThriftPayout = 9,
    /// <summary>Value added service purchase.</summary>
    VasPurchase = 10,
    /// <summary>Platform fee entry.</summary>
    Fee = 11,
    /// <summary>Refund.</summary>
    Refund = 12,
    /// <summary>Reversal of a prior transaction.</summary>
    Reversal = 13,
    /// <summary>Cross-currency FX conversion.</summary>
    FxConversion = 14,
    /// <summary>Inbound bank deposit to virtual account.</summary>
    VirtualAccountDeposit = 15,
    /// <summary>Inbound card payment / checkout charge.</summary>
    CardFunding = 16,
    /// <summary>ERP operating expense payment from wallet.</summary>
    ErpExpense = 17,
    /// <summary>ERP invoice payment settled via wallet.</summary>
    ErpInvoicePayment = 18,
    /// <summary>ERP company voucher disbursement settled via wallet.</summary>
    CompanyVoucherDisbursement = 19
}
