using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using Xunit;

namespace CebizPay.UnitTests.Savings;

public class SavingsUnitTests
{
    [Fact]
    public void CreateFixedLockPlan_WithValidParameters_CreatesPlanSuccessfully()
    {
        // Arrange & Act
        var plan = SavingsPlan.CreateFixedLockPlan(
            organizationId: null,
            createdByUserId: "user-123",
            ownerType: SavingsOwnerType.Individual,
            name: "High Yield Fixed 90 Days",
            description: "Fixed lock savings for 90 days",
            currency: Currency.NGN,
            interestRate: 0.12m, // 12% per annum
            minimumAmount: 10_000m,
            maximumAmount: 5_000_000m,
            minimumDurationDays: 90,
            maximumDurationDays: 180,
            interestPolicyVersion: 1);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(SavingsPlanType.FixedLock, plan.PlanType);
        Assert.Equal(0.12m, plan.InterestRate);
        Assert.Equal(90, plan.MinimumDurationDays);
        Assert.Equal(180, plan.MaximumDurationDays);
        Assert.True(plan.IsActive);
    }

    [Theory]
    [InlineData(0.05)] // Below 8%
    [InlineData(0.16)] // Above 15%
    public void CreateFixedLockPlan_WithInvalidInterestRate_ThrowsArgumentException(decimal invalidRate)
    {
        // Assert
        Assert.Throws<ArgumentException>(() =>
            SavingsPlan.CreateFixedLockPlan(
                organizationId: null,
                createdByUserId: "user-123",
                ownerType: SavingsOwnerType.Individual,
                name: "Invalid Rate Plan",
                description: null,
                currency: Currency.NGN,
                interestRate: invalidRate,
                minimumAmount: 10_000m,
                maximumAmount: 1_000_000m,
                minimumDurationDays: 30,
                maximumDurationDays: 90,
                interestPolicyVersion: 1));
    }

    [Theory]
    [InlineData(20)]  // Below 30 days
    [InlineData(800)] // Above 730 days (2 years)
    public void CreateFixedLockPlan_WithInvalidDuration_ThrowsArgumentException(int invalidDuration)
    {
        // Assert
        Assert.Throws<ArgumentException>(() =>
            SavingsPlan.CreateFixedLockPlan(
                organizationId: null,
                createdByUserId: "user-123",
                ownerType: SavingsOwnerType.Individual,
                name: "Invalid Duration Plan",
                description: null,
                currency: Currency.NGN,
                interestRate: 0.10m,
                minimumAmount: 10_000m,
                maximumAmount: 1_000_000m,
                minimumDurationDays: invalidDuration < 30 ? invalidDuration : 30,
                maximumDurationDays: invalidDuration > 730 ? invalidDuration : 90,
                interestPolicyVersion: 1));
    }

    [Fact]
    public void CreateGoalBasedPlan_WithValidParameters_CreatesPlanSuccessfully()
    {
        // Arrange & Act
        var plan = SavingsPlan.CreateGoalBasedPlan(
            organizationId: null,
            createdByUserId: "user-123",
            ownerType: SavingsOwnerType.Individual,
            name: "Vacation Fund",
            description: "Save monthly for vacation",
            currency: Currency.NGN,
            targetAmount: 500_000m,
            contributionAmount: 50_000m,
            contributionFrequency: SavingsContributionFrequency.Monthly,
            interestRate: 0.05m,
            interestPolicyVersion: 1);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(SavingsPlanType.GoalBased, plan.PlanType);
        Assert.Equal(500_000m, plan.TargetAmount);
        Assert.Equal(50_000m, plan.ContributionAmount);
        Assert.Equal(SavingsContributionFrequency.Monthly, plan.ContributionFrequency);
    }

    [Fact]
    public void SavingsAccount_AccrueDailyInterest_ComputesAccrualAndBalancesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = SavingsAccount.CreateFixedLockAccount(
            savingsPlanId: Guid.NewGuid(),
            ownerUserId: "user-123",
            organizationId: null,
            currency: Currency.NGN,
            interestRate: 0.12m, // 12%
            interestPolicyVersion: 1,
            durationDays: 90,
            startDateUtc: startDate);

        account.RecordContribution(100_000m, Guid.NewGuid(), "DEP-001");

