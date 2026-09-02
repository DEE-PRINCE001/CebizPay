using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for ThriftDispute aggregate.
/// Covers dispute opening, marking under review, administrative resolution, and rejection.
/// </summary>
public sealed class ThriftDisputeTests
{
    [Fact]
    public void CreateDispute_ShouldInitializeOpenStatus()
    {
        var groupId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var reporter = "reporter-user-1";
        var reason = "Missed payout for cycle 2 despite full contribution.";

        var dispute = ThriftDispute.Create(groupId, cycleId, memberId, reporter, reason);

        Assert.NotEqual(Guid.Empty, dispute.Id);
        Assert.Equal(groupId, dispute.ThriftGroupId);
        Assert.Equal(cycleId, dispute.CycleId);
        Assert.Equal(memberId, dispute.MemberId);
        Assert.Equal(reporter, dispute.ReportedByUserId);
        Assert.Equal(reason, dispute.Reason);
        Assert.Equal(ThriftDisputeStatus.Open, dispute.Status);
        Assert.Null(dispute.ResolvedByUserId);
        Assert.Null(dispute.ResolutionNotes);
        Assert.Null(dispute.ResolvedAtUtc);
    }

    [Fact]
    public void CreateDispute_WithEmptyArguments_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => ThriftDispute.Create(Guid.Empty, null, null, "user", "reason"));
        Assert.Throws<ArgumentException>(() => ThriftDispute.Create(Guid.NewGuid(), null, null, "", "reason"));
        Assert.Throws<ArgumentException>(() => ThriftDispute.Create(Guid.NewGuid(), null, null, "user", ""));
    }

    [Fact]
    public void MarkUnderReview_WhenOpen_ShouldTransitionStatus()
    {
        var dispute = ThriftDispute.Create(Guid.NewGuid(), null, null, "user-1", "Contested position");
        var now = DateTime.UtcNow;

        dispute.MarkUnderReview("admin-user", now);

        Assert.Equal(ThriftDisputeStatus.UnderReview, dispute.Status);
        Assert.Equal(now, dispute.UpdatedAtUtc);
    }

    [Fact]
    public void Resolve_WhenOpenOrUnderReview_ShouldSetResolvedStatusAndNotes()
    {
        var dispute = ThriftDispute.Create(Guid.NewGuid(), null, null, "user-1", "Contribution issue");
        var now = DateTime.UtcNow;

        dispute.Resolve("super-admin-user", "Contribution verified and credited manually.", now);

        Assert.Equal(ThriftDisputeStatus.Resolved, dispute.Status);
        Assert.Equal("super-admin-user", dispute.ResolvedByUserId);
        Assert.Equal("Contribution verified and credited manually.", dispute.ResolutionNotes);
        Assert.Equal(now, dispute.ResolvedAtUtc);
    }

    [Fact]
    public void Reject_WhenOpenOrUnderReview_ShouldSetRejectedStatusAndReason()
    {
        var dispute = ThriftDispute.Create(Guid.NewGuid(), null, null, "user-1", "Frivolous dispute");
        var now = DateTime.UtcNow;

        dispute.Reject("super-admin-user", "Dispute dismissed as user had not funded wallet.", now);

        Assert.Equal(ThriftDisputeStatus.Rejected, dispute.Status);
        Assert.Equal("super-admin-user", dispute.ResolvedByUserId);
        Assert.Equal("Dispute dismissed as user had not funded wallet.", dispute.ResolutionNotes);
        Assert.Equal(now, dispute.ResolvedAtUtc);
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_ShouldThrow()
    {
        var dispute = ThriftDispute.Create(Guid.NewGuid(), null, null, "user-1", "Issue");
        dispute.Resolve("admin", "Resolved", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => dispute.Resolve("admin", "Another note", DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => dispute.Reject("admin", "Rejecting", DateTime.UtcNow));
    }
}
