using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider abstraction for individual identity verification (BVN and NIN).
/// </summary>
public interface IIdentityVerificationProvider
{
    /// <summary>Provider identifier.</summary>
    VerificationProvider Provider { get; }

    /// <summary>
    /// Verifies Bank Verification Number (BVN) against official NIBSS registry records.
    /// </summary>
    Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies National Identification Number (NIN) against official NIMC registry records.
    /// </summary>
    Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);
}
