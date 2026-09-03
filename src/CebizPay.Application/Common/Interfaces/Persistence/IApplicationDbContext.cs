using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Payments.Entities;

namespace CebizPay.Application.Common.Interfaces.Persistence;

/// <summary>
/// Application database context contract for querying and persisting domain aggregates.
/// Adheres to Clean Architecture with zero EF Core dependencies in the Application layer.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>Gets the individual profiles entity set.</summary>
    IEntitySet<IndividualProfile> IndividualProfiles { get; }

    /// <summary>Gets the admin profiles entity set.</summary>
    IEntitySet<AdminProfile> AdminProfiles { get; }

    /// <summary>Gets the admin invitations entity set.</summary>
    IEntitySet<AdminInvitation> AdminInvitations { get; }

    /// <summary>Gets the refresh tokens entity set.</summary>
    IEntitySet<RefreshToken> RefreshTokens { get; }

    /// <summary>Gets the organizations entity set.</summary>
    IEntitySet<Organization> Organizations { get; }

    /// <summary>Gets the organization memberships entity set.</summary>
    IEntitySet<OrganizationMembership> OrganizationMemberships { get; }

    /// <summary>Gets the departments entity set.</summary>
    IEntitySet<Department> Departments { get; }

    /// <summary>Gets the workforce roles entity set.</summary>
    IEntitySet<WorkforceRole> WorkforceRoles { get; }

    /// <summary>Gets the salary levels entity set.</summary>
    IEntitySet<SalaryLevel> SalaryLevels { get; }

    /// <summary>Gets the staff invitations entity set.</summary>
    IEntitySet<StaffInvitation> StaffInvitations { get; }

    /// <summary>Gets the job postings entity set.</summary>
    IEntitySet<JobPosting> JobPostings { get; }

    /// <summary>Gets the recruitment applications entity set.</summary>
    IEntitySet<RecruitmentApplication> RecruitmentApplications { get; }

    /// <summary>Gets the inventory items entity set.</summary>
    IEntitySet<InventoryItem> InventoryItems { get; }

    /// <summary>Gets the inventory valuation policies entity set.</summary>
    IEntitySet<InventoryValuationPolicy> InventoryValuationPolicies { get; }

    /// <summary>Gets the inventory cost layers entity set.</summary>
    IEntitySet<InventoryCostLayer> InventoryCostLayers { get; }

    /// <summary>Gets the stock movements entity set.</summary>
    IEntitySet<StockMovement> StockMovements { get; }

    /// <summary>Gets the ERP services entity set.</summary>
    IEntitySet<ErpService> ErpServices { get; }

    /// <summary>Gets the suppliers entity set.</summary>
    IEntitySet<Supplier> Suppliers { get; }

    /// <summary>Gets the customers entity set.</summary>
    IEntitySet<Customer> Customers { get; }

    /// <summary>Gets the purchase orders entity set.</summary>
    IEntitySet<PurchaseOrder> PurchaseOrders { get; }

    /// <summary>Gets the purchase order items entity set.</summary>
    IEntitySet<PurchaseOrderItem> PurchaseOrderItems { get; }

    /// <summary>Gets the sales orders entity set.</summary>
    IEntitySet<SalesOrder> SalesOrders { get; }

    /// <summary>Gets the sales order items entity set.</summary>
    IEntitySet<SalesOrderItem> SalesOrderItems { get; }

    /// <summary>Gets the operating expenses entity set.</summary>
    IEntitySet<OperatingExpense> OperatingExpenses { get; }

    /// <summary>Gets the ERP invoices entity set.</summary>
    IEntitySet<ErpInvoice> ErpInvoices { get; }

    /// <summary>Gets the ERP invoice items entity set.</summary>
    IEntitySet<ErpInvoiceItem> ErpInvoiceItems { get; }

    /// <summary>Gets the ERP receipts entity set.</summary>
    IEntitySet<ErpReceipt> ErpReceipts { get; }

    /// <summary>Gets the ERP company disbursement vouchers entity set.</summary>
    IEntitySet<CompanyVoucher> CompanyVouchers { get; }

    /// <summary>Gets the KYC documents entity set.</summary>
    IEntitySet<KycDocument> KycDocuments { get; }

    /// <summary>Gets the KYB details entity set.</summary>
    IEntitySet<KybDetail> KybDetails { get; }

    /// <summary>Gets the audit logs entity set.</summary>
    IEntitySet<AuditLog> AuditLogs { get; }

    /// <summary>Gets the MFA challenges entity set.</summary>
    IEntitySet<MfaChallenge> MfaChallenges { get; }

    /// <summary>Gets the wallets entity set.</summary>
    IEntitySet<Wallet> Wallets { get; }

    /// <summary>Gets the ledger accounts entity set.</summary>
    IEntitySet<LedgerAccount> LedgerAccounts { get; }

    /// <summary>Gets the ledger transactions entity set.</summary>
    IEntitySet<LedgerTransaction> LedgerTransactions { get; }

    /// <summary>Gets the ledger entries entity set.</summary>
    IEntitySet<LedgerEntry> LedgerEntries { get; }

    /// <summary>Gets the FX conversions entity set.</summary>
    IEntitySet<FxConversion> FxConversions { get; }

    /// <summary>Gets the idempotency records entity set.</summary>
    IEntitySet<IdempotencyRecord> IdempotencyRecords { get; }

    /// <summary>Gets the peer-transfer fee policies entity set.</summary>
    IEntitySet<PeerTransferFeePolicy> PeerTransferFeePolicies { get; }

    /// <summary>Gets the bank transfers entity set.</summary>
    IEntitySet<BankTransfer> BankTransfers { get; }

    /// <summary>Gets the bank-transfer fee policies entity set.</summary>
    IEntitySet<BankTransferFeePolicy> BankTransferFeePolicies { get; }

    /// <summary>Gets the payment provider attempts entity set.</summary>
    IEntitySet<PaymentAttempt> PaymentAttempts { get; }

    /// <summary>Gets the provider webhook events entity set.</summary>
    IEntitySet<WebhookEvent> WebhookEvents { get; }

    /// <summary>Gets the dedicated virtual accounts entity set.</summary>
    IEntitySet<VirtualAccount> VirtualAccounts { get; }

    /// <summary>Gets the external funding accounts entity set.</summary>
    IEntitySet<ExternalFundingAccount> ExternalFundingAccounts { get; }

    /// <summary>Gets the platform fee policies entity set.</summary>
    IEntitySet<PlatformFeePolicy> PlatformFeePolicies { get; }

    /// <summary>Gets the funding transactions entity set.</summary>
    IEntitySet<FundingTransaction> FundingTransactions { get; }

    /// <summary>Gets the tokenized saved cards entity set.</summary>
    IEntitySet<SavedCard> SavedCards { get; }

    /// <summary>Gets the card refunds entity set.</summary>
    IEntitySet<CardRefund> CardRefunds { get; }

    /// <summary>Gets the reconciliation records entity set.</summary>
    IEntitySet<ReconciliationRecord> ReconciliationRecords { get; }

    /// <summary>Gets the recovery outstanding records entity set.</summary>
    IEntitySet<RecoveryOutstandingRecord> RecoveryOutstandingRecords { get; }

    /// <summary>Gets the card verifications entity set.</summary>
    IEntitySet<CardVerification> CardVerifications { get; }

    /// <summary>Gets the payroll batches entity set.</summary>
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollBatch> PayrollBatches { get; }

    /// <summary>Gets the payroll line items entity set.</summary>
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollItem> PayrollItems { get; }

    /// <summary>Gets the payroll execution attempts entity set.</summary>
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt> PayrollExecutionAttempts { get; }

    /// <summary>Gets the payment vouchers entity set.</summary>
    IEntitySet<CebizPay.Domain.Payroll.Entities.PaymentVoucher> PaymentVouchers { get; }

    /// <summary>Gets the corporate loan plans entity set.</summary>
    IEntitySet<CebizPay.Domain.Loans.Entities.CorporateLoanPlan> CorporateLoanPlans { get; }

    /// <summary>Gets the staff loan applications entity set.</summary>
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanApplication> LoanApplications { get; }

    /// <summary>Gets the loan contracts entity set.</summary>
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanContract> LoanContracts { get; }

    /// <summary>Gets the loan repayment schedule items entity set.</summary>
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem> LoanRepaymentScheduleItems { get; }

    /// <summary>Gets the standard individual loan policies entity set.</summary>
    IEntitySet<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy> StandardIndividualLoanPolicies { get; }

    /// <summary>Gets the savings interest policies entity set.</summary>
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsInterestPolicy> SavingsInterestPolicies { get; }

    /// <summary>Gets the savings plans entity set.</summary>
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsPlan> SavingsPlans { get; }

    /// <summary>Gets the savings accounts entity set.</summary>
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsAccount> SavingsAccounts { get; }

    /// <summary>Gets the savings contributions entity set.</summary>
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsContribution> SavingsContributions { get; }

    /// <summary>Gets the savings interest accruals entity set.</summary>
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsInterestAccrual> SavingsInterestAccruals { get; }

    /// <summary>Gets the thrift groups entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftGroup> ThriftGroups { get; }

    /// <summary>Gets the thrift members entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftMember> ThriftMembers { get; }

    /// <summary>Gets the thrift invitations entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftInvitation> ThriftInvitations { get; }

    /// <summary>Gets the thrift cycles entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftCycle> ThriftCycles { get; }

    /// <summary>Gets the thrift contributions entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftContribution> ThriftContributions { get; }

    /// <summary>Gets the thrift payouts entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftPayout> ThriftPayouts { get; }

    /// <summary>Gets the thrift reimbursements entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftReimbursement> ThriftReimbursements { get; }

    /// <summary>Gets the thrift disputes entity set.</summary>
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftDispute> ThriftDisputes { get; }

    /// <summary>Gets the VAS transactions entity set.</summary>
    IEntitySet<CebizPay.Domain.Vas.Entities.VasTransaction> VasTransactions { get; }

    /// <summary>Gets the compliance verification operations entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.VerificationOperation> VerificationOperations { get; }

    /// <summary>Gets the compliance verification evidence entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.VerificationEvidence> VerificationEvidences { get; }

    /// <summary>Gets the compliance webhook events entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.ComplianceWebhookEvent> ComplianceWebhookEvents { get; }

    /// <summary>Gets the compliance risk assessments entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.RiskAssessment> RiskAssessments { get; }

    /// <summary>Gets the compliance risk factor results entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.RiskFactorResult> RiskFactorResults { get; }

    /// <summary>Gets the customer due diligence profiles entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.CddProfile> CddProfiles { get; }

    /// <summary>Gets the enhanced due diligence cases entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.EddCase> EddCases { get; }

    /// <summary>Gets the compliance decisions entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.ComplianceDecision> ComplianceDecisions { get; }

    /// <summary>Gets the compliance restrictions entity set.</summary>
    IEntitySet<CebizPay.Domain.Compliance.Entities.ComplianceRestriction> ComplianceRestrictions { get; }

    /// <summary>Gets the announcements entity set.</summary>
    IEntitySet<CebizPay.Domain.Communication.Entities.Announcement> Announcements { get; }

    /// <summary>Gets the in-app notifications entity set.</summary>
    IEntitySet<CebizPay.Domain.Communication.Entities.InAppNotification> InAppNotifications { get; }

    /// <summary>Gets the device tokens entity set.</summary>
    IEntitySet<CebizPay.Domain.Communication.Entities.DeviceToken> DeviceTokens { get; }

    /// <summary>Gets the user notification preferences entity set.</summary>
    IEntitySet<CebizPay.Domain.Communication.Entities.UserNotificationPreference> UserNotificationPreferences { get; }

    /// <summary>Gets the notification delivery records entity set.</summary>
    IEntitySet<CebizPay.Domain.Communication.Entities.NotificationDeliveryRecord> NotificationDeliveryRecords { get; }

    /// <summary>Gets the referral settings entity set.</summary>
    IEntitySet<CebizPay.Domain.Referrals.Entities.ReferralSetting> ReferralSettings { get; }

    /// <summary>Gets the referral codes entity set.</summary>
    IEntitySet<CebizPay.Domain.Referrals.Entities.ReferralCode> ReferralCodes { get; }

    /// <summary>Gets the referral relationships entity set.</summary>
    IEntitySet<CebizPay.Domain.Referrals.Entities.ReferralRelationship> ReferralRelationships { get; }

    /// <summary>Gets the referral rewards entity set.</summary>
    IEntitySet<CebizPay.Domain.Referrals.Entities.ReferralReward> ReferralRewards { get; }

    /// <summary>Gets the customer support tickets entity set.</summary>
    IEntitySet<CebizPay.Domain.Support.Entities.SupportTicket> SupportTickets { get; }

    /// <summary>Gets the ticket thread messages entity set.</summary>
    IEntitySet<CebizPay.Domain.Support.Entities.TicketMessage> TicketMessages { get; }

    /// <summary>
    /// Saves changes asynchronously to the underlying database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction. The caller is responsible for committing or rolling back.
    /// </summary>
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
