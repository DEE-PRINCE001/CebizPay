using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Referrals.Enums;
using CebizPay.Domain.Referrals.Events;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CebizPay.UnitTests.Referrals;

public class ReferralQualificationServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task EvaluateQualification_WhenNoPendingRelationship_ReturnsFalse()
    {
        await using var db = CreateInMemoryDbContext();
        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);

        var result = await service.EvaluateQualificationAsync("unknown_user");

        Assert.False(result.IsQualified);
        Assert.False(result.RewardEligible);
        Assert.Contains("No pending referral relationship", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateQualification_WhenKycPending_ReturnsFalse()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_001";
        var referred = "referred_001";

        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZREF01", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        // KYC profile is Pending, not Verified
        var profile = new IndividualProfile(referred, "John", "Doe");
        db.IndividualProfiles.Add(profile);

        // Wallet with 5,000 deposit
        var wallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.Add(wallet);
        var funding = FundingTransaction.Create(wallet.Id, null, PaymentProvider.Monnify, "TX-1", FundingChannel.VirtualAccount, 5000m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);

        await db.SaveChangesAsync();

        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);
        var result = await service.EvaluateQualificationAsync(referred);

        Assert.False(result.IsQualified);
        Assert.Contains("KYC Tier 1", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateQualification_WhenDepositBelow1000_ReturnsFalse()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_002";
        var referred = "referred_002";

        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZREF02", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        // KYC is Verified
        var profile = new IndividualProfile(referred, "Jane", "Doe");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        // Wallet with only 999 NGN deposit (strictly below 1,000 threshold)
        var wallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.Add(wallet);
        var funding = FundingTransaction.Create(wallet.Id, null, PaymentProvider.Monnify, "TX-2", FundingChannel.VirtualAccount, 999.00m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);

        await db.SaveChangesAsync();

        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);
        var result = await service.EvaluateQualificationAsync(referred);

        Assert.False(result.IsQualified);
        Assert.Contains("1,000", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateQualification_WhenDepositExactly1000AndKycVerified_QualifiesAndCreatesEligibleReward()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_003";
        var referred = "referred_003";

        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZREF03", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        var profile = new IndividualProfile(referred, "Alice", "Smith");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.Add(wallet);
        var funding = FundingTransaction.Create(wallet.Id, null, PaymentProvider.Paystack, "TX-EXACT-1000", FundingChannel.Card, 1000.00m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);
        await db.SaveChangesAsync();

        var outbox = Substitute.For<IOutboxService>();
        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance, outbox);

        var result = await service.EvaluateQualificationAsync(referred);

        Assert.True(result.IsQualified);
        Assert.True(result.RewardEligible);

        // Verify relationship updated
        var updatedRel = await db.ReferralRelationships.FirstAsync(r => r.Id == rel.Id);
        Assert.Equal(ReferralQualificationStatus.Qualified, updatedRel.QualificationStatus);
        Assert.Equal(ReferralRewardEligibility.Eligible, updatedRel.RewardEligibility);
        Assert.Equal(1000.00m, updatedRel.QualifyingDepositAmount);
        Assert.Equal("TX-EXACT-1000", updatedRel.QualifyingDepositReference);

        // Verify reward created in Eligible state
        var reward = await db.ReferralRewards.FirstOrDefaultAsync(r => r.ReferralRelationshipId == rel.Id);
        Assert.NotNull(reward);
        Assert.Equal(ReferralRewardStatus.Eligible, reward.Status);
        Assert.Equal(500m, reward.Amount);
        Assert.Null(reward.LedgerTransactionReference); // No money moved!

        // Verify outbox domain event written
        outbox.Received(1).Write(Arg.Is<ReferralQualifiedDomainEvent>(e =>
            e.RelationshipId == rel.Id &&
            e.Eligibility == ReferralRewardEligibility.Eligible));
    }

    [Fact]
    public async Task EvaluateQualification_WhenMaxReferralsReached_QualifiesWithCapacityExceeded()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_cap";
        var referred = "referred_cap_new";

        // Setting: Max 2 referrals
        var setting = ReferralSetting.CreateDefault(500m, 2, "admin");
        db.ReferralSettings.Add(setting);

        // Referrer already has 2 qualified referrals
        var prev1 = ReferralRelationship.Create(referrer, "old_1", Guid.NewGuid(), "CBZCAP", DateTime.UtcNow);
        prev1.Qualify(1000m, "TX1", ReferralRewardEligibility.Eligible, DateTime.UtcNow);
        var prev2 = ReferralRelationship.Create(referrer, "old_2", Guid.NewGuid(), "CBZCAP", DateTime.UtcNow);
        prev2.Qualify(1000m, "TX2", ReferralRewardEligibility.Eligible, DateTime.UtcNow);
        db.ReferralRelationships.AddRange(prev1, prev2);

        // New relationship
        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZCAP", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        var profile = new IndividualProfile(referred, "Bob", "Brown");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.Add(wallet);
        var funding = FundingTransaction.Create(wallet.Id, null, PaymentProvider.Monnify, "TX-CAP-3", FundingChannel.VirtualAccount, 2500m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);

        await db.SaveChangesAsync();

        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);
        var result = await service.EvaluateQualificationAsync(referred);

        Assert.True(result.IsQualified);
        Assert.False(result.RewardEligible);

        var updatedRel = await db.ReferralRelationships.FirstAsync(r => r.Id == rel.Id);
        Assert.Equal(ReferralQualificationStatus.Qualified, updatedRel.QualificationStatus);
        Assert.Equal(ReferralRewardEligibility.CapacityExceeded, updatedRel.RewardEligibility);

        var reward = await db.ReferralRewards.FirstOrDefaultAsync(r => r.ReferralRelationshipId == rel.Id);
        Assert.NotNull(reward);
        Assert.Equal(ReferralRewardStatus.Rejected, reward.Status);
    }

    [Fact]
    public async Task EvaluateQualification_WhenIdentityCollisionDetected_HoldsForRiskReview()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_suspicious";
        var referred = "referred_suspicious";

        // Same phone number collision
        var userReferrer = new ApplicationUser { Id = referrer, UserName = "ref@cebizpay.com", Email = "ref@cebizpay.com", PhoneNumber = "08012340000" };
        var userReferred = new ApplicationUser { Id = referred, UserName = "new@cebizpay.com", Email = "new@cebizpay.com", PhoneNumber = "08012340000" };
        db.Users.AddRange(userReferrer, userReferred);

        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZRISK", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        var profile = new IndividualProfile(referred, "Suspicious", "User");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.Add(wallet);
        var funding = FundingTransaction.Create(wallet.Id, null, PaymentProvider.Monnify, "TX-COLLISION", FundingChannel.VirtualAccount, 5000m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);

        await db.SaveChangesAsync();

        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);
        var result = await service.EvaluateQualificationAsync(referred);

        Assert.True(result.IsQualified);
        Assert.False(result.RewardEligible); // Suspended

        var updatedRel = await db.ReferralRelationships.FirstAsync(r => r.Id == rel.Id);
        Assert.Equal(ReferralRewardEligibility.HeldForRiskReview, updatedRel.RewardEligibility);

        var reward = await db.ReferralRewards.FirstOrDefaultAsync(r => r.ReferralRelationshipId == rel.Id);
        Assert.NotNull(reward);
        Assert.Equal(ReferralRewardStatus.HeldForRiskReview, reward.Status);
    }

    [Fact]
    public async Task EvaluateQualification_RepeatedCalls_AreIdempotent()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_idem";
        var referred = "referred_idem";

        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZIDEM", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        var profile = new IndividualProfile(referred, "Idem", "User");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.Add(wallet);
        var funding = FundingTransaction.Create(wallet.Id, null, PaymentProvider.Monnify, "TX-IDEM", FundingChannel.VirtualAccount, 2000m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);

        await db.SaveChangesAsync();

        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);

        // First execution
        var result1 = await service.EvaluateQualificationAsync(referred);
        Assert.True(result1.IsQualified);

        // Second duplicate execution
        var result2 = await service.EvaluateQualificationAsync(referred);
        Assert.False(result2.IsQualified); // Not pending anymore

        // Verify exactly one reward was created
        var count = await db.ReferralRewards.CountAsync(r => r.ReferralRelationshipId == rel.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FinancialSafety_Phase6D_CreatesNoLedgerEntriesOrWalletMutations()
    {
        await using var db = CreateInMemoryDbContext();
        var referrer = "referrer_fin_safe";
        var referred = "referred_fin_safe";

        var referrerWallet = Wallet.CreateIndividualWallet(referrer, Currency.NGN);
        var referredWallet = Wallet.CreateIndividualWallet(referred, Currency.NGN);
        db.Wallets.AddRange(referrerWallet, referredWallet);

        var initialReferrerBalance = referrerWallet.AvailableBalance;

        var rel = ReferralRelationship.Create(referrer, referred, Guid.NewGuid(), "CBZSAFE", DateTime.UtcNow);
        db.ReferralRelationships.Add(rel);

        var profile = new IndividualProfile(referred, "Fin", "Safe");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var funding = FundingTransaction.Create(referredWallet.Id, null, PaymentProvider.Monnify, "TX-SAFE-1000", FundingChannel.VirtualAccount, 1000m, Currency.NGN);
        funding.MarkCompleted(Guid.NewGuid());
        db.FundingTransactions.Add(funding);

        await db.SaveChangesAsync();

        var service = new ReferralQualificationService(db, NullLogger<ReferralQualificationService>.Instance);
        var result = await service.EvaluateQualificationAsync(referred);

        Assert.True(result.IsQualified);

        // Assert 1: Referrer wallet balance remains completely unchanged
        var currentReferrerWallet = await db.Wallets.FirstAsync(w => w.Id == referrerWallet.Id);
        Assert.Equal(initialReferrerBalance, currentReferrerWallet.AvailableBalance);
        Assert.Equal(0m, currentReferrerWallet.AvailableBalance);

        // Assert 2: No double-entry ledger transactions created
        var ledgerTransactionsCount = await db.LedgerTransactions.CountAsync();
        Assert.Equal(0, ledgerTransactionsCount);

        // Assert 3: No ledger entries created
        var ledgerEntriesCount = await db.LedgerEntries.CountAsync();
        Assert.Equal(0, ledgerEntriesCount);
    }
}
