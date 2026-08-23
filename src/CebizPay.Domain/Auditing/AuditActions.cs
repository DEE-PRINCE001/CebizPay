namespace CebizPay.Domain.Auditing;

/// <summary>
/// Authoritative catalog of centralized audit action names.
/// </summary>
public static class AuditActions
{
    // Auth & Identity & Security
    /// <summary>Audit action when a new user registers.</summary>
    public const string UserRegistered = "USER_REGISTERED";
    /// <summary>Audit action when a user password is changed.</summary>
    public const string PasswordChanged = "PASSWORD_CHANGED";
    /// <summary>Audit action when MFA is enabled.</summary>
    public const string MfaEnabled = "MFA_ENABLED";
    /// <summary>Audit action when MFA is disabled.</summary>
    public const string MfaDisabled = "MFA_DISABLED";
    /// <summary>Audit action when a transaction PIN is set.</summary>
    public const string PinSet = "PIN_SET";
    /// <summary>Audit action when a transaction PIN is changed.</summary>
    public const string PinChanged = "PIN_CHANGED";
    /// <summary>Audit action when a transaction PIN is locked due to failed attempts.</summary>
    public const string PinLocked = "PIN_LOCKED";

    // KYC / Individual Compliance
    /// <summary>Audit action when individual KYC is submitted.</summary>
    public const string KycSubmitted = "KYC_SUBMITTED";
    /// <summary>Audit action when individual KYC is verified.</summary>
    public const string KycVerified = "KYC_VERIFIED";
    /// <summary>Audit action when individual KYC is rejected.</summary>
    public const string KycRejected = "KYC_REJECTED";

    // KYB / Organization Compliance
    /// <summary>Audit action when organization KYB is submitted.</summary>
    public const string KybSubmitted = "KYB_SUBMITTED";
    /// <summary>Audit action when organization KYB is verified.</summary>
    public const string KybVerified = "KYB_VERIFIED";
    /// <summary>Audit action when organization KYB is rejected.</summary>
    public const string KybRejected = "KYB_REJECTED";

    // Organization Lifecycle & Memberships
    /// <summary>Audit action when an organization is suspended.</summary>
    public const string OrganizationSuspended = "ORGANIZATION_SUSPENDED";
    /// <summary>Audit action when an organization is reactivated.</summary>
    public const string OrganizationReactivated = "ORGANIZATION_REACTIVATED";
    /// <summary>Audit action when an organization membership is suspended.</summary>
    public const string MembershipSuspended = "MEMBERSHIP_SUSPENDED";
    /// <summary>Audit action when an organization membership is reactivated.</summary>
    public const string MembershipReactivated = "MEMBERSHIP_REACTIVATED";
    /// <summary>Audit action when an organization membership is terminated.</summary>
    public const string MembershipTerminated = "MEMBERSHIP_TERMINATED";

    // Departments, Roles, Salary Levels & Staff Management
    /// <summary>Audit action when a department is created.</summary>
    public const string DepartmentCreated = "DEPARTMENT_CREATED";
    /// <summary>Audit action when a department is updated.</summary>
    public const string DepartmentUpdated = "DEPARTMENT_UPDATED";
    /// <summary>Audit action when a department is deleted.</summary>
    public const string DepartmentDeleted = "DEPARTMENT_DELETED";
    /// <summary>Audit action when a workforce role is created.</summary>
    public const string RoleCreated = "ROLE_CREATED";
    /// <summary>Audit action when a workforce role is updated.</summary>
    public const string RoleUpdated = "ROLE_UPDATED";
    /// <summary>Audit action when a workforce role is deleted.</summary>
    public const string RoleDeleted = "ROLE_DELETED";
    /// <summary>Audit action when a salary level is created.</summary>
    public const string SalaryLevelCreated = "SALARY_LEVEL_CREATED";
    /// <summary>Audit action when a salary level is updated.</summary>
    public const string SalaryLevelUpdated = "SALARY_LEVEL_UPDATED";
    /// <summary>Audit action when a salary level is deleted.</summary>
    public const string SalaryLevelDeleted = "SALARY_LEVEL_DELETED";
    /// <summary>Audit action when a staff member is directly created.</summary>
    public const string StaffCreated = "STAFF_CREATED";
    /// <summary>Audit action when a staff member workforce structure is assigned or reassigned.</summary>
    public const string StaffAssigned = "STAFF_ASSIGNED";
    /// <summary>Audit action when a staff member is reactivated.</summary>
    public const string StaffReactivated = "STAFF_REACTIVATED";
    /// <summary>Audit action when a staff member is terminated.</summary>
    public const string StaffTerminated = "STAFF_TERMINATED";
    /// <summary>Audit action when bulk staff invitations are created.</summary>
    public const string StaffBulkInvited = "STAFF_BULK_INVITED";

