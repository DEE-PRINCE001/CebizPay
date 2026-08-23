using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain entity representing a rotation cycle in a Thrift group.
/// Tracks scheduled member collection progress and pool payout settlement to the cycle beneficiary.
/// </summary>
public class ThriftCycle
{
    private readonly List<ThriftContribution> _contributions = [];

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Cycle number in the rotation (1-indexed).</summary>
    public int CycleNumber { get; private set; }

    /// <summary>Cycle start timestamp.</summary>
    public DateTime StartDateUtc { get; private set; }

    /// <summary>Cycle end timestamp.</summary>
    public DateTime EndDateUtc { get; private set; }

    /// <summary>Collection due timestamp (02:00 UTC automated trigger date).</summary>
    public DateTime DueDateUtc { get; private set; }

    /// <summary>Target payout rotation position for this cycle.</summary>
    public int TargetPayoutPosition { get; private set; }

    /// <summary>Identity user ID of the beneficiary member entitled to receive this cycle's pool payout.</summary>
    public string TargetBeneficiaryUserId { get; private set; } = string.Empty;

    /// <summary>Total expected contribution pool for this cycle.</summary>
    public decimal TotalExpectedPool { get; private set; }

    /// <summary>Actual successfully collected contribution pool available for payout distribution.</summary>
    public decimal TotalCollectedPool { get; private set; }

    /// <summary>Current cycle status (Upcoming, Collecting, ReadyForPayout, Paid, Failed).</summary>
    public ThriftCycleStatus Status { get; private set; } = ThriftCycleStatus.Upcoming;

    /// <summary>Timestamp when pool payout was settled to beneficiary.</summary>
    public DateTime? PayoutCompletedAtUtc { get; private set; }

    /// <summary>Ledger transaction ID corresponding to the pool payout settlement.</summary>
    public Guid? PayoutLedgerTransactionId { get; private set; }

    /// <summary>Failure reason if cycle processing failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last state update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Member contributions collected for this cycle.</summary>
    public IReadOnlyCollection<ThriftContribution> Contributions => _contributions.AsReadOnly();

    private ThriftCycle() { } // EF Core

    /// <summary>
    /// Creates a new rotation cycle.
    /// </summary>
    public static ThriftCycle Create(
        Guid thriftGroupId,
        int cycleNumber,
        DateTime startDateUtc,
        DateTime endDateUtc,
        DateTime dueDateUtc,
        int targetPayoutPosition,
        string targetBeneficiaryUserId,
        decimal totalExpectedPool)
    {
        if (thriftGroupId == Guid.Empty)
            throw new ArgumentException("ThriftGroupId is required.", nameof(thriftGroupId));
        if (cycleNumber <= 0)
            throw new ArgumentException("CycleNumber must be positive.", nameof(cycleNumber));
        if (string.IsNullOrWhiteSpace(targetBeneficiaryUserId))
            throw new ArgumentException("TargetBeneficiaryUserId is required.", nameof(targetBeneficiaryUserId));

        return new ThriftCycle
        {
            Id = Guid.NewGuid(),
            ThriftGroupId = thriftGroupId,
            CycleNumber = cycleNumber,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            DueDateUtc = dueDateUtc,
            TargetPayoutPosition = targetPayoutPosition,
            TargetBeneficiaryUserId = targetBeneficiaryUserId,
            TotalExpectedPool = totalExpectedPool,
            TotalCollectedPool = 0m,
            Status = ThriftCycleStatus.Collecting,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds or updates a member's contribution record for this cycle.
    /// </summary>
    public void AddContribution(ThriftContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        _contributions.Add(contribution);
        if (contribution.Status == ThriftContributionStatus.Successful)
        {
            TotalCollectedPool += contribution.Amount;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the cycle ready for payout distribution once collection is completed.
    /// </summary>
    public void MarkReadyForPayout()
    {
        if (Status != ThriftCycleStatus.Collecting)
            return;

        Status = ThriftCycleStatus.ReadyForPayout;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the cycle paid following successful ledger distribution to the beneficiary.
    /// </summary>
    public void MarkPaid(Guid ledgerTransactionId, DateTime asOfUtc)
    {
        Status = ThriftCycleStatus.Paid;
        PayoutCompletedAtUtc = asOfUtc;
        PayoutLedgerTransactionId = ledgerTransactionId;
        UpdatedAtUtc = asOfUtc;
    }

    /// <summary>
    /// Marks the cycle failed due to an error.
    /// </summary>
    public void MarkFailed(string reason)
    {
        Status = ThriftCycleStatus.Failed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
