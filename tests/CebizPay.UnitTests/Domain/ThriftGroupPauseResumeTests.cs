using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for ThriftGroup administrative pause and resume lifecycle transitions.
/// </summary>
public sealed class ThriftGroupPauseResumeTests
{
    private static ThriftGroup CreateTestGroup()
    {
        return ThriftGroup.Create(
            organizationId: null,
            creatorUserId: "creator-user",
            name: "Community Thrift",
            description: "Test group",
            currency: Currency.NGN,
            contributionAmount: 20000m,
            frequency: ThriftFrequency.Monthly,
            totalPositions: 3,
            startDateUtc: DateTime.UtcNow.AddDays(5),
            positionSelectionDeadlineUtc: DateTime.UtcNow.AddDays(3));
    }

    [Fact]
    public void Pause_WhenOpenOrActive_ShouldSetPausedStatus()
    {
        var group = CreateTestGroup();
        var now = DateTime.UtcNow;

        group.Pause("Investigating suspicious delinquency pattern", now);

        Assert.Equal(ThriftStatus.Paused, group.Status);
        Assert.Equal(now, group.UpdatedAtUtc);
    }

    [Fact]
    public void Pause_WithEmptyReason_ShouldThrow()
    {
        var group = CreateTestGroup();

        Assert.Throws<ArgumentException>(() => group.Pause("", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => group.Pause("   ", DateTime.UtcNow));
    }

    [Fact]
    public void Pause_WhenCompleted_ShouldThrow()
    {
        var group = CreateTestGroup();
        group.CompleteGroup();

        Assert.Throws<InvalidOperationException>(() => group.Pause("Pause completed group", DateTime.UtcNow));
    }

    [Fact]
    public void Resume_WhenPaused_ShouldRestoreActiveOrLockedStatus()
    {
        var group = CreateTestGroup();
        group.Pause("Temporary freeze", DateTime.UtcNow);

        var now = DateTime.UtcNow;
        group.Resume(now);

        Assert.Equal(ThriftStatus.Locked, group.Status);
        Assert.Equal(now, group.UpdatedAtUtc);
    }

    [Fact]
    public void Resume_WhenNotPaused_ShouldThrow()
    {
        var group = CreateTestGroup();

        Assert.Throws<InvalidOperationException>(() => group.Resume(DateTime.UtcNow));
    }
}
