using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain aggregate representation of a Thrift dispute or oversight case reported by a participant or flagged by the system.
/// Handled by Super Admin to investigate contributions, missed cycles, delinquency or payout concerns.
/// </summary>
public class ThriftDispute
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Target Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Optional associated cycle ID.</summary>
    public Guid? CycleId { get; private set; }

    /// <summary>Optional associated member ID.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>User ID of the reporting user or SYSTEM.</summary>
    public string ReportedByUserId { get; private set; } = string.Empty;

    /// <summary>Detailed description / reason for the dispute.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Current dispute lifecycle status.</summary>
    public ThriftDisputeStatus Status { get; private set; } = ThriftDisputeStatus.Open;

    /// <summary>Administrative resolution notes / findings.</summary>
    public string? ResolutionNotes { get; private set; }

    /// <summary>Super Admin user ID who decided/resolved the dispute.</summary>
    public string? ResolvedByUserId { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Resolution timestamp.</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private ThriftDispute() { } // EF Core

    /// <summary>
    /// Creates a new Thrift dispute in Open status.
    /// </summary>
    public static ThriftDispute Create(
        Guid thriftGroupId,
        Guid? cycleId,
        Guid? memberId,
        string reportedByUserId,
        string reason)
    {
        if (thriftGroupId == Guid.Empty)
            throw new ArgumentException("ThriftGroupId is required.", nameof(thriftGroupId));
        if (string.IsNullOrWhiteSpace(reportedByUserId))
            throw new ArgumentException("ReportedByUserId is required.", nameof(reportedByUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        return new ThriftDispute
        {
            Id = Guid.NewGuid(),
            ThriftGroupId = thriftGroupId,
            CycleId = cycleId,
            MemberId = memberId,
            ReportedByUserId = reportedByUserId.Trim(),
            Reason = reason.Trim(),
            Status = ThriftDisputeStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the dispute as actively under review by an administrator.
    /// </summary>
    public void MarkUnderReview(string reviewerUserId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("ReviewerUserId is required.", nameof(reviewerUserId));

        if (Status != ThriftDisputeStatus.Open)
            throw new InvalidOperationException($"Cannot mark dispute under review from status '{Status}'.");

        Status = ThriftDisputeStatus.UnderReview;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Resolves the dispute with administrative findings.
    /// </summary>
    public void Resolve(string resolverUserId, string resolutionNotes, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(resolverUserId))
            throw new ArgumentException("ResolverUserId is required.", nameof(resolverUserId));
        if (string.IsNullOrWhiteSpace(resolutionNotes))
            throw new ArgumentException("ResolutionNotes are required.", nameof(resolutionNotes));

        if (Status != ThriftDisputeStatus.Open && Status != ThriftDisputeStatus.UnderReview)
            throw new InvalidOperationException($"Cannot resolve dispute with status '{Status}'.");

        Status = ThriftDisputeStatus.Resolved;
        ResolvedByUserId = resolverUserId.Trim();
        ResolutionNotes = resolutionNotes.Trim();
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Rejects or dismisses the dispute as unfounded.
    /// </summary>
    public void Reject(string rejecterUserId, string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(rejecterUserId))
            throw new ArgumentException("RejecterUserId is required.", nameof(rejecterUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (Status != ThriftDisputeStatus.Open && Status != ThriftDisputeStatus.UnderReview)
            throw new InvalidOperationException($"Cannot reject dispute with status '{Status}'.");

        Status = ThriftDisputeStatus.Rejected;
        ResolvedByUserId = rejecterUserId.Trim();
        ResolutionNotes = reason.Trim();
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
    }
}
