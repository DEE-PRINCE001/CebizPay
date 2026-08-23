using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain entity representing an individual member's participation in a Thrift group.
/// Tracks assigned payout position, contribution tracking, consecutive delinquency counters, and payout eligibility.
/// </summary>
public class ThriftMember
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Identity user ID of the participating member.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Assigned payout rotation position (1 to TotalPositions).</summary>
    public int? Position { get; private set; }

    /// <summary>Current membership status (Active, Suspended, Removed, Refunded).</summary>
    public ThriftMemberStatus Status { get; private set; } = ThriftMemberStatus.Active;

    /// <summary>Counter of consecutive missed contribution cycles.</summary>
    public int ConsecutiveMissedCycles { get; private set; }

    /// <summary>Total sum of successful contributions paid into the thrift pool to date.</summary>
    public decimal TotalContributed { get; private set; }

    /// <summary>Total sum of cycle payout distributions received to date.</summary>
    public decimal TotalPayoutReceived { get; private set; }

    /// <summary>Joined timestamp.</summary>
    public DateTime JoinedAtUtc { get; private set; }

    /// <summary>Timestamp when payout position was selected.</summary>
    public DateTime? PositionSelectedAtUtc { get; private set; }

    /// <summary>Timestamp when member was suspended due to 2 consecutive missed contributions.</summary>
    public DateTime? SuspendedAtUtc { get; private set; }

    /// <summary>Last state update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private ThriftMember() { } // EF Core

    /// <summary>
    /// Creates a new thrift member.
    /// </summary>
    public static ThriftMember Create(Guid thriftGroupId, string userId)
    {
        if (thriftGroupId == Guid.Empty)
            throw new ArgumentException("ThriftGroupId is required.", nameof(thriftGroupId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        return new ThriftMember
        {
            Id = Guid.NewGuid(),
            ThriftGroupId = thriftGroupId,
            UserId = userId,
            Status = ThriftMemberStatus.Active,
            ConsecutiveMissedCycles = 0,
            TotalContributed = 0m,
            TotalPayoutReceived = 0m,
            JoinedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Selects an available payout position.
    /// </summary>
    public void SelectPosition(int position)
    {
        if (position <= 0)
            throw new ArgumentException("Position must be a positive integer.", nameof(position));
        if (Status != ThriftMemberStatus.Active)
            throw new InvalidOperationException($"Cannot select position when member status is {Status}.");

        Position = position;
        PositionSelectedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a successful cycle contribution.
    /// </summary>
    public void RecordSuccessfulContribution(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be positive.", nameof(amount));

        TotalContributed += amount;
        ConsecutiveMissedCycles = 0; // Reset consecutive missed counter
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a missed cycle contribution and suspends payout eligibility after 2 consecutive misses.
    /// </summary>
    public bool RecordMissedContribution()
    {
        ConsecutiveMissedCycles++;
        UpdatedAtUtc = DateTime.UtcNow;

        if (ConsecutiveMissedCycles >= 2 && Status == ThriftMemberStatus.Active)
        {
            Status = ThriftMemberStatus.Suspended;
            SuspendedAtUtc = DateTime.UtcNow;
            return true; // Newly suspended
        }

        return false;
    }

    /// <summary>
    /// Records an authoritative cycle payout distribution received by this member.
    /// </summary>
    public void RecordPayout(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payout amount must be positive.", nameof(amount));

        TotalPayoutReceived += amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Reinstates a suspended member after clearing delinquent obligations.
    /// </summary>
    public void Reactivate()
    {
        Status = ThriftMemberStatus.Active;
        ConsecutiveMissedCycles = 0;
        SuspendedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the net refundable amount for a departing member: Net = TotalContributed - TotalPayoutReceived.
    /// </summary>
    public decimal CalculateNetRefundableAmount()
    {
        return Math.Max(0m, TotalContributed - TotalPayoutReceived);
    }

    /// <summary>
    /// Marks the member as refunded following net contribution reimbursement.
    /// </summary>
    public void MarkRefunded()
    {
        Status = ThriftMemberStatus.Refunded;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
