using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Application contract for platform peer-transfer fee policy management.
/// Policies are versioned and effective-dated; only one policy may be active at a time.
/// </summary>
public interface IFeePolicyService
{
    /// <summary>
    /// Returns the currently active peer-transfer fee policy (enabled and with latest EffectiveFrom &lt;= now),
    /// or null if no active policy exists.
    /// </summary>
    Task<PeerTransferFeePolicy?> GetActivePolicyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all historical and current fee policies ordered by version descending.
    /// </summary>
    Task<IReadOnlyList<PeerTransferFeePolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and activates a new fee policy. The previous active policy is deactivated.
    /// The new policy version is auto-incremented from the current highest version.
    /// </summary>
    /// <param name="mode">FREE or PERCENTAGE fee mode.</param>
    /// <param name="percentageRate">Required for PERCENTAGE mode (decimal fraction, e.g. 0.01 = 1%). Null for FREE.</param>
    /// <param name="minimumFee">Required for PERCENTAGE mode. Null for FREE.</param>
    /// <param name="maximumFee">Required for PERCENTAGE mode. Null for FREE.</param>
    /// <param name="createdByUserId">Super Admin UserId authorizing this change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PeerTransferFeePolicy> CreateAndActivatePolicyAsync(
        FeePolicyMode mode,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        string createdByUserId,
        CancellationToken cancellationToken = default);
}
