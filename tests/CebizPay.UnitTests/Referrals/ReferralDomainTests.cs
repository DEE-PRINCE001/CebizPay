using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Referrals.Enums;
using Xunit;

namespace CebizPay.UnitTests.Referrals;

public class ReferralDomainTests
{
    [Fact]
    public void ReferralSetting_CreateDefault_HasValidDefaults()
    {
        var setting = ReferralSetting.CreateDefault(500.00m, 10, "admin_user");

        Assert.Equal(500.00m, setting.RewardAmountPerSuccessfulReferral);
        Assert.Equal(10, setting.MaximumSuccessfulReferralsPerUser);
        Assert.True(setting.IsActive);
        Assert.Equal(1, setting.Version);
        Assert.Equal("admin_user", setting.UpdatedBy);
    }

    [Fact]
    public void ReferralSetting_Update_UpdatesValuesAndIncrementsVersion()
    {
        var setting = ReferralSetting.CreateDefault(500.00m, 10, "admin1");
        var now = DateTime.UtcNow;

        setting.Update(1000.00m, 20, false, "admin2", now);

        Assert.Equal(1000.00m, setting.RewardAmountPerSuccessfulReferral);
        Assert.Equal(20, setting.MaximumSuccessfulReferralsPerUser);
        Assert.False(setting.IsActive);
        Assert.Equal(2, setting.Version);
        Assert.Equal("admin2", setting.UpdatedBy);
        Assert.Equal(now, setting.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-500, 10)]
    [InlineData(500, 0)]
    [InlineData(500, -5)]
    public void ReferralSetting_InvalidParameters_ThrowsArgumentException(decimal amount, int maxReferrals)
    {
        Assert.Throws<ArgumentException>(() => ReferralSetting.CreateDefault(amount, maxReferrals));
    }

    [Fact]
    public void ReferralCode_Create_NormalizesCodeToUppercase()
    {
        var now = DateTime.UtcNow;
        var code = ReferralCode.Create("user_123", "cbz789xyz", now);

        Assert.Equal("user_123", code.UserId);
        Assert.Equal("CBZ789XYZ", code.Code);
        Assert.True(code.IsActive);
        Assert.Equal(now, code.CreatedAtUtc);
    }

    [Fact]
    public void ReferralCode_Deactivate_SetsIsActiveFalse()
    {
        var code = ReferralCode.Create("user_123", "CBZ123", DateTime.UtcNow);
        code.Deactivate();

        Assert.False(code.IsActive);
    }

    [Fact]
    public void ReferralRelationship_SelfReferral_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReferralRelationship.Create("user_same", "user_same", Guid.NewGuid(), "CBZ123", DateTime.UtcNow));
    }

    [Fact]
    public void ReferralRelationship_Qualify_SetsMilestoneProperties()
    {
        var rel = ReferralRelationship.Create("referrer_1", "referred_2", Guid.NewGuid(), "CBZ123", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        rel.Qualify(1500.00m, "TX-DEP-999", ReferralRewardEligibility.Eligible, now);

        Assert.Equal(ReferralQualificationStatus.Qualified, rel.QualificationStatus);
        Assert.Equal(ReferralRewardEligibility.Eligible, rel.RewardEligibility);
        Assert.Equal(1500.00m, rel.QualifyingDepositAmount);
        Assert.Equal("TX-DEP-999", rel.QualifyingDepositReference);
        Assert.Equal(now, rel.QualifiedAtUtc);
    }

    [Fact]
    public void ReferralRelationship_Disqualified_CannotBeQualified()
    {
        var rel = ReferralRelationship.Create("referrer_1", "referred_2", Guid.NewGuid(), "CBZ123", DateTime.UtcNow);
        rel.Disqualify("Fraudulent account ring", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            rel.Qualify(1000m, "TX-1", ReferralRewardEligibility.Eligible, DateTime.UtcNow));
    }

    [Fact]
    public void ReferralReward_Create_InitializesEntitlementWithoutMoneyMovement()
    {
        var relId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var reward = ReferralReward.Create(relId, "referrer_1", "referred_2", 500m, ReferralRewardStatus.Eligible, now);

        Assert.Equal(relId, reward.ReferralRelationshipId);
        Assert.Equal("referrer_1", reward.ReferrerUserId);
        Assert.Equal("referred_2", reward.ReferredUserId);
        Assert.Equal(500m, reward.Amount);
        Assert.Equal("NGN", reward.Currency);
        Assert.Equal(ReferralRewardStatus.Eligible, reward.Status);
        Assert.Null(reward.LedgerTransactionReference); // No money movement in Phase 6D!
    }

    [Fact]
    public void ReferralReward_HoldForRiskReview_UpdatesStatus()
    {
        var reward = ReferralReward.Create(Guid.NewGuid(), "r1", "r2", 500m, ReferralRewardStatus.Pending, DateTime.UtcNow);
        reward.HoldForRiskReview(DateTime.UtcNow);

        Assert.Equal(ReferralRewardStatus.HeldForRiskReview, reward.Status);
    }
}
