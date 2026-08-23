namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Status of a member's scheduled contribution in a thrift cycle.
/// </summary>
public enum ThriftContributionStatus
{
    /// <summary>Scheduled contribution pending collection.</summary>
    Pending = 1,

    /// <summary>Contribution successfully collected and posted to the central ledger.</summary>
    Successful = 2,

    /// <summary>Contribution failed (insufficient wallet and card charge failed/declined).</summary>
    Missed = 3,

    /// <summary>Contribution attempt failed with an unrecoverable error.</summary>
    Failed = 4
}
