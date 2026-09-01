#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider-neutral normalized result returned by external/internal compliance verification provider checks.
/// </summary>
public sealed record VerificationProviderResult
{
    /// <summary>Normalized result status.</summary>
    public VerificationResultStatus ResultStatus { get; init; }

    /// <summary>External provider transaction, job, or verification reference.</summary>
    public string? ProviderReference { get; init; }

    /// <summary>Optional confidence / match score (0.00 - 100.00) if supplied by provider.</summary>
    public decimal? ConfidenceScore { get; init; }

    /// <summary>Human-readable non-sensitive summary of result.</summary>
    public string? SafeSummary { get; init; }

    /// <summary>Provider-specific error or rejection code.</summary>
    public string? FailureCode { get; init; }

    /// <summary>Non-sensitive failure reason if verification failed.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Sanitized metadata (JSON) containing verified field flags and non-PII details.</summary>
    public string? SafeMetadata { get; init; }

    /// <summary>Encrypted/protected reference key for raw provider audit logs if required by regulation.</summary>
    public string? RawPayloadRef { get; init; }

    /// <summary>Whether this result represents a successful match/verification.</summary>
    public bool Succeeded => ResultStatus == VerificationResultStatus.Match;

    /// <summary>Whether this result is a technical error eligible for failover.</summary>
    public bool IsTechnicalFailure => ResultStatus == VerificationResultStatus.TechnicalFailure || ResultStatus == VerificationResultStatus.Unavailable;

    /// <summary>Whether this result is asynchronous and pending provider callback.</summary>
    public bool IsPending => ResultStatus == VerificationResultStatus.Pending;

    public static VerificationProviderResult Match(
        string? providerReference = null,
        decimal? confidenceScore = null,
        string? safeSummary = null,
        string? safeMetadata = null,
        string? rawPayloadRef = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.Match,
            ProviderReference = providerReference,
            ConfidenceScore = confidenceScore,
            SafeSummary = safeSummary ?? "Verification matched successfully.",
            SafeMetadata = safeMetadata,
            RawPayloadRef = rawPayloadRef
        };

    public static VerificationProviderResult Mismatch(
        string? failureCode = null,
        string? failureReason = null,
        string? providerReference = null,
        string? safeMetadata = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.Mismatch,
            FailureCode = failureCode ?? "MISMATCH",
            FailureReason = failureReason ?? "Submitted details do not match registry records.",
            ProviderReference = providerReference,
            SafeMetadata = safeMetadata
        };

    public static VerificationProviderResult NotFound(
        string? failureCode = null,
        string? failureReason = null,
        string? providerReference = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.NotFound,
            FailureCode = failureCode ?? "NOT_FOUND",
            FailureReason = failureReason ?? "Identifier not found in registry.",
            ProviderReference = providerReference
        };

    public static VerificationProviderResult Pending(
        string? providerReference,
        string? safeSummary = null,
        string? safeMetadata = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.Pending,
            ProviderReference = providerReference,
            SafeSummary = safeSummary ?? "Verification is processing asynchronously.",
            SafeMetadata = safeMetadata
        };

    public static VerificationProviderResult TechnicalFailure(
        string? failureCode,
        string? failureReason,
        string? providerReference = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.TechnicalFailure,
            FailureCode = failureCode ?? "TECHNICAL_FAILURE",
            FailureReason = failureReason ?? "Provider returned a technical failure.",
            ProviderReference = providerReference
        };

    public static VerificationProviderResult Unavailable(
        string? failureReason = null,
        string? providerReference = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.Unavailable,
            FailureCode = "PROVIDER_UNAVAILABLE",
            FailureReason = failureReason ?? "Provider service or verification rail is currently unavailable.",
            ProviderReference = providerReference
        };

    public static VerificationProviderResult ReviewRequired(
        string? reviewReason,
        string? providerReference = null,
        string? safeMetadata = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.ReviewRequired,
            FailureCode = "REVIEW_REQUIRED",
            FailureReason = reviewReason ?? "Verification flagged for compliance officer review.",
            ProviderReference = providerReference,
            SafeMetadata = safeMetadata
        };

    public static VerificationProviderResult InvalidRequest(
        string? failureReason,
        string? failureCode = null) =>
        new()
        {
            ResultStatus = VerificationResultStatus.InvalidRequest,
            FailureCode = failureCode ?? "INVALID_REQUEST",
            FailureReason = failureReason ?? "Invalid verification request parameters."
        };
}
