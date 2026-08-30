using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Service abstraction for platform fee policy administration, retrieval, and lifecycle management.
/// </summary>
public interface IPlatformFeePolicyService
{
    /// <summary>
    /// Gets the currently active fee policy for the specified financial operation type.
    /// </summary>
    Task<PlatformFeePolicy?> GetActivePolicyAsync(
        FeeOperationType operationType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all historical fee policies, optionally filtered by operation type, ordered by version descending.
    /// </summary>
    Task<IReadOnlyList<PlatformFeePolicy>> GetAllPoliciesAsync(
        FeeOperationType? operationType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and activates a new platform fee policy version, automatically deactivating the prior active version for that operation type.
    /// </summary>
    Task<PlatformFeePolicy> CreateAndActivatePolicyAsync(
        FeeOperationType operationType,
        FeeCalculationMethod calculationMethod,
        FeeBearer feeBearer,
        decimal? fixedAmount,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        Currency currency,
        string createdByUserId,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken = default);
}
