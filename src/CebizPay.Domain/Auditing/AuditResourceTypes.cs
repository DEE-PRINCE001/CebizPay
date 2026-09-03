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
    /// <summary>Department resource type.</summary>
    public const string Department = "DEPARTMENT";
    /// <summary>Workforce role resource type.</summary>
    public const string WorkforceRole = "WORKFORCE_ROLE";
    /// <summary>Salary level resource type.</summary>
    public const string SalaryLevel = "SALARY_LEVEL";
    /// <summary>Staff member resource type.</summary>
    public const string StaffMember = "STAFF_MEMBER";
    /// <summary>Job posting resource type.</summary>
    public const string JobPosting = "JOB_POSTING";
    /// <summary>Recruitment application resource type.</summary>
    public const string RecruitmentApplication = "RECRUITMENT_APPLICATION";
    /// <summary>Individual KYC document resource type.</summary>
    public const string KycDocument = "KYC_DOCUMENT";
    /// <summary>Organization KYB application resource type.</summary>
    public const string KybApplication = "KYB_APPLICATION";
    /// <summary>Platform Admin profile resource type.</summary>
    public const string AdminProfile = "ADMIN_PROFILE";
    /// <summary>Platform Admin invitation resource type.</summary>
    public const string AdminInvitation = "ADMIN_INVITATION";
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
    /// <summary>Reconciliation record resource type.</summary>
    public const string ReconciliationRecord = "RECONCILIATION_RECORD";
    /// <summary>Recovery outstanding resource type.</summary>
    public const string RecoveryOutstanding = "RECOVERY_OUTSTANDING";
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

    // Savings
    /// <summary>Savings plan resource type.</summary>
    public const string SavingsPlan = "SAVINGS_PLAN";
    /// <summary>Savings account/contract instance resource type.</summary>
    public const string SavingsAccount = "SAVINGS_ACCOUNT";
    /// <summary>Savings contribution resource type.</summary>
    public const string SavingsContribution = "SAVINGS_CONTRIBUTION";
    /// <summary>Savings interest policy resource type.</summary>
    public const string SavingsInterestPolicy = "SAVINGS_INTEREST_POLICY";
    /// <summary>Savings interest accrual resource type.</summary>
    public const string SavingsInterestAccrual = "SAVINGS_INTEREST_ACCRUAL";

    // Thrift / Ajo / Esusu
    /// <summary>Thrift group resource type.</summary>
    public const string ThriftGroup = "THRIFT_GROUP";
    /// <summary>Thrift member resource type.</summary>
    public const string ThriftMember = "THRIFT_MEMBER";
    /// <summary>Thrift invitation resource type.</summary>
    public const string ThriftInvitation = "THRIFT_INVITATION";
    /// <summary>Thrift cycle resource type.</summary>
    public const string ThriftCycle = "THRIFT_CYCLE";
    /// <summary>Thrift contribution resource type.</summary>
    public const string ThriftContribution = "THRIFT_CONTRIBUTION";
    /// <summary>Thrift payout resource type.</summary>
    public const string ThriftPayout = "THRIFT_PAYOUT";
    /// <summary>Thrift reimbursement resource type.</summary>
    public const string ThriftReimbursement = "THRIFT_REIMBURSEMENT";
    /// <summary>Thrift dispute resource type.</summary>
    public const string ThriftDispute = "THRIFT_DISPUTE";

    // Value-Added Services (VAS)
    /// <summary>VAS transaction resource type.</summary>
    public const string VasTransaction = "VAS_TRANSACTION";

    // ERP Inventory, Services, Suppliers & Customers
    /// <summary>Inventory item resource type.</summary>
    public const string InventoryItem = "INVENTORY_ITEM";
    /// <summary>Stock movement transaction resource type.</summary>
    public const string StockMovement = "STOCK_MOVEMENT";
    /// <summary>Inventory valuation policy resource type.</summary>
    public const string InventoryValuationPolicy = "INVENTORY_VALUATION_POLICY";
    /// <summary>ERP service catalog resource type.</summary>
    public const string ErpService = "ERP_SERVICE";
    /// <summary>Supplier resource type.</summary>
    public const string Supplier = "SUPPLIER";
    /// <summary>Customer resource type.</summary>
    public const string Customer = "CUSTOMER";

    // ERP Orders, Expenses, Invoices & Receipts (Phase 5D)
    /// <summary>Purchase order resource type.</summary>
    public const string PurchaseOrder = "PURCHASE_ORDER";
    /// <summary>Sales order resource type.</summary>
    public const string SalesOrder = "SALES_ORDER";
    /// <summary>Operating expense resource type.</summary>
    public const string OperatingExpense = "OPERATING_EXPENSE";
    /// <summary>Invoice resource type.</summary>
    public const string Invoice = "INVOICE";
    /// <summary>Receipt resource type.</summary>
    public const string Receipt = "RECEIPT";
    /// <summary>Company voucher resource type.</summary>
    public const string CompanyVoucher = "COMPANY_VOUCHER";
    /// <summary>External funding account resource type.</summary>
    public const string ExternalFundingAccount = "EXTERNAL_FUNDING_ACCOUNT";
    /// <summary>Generalized platform fee policy resource type.</summary>
    public const string PlatformFeePolicy = "PLATFORM_FEE_POLICY";
    /// <summary>Saved card token resource type.</summary>
    public const string SavedCard = "SAVED_CARD";
    /// <summary>Card refund resource type.</summary>
    public const string CardRefund = "CARD_REFUND";
    /// <summary>Card verification resource type.</summary>
    public const string CardVerification = "CARD_VERIFICATION";
    /// <summary>Risk assessment resource type.</summary>
    public const string RiskAssessment = "RISK_ASSESSMENT";
    /// <summary>CDD profile resource type.</summary>
    public const string CddProfile = "CDD_PROFILE";
    /// <summary>EDD case resource type.</summary>
    public const string EddCase = "EDD_CASE";
    /// <summary>Compliance decision resource type.</summary>
    public const string ComplianceDecision = "COMPLIANCE_DECISION";
    /// <summary>Compliance restriction resource type.</summary>
    public const string ComplianceRestriction = "COMPLIANCE_RESTRICTION";
    /// <summary>Announcement resource type.</summary>
    public const string Announcement = "ANNOUNCEMENT";

    // Referral Program (Batch 6D)
    /// <summary>Referral setting resource type.</summary>
    public const string ReferralSetting = "REFERRAL_SETTING";
    /// <summary>Referral code resource type.</summary>
    public const string ReferralCode = "REFERRAL_CODE";
    /// <summary>Referral relationship resource type.</summary>
    public const string ReferralRelationship = "REFERRAL_RELATIONSHIP";
    /// <summary>Referral reward resource type.</summary>
    public const string ReferralReward = "REFERRAL_REWARD";
}
