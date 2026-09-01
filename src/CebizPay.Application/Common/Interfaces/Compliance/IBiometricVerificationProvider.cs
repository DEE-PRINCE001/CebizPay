using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider abstraction for biometric liveness detection and facial biometric matching.
/// </summary>
public interface IBiometricVerificationProvider
{
    /// <summary>Provider identifier.</summary>
    VerificationProvider Provider { get; }

    /// <summary>
    /// Performs 3D/passive liveness detection and optional 1:1 facial biometric matching against an ID photo.
    /// </summary>
    Task<VerificationProviderResult> VerifyBiometricsAsync(
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        string? idNumber = null,
        CancellationToken cancellationToken = default);
}
