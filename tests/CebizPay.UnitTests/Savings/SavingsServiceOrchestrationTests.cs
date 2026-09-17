#pragma warning disable CA1848, CS1591
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Savings;

public sealed class SavingsServiceOrchestrationTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task OpenAccountAsync_WithExternalProvider_OnboardsCustomerCreatesAndFundsExternalPlan()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var policyService = Substitute.For<ISavingsInterestPolicyService>();
        var provider = Substitute.For<ISavingsProvider>();
        var factory = Substitute.For<ISavingsProviderFactory>();
        var logger = Substitute.For<ILogger<SavingsService>>();

        const string userId = "user-orch-1";
        const string providerName = "Cowrywise";
        const string extCustId = "cw-cust-123";
        const string extPlanId = "cw-plan-456";

        provider.ProviderName.Returns(providerName);
        factory.GetActiveProvider().Returns(provider);
        factory.GetProvider(providerName).Returns(provider);

        provider.EnsureCustomerAsync(Arg.Any<ExternalCustomerRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalCustomerResult(extCustId, "cw-sub-1", true, "active"));

        provider.CreatePlanAsync(Arg.Any<ExternalCreatePlanRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalSavingsPlanResult(extPlanId, null, "active", 0.12m, DateTime.UtcNow.AddDays(90)));

        provider.FundPlanAsync(Arg.Any<ExternalFundingRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalFundingResult("ft-123", true, 50_000m, DateTime.UtcNow));

        var ledgerTx = new LedgerTransaction(LedgerTransactionType.SavingsContribution);
        ledgerService.PostSavingsContributionCoreAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ledgerTx);

        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        wallet.Credit(100_000m);
        dbContext.Wallets.Add(wallet);

        var profile = new IndividualProfile(userId, "Amina", "Bello");
        dbContext.IndividualProfiles.Add(profile);

        var plan = SavingsPlan.CreateFixedLockPlan(
            null, userId, SavingsOwnerType.Individual, "Cowrywise 90-Day Lock", "External high yield lock",
            Currency.NGN, 0.12m, 10_000m, 1_000_000m, 30, 180, 1);
        dbContext.SavingsPlans.Add(plan);
        await dbContext.SaveChangesAsync();

        var sut = new SavingsService(dbContext, ledgerService, policyService, factory, logger);

        var request = new OpenSavingsAccountRequest(
            plan.Id,
            null,
            50_000m,
            90,
            null,
            null,
            null);

        // Act
        var result = await sut.OpenAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(providerName, result.ProviderName);
        Assert.Equal(extPlanId, result.ExternalPlanId);
        Assert.Equal(50_000m, result.PrincipalBalance);

        await provider.Received(1).EnsureCustomerAsync(
            Arg.Is<ExternalCustomerRequest>(r => r.UserId == userId && r.FirstName == "Amina" && r.LastName == "Bello"),
            Arg.Any<CancellationToken>());

        await provider.Received(1).CreatePlanAsync(
            Arg.Is<ExternalCreatePlanRequest>(r => r.ExternalCustomerId == extCustId && r.PrincipalAmount == 50_000m),
            Arg.Any<CancellationToken>());

        await ledgerService.Received(1).PostSavingsContributionCoreAsync(
            wallet.Id, 50_000m, Currency.NGN, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await provider.Received(1).FundPlanAsync(
            Arg.Is<ExternalFundingRequest>(r => r.ExternalPlanId == extPlanId && r.Amount == 50_000m),
            Arg.Any<CancellationToken>());

        var savedAccount = await dbContext.SavingsAccounts.FirstOrDefaultAsync(a => a.Id == result.Id);
        Assert.NotNull(savedAccount);
        Assert.Equal(providerName, savedAccount.ProviderName);
        Assert.Equal(extCustId, savedAccount.ExternalCustomerId);
        Assert.Equal(extPlanId, savedAccount.ExternalPlanId);
    }

    [Fact]
    public async Task ContributeAsync_WithExternalProvider_DebitsLedgerAndFundsExternalPlan()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var policyService = Substitute.For<ISavingsInterestPolicyService>();
        var provider = Substitute.For<ISavingsProvider>();
        var factory = Substitute.For<ISavingsProviderFactory>();
        var logger = Substitute.For<ILogger<SavingsService>>();

        const string userId = "user-orch-2";
        const string providerName = "Cowrywise";
        const string extPlanId = "cw-plan-789";

        provider.ProviderName.Returns(providerName);
        factory.GetProvider(providerName).Returns(provider);

        var ledgerTx = new LedgerTransaction(LedgerTransactionType.SavingsContribution);
        ledgerService.PostSavingsContributionCoreAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ledgerTx);

        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        wallet.Credit(100_000m);
        dbContext.Wallets.Add(wallet);

        var plan = SavingsPlan.CreateGoalBasedPlan(
            null, userId, SavingsOwnerType.Individual, "Vacation Fund", null,
            Currency.NGN, 500_000m, 10_000m, SavingsContributionFrequency.Monthly, 0.10m, 1);
        dbContext.SavingsPlans.Add(plan);

        var account = SavingsAccount.CreateGoalBasedAccount(
            plan.Id, userId, null, Currency.NGN, 500_000m, 10_000m, SavingsContributionFrequency.Monthly,
            0.10m, 1, DateTime.UtcNow, DateTime.UtcNow.AddDays(180));
        account.LinkExternalProvider(providerName, "cw-cust-2", extPlanId, "active");
        account.RecordContribution(20_000m, Guid.NewGuid(), "REF-INITIAL");
        dbContext.SavingsAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var sut = new SavingsService(dbContext, ledgerService, policyService, factory, logger);

        // Act
        var result = await sut.ContributeAsync(account.Id, userId, 15_000m);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(35_000m, result.PrincipalBalance);

        await ledgerService.Received(1).PostSavingsContributionCoreAsync(
            wallet.Id, 15_000m, Currency.NGN, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await provider.Received(1).FundPlanAsync(
            Arg.Is<ExternalFundingRequest>(r => r.ExternalPlanId == extPlanId && r.Amount == 15_000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithdrawAsync_WithExternalProvider_LiquidatesExternalPlanAndCreditsSettledAmount()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var policyService = Substitute.For<ISavingsInterestPolicyService>();
        var provider = Substitute.For<ISavingsProvider>();
        var factory = Substitute.For<ISavingsProviderFactory>();
        var logger = Substitute.For<ILogger<SavingsService>>();

        const string userId = "user-orch-3";
        const string providerName = "Anchor";
        const string extPlanId = "anc-subacc-999";

        provider.ProviderName.Returns(providerName);
        factory.GetProvider(providerName).Returns(provider);

        provider.LiquidatePlanAsync(Arg.Any<ExternalLiquidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalLiquidationResult(
                "ANC-TX-LIQ-1",
                100_000m,
                2_500m,
                0m,
                97_500m,
                true,
                DateTime.UtcNow));

        var ledgerTx = new LedgerTransaction(LedgerTransactionType.SavingsWithdrawal);
        ledgerService.PostSavingsWithdrawalCoreAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ledgerTx);

        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var plan = SavingsPlan.CreateFixedLockPlan(
            null, userId, SavingsOwnerType.Individual, "Anchor Locked Deposit", null,
            Currency.NGN, 0.12m, 10_000m, 5_000_000m, 30, 90, 1);
        dbContext.SavingsPlans.Add(plan);

        var account = SavingsAccount.CreateFixedLockAccount(
            plan.Id, userId, null, Currency.NGN, 0.12m, 1, 90, DateTime.UtcNow);
        account.LinkExternalProvider(providerName, "anc-cust-1", extPlanId, "active");
        account.RecordContribution(100_000m, Guid.NewGuid(), "REF-001");
        dbContext.SavingsAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var sut = new SavingsService(dbContext, ledgerService, policyService, factory, logger);

        // Act
        var result = await sut.WithdrawAsync(account.Id, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(97_500m, result.PayoutAmount);
        Assert.Equal(2_500m, result.PenaltyAmount);
        Assert.True(result.IsEarlyWithdrawal);

        await provider.Received(1).LiquidatePlanAsync(
            Arg.Is<ExternalLiquidationRequest>(r => r.ExternalPlanId == extPlanId && r.Amount == 100_000m && r.IsEarlyExit),
            Arg.Any<CancellationToken>());

        await ledgerService.Received(1).PostSavingsWithdrawalCoreAsync(
            wallet.Id, 97_500m, Currency.NGN, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var updatedAccount = await dbContext.SavingsAccounts.FindAsync(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(SavingsAccountStatus.Withdrawn, updatedAccount.Status);
        Assert.Equal(0m, updatedAccount.PrincipalBalance);
    }

    [Fact]
    public async Task ProcessDailyInterestAccrualAsync_WithExternalProvider_SyncsYieldWithoutInternalInterestExpense()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var policyService = Substitute.For<ISavingsInterestPolicyService>();
        var provider = Substitute.For<ISavingsProvider>();
        var factory = Substitute.For<ISavingsProviderFactory>();
        var logger = Substitute.For<ILogger<SavingsService>>();

        const string userId = "user-orch-4";
        const string providerName = "Cowrywise";
        const string extPlanId = "cw-plan-yield-1";

        provider.ProviderName.Returns(providerName);
        factory.GetProvider(providerName).Returns(provider);

        // Provider reports 350 NGN accumulated interest
        provider.GetPositionAsync(extPlanId, Arg.Any<CancellationToken>())
            .Returns(new ExternalSavingsPosition(
                extPlanId,
                100_000m,
                350m,
                350m,
                false,
                "active",
                DateTime.UtcNow));

        var plan = SavingsPlan.CreateFixedLockPlan(
            null, userId, SavingsOwnerType.Individual, "Cowrywise Yield Plan", null,
            Currency.NGN, 0.12m, 10_000m, 1_000_000m, 30, 90, 1);
        dbContext.SavingsPlans.Add(plan);

        var account = SavingsAccount.CreateFixedLockAccount(
            plan.Id, userId, null, Currency.NGN, 0.12m, 1, 90, DateTime.UtcNow.AddDays(-10));
        account.LinkExternalProvider(providerName, "cw-cust-1", extPlanId, "active");
        account.RecordContribution(100_000m, Guid.NewGuid(), "REF-001");
        dbContext.SavingsAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var sut = new SavingsService(dbContext, ledgerService, policyService, factory, logger);

        // Act
        var processedCount = await sut.ProcessDailyInterestAccrualAsync(DateTime.UtcNow.Date);

        // Assert
        Assert.Equal(1, processedCount);

        var updatedAccount = await dbContext.SavingsAccounts
            .Include(a => a.InterestAccruals)
            .FirstOrDefaultAsync(a => a.Id == account.Id);

        Assert.NotNull(updatedAccount);
        Assert.Equal(350m, updatedAccount.AccruedInterest);
        Assert.Single(updatedAccount.InterestAccruals);
        Assert.Equal(350m, updatedAccount.InterestAccruals.First().Amount);
    }
}
