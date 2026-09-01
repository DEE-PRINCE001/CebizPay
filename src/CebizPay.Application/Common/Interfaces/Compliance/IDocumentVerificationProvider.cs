using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider abstraction for government-issued identity document OCR and authenticity verification.
/// </summary>
public interface IDocumentVerificationProvider
{
    /// <summary>Provider identifier.</summary>
    VerificationProvider Provider { get; }

    /// <summary>
    /// Performs OCR, MRZ inspection, and registry cross-validation of a government identity document.
    /// </summary>
    Task<VerificationProviderResult> VerifyDocumentAsync(
        DocumentType documentType,
        string documentNumber,
        string documentImageBase64,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default);
}