    // Recruitment & Job Postings
    /// <summary>Audit action when a draft job posting is created.</summary>
    public const string JobPostingCreated = "JOB_POSTING_CREATED";
    /// <summary>Audit action when a job posting is updated.</summary>
    public const string JobPostingUpdated = "JOB_POSTING_UPDATED";
    /// <summary>Audit action when a job posting is published.</summary>
    public const string JobPostingPublished = "JOB_POSTING_PUBLISHED";
    /// <summary>Audit action when a job posting is closed.</summary>
    public const string JobPostingClosed = "JOB_POSTING_CLOSED";
    /// <summary>Audit action when a job posting is cancelled.</summary>
    public const string JobPostingCancelled = "JOB_POSTING_CANCELLED";
    /// <summary>Audit action when a candidate application is submitted.</summary>
    public const string ApplicationSubmitted = "APPLICATION_SUBMITTED";
    /// <summary>Audit action when an application status is marked under review.</summary>
    public const string ApplicationReviewed = "APPLICATION_REVIEWED";
    /// <summary>Audit action when an application is shortlisted.</summary>
    public const string ApplicationShortlisted = "APPLICATION_SHORTLISTED";
    /// <summary>Audit action when an application is rejected.</summary>
    public const string ApplicationRejected = "APPLICATION_REJECTED";
    /// <summary>Audit action when an application is accepted.</summary>
    public const string ApplicationAccepted = "APPLICATION_ACCEPTED";
    /// <summary>Audit action when an application is withdrawn by candidate.</summary>
    public const string ApplicationWithdrawn = "APPLICATION_WITHDRAWN";

    // Platform Administration & Permissions
    /// <summary>Audit action when an admin permission is granted.</summary>
    public const string AdminPermissionGranted = "ADMIN_PERMISSION_GRANTED";
    /// <summary>Audit action when an admin permission is revoked.</summary>
    public const string AdminPermissionRevoked = "ADMIN_PERMISSION_REVOKED";
    /// <summary>Audit action when an admin status changes.</summary>
    public const string AdminStatusChanged = "ADMIN_STATUS_CHANGED";
    /// <summary>Audit action when an admin is invited.</summary>
    public const string AdminInvited = "ADMIN_INVITED";
    /// <summary>Audit action when an admin is deleted.</summary>
    public const string AdminDeleted = "ADMIN_DELETED";

    // Fee Policies
    /// <summary>Audit action when a fee policy is created.</summary>
    public const string FeePolicyCreated = "FEE_POLICY_CREATED";
    /// <summary>Audit action when a fee policy is activated.</summary>
    public const string FeePolicyActivated = "FEE_POLICY_ACTIVATED";
    /// <summary>Audit action when a fee policy is deactivated.</summary>
    public const string FeePolicyDeactivated = "FEE_POLICY_DEACTIVATED";
    /// <summary>Audit action when a bank transfer fee policy is created.</summary>
    public const string BankTransferFeePolicyCreated = "BANK_TRANSFER_FEE_POLICY_CREATED";
    /// <summary>Audit action when a bank transfer fee policy is activated.</summary>
    public const string BankTransferFeePolicyActivated = "BANK_TRANSFER_FEE_POLICY_ACTIVATED";
    /// <summary>Audit action when a bank transfer fee policy is deactivated.</summary>
    public const string BankTransferFeePolicyDeactivated = "BANK_TRANSFER_FEE_POLICY_DEACTIVATED";

