using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// PostgreSQL-backed fee policy service. Maintains a versioned history of peer-transfer fee policies.
/// Only one policy may be active (IsEnabled=true) at any time.
/// </summary>
public sealed class FeePolicyService : IFeePolicyService
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="FeePolicyService"/>.
    /// </summary>
    public FeePolicyService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<PeerTransferFeePolicy?> GetActivePolicyAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbContext.PeerTransferFeePolicies
            .Where(p => p.IsEnabled && p.EffectiveFrom <= now)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PeerTransferFeePolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.PeerTransferFeePolicies
            .OrderByDescending(p => p.Version)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PeerTransferFeePolicy> CreateAndActivatePolicyAsync(
        FeePolicyMode mode,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        // Determine next version number
        var highestVersion = await _dbContext.PeerTransferFeePolicies
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;

        var nextVersion = highestVersion + 1;

        // Deactivate the current active policy
        var activePolicies = await _dbContext.PeerTransferFeePolicies
            .Where(p => p.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var existing in activePolicies)
        {
            existing.Deactivate();
        }

        // Create new active policy
        var newPolicy = PeerTransferFeePolicy.Create(
            mode,
            percentageRate,
            minimumFee,
            maximumFee,
            nextVersion,
            createdByUserId,
            effectiveFrom: DateTime.UtcNow);

        _dbContext.PeerTransferFeePolicies.Add(newPolicy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newPolicy;
    }
}
