using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// PostgreSQL-backed fee policy service for outbound bank transfers.
/// Maintains a versioned history of bank-transfer fee policies.
/// Only one policy may be active (IsEnabled=true) at any time.
/// </summary>
public sealed class BankTransferFeePolicyService : IBankTransferFeePolicyService
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="BankTransferFeePolicyService"/>.
    /// </summary>
    public BankTransferFeePolicyService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<BankTransferFeePolicy?> GetActivePolicyAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbContext.BankTransferFeePolicies
            .Where(p => p.IsEnabled && p.EffectiveFrom <= now)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BankTransferFeePolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BankTransferFeePolicies
            .OrderByDescending(p => p.Version)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<BankTransferFeePolicy> CreateAndActivatePolicyAsync(
        FeePolicyMode mode,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        // Determine next version number
        var highestVersion = await _dbContext.BankTransferFeePolicies
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;

        var nextVersion = highestVersion + 1;

        // Deactivate current active policies
        var activePolicies = await _dbContext.BankTransferFeePolicies
            .Where(p => p.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var existing in activePolicies)
        {
            existing.Deactivate();
        }

        // Create new active policy
        var newPolicy = BankTransferFeePolicy.Create(
            mode,
            percentageRate,
            minimumFee,
            maximumFee,
            nextVersion,
            createdByUserId,
            effectiveFrom: DateTime.UtcNow);

        _dbContext.BankTransferFeePolicies.Add(newPolicy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newPolicy;
    }
}