    // Transfers & Financial Operations
    /// <summary>Audit action when a peer wallet transfer completes.</summary>
    public const string PeerTransferCompleted = "PEER_TRANSFER_COMPLETED";
    /// <summary>Audit action when a peer wallet transfer is reversed.</summary>
    public const string PeerTransferReversed = "PEER_TRANSFER_REVERSED";
    /// <summary>Audit action when a bank transfer is created (immediate debit committed in PENDING status).</summary>
    public const string BankTransferCreated = "BANK_TRANSFER_CREATED";
    /// <summary>Audit action when a bank transfer is marked processing.</summary>
    public const string BankTransferProcessing = "BANK_TRANSFER_PROCESSING";
    /// <summary>Audit action when a bank transfer is confirmed completed.</summary>
    public const string BankTransferCompleted = "BANK_TRANSFER_COMPLETED";
    /// <summary>Audit action when a bank transfer definitively fails.</summary>
    public const string BankTransferFailed = "BANK_TRANSFER_FAILED";
    /// <summary>Audit action when a bank transfer status is marked unknown.</summary>
    public const string BankTransferUnknown = "BANK_TRANSFER_UNKNOWN";
    /// <summary>Audit action when a bank transfer is reversed and funds are refunded to sender.</summary>
    public const string BankTransferReversed = "BANK_TRANSFER_REVERSED";

    // Payments & Provider Attempts
    /// <summary>Audit action when a payment provider attempt is created.</summary>
    public const string PaymentAttemptCreated = "PAYMENT_ATTEMPT_CREATED";
    /// <summary>Audit action when a payment provider attempt begins processing.</summary>
    public const string PaymentAttemptProcessing = "PAYMENT_ATTEMPT_PROCESSING";
    /// <summary>Audit action when a payment provider attempt succeeds.</summary>
    public const string PaymentAttemptSucceeded = "PAYMENT_ATTEMPT_SUCCEEDED";
    /// <summary>Audit action when a payment provider attempt fails.</summary>
    public const string PaymentAttemptFailed = "PAYMENT_ATTEMPT_FAILED";
    /// <summary>Audit action when a payment provider attempt outcome is unknown.</summary>
    public const string PaymentAttemptUnknown = "PAYMENT_ATTEMPT_UNKNOWN";
    /// <summary>Audit action when a payment provider attempt is cancelled.</summary>
    public const string PaymentAttemptCancelled = "PAYMENT_ATTEMPT_CANCELLED";

    // Webhooks & Reconciliation
    /// <summary>Audit action when a provider webhook is received.</summary>
    public const string WebhookReceived = "WEBHOOK_RECEIVED";
    /// <summary>Audit action when a provider webhook is processed.</summary>
    public const string WebhookProcessed = "WEBHOOK_PROCESSED";
    /// <summary>Audit action when a provider webhook is rejected (invalid signature/payload).</summary>
    public const string WebhookRejected = "WEBHOOK_REJECTED";
    /// <summary>Audit action when a duplicate provider webhook is safely acknowledged.</summary>
    public const string WebhookDuplicate = "WEBHOOK_DUPLICATE";
    /// <summary>Audit action when a payment attempt is reconciled via webhook or query.</summary>
    public const string PaymentAttemptReconciled = "PAYMENT_ATTEMPT_RECONCILED";

    // Provider Failover
    /// <summary>Audit action when provider failover is initiated.</summary>
    public const string ProviderFailoverInitiated = "PROVIDER_FAILOVER_INITIATED";
    /// <summary>Audit action when provider failover succeeds.</summary>
    public const string ProviderFailoverSucceeded = "PROVIDER_FAILOVER_SUCCEEDED";
    /// <summary>Audit action when provider failover fails.</summary>
    public const string ProviderFailoverFailed = "PROVIDER_FAILOVER_FAILED";

