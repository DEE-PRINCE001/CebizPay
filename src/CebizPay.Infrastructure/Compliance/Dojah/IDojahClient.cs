using CebizPay.Application.Common.Interfaces.Compliance;

namespace CebizPay.Infrastructure.Compliance.Dojah;

/// <summary>
/// HTTP client contract for interacting with Dojah compliance APIs.
/// </summary>
public interface IDojahClient
{
    /// <summary>Verifies an 11-digit BVN against NIBSS registry.</summary>
    Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies an 11-digit NIN against NIMC registry.</summary>
    Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Performs selfie photo ID and facial matching.</summary>
    Task<VerificationProviderResult> VerifyPhotoIdAsync(
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        CancellationToken cancellationToken = default);

    /// <summary>Performs government document analysis and OCR.</summary>
    Task<VerificationProviderResult> AnalyzeDocumentAsync(
        string documentImageBase64,
        string? docType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Performs real-time AML / PEP / Sanctions screening.</summary>
    Task<VerificationProviderResult> ScreenAmlAsync(
        string name,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Performs CAC corporate registry business lookup via the Advanced endpoint.</summary>
    Task<VerificationProviderResult> LookupCacAsync(
        string rcNumber,
        string companyType,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the authoritative verification record by reference ID directly from Dojah.</summary>
    Task<string?> GetVerificationRawJsonAsync(
        string referenceId,
        CancellationToken cancellationToken = default);
}
