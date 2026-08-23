namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Status of an individual member within a thrift group.
/// </summary>
public enum ThriftMemberStatus
{
    /// <summary>Member invited but has not yet accepted / joined.</summary>
    Invited = 1,

    /// <summary>Active participating member in good standing.</summary>
    Active = 2,

    /// <summary>Payout eligibility suspended following two consecutive missed contributions.</summary>
    Suspended = 3,

    /// <summary>Member removed from group before cycle completion.</summary>
    Removed = 4,

    /// <summary>Departing member net refundable contributions fully reimbursed.</summary>
    Refunded = 5
}
