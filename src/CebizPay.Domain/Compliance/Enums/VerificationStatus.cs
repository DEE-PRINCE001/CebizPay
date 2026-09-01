namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Lifecycle status of an internal verification operation.
/// </summary>
public enum VerificationStatus
{
    /// <summary>Operation created and awaiting provider dispatch.</summary>
    Initiated = 1,

    /// <summary>Provider dispatch is in progress.</summary>
    Processing = 2,

    /// <summary>Provider initiated asynchronous job; awaiting webhook callback or polling completion.</summary>
    PendingCallback = 3,

    /// <summary>Verification finished with definitive provider outcome recorded as evidence.</summary>
    Completed = 4,

    /// <summary>Operation failed technically across all attempted providers without usable evidence.</summary>
    Failed = 5,

    /// <summary>Provider result or capability policy requires human compliance officer review.</summary>
    ReviewRequired = 6,

    /// <summary>Operation cancelled before completion.</summary>
    Cancelled = 7
}
