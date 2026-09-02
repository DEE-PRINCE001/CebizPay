namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Lifecycle status of a Thrift (Ajo / Esusu) group.
/// </summary>
public enum ThriftStatus
{
    /// <summary>Group created and open for invitations and member joins.</summary>
    OpenForMembers = 1,

    /// <summary>Members selecting preferred payout positions.</summary>
    PositionSelection = 2,

    /// <summary>Positions locked; ready for cycle activation.</summary>
    Locked = 3,

    /// <summary>Active rotation running through cycles.</summary>
    Active = 4,

    /// <summary>All cycles completed and final payouts distributed.</summary>
    Completed = 5,

    /// <summary>Group cancelled before cycle start.</summary>
    Cancelled = 6,

    /// <summary>Group paused by Super Admin for investigation or administrative intervention.</summary>
    Paused = 7
}
