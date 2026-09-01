using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Immutable verification evidence captured from an external or internal verification provider check.
/// In accordance with compliance standards, external provider results constitute evidence, not authoritative CebizPay approval.
/// </summary>
public class VerificationEvidence
{
    /// <summary>Unique evidence identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Associated parent verification operation ID.</summary>
    public Guid VerificationOperationId { get; private set; }

    /// <summary>Optional user ID for natural person verification (Individual KYC).</summary>
    public string? UserId { get; private set; }

    /// <summary>Optional organization ID for legal entity verification (Organization KYB).</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Verification scope (Individual KYC vs Organization KYB).</summary>
    public VerificationType VerificationType { get; private set; }

    /// <summary>Verified capability (Identity, Biometrics, Document, AML, Business, etc.).</summary>
    public VerificationCapability Capability { get; private set; }

    /// <summary>External or internal verification provider that produced this evidence.</summary>
    public VerificationProvider Provider { get; private set; }

    /// <summary>External provider transaction or verification reference.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>Normalized result status.</summary>
    public VerificationResultStatus ResultStatus { get; private set; }

    /// <summary>Optional match confidence score (0.00 - 100.00) if supplied by provider.</summary>
    public decimal? ConfidenceScore { get; private set; }

    /// <summary>Timestamp when verification evidence was generated.</summary>
    public DateTime VerifiedAtUtc { get; private set; }

    /// <summary>Optional expiration timestamp for time-sensitive evidence (e.g. watchlist screening, PEP).</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>Sanitized metadata (JSON) containing non-PII attributes such as match indicators and verified field flags.</summary>
    public string? SafeMetadata { get; private set; }

    /// <summary>Provider-specific failure code if verification was unsuccessful.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Human-readable non-sensitive failure reason if verification failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Encrypted/protected reference key for raw provider audit logs if required by regulation.</summary>
    public string? RawPayloadRef { get; private set; }

    private VerificationEvidence() { } // EF Core

    /// <summary>
    /// Creates a new immutable verification evidence record.
    /// </summary>
    public static VerificationEvidence Create(
        Guid verificationOperationId,
        VerificationType verificationType,
        VerificationCapability capability,
        VerificationProvider provider,
        VerificationResultStatus resultStatus,
        string? userId = null,
        Guid? organizationId = null,
        string? providerReference = null,
        decimal? confidenceScore = null,
        DateTime? verifiedAtUtc = null,
        DateTime? expiresAtUtc = null,
        string? safeMetadata = null,
        string? failureCode = null,
        string? failureReason = null,
        string? rawPayloadRef = null)
    {
        if (verificationOperationId == Guid.Empty)
            throw new ArgumentException("VerificationOperationId is required.", nameof(verificationOperationId));

        if (verificationType == VerificationType.IndividualKyc && string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required for Individual KYC evidence.", nameof(userId));

        if (verificationType == VerificationType.OrganizationKyb && (!organizationId.HasValue || organizationId.Value == Guid.Empty))
            throw new ArgumentException("OrganizationId is required for Organization KYB evidence.", nameof(organizationId));

        return new VerificationEvidence
        {
            Id = Guid.NewGuid(),
            VerificationOperationId = verificationOperationId,
            UserId = userId?.Trim(),
            OrganizationId = organizationId,
            VerificationType = verificationType,
            Capability = capability,
            Provider = provider,
            ProviderReference = providerReference?.Trim(),
            ResultStatus = resultStatus,
            ConfidenceScore = confidenceScore,
            VerifiedAtUtc = verifiedAtUtc ?? DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            SafeMetadata = safeMetadata?.Trim(),
            FailureCode = failureCode?.Trim(),
            FailureReason = failureReason?.Trim(),
            RawPayloadRef = rawPayloadRef?.Trim()
        };
    }
}
