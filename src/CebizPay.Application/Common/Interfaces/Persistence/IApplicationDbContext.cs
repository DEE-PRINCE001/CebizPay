using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.Common.Interfaces.Persistence;

/// <summary>
/// Application database context contract for querying and persisting domain aggregates.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>Gets the individual profiles entity set.</summary>
    DbSet<IndividualProfile> IndividualProfiles { get; }

    /// <summary>Gets the admin profiles entity set.</summary>
    DbSet<AdminProfile> AdminProfiles { get; }

    /// <summary>Gets the organizations entity set.</summary>
    DbSet<Organization> Organizations { get; }

    /// <summary>Gets the organization memberships entity set.</summary>
    DbSet<OrganizationMembership> OrganizationMemberships { get; }

    /// <summary>Gets the departments entity set.</summary>
    DbSet<Department> Departments { get; }

    /// <summary>Gets the workforce roles entity set.</summary>
    DbSet<WorkforceRole> WorkforceRoles { get; }

    /// <summary>Gets the salary levels entity set.</summary>
    DbSet<SalaryLevel> SalaryLevels { get; }

    /// <summary>Gets the staff invitations entity set.</summary>
    DbSet<StaffInvitation> StaffInvitations { get; }

    /// <summary>Gets the KYC documents entity set.</summary>
    DbSet<KycDocument> KycDocuments { get; }

    /// <summary>Gets the KYB details entity set.</summary>
    DbSet<KybDetail> KybDetails { get; }

    /// <summary>Gets the audit logs entity set.</summary>
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>Gets the MFA challenges entity set.</summary>
    DbSet<MfaChallenge> MfaChallenges { get; }

    /// <summary>Gets the wallets entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.Wallet> Wallets { get; }

    /// <summary>Gets the ledger accounts entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.LedgerAccount> LedgerAccounts { get; }

    /// <summary>Gets the ledger transactions entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.LedgerTransaction> LedgerTransactions { get; }

    /// <summary>Gets the ledger entries entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.LedgerEntry> LedgerEntries { get; }

    /// <summary>Gets the FX conversions entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.FxConversion> FxConversions { get; }

    /// <summary>Gets the idempotency records entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.IdempotencyRecord> IdempotencyRecords { get; }

    /// <summary>Gets the peer-transfer fee policies entity set.</summary>
    DbSet<CebizPay.Domain.Finance.Entities.PeerTransferFeePolicy> PeerTransferFeePolicies { get; }

    /// <summary>
    /// Saves changes asynchronously to the underlying database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction. The caller is responsible for committing or rolling back.
    /// </summary>
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
