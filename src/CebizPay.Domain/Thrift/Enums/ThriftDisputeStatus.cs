namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Status of a Thrift oversight dispute.
/// </summary>
public enum ThriftDisputeStatus
{
    /// <summary>Dispute opened and awaiting administrative review.</summary>
    Open = 1,

    /// <summary>Dispute actively under administrative review.</summary>
    UnderReview = 2,

    /// <summary>Dispute resolved with administrative intervention/findings.</summary>
    Resolved = 3,

    /// <summary>Dispute rejected as unfounded or dismissed.</summary>
    Rejected = 4
}
