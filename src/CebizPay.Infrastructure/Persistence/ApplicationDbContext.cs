using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Represents the primary database context for the application, including Identity, Outbox, and Phase 1A entities.
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

    /// <inheritdoc/>
    public DbSet<IndividualProfile> IndividualProfiles => Set<IndividualProfile>();

    /// <inheritdoc/>
    public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();

    /// <inheritdoc/>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <inheritdoc/>
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    /// <inheritdoc/>
    public DbSet<Department> Departments => Set<Department>();

    /// <inheritdoc/>
    public DbSet<WorkforceRole> WorkforceRoles => Set<WorkforceRole>();

    /// <inheritdoc/>
    public DbSet<SalaryLevel> SalaryLevels => Set<SalaryLevel>();

    /// <inheritdoc/>
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

    /// <inheritdoc/>
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();

    /// <inheritdoc/>
    public DbSet<KybDetail> KybDetails => Set<KybDetail>();

    /// <inheritdoc/>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <inheritdoc/>
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();

    /// <inheritdoc/>
    public DbSet<CebizPay.Domain.Finance.Entities.Wallet> Wallets => Set<CebizPay.Domain.Finance.Entities.Wallet>();

    /// <inheritdoc/>
    public DbSet<CebizPay.Domain.Finance.Entities.LedgerAccount> LedgerAccounts => Set<CebizPay.Domain.Finance.Entities.LedgerAccount>();

    /// <inheritdoc/>
    public DbSet<CebizPay.Domain.Finance.Entities.LedgerTransaction> LedgerTransactions => Set<CebizPay.Domain.Finance.Entities.LedgerTransaction>();

    /// <inheritdoc/>
    public DbSet<CebizPay.Domain.Finance.Entities.LedgerEntry> LedgerEntries => Set<CebizPay.Domain.Finance.Entities.LedgerEntry>();

    /// <inheritdoc/>
    public DbSet<CebizPay.Domain.Finance.Entities.FxConversion> FxConversions => Set<CebizPay.Domain.Finance.Entities.FxConversion>();

    /// <inheritdoc/>
    public DbSet<CebizPay.Domain.Finance.Entities.IdempotencyRecord> IdempotencyRecords => Set<CebizPay.Domain.Finance.Entities.IdempotencyRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}