namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Status of a specific rotation cycle within a thrift group.
/// </summary>
public enum ThriftCycleStatus
{
    /// <summary>Upcoming cycle scheduled for future collection.</summary>
    Upcoming = 1,

    /// <summary>Cycle currently collecting scheduled member contributions.</summary>
    Collecting = 2,

    /// <summary>Contributions collected; ready for beneficiary payout execution.</summary>
    ReadyForPayout = 3,

    /// <summary>Payout successfully distributed to the designated beneficiary wallet.</summary>
    Paid = 4,

    /// <summary>Cycle collection or payout failed and requires administrative review.</summary>
    Failed = 5
}