    // Virtual Accounts & Inbound Funding
    /// <summary>Audit action when a dedicated virtual account is provisioned.</summary>
    public const string VirtualAccountCreated = "VIRTUAL_ACCOUNT_CREATED";
    /// <summary>Audit action when a virtual account is activated.</summary>
    public const string VirtualAccountActivated = "VIRTUAL_ACCOUNT_ACTIVATED";
    /// <summary>Audit action when a virtual account is suspended.</summary>
    public const string VirtualAccountSuspended = "VIRTUAL_ACCOUNT_SUSPENDED";
    /// <summary>Audit action when an inbound funding deposit is received and credited.</summary>
    public const string FundingReceived = "FUNDING_RECEIVED";
    /// <summary>Audit action when card funding is initialized.</summary>
    public const string CardFundingInitiated = "CARD_FUNDING_INITIATED";
    /// <summary>Audit action when card funding is completed and credited.</summary>
    public const string CardFundingCompleted = "CARD_FUNDING_COMPLETED";
    /// <summary>Audit action when card funding fails.</summary>
    public const string CardFundingFailed = "CARD_FUNDING_FAILED";

    // Payroll & Payment Vouchers
    /// <summary>Audit action when a payroll batch is created.</summary>
    public const string PayrollCreated = "PAYROLL_CREATED";
    /// <summary>Audit action when payroll execution starts.</summary>
    public const string PayrollStarted = "PAYROLL_STARTED";
    /// <summary>Audit action when a payroll batch is fully completed.</summary>
    public const string PayrollCompleted = "PAYROLL_COMPLETED";
    /// <summary>Audit action when a payroll batch partially completes.</summary>
    public const string PayrollPartiallyCompleted = "PAYROLL_PARTIALLY_COMPLETED";
    /// <summary>Audit action when a payroll batch fails completely.</summary>
    public const string PayrollFailed = "PAYROLL_FAILED";
    /// <summary>Audit action when a payroll batch is cancelled.</summary>
    public const string PayrollCancelled = "PAYROLL_CANCELLED";
    /// <summary>Audit action when a payroll item starts execution.</summary>
    public const string PayrollItemStarted = "PAYROLL_ITEM_STARTED";
    /// <summary>Audit action when a payroll item completes successfully.</summary>
    public const string PayrollItemCompleted = "PAYROLL_ITEM_COMPLETED";
    /// <summary>Audit action when a payroll item fails.</summary>
    public const string PayrollItemFailed = "PAYROLL_ITEM_FAILED";
    /// <summary>Audit action when a payroll item is retried.</summary>
    public const string PayrollItemRetried = "PAYROLL_ITEM_RETRIED";
    /// <summary>Audit action when a payment voucher is created.</summary>
    public const string PaymentVoucherCreated = "PAYMENT_VOUCHER_CREATED";
    /// <summary>Audit action when payment voucher metadata is updated.</summary>
    public const string PaymentVoucherMetadataUpdated = "PAYMENT_VOUCHER_METADATA_UPDATED";

    // Credit & Loans
    /// <summary>Audit action when a corporate loan plan is created.</summary>
    public const string LoanPlanCreated = "LOAN_PLAN_CREATED";
    /// <summary>Audit action when a corporate loan plan is updated.</summary>
    public const string LoanPlanUpdated = "LOAN_PLAN_UPDATED";
    /// <summary>Audit action when a staff loan application is submitted.</summary>
    public const string LoanApplicationSubmitted = "LOAN_APPLICATION_SUBMITTED";
    /// <summary>Audit action when a loan application is placed under review.</summary>
    public const string LoanApplicationUnderReview = "LOAN_APPLICATION_UNDER_REVIEW";
    /// <summary>Audit action when a loan application is approved.</summary>
    public const string LoanApplicationApproved = "LOAN_APPLICATION_APPROVED";
    /// <summary>Audit action when a loan application is declined.</summary>
    public const string LoanApplicationDeclined = "LOAN_APPLICATION_DECLINED";
    /// <summary>Audit action when a loan application is cancelled.</summary>
    public const string LoanApplicationCancelled = "LOAN_APPLICATION_CANCELLED";
    /// <summary>Audit action when a loan contract is created.</summary>
    public const string LoanContractCreated = "LOAN_CONTRACT_CREATED";
    /// <summary>Audit action when a loan is disbursed to employee wallet.</summary>
    public const string LoanDisbursed = "LOAN_DISBURSED";
    /// <summary>Audit action when loan repayment schedule is generated.</summary>
    public const string LoanRepaymentScheduled = "LOAN_REPAYMENT_SCHEDULED";
    /// <summary>Audit action when a loan repayment installment is paid.</summary>
    public const string LoanRepaymentPaid = "LOAN_REPAYMENT_PAID";
    /// <summary>Audit action when a loan repayment installment is missed.</summary>
    public const string LoanRepaymentMissed = "LOAN_REPAYMENT_MISSED";
    /// <summary>Audit action when a payroll loan is converted to a standard individual loan upon staff offboarding.</summary>
    public const string LoanConvertedToIndividual = "LOAN_CONVERTED_TO_INDIVIDUAL";

