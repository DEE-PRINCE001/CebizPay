namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Provider-neutral result model for payment gateway operations.
/// </summary>
public sealed record PaymentProviderResult
{
    /// <summary>Outcome status classification.</summary>
    public PaymentProviderResultStatus Status { get; init; }

    /// <summary>External provider transaction/session reference (if assigned).</summary>
    public string? ProviderReference { get; init; }

    /// <summary>Provider-specific failure / error code (if failed).</summary>
    public string? FailureCode { get; init; }

    /// <summary>Failure or rejection reason (if failed/unknown).</summary>
    public string? FailureReason { get; init; }

    /// <summary>Safe sanitized metadata / response reference.</summary>
    public string? SafeMetadata { get; init; }

    /// <summary>
    /// Factory for a successful provider outcome.
    /// </summary>
    public static PaymentProviderResult Success(string providerReference, string? safeMetadata = null) =>
        new()
        {
            Status = PaymentProviderResultStatus.Success,
            ProviderReference = providerReference,
            SafeMetadata = safeMetadata
        };

    /// <summary>
    /// Factory for a business rejection outcome (do not automatically failover).
    /// </summary>
    public static PaymentProviderResult BusinessFailure(string failureCode, string failureReason, string? safeMetadata = null) =>
        new()
        {
            Status = PaymentProviderResultStatus.BusinessFailure,
            FailureCode = failureCode,
            FailureReason = failureReason,
            SafeMetadata = safeMetadata
        };

    /// <summary>
    /// Factory for a technical / infrastructure failure outcome (fallback/retry may be permitted).
    /// </summary>
    public static PaymentProviderResult TechnicalFailure(string failureCode, string failureReason, string? safeMetadata = null) =>
        new()
        {
            Status = PaymentProviderResultStatus.TechnicalFailure,
            FailureCode = failureCode,
            FailureReason = failureReason,
            SafeMetadata = safeMetadata
        };

    /// <summary>
    /// Factory for an unknown / timeout outcome (requires reconciliation before any action).
    /// </summary>
    public static PaymentProviderResult Unknown(string? reason = null, string? safeMetadata = null) =>
        new()
        {
            Status = PaymentProviderResultStatus.Unknown,
            FailureReason = reason,
            SafeMetadata = safeMetadata
        };
}
