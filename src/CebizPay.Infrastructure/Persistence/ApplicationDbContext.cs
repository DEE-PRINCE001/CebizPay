using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Represents the primary database context for the application, including Identity, Outbox, and Phase 1A/2A entities.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole, string>, IApplicationDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for configuring the database context.</param>
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets or sets the outbox messages entity set.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Gets the individual profiles entity set.</summary>
    public DbSet<IndividualProfile> IndividualProfiles => Set<IndividualProfile>();

    /// <summary>Gets the admin profiles entity set.</summary>
    public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();

    /// <summary>Gets the organizations entity set.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>Gets the organization memberships entity set.</summary>
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    /// <summary>Gets the departments entity set.</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>Gets the workforce roles entity set.</summary>
    public DbSet<WorkforceRole> WorkforceRoles => Set<WorkforceRole>();

    /// <summary>Gets the salary levels entity set.</summary>
    public DbSet<SalaryLevel> SalaryLevels => Set<SalaryLevel>();

    /// <summary>Gets the staff invitations entity set.</summary>
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

    /// <summary>Gets the job postings entity set.</summary>
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();

    /// <summary>Gets the recruitment applications entity set.</summary>
    public DbSet<RecruitmentApplication> RecruitmentApplications => Set<RecruitmentApplication>();

    /// <summary>Gets the inventory items entity set.</summary>
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    /// <summary>Gets the inventory valuation policies entity set.</summary>
    public DbSet<InventoryValuationPolicy> InventoryValuationPolicies => Set<InventoryValuationPolicy>();

    /// <summary>Gets the inventory cost layers entity set.</summary>
    public DbSet<InventoryCostLayer> InventoryCostLayers => Set<InventoryCostLayer>();

    /// <summary>Gets the stock movements entity set.</summary>
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    /// <summary>Gets the ERP services entity set.</summary>
    public DbSet<ErpService> ErpServices => Set<ErpService>();

    /// <summary>Gets the suppliers entity set.</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>Gets the customers entity set.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Gets the purchase orders entity set.</summary>
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    /// <summary>Gets the purchase order items entity set.</summary>
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    /// <summary>Gets the sales orders entity set.</summary>
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    /// <summary>Gets the sales order items entity set.</summary>
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

    /// <summary>Gets the operating expenses entity set.</summary>
    public DbSet<OperatingExpense> OperatingExpenses => Set<OperatingExpense>();

    /// <summary>Gets the ERP invoices entity set.</summary>
    public DbSet<ErpInvoice> ErpInvoices => Set<ErpInvoice>();

    /// <summary>Gets the ERP invoice items entity set.</summary>
    public DbSet<ErpInvoiceItem> ErpInvoiceItems => Set<ErpInvoiceItem>();

    /// <summary>Gets the ERP receipts entity set.</summary>
    public DbSet<ErpReceipt> ErpReceipts => Set<ErpReceipt>();

    /// <summary>Gets the ERP company disbursement vouchers entity set.</summary>
    public DbSet<CompanyVoucher> CompanyVouchers => Set<CompanyVoucher>();

    /// <summary>Gets the KYC documents entity set.</summary>
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();

    /// <summary>Gets the KYB details entity set.</summary>
    public DbSet<KybDetail> KybDetails => Set<KybDetail>();

    /// <summary>Gets the audit logs entity set.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Gets the MFA challenges entity set.</summary>
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();

    /// <summary>Gets the wallets entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.Wallet> Wallets => Set<CebizPay.Domain.Finance.Entities.Wallet>();

    /// <summary>Gets the ledger accounts entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.LedgerAccount> LedgerAccounts => Set<CebizPay.Domain.Finance.Entities.LedgerAccount>();

    /// <summary>Gets the ledger transactions entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.LedgerTransaction> LedgerTransactions => Set<CebizPay.Domain.Finance.Entities.LedgerTransaction>();

    /// <summary>Gets the ledger entries entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.LedgerEntry> LedgerEntries => Set<CebizPay.Domain.Finance.Entities.LedgerEntry>();

    /// <summary>Gets the FX conversions entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.FxConversion> FxConversions => Set<CebizPay.Domain.Finance.Entities.FxConversion>();

    /// <summary>Gets the idempotency records entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.IdempotencyRecord> IdempotencyRecords => Set<CebizPay.Domain.Finance.Entities.IdempotencyRecord>();

    /// <summary>Gets the peer-transfer fee policies entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.PeerTransferFeePolicy> PeerTransferFeePolicies => Set<CebizPay.Domain.Finance.Entities.PeerTransferFeePolicy>();

    /// <summary>Gets the bank transfers entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.BankTransfer> BankTransfers => Set<CebizPay.Domain.Finance.Entities.BankTransfer>();

    /// <summary>Gets the bank-transfer fee policies entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.BankTransferFeePolicy> BankTransferFeePolicies => Set<CebizPay.Domain.Finance.Entities.BankTransferFeePolicy>();

    /// <summary>Gets the payment provider attempts entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.PaymentAttempt> PaymentAttempts => Set<CebizPay.Domain.Payments.Entities.PaymentAttempt>();

    /// <summary>Gets the provider webhook events entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.WebhookEvent> WebhookEvents => Set<CebizPay.Domain.Payments.Entities.WebhookEvent>();

    /// <summary>Gets the dedicated virtual accounts entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.VirtualAccount> VirtualAccounts => Set<CebizPay.Domain.Payments.Entities.VirtualAccount>();

    /// <summary>Gets the external funding accounts entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.ExternalFundingAccount> ExternalFundingAccounts => Set<CebizPay.Domain.Finance.Entities.ExternalFundingAccount>();

    /// <summary>Gets the platform fee policies entity set.</summary>
    public DbSet<CebizPay.Domain.Finance.Entities.PlatformFeePolicy> PlatformFeePolicies => Set<CebizPay.Domain.Finance.Entities.PlatformFeePolicy>();

    /// <summary>Gets the funding transactions entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.FundingTransaction> FundingTransactions => Set<CebizPay.Domain.Payments.Entities.FundingTransaction>();

    /// <summary>Gets the tokenized saved cards entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.SavedCard> SavedCards => Set<CebizPay.Domain.Payments.Entities.SavedCard>();

    /// <summary>Gets the card refunds entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.CardRefund> CardRefunds => Set<CebizPay.Domain.Payments.Entities.CardRefund>();

    /// <summary>Gets the card verifications entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.CardVerification> CardVerifications => Set<CebizPay.Domain.Payments.Entities.CardVerification>();

    /// <summary>Gets the payroll batches entity set.</summary>
    public DbSet<CebizPay.Domain.Payroll.Entities.PayrollBatch> PayrollBatches => Set<CebizPay.Domain.Payroll.Entities.PayrollBatch>();

    /// <summary>Gets the payroll line items entity set.</summary>
    public DbSet<CebizPay.Domain.Payroll.Entities.PayrollItem> PayrollItems => Set<CebizPay.Domain.Payroll.Entities.PayrollItem>();

    /// <summary>Gets the payroll execution attempts entity set.</summary>
    public DbSet<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt> PayrollExecutionAttempts => Set<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt>();

    /// <summary>Gets the payment vouchers entity set.</summary>
    public DbSet<CebizPay.Domain.Payroll.Entities.PaymentVoucher> PaymentVouchers => Set<CebizPay.Domain.Payroll.Entities.PaymentVoucher>();

    /// <summary>Gets the corporate loan plans entity set.</summary>
    public DbSet<CebizPay.Domain.Loans.Entities.CorporateLoanPlan> CorporateLoanPlans => Set<CebizPay.Domain.Loans.Entities.CorporateLoanPlan>();

    /// <summary>Gets the staff loan applications entity set.</summary>
    public DbSet<CebizPay.Domain.Loans.Entities.LoanApplication> LoanApplications => Set<CebizPay.Domain.Loans.Entities.LoanApplication>();

    /// <summary>Gets the loan contracts entity set.</summary>
    public DbSet<CebizPay.Domain.Loans.Entities.LoanContract> LoanContracts => Set<CebizPay.Domain.Loans.Entities.LoanContract>();

    /// <summary>Gets the loan repayment schedule items entity set.</summary>
    public DbSet<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem> LoanRepaymentScheduleItems => Set<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem>();

    /// <summary>Gets the standard individual loan policies entity set.</summary>
    public DbSet<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy> StandardIndividualLoanPolicies => Set<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy>();

    /// <summary>Gets the savings interest policies entity set.</summary>
    public DbSet<CebizPay.Domain.Savings.Entities.SavingsInterestPolicy> SavingsInterestPolicies => Set<CebizPay.Domain.Savings.Entities.SavingsInterestPolicy>();

    /// <summary>Gets the savings plans entity set.</summary>
    public DbSet<CebizPay.Domain.Savings.Entities.SavingsPlan> SavingsPlans => Set<CebizPay.Domain.Savings.Entities.SavingsPlan>();

    /// <summary>Gets the savings accounts entity set.</summary>
    public DbSet<CebizPay.Domain.Savings.Entities.SavingsAccount> SavingsAccounts => Set<CebizPay.Domain.Savings.Entities.SavingsAccount>();

    /// <summary>Gets the savings contributions entity set.</summary>
    public DbSet<CebizPay.Domain.Savings.Entities.SavingsContribution> SavingsContributions => Set<CebizPay.Domain.Savings.Entities.SavingsContribution>();

    /// <summary>Gets the savings interest accruals entity set.</summary>
    public DbSet<CebizPay.Domain.Savings.Entities.SavingsInterestAccrual> SavingsInterestAccruals => Set<CebizPay.Domain.Savings.Entities.SavingsInterestAccrual>();

    /// <summary>Gets the thrift groups entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftGroup> ThriftGroups => Set<CebizPay.Domain.Thrift.Entities.ThriftGroup>();

    /// <summary>Gets the thrift members entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftMember> ThriftMembers => Set<CebizPay.Domain.Thrift.Entities.ThriftMember>();

    /// <summary>Gets the thrift invitations entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftInvitation> ThriftInvitations => Set<CebizPay.Domain.Thrift.Entities.ThriftInvitation>();

    /// <summary>Gets the thrift cycles entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftCycle> ThriftCycles => Set<CebizPay.Domain.Thrift.Entities.ThriftCycle>();

    /// <summary>Gets the thrift contributions entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftContribution> ThriftContributions => Set<CebizPay.Domain.Thrift.Entities.ThriftContribution>();

    /// <summary>Gets the thrift payouts entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftPayout> ThriftPayouts => Set<CebizPay.Domain.Thrift.Entities.ThriftPayout>();

    /// <summary>Gets the thrift reimbursements entity set.</summary>
    public DbSet<CebizPay.Domain.Thrift.Entities.ThriftReimbursement> ThriftReimbursements => Set<CebizPay.Domain.Thrift.Entities.ThriftReimbursement>();

    /// <summary>Gets the VAS transactions entity set.</summary>
    public DbSet<CebizPay.Domain.Vas.Entities.VasTransaction> VasTransactions => Set<CebizPay.Domain.Vas.Entities.VasTransaction>();

    // Explicit IApplicationDbContext implementations returning IEntitySet<T>
    IEntitySet<IndividualProfile> IApplicationDbContext.IndividualProfiles => new EntitySet<IndividualProfile>(IndividualProfiles);
    IEntitySet<AdminProfile> IApplicationDbContext.AdminProfiles => new EntitySet<AdminProfile>(AdminProfiles);
    IEntitySet<Organization> IApplicationDbContext.Organizations => new EntitySet<Organization>(Organizations);
    IEntitySet<OrganizationMembership> IApplicationDbContext.OrganizationMemberships => new EntitySet<OrganizationMembership>(OrganizationMemberships);
    IEntitySet<Department> IApplicationDbContext.Departments => new EntitySet<Department>(Departments);
    IEntitySet<WorkforceRole> IApplicationDbContext.WorkforceRoles => new EntitySet<WorkforceRole>(WorkforceRoles);
    IEntitySet<SalaryLevel> IApplicationDbContext.SalaryLevels => new EntitySet<SalaryLevel>(SalaryLevels);
    IEntitySet<StaffInvitation> IApplicationDbContext.StaffInvitations => new EntitySet<StaffInvitation>(StaffInvitations);
    IEntitySet<JobPosting> IApplicationDbContext.JobPostings => new EntitySet<JobPosting>(JobPostings);
    IEntitySet<RecruitmentApplication> IApplicationDbContext.RecruitmentApplications => new EntitySet<RecruitmentApplication>(RecruitmentApplications);
    IEntitySet<InventoryItem> IApplicationDbContext.InventoryItems => new EntitySet<InventoryItem>(InventoryItems);
    IEntitySet<InventoryValuationPolicy> IApplicationDbContext.InventoryValuationPolicies => new EntitySet<InventoryValuationPolicy>(InventoryValuationPolicies);
    IEntitySet<InventoryCostLayer> IApplicationDbContext.InventoryCostLayers => new EntitySet<InventoryCostLayer>(InventoryCostLayers);
    IEntitySet<StockMovement> IApplicationDbContext.StockMovements => new EntitySet<StockMovement>(StockMovements);
    IEntitySet<ErpService> IApplicationDbContext.ErpServices => new EntitySet<ErpService>(ErpServices);
    IEntitySet<Supplier> IApplicationDbContext.Suppliers => new EntitySet<Supplier>(Suppliers);
    IEntitySet<Customer> IApplicationDbContext.Customers => new EntitySet<Customer>(Customers);
    IEntitySet<PurchaseOrder> IApplicationDbContext.PurchaseOrders => new EntitySet<PurchaseOrder>(PurchaseOrders);
    IEntitySet<PurchaseOrderItem> IApplicationDbContext.PurchaseOrderItems => new EntitySet<PurchaseOrderItem>(PurchaseOrderItems);
    IEntitySet<SalesOrder> IApplicationDbContext.SalesOrders => new EntitySet<SalesOrder>(SalesOrders);
    IEntitySet<SalesOrderItem> IApplicationDbContext.SalesOrderItems => new EntitySet<SalesOrderItem>(SalesOrderItems);
    IEntitySet<OperatingExpense> IApplicationDbContext.OperatingExpenses => new EntitySet<OperatingExpense>(OperatingExpenses);
    IEntitySet<ErpInvoice> IApplicationDbContext.ErpInvoices => new EntitySet<ErpInvoice>(ErpInvoices);
    IEntitySet<ErpInvoiceItem> IApplicationDbContext.ErpInvoiceItems => new EntitySet<ErpInvoiceItem>(ErpInvoiceItems);
    IEntitySet<ErpReceipt> IApplicationDbContext.ErpReceipts => new EntitySet<ErpReceipt>(ErpReceipts);
    IEntitySet<CompanyVoucher> IApplicationDbContext.CompanyVouchers => new EntitySet<CompanyVoucher>(CompanyVouchers);
    IEntitySet<KycDocument> IApplicationDbContext.KycDocuments => new EntitySet<KycDocument>(KycDocuments);
    IEntitySet<KybDetail> IApplicationDbContext.KybDetails => new EntitySet<KybDetail>(KybDetails);
    IEntitySet<AuditLog> IApplicationDbContext.AuditLogs => new EntitySet<AuditLog>(AuditLogs);
    IEntitySet<MfaChallenge> IApplicationDbContext.MfaChallenges => new EntitySet<MfaChallenge>(MfaChallenges);
    IEntitySet<CebizPay.Domain.Finance.Entities.Wallet> IApplicationDbContext.Wallets => new EntitySet<CebizPay.Domain.Finance.Entities.Wallet>(Wallets);
    IEntitySet<CebizPay.Domain.Finance.Entities.LedgerAccount> IApplicationDbContext.LedgerAccounts => new EntitySet<CebizPay.Domain.Finance.Entities.LedgerAccount>(LedgerAccounts);
    IEntitySet<CebizPay.Domain.Finance.Entities.LedgerTransaction> IApplicationDbContext.LedgerTransactions => new EntitySet<CebizPay.Domain.Finance.Entities.LedgerTransaction>(LedgerTransactions);
    IEntitySet<CebizPay.Domain.Finance.Entities.LedgerEntry> IApplicationDbContext.LedgerEntries => new EntitySet<CebizPay.Domain.Finance.Entities.LedgerEntry>(LedgerEntries);
    IEntitySet<CebizPay.Domain.Finance.Entities.FxConversion> IApplicationDbContext.FxConversions => new EntitySet<CebizPay.Domain.Finance.Entities.FxConversion>(FxConversions);
    IEntitySet<CebizPay.Domain.Finance.Entities.IdempotencyRecord> IApplicationDbContext.IdempotencyRecords => new EntitySet<CebizPay.Domain.Finance.Entities.IdempotencyRecord>(IdempotencyRecords);
    IEntitySet<CebizPay.Domain.Finance.Entities.PeerTransferFeePolicy> IApplicationDbContext.PeerTransferFeePolicies => new EntitySet<CebizPay.Domain.Finance.Entities.PeerTransferFeePolicy>(PeerTransferFeePolicies);
    IEntitySet<CebizPay.Domain.Finance.Entities.BankTransfer> IApplicationDbContext.BankTransfers => new EntitySet<CebizPay.Domain.Finance.Entities.BankTransfer>(BankTransfers);
    IEntitySet<CebizPay.Domain.Finance.Entities.BankTransferFeePolicy> IApplicationDbContext.BankTransferFeePolicies => new EntitySet<CebizPay.Domain.Finance.Entities.BankTransferFeePolicy>(BankTransferFeePolicies);
    IEntitySet<CebizPay.Domain.Payments.Entities.PaymentAttempt> IApplicationDbContext.PaymentAttempts => new EntitySet<CebizPay.Domain.Payments.Entities.PaymentAttempt>(PaymentAttempts);
    IEntitySet<CebizPay.Domain.Payments.Entities.WebhookEvent> IApplicationDbContext.WebhookEvents => new EntitySet<CebizPay.Domain.Payments.Entities.WebhookEvent>(WebhookEvents);
    IEntitySet<CebizPay.Domain.Payments.Entities.VirtualAccount> IApplicationDbContext.VirtualAccounts => new EntitySet<CebizPay.Domain.Payments.Entities.VirtualAccount>(VirtualAccounts);
    IEntitySet<CebizPay.Domain.Finance.Entities.ExternalFundingAccount> IApplicationDbContext.ExternalFundingAccounts => new EntitySet<CebizPay.Domain.Finance.Entities.ExternalFundingAccount>(ExternalFundingAccounts);
    IEntitySet<CebizPay.Domain.Finance.Entities.PlatformFeePolicy> IApplicationDbContext.PlatformFeePolicies => new EntitySet<CebizPay.Domain.Finance.Entities.PlatformFeePolicy>(PlatformFeePolicies);
    IEntitySet<CebizPay.Domain.Payments.Entities.FundingTransaction> IApplicationDbContext.FundingTransactions => new EntitySet<CebizPay.Domain.Payments.Entities.FundingTransaction>(FundingTransactions);
    IEntitySet<CebizPay.Domain.Payments.Entities.SavedCard> IApplicationDbContext.SavedCards => new EntitySet<CebizPay.Domain.Payments.Entities.SavedCard>(SavedCards);
    IEntitySet<CebizPay.Domain.Payments.Entities.CardRefund> IApplicationDbContext.CardRefunds => new EntitySet<CebizPay.Domain.Payments.Entities.CardRefund>(CardRefunds);
    IEntitySet<CebizPay.Domain.Payments.Entities.CardVerification> IApplicationDbContext.CardVerifications => new EntitySet<CebizPay.Domain.Payments.Entities.CardVerification>(CardVerifications);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollBatch> IApplicationDbContext.PayrollBatches => new EntitySet<CebizPay.Domain.Payroll.Entities.PayrollBatch>(PayrollBatches);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollItem> IApplicationDbContext.PayrollItems => new EntitySet<CebizPay.Domain.Payroll.Entities.PayrollItem>(PayrollItems);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt> IApplicationDbContext.PayrollExecutionAttempts => new EntitySet<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt>(PayrollExecutionAttempts);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PaymentVoucher> IApplicationDbContext.PaymentVouchers => new EntitySet<CebizPay.Domain.Payroll.Entities.PaymentVoucher>(PaymentVouchers);
    IEntitySet<CebizPay.Domain.Loans.Entities.CorporateLoanPlan> IApplicationDbContext.CorporateLoanPlans => new EntitySet<CebizPay.Domain.Loans.Entities.CorporateLoanPlan>(CorporateLoanPlans);
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanApplication> IApplicationDbContext.LoanApplications => new EntitySet<CebizPay.Domain.Loans.Entities.LoanApplication>(LoanApplications);
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanContract> IApplicationDbContext.LoanContracts => new EntitySet<CebizPay.Domain.Loans.Entities.LoanContract>(LoanContracts);
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem> IApplicationDbContext.LoanRepaymentScheduleItems => new EntitySet<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem>(LoanRepaymentScheduleItems);
    IEntitySet<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy> IApplicationDbContext.StandardIndividualLoanPolicies => new EntitySet<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy>(StandardIndividualLoanPolicies);
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsInterestPolicy> IApplicationDbContext.SavingsInterestPolicies => new EntitySet<CebizPay.Domain.Savings.Entities.SavingsInterestPolicy>(SavingsInterestPolicies);
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsPlan> IApplicationDbContext.SavingsPlans => new EntitySet<CebizPay.Domain.Savings.Entities.SavingsPlan>(SavingsPlans);
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsAccount> IApplicationDbContext.SavingsAccounts => new EntitySet<CebizPay.Domain.Savings.Entities.SavingsAccount>(SavingsAccounts);
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsContribution> IApplicationDbContext.SavingsContributions => new EntitySet<CebizPay.Domain.Savings.Entities.SavingsContribution>(SavingsContributions);
    IEntitySet<CebizPay.Domain.Savings.Entities.SavingsInterestAccrual> IApplicationDbContext.SavingsInterestAccruals => new EntitySet<CebizPay.Domain.Savings.Entities.SavingsInterestAccrual>(SavingsInterestAccruals);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftGroup> IApplicationDbContext.ThriftGroups => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftGroup>(ThriftGroups);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftMember> IApplicationDbContext.ThriftMembers => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftMember>(ThriftMembers);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftInvitation> IApplicationDbContext.ThriftInvitations => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftInvitation>(ThriftInvitations);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftCycle> IApplicationDbContext.ThriftCycles => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftCycle>(ThriftCycles);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftContribution> IApplicationDbContext.ThriftContributions => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftContribution>(ThriftContributions);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftPayout> IApplicationDbContext.ThriftPayouts => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftPayout>(ThriftPayouts);
    IEntitySet<CebizPay.Domain.Thrift.Entities.ThriftReimbursement> IApplicationDbContext.ThriftReimbursements => new EntitySet<CebizPay.Domain.Thrift.Entities.ThriftReimbursement>(ThriftReimbursements);
    IEntitySet<CebizPay.Domain.Vas.Entities.VasTransaction> IApplicationDbContext.VasTransactions => new EntitySet<CebizPay.Domain.Vas.Entities.VasTransaction>(VasTransactions);

    /// <inheritdoc/>
    async Task<IDbTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var tx = await Database.BeginTransactionAsync(cancellationToken);
        return new EfCoreDbTransaction(tx);
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceAuditLogImmutability();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceAuditLogImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SaveChanges()
    {
        EnforceAuditLogImmutability();
        return base.SaveChanges();
    }

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAuditLogImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void EnforceAuditLogImmutability()
    {
        var invalidEntries = ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        if (invalidEntries.Count > 0)
        {
            throw new InvalidOperationException("Audit logs are strictly immutable and cannot be updated or deleted.");
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}