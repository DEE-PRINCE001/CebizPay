using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Enums;

namespace CebizPay.Application.Common.Models.Vas;

/// <summary>
/// Status of a VAS purchase operation outcome from an external provider.
/// </summary>
public enum VasPurchaseResultStatus
{
    /// <summary>Provider confirmed successful fulfillment.</summary>
    Success = 1,

    /// <summary>Provider rejected the request definitively (e.g. invalid phone number, inactive line).</summary>
    BusinessFailure = 2,

    /// <summary>Transient/technical gateway failure (e.g., HTTP 500, network error) eligible for retry.</summary>
    TechnicalFailure = 3,

    /// <summary>Indeterminate outcome or request timeout (requires reconciliation/status requery).</summary>
    Unknown = 4
}

/// <summary>
/// Immutable provider-neutral result of a VAS gateway operation.
/// </summary>
public sealed record VasPurchaseProviderResult(
    VasPurchaseResultStatus Status,
    string? ProviderReference,
    string? FailureCode,
    string? FailureReason,
    string? SafeMetadata)
{
    /// <summary>Creates a successful provider result.</summary>
    public static VasPurchaseProviderResult Success(string providerReference, string? safeMetadata = null) =>
        new(VasPurchaseResultStatus.Success, providerReference, null, null, safeMetadata);

    /// <summary>Creates a business failure provider result.</summary>
    public static VasPurchaseProviderResult BusinessFailure(string failureCode, string failureReason, string? safeMetadata = null) =>
        new(VasPurchaseResultStatus.BusinessFailure, null, failureCode, failureReason, safeMetadata);

    /// <summary>Creates a technical failure provider result.</summary>
    public static VasPurchaseProviderResult TechnicalFailure(string failureCode, string failureReason, string? safeMetadata = null) =>
        new(VasPurchaseResultStatus.TechnicalFailure, null, failureCode, failureReason, safeMetadata);

    /// <summary>Creates an unknown/indeterminate outcome provider result.</summary>
    public static VasPurchaseProviderResult Unknown(string reason, string? safeMetadata = null) =>
        new(VasPurchaseResultStatus.Unknown, null, null, reason, safeMetadata);
}

/// <summary>
/// Result of an automated phone number operator resolution query.
/// </summary>
public sealed record VasOperatorResolutionResult(
    bool Succeeded,
    VasNetwork? Network,
    string? ErrorMessage)
{
    /// <summary>Creates a successful operator detection result.</summary>
    public static VasOperatorResolutionResult Success(VasNetwork network) =>
        new(true, network, null);

    /// <summary>Creates a failed operator detection result.</summary>
    public static VasOperatorResolutionResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}

/// <summary>
/// Provider-neutral DTO representing an available mobile data bundle.
/// </summary>
public sealed record DataBundleDto(
    string ProductCode,
    VasNetwork Network,
    string Name,
    string Volume,
    string Validity,
    decimal Amount,
    Currency Currency);

/// <summary>
/// Result DTO returned by the purchase execution service.
/// </summary>
public sealed record VasPurchaseResult(
    bool Succeeded,
    VasTransactionStatus Status,
    string Reference,
    string? ProviderReference,
    string? ErrorMessage);

/// <summary>
/// Public API response DTO for a VAS purchase operation.
/// </summary>
public sealed record VasPurchaseResponseDto(
    string Reference,
    string Type,
    string Status,
    decimal Amount,
    string Currency,
    string Network,
    string MaskedPhoneNumber,
    string? ProductCode,
    string? ProductName,
    DateTime CreatedAtUtc);

/// <summary>
/// Detailed response DTO for querying a VAS transaction.
/// </summary>
public sealed record VasTransactionResponseDto(
    Guid Id,
    string Reference,
    string Type,
    string Status,
    decimal Amount,
    string Currency,
    string Network,
    string MaskedPhoneNumber,
    string? ProductCode,
    string? ProductName,
    string? ProviderReference,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ReversedAtUtc,
    string? FailureReason);

/// <summary>
/// Public API response DTO for operator detection.
/// </summary>
public sealed record OperatorDetectionResponseDto(
    bool Succeeded,
    string? Network,
    string? ErrorMessage);
