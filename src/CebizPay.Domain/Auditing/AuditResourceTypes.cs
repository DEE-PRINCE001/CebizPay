namespace CebizPay.Domain.Auditing;

/// <summary>
/// Authoritative catalog of centralized audit resource types.
/// </summary>
public static class AuditResourceTypes
{
    /// <summary>User resource type.</summary>
    public const string User = "USER";
    /// <summary>Organization resource type.</summary>
    public const string Organization = "ORGANIZATION";
    /// <summary>Organization membership resource type.</summary>
    public const string Membership = "MEMBERSHIP";
    /// <summary>Individual KYC document resource type.</summary>
    public const string KycDocument = "KYC_DOCUMENT";
    /// <summary>Organization KYB application resource type.</summary>
    public const string KybApplication = "KYB_APPLICATION";
    /// <summary>Platform Admin profile resource type.</summary>
    public const string AdminProfile = "ADMIN_PROFILE";
    /// <summary>Platform Fee policy resource type.</summary>
    public const string FeePolicy = "FEE_POLICY";
    /// <summary>Wallet resource type.</summary>
    public const string Wallet = "WALLET";
    /// <summary>Ledger transaction resource type.</summary>
    public const string LedgerTransaction = "LEDGER_TRANSACTION";
    /// <summary>Peer transfer resource type.</summary>
    public const string PeerTransfer = "PEER_TRANSFER";
    /// <summary>Bank transfer resource type.</summary>
    public const string BankTransfer = "BANK_TRANSFER";
    /// <summary>Bank transfer fee policy resource type.</summary>
    public const string BankTransferFeePolicy = "BANK_TRANSFER_FEE_POLICY";
    /// <summary>Payment provider attempt resource type.</summary>
    public const string PaymentAttempt = "PAYMENT_ATTEMPT";
    /// <summary>Webhook event resource type.</summary>
    public const string WebhookEvent = "WEBHOOK_EVENT";
    /// <summary>Payment failover resource type.</summary>
    public const string PaymentFailover = "PAYMENT_FAILOVER";
    /// <summary>Virtual account resource type.</summary>
    public const string VirtualAccount = "VIRTUAL_ACCOUNT";
    /// <summary>Funding transaction resource type.</summary>
    public const string FundingTransaction = "FUNDING_TRANSACTION";
    /// <summary>Payroll batch resource type.</summary>
    public const string PayrollBatch = "PAYROLL_BATCH";
    /// <summary>Payroll item resource type.</summary>
    public const string PayrollItem = "PAYROLL_ITEM";
    /// <summary>Payment voucher resource type.</summary>
    public const string PaymentVoucher = "PAYMENT_VOUCHER";
    /// <summary>Corporate loan plan resource type.</summary>
    public const string LoanPlan = "LOAN_PLAN";
    /// <summary>Staff loan application resource type.</summary>
    public const string LoanApplication = "LOAN_APPLICATION";
    /// <summary>Loan contract resource type.</summary>
    public const string LoanContract = "LOAN_CONTRACT";
    /// <summary>Loan repayment installment resource type.</summary>
    public const string LoanRepayment = "LOAN_REPAYMENT";
}
