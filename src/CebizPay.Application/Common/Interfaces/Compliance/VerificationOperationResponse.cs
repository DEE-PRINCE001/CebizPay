#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider-neutral response DTO representing the result of an orchestrated compliance verification operation.
/// </summary>
public sealed record VerificationOperationResponse(
    Guid OperationId,
    string Reference,
    VerificationType VerificationType,
    VerificationCapability Capability,
    VerificationStatus Status,
    VerificationProvider PrimaryProvider,
    VerificationProvider ActiveProvider,
    bool UsedFallback,
    VerificationResultStatus? LatestResultStatus,
    decimal? MatchScore,
    string? Summary,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<VerificationEvidenceSummaryDto> Evidences);

/// <summary>
/// Public-safe summary of an immutable verification evidence record without PII.
/// </summary>
public sealed record VerificationEvidenceSummaryDto(
    Guid EvidenceId,
    VerificationCapability Capability,
    VerificationProvider Provider,
    VerificationResultStatus ResultStatus,
    decimal? ConfidenceScore,
    DateTime VerifiedAtUtc,
    DateTime? ExpiresAtUtc,
    string? FailureCode,
    string? FailureReason,
    string? SafeMetadata);
