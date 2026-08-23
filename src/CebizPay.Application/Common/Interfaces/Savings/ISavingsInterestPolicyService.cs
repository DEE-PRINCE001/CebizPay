using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Application.Common.Interfaces.Savings;

/// <summary>
/// Service contract managing Super Admin versioned interest policies for savings plans.
/// </summary>
public interface ISavingsInterestPolicyService
{
    /// <summary>
    /// Returns the currently active interest policy for the given plan type.
    /// </summary>
    Task<SavingsInterestPolicyDto?> GetActivePolicyAsync(SavingsPlanType planType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and activates a new policy version, atomically deactivating previous versions.
    /// </summary>
    Task<SavingsInterestPolicyDto> CreateAndActivatePolicyAsync(CreateSavingsInterestPolicyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all historical and active interest policy versions.
    /// </summary>
    Task<IReadOnlyList<SavingsInterestPolicyDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);
}
