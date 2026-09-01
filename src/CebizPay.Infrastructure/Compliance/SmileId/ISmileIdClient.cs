using CebizPay.Application.Common.Interfaces.Compliance;

namespace CebizPay.Infrastructure.Compliance.SmileId;

/// <summary>
/// HTTP client contract for Smile ID compliance and biometric verification APIs.
/// </summary>
public interface ISmileIdClient
{
    /// <summary>Verifies BVN via Smile ID Enhanced KYC rail.</summary>
    Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies NIN via Smile ID Enhanced KYC rail.</summary>
    Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Performs SmartSelfie™ biometric liveness and 1:1 facial matching.</summary>
    Task<VerificationProviderResult> VerifyBiometricsAsync(
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        string? idNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>Performs document OCR and authenticity verification.</summary>
    Task<VerificationProviderResult> VerifyDocumentAsync(
        string documentImageBase64,
        string idType,
        string idNumber,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Screens an individual or corporate entity against global AML watchlists.</summary>
    Task<VerificationProviderResult> ScreenAmlAsync(
        string name,
        bool isEntity = false,
        CancellationToken cancellationToken = default);

    /// <summary>Performs business / corporate registration verification.</summary>
    Task<VerificationProviderResult> VerifyBusinessAsync(
        string registrationNumber,
        string? businessName = null,
        CancellationToken cancellationToken = default);
}
