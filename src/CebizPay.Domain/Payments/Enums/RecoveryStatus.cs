#pragma warning disable CS1591
namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Status of an outstanding financial recovery owed by an account holder.
/// </summary>
public enum RecoveryStatus
{
    /// <summary>Recovery record open and awaiting balance recovery.</summary>
    Pending = 1,

    /// <summary>Partial amount recovered; remaining balance outstanding.</summary>
    PartiallyRecovered = 2,

    /// <summary>Fully settled and reconciled.</summary>
    FullyRecovered = 3,

    /// <summary>Uncollectible and written off by finance executive approval.</summary>
    WrittenOff = 4,

    /// <summary>Subject to customer dispute or investigation.</summary>
    Disputed = 5
}
