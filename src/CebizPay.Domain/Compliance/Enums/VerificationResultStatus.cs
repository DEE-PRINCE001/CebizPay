namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Normalized provider-neutral result classification for an external verification check.
/// External evidence is distinct from final CebizPay compliance approval decisions.
/// </summary>
public enum VerificationResultStatus
{
    /// <summary>Identity or business attributes definitively matched official registry.</summary>
    Match = 1,

    /// <summary>Supplied attributes do not match official registry records.</summary>
    Mismatch = 2,

    /// <summary>Identifier (BVN, NIN, CAC) not found in registry.</summary>
    NotFound = 3,

    /// <summary>Asynchronous provider job is pending verification.</summary>
    Pending = 4,

    /// <summary>Provider returned technical failure (HTTP 5xx, network error, upstream failure).</summary>
    TechnicalFailure = 5,

    /// <summary>Provider service or specific verification rail is currently unavailable.</summary>
    Unavailable = 6,

    /// <summary>Provider returned inconclusive or flagged result requiring manual compliance review.</summary>
    ReviewRequired = 7,

    /// <summary>Supplied parameters were rejected by the provider as malformed.</summary>
    InvalidRequest = 8
}