        // Act - Daily accrual for 100,000 NGN @ 12% is (100,000 * 0.12) / 365 = 32.8767 NGN
        var dailyInterest = Math.Round(100_000m * (0.12m / 365m), 4, MidpointRounding.AwayFromZero);
        account.AccrueDailyInterest(dailyInterest, startDate.Date);

        // Assert
        Assert.Equal(100_000m, account.PrincipalBalance);
        Assert.Equal(dailyInterest, account.AccruedInterest);
        Assert.Single(account.InterestAccruals);
        Assert.Equal(dailyInterest, account.InterestAccruals.First().Amount);
    }

    [Fact]
    public void CalculateWithdrawalTerms_WhenBeforeMaturity_AppliesEarlyPenaltyAndForfeitsAllInterest()
    {
        // Arrange
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = SavingsAccount.CreateFixedLockAccount(
            savingsPlanId: Guid.NewGuid(),
            ownerUserId: "user-123",
            organizationId: null,
            currency: Currency.NGN,
            interestRate: 0.10m,
            interestPolicyVersion: 1,
            durationDays: 90,
            startDateUtc: startDate);

        account.RecordContribution(100_000m, Guid.NewGuid(), "DEP-001");
        account.AccrueDailyInterest(200m, startDate.Date);

        // Act - Request early withdrawal at Day 45 (Maturity is Day 90)
        var terms = account.CalculateWithdrawalTerms(startDate.AddDays(45));

        // Assert
        Assert.True(terms.IsEarly);
        Assert.Equal(200m, terms.ForfeitedInterest); // 100% accrued interest forfeited
        Assert.Equal(2_500m, terms.PenaltyAmount);   // 2.5% of 100,000 NGN principal = 2,500 NGN
        Assert.Equal(97_500m, terms.PayoutAmount);  // Principal (100,000) - Penalty (2,500) = 97,500 NGN
    }

    [Fact]
    public void CalculateWithdrawalTerms_WhenAtOrAfterMaturity_DisbursesFullPrincipalAndAccruedInterestWithoutPenalty()
    {
        // Arrange
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = SavingsAccount.CreateFixedLockAccount(
            savingsPlanId: Guid.NewGuid(),
            ownerUserId: "user-123",
            organizationId: null,
            currency: Currency.NGN,
            interestRate: 0.10m,
            interestPolicyVersion: 1,
            durationDays: 90,
            startDateUtc: startDate);

        account.RecordContribution(100_000m, Guid.NewGuid(), "DEP-001");
        account.AccrueDailyInterest(2_465.75m, startDate.Date);

        // Act - Request withdrawal at Day 90 (Maturity reached)
        var terms = account.CalculateWithdrawalTerms(startDate.AddDays(90));

        // Assert
        Assert.False(terms.IsEarly);
        Assert.Equal(0m, terms.PenaltyAmount);
        Assert.Equal(0m, terms.ForfeitedInterest);
        Assert.Equal(102_465.75m, terms.PayoutAmount); // Full Principal + Full Accrued Interest
    }

    [Fact]
    public void ExecuteWithdrawal_TransitionsStatusToWithdrawnAndRecordsAuditSnapshot()
    {
        // Arrange
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = SavingsAccount.CreateFixedLockAccount(
            savingsPlanId: Guid.NewGuid(),
            ownerUserId: "user-123",
            organizationId: null,
            currency: Currency.NGN,
            interestRate: 0.10m,
            interestPolicyVersion: 1,
            durationDays: 90,
            startDateUtc: startDate);

        account.RecordContribution(100_000m, Guid.NewGuid(), "DEP-001");
        var terms = account.CalculateWithdrawalTerms(startDate.AddDays(90));
        var ledgerTxId = Guid.NewGuid();

        // Act
        account.ExecuteWithdrawal(terms.PayoutAmount, terms.PenaltyAmount, terms.ForfeitedInterest, ledgerTxId, startDate.AddDays(90));

        // Assert
        Assert.Equal(SavingsAccountStatus.Withdrawn, account.Status);
        Assert.Equal(0m, account.PrincipalBalance);
        Assert.Equal(0m, account.AccruedInterest);
        Assert.Equal(ledgerTxId, account.WithdrawalLedgerTransactionId);
        Assert.NotNull(account.WithdrawnAtUtc);
    }
}
