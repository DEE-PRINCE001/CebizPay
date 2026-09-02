using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain aggregate root representing a Thrift (Ajo / Esusu / Rotational Savings &amp; Credit Association) Group.
/// Coordinates participating members, locked payout positions, scheduled cycles, and central ledger pooled settlements.
/// </summary>
public class ThriftGroup
{
    private readonly List<ThriftMember> _members = [];
    private readonly List<ThriftInvitation> _invitations = [];
    private readonly List<ThriftCycle> _cycles = [];

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning organization ID for workplace groups, or null for peer community groups.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Identity user ID of the creator / administrator.</summary>
    public string CreatorUserId { get; private set; } = string.Empty;

    /// <summary>Group name (e.g. Engineering Circle Esusu).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Group description / purpose.</summary>
    public string? Description { get; private set; }

    /// <summary>Transactional currency for contributions and payouts.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Fixed contribution amount required from each member per cycle.</summary>
    public decimal ContributionAmount { get; private set; }

    /// <summary>Cycle rotation frequency (Daily, Weekly, Monthly).</summary>
    public ThriftFrequency Frequency { get; private set; } = ThriftFrequency.Monthly;

    /// <summary>Total number of participating members / payout positions in the rotation.</summary>
    public int TotalPositions { get; private set; }

    /// <summary>Scheduled cycle start timestamp.</summary>
    public DateTime StartDateUtc { get; private set; }

    /// <summary>Scheduled completion timestamp.</summary>
    public DateTime EndDateUtc { get; private set; }

