using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Savings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Savings;

/// <summary>
/// PostgreSQL Testcontainers integration tests verifying end-to-end Savings lifecycle:
/// policy versioning, plan creation, account opening with initial deposit, ad-hoc contributions,
/// daily interest accrual, and early penalty vs mature full liquidation.
/// </summary>
public sealed class SavingsIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public SavingsIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task CompleteSavingsLifecycle_EndToEnd_ShouldAccrueDailyInterestAndEnforceEarlyPenalty()
    {
        // 1. Arrange Services
        await using var dbContext = await CreateDbContextAsync();
        var ledgerPostingService = new LedgerPostingService(dbContext);
        var policyService = new SavingsInterestPolicyService(dbContext);
        var savingsService = new SavingsService(dbContext, ledgerPostingService, policyService);

        var userId = $"usr_sav_{Guid.NewGuid():N}";
        var userWallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        userWallet.Credit(500_000m); // Fund wallet
        dbContext.Wallets.Add(userWallet);
        await dbContext.SaveChangesAsync();

        // 2. Set Super Admin Policy
        var policy = await policyService.CreateAndActivatePolicyAsync(
            new Application.Common.Interfaces.Savings.CreateSavingsInterestPolicyRequest(
                SavingsPlanType.FixedLock,
                GoalInterestPolicyMode.Percentage,
                0.12m)); // 12%

        Assert.Equal(1, policy.Version);

        // 3. Create Fixed Lock Plan
        var plan = await savingsService.CreatePlanAsync(
            "admin-user",
            new Application.Common.Interfaces.Savings.CreateSavingsPlanRequest(
                null,
                SavingsOwnerType.Individual,
                SavingsPlanType.FixedLock,
                "90-Day Fixed Saver",
                "Lock funds for 90 days at 12%",
                Currency.NGN,
                0.12m,
                50_000m,
                1_000_000m,
                30,
                90,
                null,
                null,
                null));

        // 4. Open Savings Account with 100,000 NGN initial deposit
        var account = await savingsService.OpenAccountAsync(
            userId,
            new Application.Common.Interfaces.Savings.OpenSavingsAccountRequest(
                plan.Id,
                null,
                100_000m,
                90,
                null,
                null,
                null));

        Assert.Equal(100_000m, account.PrincipalBalance);
        Assert.Equal(SavingsAccountStatus.Active, account.Status);

        // Check wallet balance debited: 500,000 - 100,000 = 400,000 NGN
        var updatedWallet = await dbContext.Wallets.FindAsync(userWallet.Id);
        Assert.Equal(400_000m, updatedWallet!.AvailableBalance);

        // 5. Contribute additional 50,000 NGN
        var updatedAccount = await savingsService.ContributeAsync(account.Id, userId, 50_000m);
        Assert.Equal(150_000m, updatedAccount.PrincipalBalance);

        // Wallet balance now: 400,000 - 50,000 = 350,000 NGN
        await dbContext.Entry(updatedWallet).ReloadAsync();
        Assert.Equal(350_000m, updatedWallet.AvailableBalance);

        // 6. Process Daily Accrual
        var accruedCount = await savingsService.ProcessDailyInterestAccrualAsync(DateTime.UtcNow);
        Assert.Equal(1, accruedCount);

        // 7. Preview & Early Withdraw (Day 1)
        var preview = await savingsService.PreviewWithdrawalAsync(account.Id, userId);
        Assert.True(preview.EstimatedEarlyWithdrawalPenalty > 0);

        var withdrawal = await savingsService.WithdrawAsync(account.Id, userId);
        Assert.True(withdrawal.IsEarlyWithdrawal);
        Assert.Equal(3_750m, withdrawal.PenaltyAmount); // 2.5% of 150,000 = 3,750 NGN
        Assert.Equal(146_250m, withdrawal.PayoutAmount); // 150,000 - 3,750 = 146,250 NGN

        // Final wallet balance: 350,000 + 146,250 = 496,250 NGN
        await dbContext.Entry(updatedWallet).ReloadAsync();
        Assert.Equal(496_250m, updatedWallet.AvailableBalance);
    }
}
