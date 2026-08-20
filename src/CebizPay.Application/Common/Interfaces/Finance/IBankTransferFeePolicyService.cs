using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Contract for managing and querying versioned platform bank-transfer fee policies.
/// </summary>
public interface IBankTransferFeePolicyService
{
    /// <summary>
    /// Returns the currently active and effective bank-transfer fee policy, or null if none is configured.
    /// </summary>
    Task<BankTransferFeePolicy?> GetActivePolicyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all historical and current bank-transfer fee policies ordered by version descending.
    /// </summary>
    Task<IReadOnlyList<BankTransferFeePolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and activates a new bank-transfer fee policy, automatically deactivating any previously active policy.
    /// </summary>
    Task<BankTransferFeePolicy> CreateAndActivatePolicyAsync(
        FeePolicyMode mode,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        string createdByUserId,
        CancellationToken cancellationToken = default);
}
