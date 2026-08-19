using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;

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

    /// <summary>
    /// Saves changes asynchronously to the underlying database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction. The caller is responsible for committing or rolling back.
    /// </summary>
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