    // Savings
    /// <summary>Audit action when a savings plan is created.</summary>
    public const string SavingsPlanCreated = "SAVINGS_PLAN_CREATED";
    /// <summary>Audit action when a savings account/instance is created.</summary>
    public const string SavingsAccountCreated = "SAVINGS_ACCOUNT_CREATED";
    /// <summary>Audit action when a savings contribution is made.</summary>
    public const string SavingsContributionMade = "SAVINGS_CONTRIBUTION_MADE";
    /// <summary>Audit action when savings daily interest is accrued.</summary>
    public const string SavingsInterestAccrued = "SAVINGS_INTEREST_ACCRUED";
    /// <summary>Audit action when a savings withdrawal is executed at maturity.</summary>
    public const string SavingsWithdrawal = "SAVINGS_WITHDRAWAL";
    /// <summary>Audit action when an early savings withdrawal is executed with penalty.</summary>
    public const string SavingsEarlyWithdrawal = "SAVINGS_EARLY_WITHDRAWAL";
    /// <summary>Audit action when a savings account reaches maturity.</summary>
    public const string SavingsPlanMatured = "SAVINGS_PLAN_MATURED";
    /// <summary>Audit action when a savings interest policy is created.</summary>
    public const string SavingsInterestPolicyCreated = "SAVINGS_INTEREST_POLICY_CREATED";
    /// <summary>Audit action when a savings interest policy is activated.</summary>
    public const string SavingsInterestPolicyActivated = "SAVINGS_INTEREST_POLICY_ACTIVATED";
    /// <summary>Audit action when a savings interest policy is deactivated.</summary>
    public const string SavingsInterestPolicyDeactivated = "SAVINGS_INTEREST_POLICY_DEACTIVATED";

    // Thrift / Ajo / Esusu
    /// <summary>Audit action when a thrift group is created.</summary>
    public const string ThriftCreated = "THRIFT_CREATED";
    /// <summary>Audit action when a member is invited to a thrift group.</summary>
    public const string ThriftMemberInvited = "THRIFT_MEMBER_INVITED";
    /// <summary>Audit action when a member joins a thrift group.</summary>
    public const string ThriftMemberJoined = "THRIFT_MEMBER_JOINED";
    /// <summary>Audit action when a thrift member selects a payout position.</summary>
    public const string ThriftPositionSelected = "THRIFT_POSITION_SELECTED";
    /// <summary>Audit action when thrift payout positions are locked.</summary>
    public const string ThriftPositionsLocked = "THRIFT_POSITIONS_LOCKED";
    /// <summary>Audit action when a thrift cycle is started.</summary>
    public const string ThriftCycleStarted = "THRIFT_CYCLE_STARTED";
    /// <summary>Audit action when a thrift contribution is successfully collected.</summary>
    public const string ThriftContributionCollected = "THRIFT_CONTRIBUTION_COLLECTED";
    /// <summary>Audit action when a thrift contribution is missed.</summary>
    public const string ThriftContributionMissed = "THRIFT_CONTRIBUTION_MISSED";
    /// <summary>Audit action when a thrift cycle payout is completed.</summary>
    public const string ThriftPayoutCompleted = "THRIFT_PAYOUT_COMPLETED";
    /// <summary>Audit action when a thrift member's payout is suspended due to consecutive misses.</summary>
    public const string ThriftPayoutSuspended = "THRIFT_PAYOUT_SUSPENDED";
    /// <summary>Audit action when a departing thrift member is reimbursed net contributions.</summary>
    public const string ThriftMemberReimbursed = "THRIFT_MEMBER_REIMBURSED";
    /// <summary>Audit action when a member is removed from a thrift group.</summary>
    public const string ThriftMemberRemoved = "THRIFT_MEMBER_REMOVED";

