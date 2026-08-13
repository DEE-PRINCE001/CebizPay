namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Status of an idempotency request record.
/// </summary>
public enum IdempotencyStatus
{
    /// <summary>Request is currently being processed.</summary>
    Processing = 1,
    /// <summary>Request completed successfully.</summary>
    Completed = 2,
    /// <summary>Request failed.</summary>
    Failed = 3
}
