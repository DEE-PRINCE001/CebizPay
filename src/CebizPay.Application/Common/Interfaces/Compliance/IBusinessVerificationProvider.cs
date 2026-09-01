using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider abstraction for Corporate Affairs Commission (CAC) business verification and beneficial ownership inquiry.
/// </summary>
public interface IBusinessVerificationProvider
{
    /// <summary>Provider identifier.</summary>
    VerificationProvider Provider { get; }

    /// <summary>
    /// Verifies corporate registration and entity status against CAC registry records.
    /// </summary>
    Task<VerificationProviderResult> VerifyBusinessAsync(
        string cacNumber,
        string companyName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves verified corporate directors and ultimate beneficial owners (UBOs).
    /// </summary>
    Task<VerificationProviderResult> GetBeneficialOwnersAsync(
        string cacNumber,
        CancellationToken cancellationToken = default);
}