    // Value-Added Services (VAS) Audit Actions
    /// <summary>Audit action when a VAS purchase transaction is initialized.</summary>
    public const string VasPurchaseCreated = "VAS_PURCHASE_CREATED";
    /// <summary>Audit action when a VAS purchase begins active gateway processing.</summary>
    public const string VasPurchaseProcessing = "VAS_PURCHASE_PROCESSING";
    /// <summary>Audit action when a VAS purchase completes successfully.</summary>
    public const string VasPurchaseSucceeded = "VAS_PURCHASE_SUCCEEDED";
    /// <summary>Audit action when a VAS purchase definitively fails.</summary>
    public const string VasPurchaseFailed = "VAS_PURCHASE_FAILED";
    /// <summary>Audit action when a failed VAS purchase is financially reversed.</summary>
    public const string VasPurchaseReversed = "VAS_PURCHASE_REVERSED";
    /// <summary>Audit action when a VAS transaction is resolved via background reconciliation.</summary>
    public const string VasPurchaseReconciled = "VAS_PURCHASE_RECONCILED";

    // ERP Inventory, Services, Suppliers & Customers
    /// <summary>Audit action when an inventory item is created.</summary>
    public const string InventoryItemCreated = "INVENTORY_ITEM_CREATED";
    /// <summary>Audit action when an inventory item is updated.</summary>
    public const string InventoryItemUpdated = "INVENTORY_ITEM_UPDATED";
    /// <summary>Audit action when an inventory item is deactivated or soft-deleted.</summary>
    public const string InventoryItemDeactivated = "INVENTORY_ITEM_DEACTIVATED";
    /// <summary>Audit action when incoming stock is received.</summary>
    public const string StockReceived = "STOCK_RECEIVED";
    /// <summary>Audit action when stock is issued out.</summary>
    public const string StockIssued = "STOCK_ISSUED";
    /// <summary>Audit action when stock quantity is manually adjusted.</summary>
    public const string StockAdjusted = "STOCK_ADJUSTED";
    /// <summary>Audit action when an inventory valuation policy is initially created.</summary>
    public const string InventoryValuationPolicyCreated = "INVENTORY_VALUATION_POLICY_CREATED";
    /// <summary>Audit action when an organization's active inventory valuation policy is changed (WAC / FIFO).</summary>
    public const string InventoryValuationPolicyChanged = "INVENTORY_VALUATION_POLICY_CHANGED";
    /// <summary>Audit action when an ERP service is created.</summary>
    public const string ServiceCreated = "SERVICE_CREATED";
    /// <summary>Audit action when an ERP service is updated.</summary>
    public const string ServiceUpdated = "SERVICE_UPDATED";
    /// <summary>Audit action when an ERP service is deactivated or deleted.</summary>
    public const string ServiceDeleted = "SERVICE_DELETED";
    /// <summary>Audit action when a supplier is created.</summary>
    public const string SupplierCreated = "SUPPLIER_CREATED";
    /// <summary>Audit action when a supplier is updated.</summary>
    public const string SupplierUpdated = "SUPPLIER_UPDATED";
    /// <summary>Audit action when a supplier is deactivated or deleted.</summary>
    public const string SupplierDeleted = "SUPPLIER_DELETED";
    /// <summary>Audit action when a customer is created.</summary>
    public const string CustomerCreated = "CUSTOMER_CREATED";
    /// <summary>Audit action when a customer is updated.</summary>
    public const string CustomerUpdated = "CUSTOMER_UPDATED";
    /// <summary>Audit action when a customer is deactivated or deleted.</summary>
    public const string CustomerDeleted = "CUSTOMER_DELETED";
}