    /// <summary>Deadline for members to select payout positions before automatic or administrative locking.</summary>
    public DateTime PositionSelectionDeadlineUtc { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public ThriftStatus Status { get; private set; } = ThriftStatus.OpenForMembers;

    /// <summary>Current active cycle number in the rotation (1-indexed).</summary>
    public int CurrentCycleNumber { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Participating members.</summary>
    public IReadOnlyCollection<ThriftMember> Members => _members.AsReadOnly();

    /// <summary>Invitations issued for this group.</summary>
    public IReadOnlyCollection<ThriftInvitation> Invitations => _invitations.AsReadOnly();

    /// <summary>Rotation cycles.</summary>
    public IReadOnlyCollection<ThriftCycle> Cycles => _cycles.AsReadOnly();

    private ThriftGroup() { } // EF Core

    /// <summary>
    /// Creates a new Thrift group in OpenForMembers status.
    /// </summary>
    public static ThriftGroup Create(
        Guid? organizationId,
        string creatorUserId,
        string name,
        string? description,
        Currency currency,
        decimal contributionAmount,
        ThriftFrequency frequency,
        int totalPositions,
        DateTime startDateUtc,
        DateTime positionSelectionDeadlineUtc)
    {
        if (string.IsNullOrWhiteSpace(creatorUserId))
            throw new ArgumentException("CreatorUserId is required.", nameof(creatorUserId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group Name is required.", nameof(name));
        if (contributionAmount <= 0)
            throw new ArgumentException("ContributionAmount must be positive.", nameof(contributionAmount));
        if (totalPositions < 2)
            throw new ArgumentException("TotalPositions must be at least 2.", nameof(totalPositions));
        if (startDateUtc <= DateTime.UtcNow)
            throw new ArgumentException("StartDate must be in the future.", nameof(startDateUtc));

        // Calculate expected duration based on frequency and total positions
        var endDateUtc = frequency switch
        {
            ThriftFrequency.Daily => startDateUtc.AddDays(totalPositions),
            ThriftFrequency.Weekly => startDateUtc.AddDays(totalPositions * 7),
            ThriftFrequency.Monthly => startDateUtc.AddMonths(totalPositions),
            _ => startDateUtc.AddMonths(totalPositions)
        };

        var group = new ThriftGroup
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatorUserId = creatorUserId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Currency = currency,
            ContributionAmount = contributionAmount,
            Frequency = frequency,
            TotalPositions = totalPositions,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            PositionSelectionDeadlineUtc = positionSelectionDeadlineUtc,
            Status = ThriftStatus.OpenForMembers,
            CurrentCycleNumber = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Automatically add creator as first member
        group.AddMember(creatorUserId);

        return group;
    }

    /// <summary>
    /// Adds a member to the thrift group.
    /// </summary>
    public ThriftMember AddMember(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (_members.Any(m => m.UserId == userId && m.Status != ThriftMemberStatus.Removed))
            throw new InvalidOperationException($"User {userId} is already a member of this thrift group.");
        if (_members.Count(m => m.Status != ThriftMemberStatus.Removed) >= TotalPositions)
            throw new InvalidOperationException("Thrift group has reached maximum member capacity.");

        var member = ThriftMember.Create(Id, userId);
        _members.Add(member);
        UpdatedAtUtc = DateTime.UtcNow;
        return member;
    }

    /// <summary>
    /// Issues an invitation code for a prospective member.
    /// </summary>
    public ThriftInvitation CreateInvitation(string? email, string invitedByUserId, TimeSpan? expiry = null)
    {
        if (Status != ThriftStatus.OpenForMembers && Status != ThriftStatus.PositionSelection)
            throw new InvalidOperationException("Cannot invite members once positions are locked or cycles are active.");

        var invitation = ThriftInvitation.Create(Id, email, invitedByUserId, expiry ?? TimeSpan.FromDays(7));
        _invitations.Add(invitation);
        return invitation;
    }

    /// <summary>
    /// Locks payout positions and transitions group to Locked / Active status once all positions are filled.
    /// </summary>
    public void LockPositions()
    {
        if (Status == ThriftStatus.Locked || Status == ThriftStatus.Active)
            return; // Already locked

        var activeMembers = _members.Where(m => m.Status == ThriftMemberStatus.Active).ToList();
        if (activeMembers.Count < TotalPositions)
            throw new InvalidOperationException($"Cannot lock positions: {activeMembers.Count} of {TotalPositions} positions are filled.");

        var assignedPositions = activeMembers.Select(m => m.Position).Where(p => p.HasValue).Select(p => p!.Value).ToHashSet();
        if (assignedPositions.Count != TotalPositions || assignedPositions.Any(p => p < 1 || p > TotalPositions))
            throw new InvalidOperationException("All members must have a unique payout position from 1 to TotalPositions before locking.");

        Status = ThriftStatus.Locked;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Starts a new cycle in the rotation.
    /// </summary>
    public ThriftCycle StartCycle(int cycleNumber, DateTime startDateUtc, DateTime endDateUtc, DateTime dueDateUtc)
    {
        if (Status != ThriftStatus.Locked && Status != ThriftStatus.Active)
            throw new InvalidOperationException($"Cannot start cycle when thrift group status is {Status}.");

        var beneficiary = _members.FirstOrDefault(m => m.Position == cycleNumber && m.Status == ThriftMemberStatus.Active)
            ?? throw new InvalidOperationException($"No active member assigned to position {cycleNumber}.");

        var expectedPool = (TotalPositions - 1) * ContributionAmount; // All members except the recipient or all members contribute
        var cycle = ThriftCycle.Create(
            Id,
            cycleNumber,
            startDateUtc,
            endDateUtc,
            dueDateUtc,
            cycleNumber,
            beneficiary.UserId,
            TotalPositions * ContributionAmount);

        _cycles.Add(cycle);
        CurrentCycleNumber = cycleNumber;
        Status = ThriftStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;

        return cycle;
    }

    /// <summary>
    /// Marks the thrift group completed once all cycles have been paid out.
    /// </summary>
    public void CompleteGroup()
    {
        Status = ThriftStatus.Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Pauses the thrift group for investigation or administrative intervention.
    /// </summary>
    public void Pause(string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Pause reason is required.", nameof(reason));

        if (Status == ThriftStatus.Completed || Status == ThriftStatus.Cancelled)
            throw new InvalidOperationException($"Cannot pause thrift group in status '{Status}'.");

        Status = ThriftStatus.Paused;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Resumes a paused thrift group back to its active rotation.
    /// </summary>
    public void Resume(DateTime now)
    {
        if (Status != ThriftStatus.Paused)
            throw new InvalidOperationException($"Cannot resume thrift group with status '{Status}'.");

        Status = CurrentCycleNumber > 0 ? ThriftStatus.Active : ThriftStatus.Locked;
        UpdatedAtUtc = now;
    }
}
