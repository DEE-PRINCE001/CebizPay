using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using Xunit;

namespace CebizPay.UnitTests.Thrift;

public class ThriftUnitTests
{
    [Fact]
    public void CreateThriftGroup_InitializesGroupAndCreatorAsFirstMember()
    {
        // Arrange & Act
        var startDate = DateTime.UtcNow.AddDays(7);
        var group = ThriftGroup.Create(
            organizationId: null,
            creatorUserId: "user-creator",
            name: "Office Monthly Ajo",
            description: "Monthly rotational savings",
            currency: Currency.NGN,
            contributionAmount: 50_000m,
            frequency: ThriftFrequency.Monthly,
            totalPositions: 5,
            startDateUtc: startDate,
            positionSelectionDeadlineUtc: startDate.AddDays(-2));

        // Assert
        Assert.NotNull(group);
        Assert.Equal(ThriftStatus.OpenForMembers, group.Status);
        Assert.Equal(5, group.TotalPositions);
        Assert.Single(group.Members);
        Assert.Equal("user-creator", group.Members.First().UserId);
        Assert.Equal(ThriftMemberStatus.Active, group.Members.First().Status);
    }

    [Fact]
    public void SelectPosition_WithValidPosition_AssignsPositionSuccessfully()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(7);
        var group = ThriftGroup.Create(
            organizationId: null,
            creatorUserId: "user-creator",
            name: "Office Monthly Ajo",
            description: null,
            currency: Currency.NGN,
            contributionAmount: 50_000m,
            frequency: ThriftFrequency.Monthly,
            totalPositions: 3,
            startDateUtc: startDate,
            positionSelectionDeadlineUtc: startDate.AddDays(-2));

        var member = group.Members.First();

        // Act
        member.SelectPosition(1);

        // Assert
        Assert.Equal(1, member.Position);
        Assert.NotNull(member.PositionSelectedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectPosition_WithInvalidPosition_ThrowsArgumentException(int invalidPosition)
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(7);
        var group = ThriftGroup.Create(
            organizationId: null,
            creatorUserId: "user-creator",
            name: "Office Monthly Ajo",
            description: null,
            currency: Currency.NGN,
            contributionAmount: 50_000m,
            frequency: ThriftFrequency.Monthly,
            totalPositions: 3,
            startDateUtc: startDate,
            positionSelectionDeadlineUtc: startDate.AddDays(-2));

        var member = group.Members.First();

        // Assert
        Assert.Throws<ArgumentException>(() => member.SelectPosition(invalidPosition));
    }

    [Fact]
    public void ConsecutiveMissedContributions_SuspendsMemberAfterTwoMisses()
    {
        // Arrange
        var member = ThriftMember.Create(Guid.NewGuid(), "user-delinquent");

        // Act 1: First missed cycle
        var suspendedAfterFirst = member.RecordMissedContribution();

        // Assert 1
        Assert.False(suspendedAfterFirst);
        Assert.Equal(1, member.ConsecutiveMissedCycles);
        Assert.Equal(ThriftMemberStatus.Active, member.Status);

        // Act 2: Second consecutive missed cycle
        var suspendedAfterSecond = member.RecordMissedContribution();

        // Assert 2
        Assert.True(suspendedAfterSecond);
        Assert.Equal(2, member.ConsecutiveMissedCycles);
        Assert.Equal(ThriftMemberStatus.Suspended, member.Status);
        Assert.NotNull(member.SuspendedAtUtc);
    }

    [Fact]
    public void SuccessfulContribution_ResetsConsecutiveMissedCyclesCount()
    {
        // Arrange
        var member = ThriftMember.Create(Guid.NewGuid(), "user-recovery");
        member.RecordMissedContribution(); // 1 miss
        Assert.Equal(1, member.ConsecutiveMissedCycles);

        // Act - Contributes successfully
        member.RecordSuccessfulContribution(50_000m);

        // Assert
        Assert.Equal(0, member.ConsecutiveMissedCycles);
        Assert.Equal(50_000m, member.TotalContributed);
        Assert.Equal(ThriftMemberStatus.Active, member.Status);
    }

    [Fact]
    public void DepartingMember_CalculateNetRefundableAmount_FormulaMatchesExactContributedMinusPaid()
    {
        // Arrange
        var member = ThriftMember.Create(Guid.NewGuid(), "user-departing");
        member.RecordSuccessfulContribution(50_000m);
        member.RecordSuccessfulContribution(50_000m);
        member.RecordSuccessfulContribution(50_000m); // TotalContributed = 150,000 NGN

        // Case A: Member has received NO payout yet
        var refundNoPayout = member.CalculateNetRefundableAmount();
        Assert.Equal(150_000m, refundNoPayout);

        // Case B: Member received partial/full rotation payout (e.g. 100,000 NGN)
        member.RecordPayout(100_000m);
        var refundWithPayout = member.CalculateNetRefundableAmount();
        Assert.Equal(50_000m, refundWithPayout); // 150,000 - 100,000 = 50,000 NGN

        // Case C: Member received full pool (e.g. 200,000 NGN) which exceeds contributions
        member.RecordPayout(100_000m); // TotalPayout = 200,000 NGN
        var refundExcessPayout = member.CalculateNetRefundableAmount();
        Assert.Equal(0m, refundExcessPayout); // Max(0, 150,000 - 200,000) = 0 NGN
    }
}
