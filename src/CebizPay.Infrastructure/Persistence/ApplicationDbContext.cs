using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
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

    /// <summary>Gets the funding transactions entity set.</summary>
    public DbSet<CebizPay.Domain.Payments.Entities.FundingTransaction> FundingTransactions => Set<CebizPay.Domain.Payments.Entities.FundingTransaction>();

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

    // Explicit IApplicationDbContext implementations returning IEntitySet<T>
    IEntitySet<IndividualProfile> IApplicationDbContext.IndividualProfiles => new EntitySet<IndividualProfile>(IndividualProfiles);
    IEntitySet<AdminProfile> IApplicationDbContext.AdminProfiles => new EntitySet<AdminProfile>(AdminProfiles);
    IEntitySet<Organization> IApplicationDbContext.Organizations => new EntitySet<Organization>(Organizations);
    IEntitySet<OrganizationMembership> IApplicationDbContext.OrganizationMemberships => new EntitySet<OrganizationMembership>(OrganizationMemberships);
    IEntitySet<Department> IApplicationDbContext.Departments => new EntitySet<Department>(Departments);
    IEntitySet<WorkforceRole> IApplicationDbContext.WorkforceRoles => new EntitySet<WorkforceRole>(WorkforceRoles);
    IEntitySet<SalaryLevel> IApplicationDbContext.SalaryLevels => new EntitySet<SalaryLevel>(SalaryLevels);
    IEntitySet<StaffInvitation> IApplicationDbContext.StaffInvitations => new EntitySet<StaffInvitation>(StaffInvitations);
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
    IEntitySet<CebizPay.Domain.Payments.Entities.FundingTransaction> IApplicationDbContext.FundingTransactions => new EntitySet<CebizPay.Domain.Payments.Entities.FundingTransaction>(FundingTransactions);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollBatch> IApplicationDbContext.PayrollBatches => new EntitySet<CebizPay.Domain.Payroll.Entities.PayrollBatch>(PayrollBatches);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollItem> IApplicationDbContext.PayrollItems => new EntitySet<CebizPay.Domain.Payroll.Entities.PayrollItem>(PayrollItems);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt> IApplicationDbContext.PayrollExecutionAttempts => new EntitySet<CebizPay.Domain.Payroll.Entities.PayrollExecutionAttempt>(PayrollExecutionAttempts);
    IEntitySet<CebizPay.Domain.Payroll.Entities.PaymentVoucher> IApplicationDbContext.PaymentVouchers => new EntitySet<CebizPay.Domain.Payroll.Entities.PaymentVoucher>(PaymentVouchers);
    IEntitySet<CebizPay.Domain.Loans.Entities.CorporateLoanPlan> IApplicationDbContext.CorporateLoanPlans => new EntitySet<CebizPay.Domain.Loans.Entities.CorporateLoanPlan>(CorporateLoanPlans);
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanApplication> IApplicationDbContext.LoanApplications => new EntitySet<CebizPay.Domain.Loans.Entities.LoanApplication>(LoanApplications);
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanContract> IApplicationDbContext.LoanContracts => new EntitySet<CebizPay.Domain.Loans.Entities.LoanContract>(LoanContracts);
    IEntitySet<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem> IApplicationDbContext.LoanRepaymentScheduleItems => new EntitySet<CebizPay.Domain.Loans.Entities.LoanRepaymentScheduleItem>(LoanRepaymentScheduleItems);
    IEntitySet<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy> IApplicationDbContext.StandardIndividualLoanPolicies => new EntitySet<CebizPay.Domain.Loans.Entities.StandardIndividualLoanPolicy>(StandardIndividualLoanPolicies);

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