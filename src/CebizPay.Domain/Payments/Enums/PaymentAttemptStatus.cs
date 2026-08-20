namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Lifecycle status of an individual external payment provider attempt.
///
/// State transitions:
///   Created -> Processing
///   Created -> Cancelled
///   Processing -> Succeeded
///   Processing -> Failed
///   Processing -> Unknown
///   Unknown -> Succeeded
///   Unknown -> Failed
///   Unknown -> Cancelled
/// </summary>
public enum PaymentAttemptStatus
{
    /// <summary>Attempt created and initialized internally, not yet dispatched to provider.</summary>
    Created = 1,

    /// <summary>Attempt dispatched and actively in-flight with external provider.</summary>
    Processing = 2,

    /// <summary>Attempt confirmed successful by provider. Terminal state.</summary>
    Succeeded = 3,

    /// <summary>Attempt definitively failed / rejected by provider. Terminal state.</summary>
    Failed = 4,

    /// <summary>Attempt outcome indeterminate (e.g. timeout / network partition). Requires reconciliation.</summary>
    Unknown = 5,

    /// <summary>Attempt cancelled before execution or abandoned after indeterminate state. Terminal state.</summary>
    Cancelled = 6
}
