using CebizPay.Application.Common.Exceptions;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

public sealed class LedgerPostingIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public LedgerPostingIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateDbContext()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task PostSingleCurrencyTransaction_ShouldUpdateBalancesAndCreateBalancedEntries()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var receiverId = $"user_receiver_{Guid.NewGuid():N}";

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        var receiverWallet = await walletService.GetOrCreateIndividualWalletAsync(receiverId, Currency.NGN);

        // Fund sender wallet directly for setup
        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var senderAccount = await dbContext.LedgerAccounts.FirstAsync(l => l.WalletId == senderWallet.Id);
        var receiverAccount = await dbContext.LedgerAccounts.FirstAsync(l => l.WalletId == receiverWallet.Id);

        // Act
        var transaction = await postingService.PostSingleCurrencyTransactionAsync(
            senderAccount.Id,
            receiverAccount.Id,
            amount: 25000m,
            currency: Currency.NGN,
            transactionType: LedgerTransactionType.PeerTransfer,
            description: "P2P transfer test");

        // Assert
        Assert.NotNull(transaction);
        Assert.Equal(LedgerTransactionStatus.Completed, transaction.Status);

        // Verify balance materialization
        var updatedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        var updatedReceiver = await dbContext.Wallets.FindAsync(receiverWallet.Id);

        Assert.Equal(75000m, updatedSender!.AvailableBalance);
        Assert.Equal(25000m, updatedReceiver!.AvailableBalance);

        // Verify double-entry balancing invariant
        var entries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == transaction.Id)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        var debit = entries.Single(e => e.Direction == LedgerEntryDirection.Debit);
        var credit = entries.Single(e => e.Direction == LedgerEntryDirection.Credit);

        Assert.Equal(debit.Amount, credit.Amount); // Total Debits == Total Credits
        Assert.Equal(25000m, debit.Amount);
    }

    [Fact]
    public async Task WalletService_ReportingOnlyCurrencies_ShouldBeRejected()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var userId = $"user_{Guid.NewGuid():N}";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => walletService.GetOrCreateIndividualWalletAsync(userId, Currency.USD));
        await Assert.ThrowsAsync<ArgumentException>(() => walletService.GetOrCreateIndividualWalletAsync(userId, Currency.GHS));
        await Assert.ThrowsAsync<ArgumentException>(() => walletService.GetOrCreateIndividualWalletAsync(userId, Currency.EUR));
        await Assert.ThrowsAsync<ArgumentException>(() => walletService.GetOrCreateIndividualWalletAsync(userId, Currency.INR));
    }

    [Fact]
    public async Task PostCrossCurrencyTransaction_OptionA_ShouldBalancePerCurrencySideAndPersistFxRecord()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var orgId = Guid.NewGuid();
        var staffId = $"staff_{Guid.NewGuid():N}";

        var orgNgnWallet = await walletService.GetOrCreateOrganizationWalletAsync(orgId, Currency.NGN);
        var staffUsdtWallet = await walletService.GetOrCreateIndividualWalletAsync(staffId, Currency.USDT);

        // Fund org NGN wallet
        orgNgnWallet.Credit(1500000m);
        await dbContext.SaveChangesAsync();

        // Act - Cross-currency FX transfer: ₦1,500,000 NGN -> 1,000 USDT (Rate = 0.00066667)
        var (transaction, fxRecord) = await postingService.PostCrossCurrencyTransactionAsync(
            sourceWalletId: orgNgnWallet.Id,
            targetWalletId: staffUsdtWallet.Id,
            sourceAmount: 1500000m,
            targetAmount: 1000m,
            rate: 0.00066667m,
            rateProvider: "CebizPayFxEngine",
            rateTimestamp: DateTime.UtcNow,
            description: "Cross-currency payroll conversion");

        // Assert
        Assert.Equal(LedgerTransactionStatus.Completed, transaction.Status);

        var updatedOrgWallet = await dbContext.Wallets.FindAsync(orgNgnWallet.Id);
        var updatedStaffWallet = await dbContext.Wallets.FindAsync(staffUsdtWallet.Id);

        Assert.Equal(0m, updatedOrgWallet!.AvailableBalance);
        Assert.Equal(1000m, updatedStaffWallet!.AvailableBalance);

        // Verify FX record
        Assert.Equal(transaction.Id, fxRecord.LedgerTransactionId);
        Assert.Equal(Currency.NGN, fxRecord.SourceCurrency);
        Assert.Equal(Currency.USDT, fxRecord.TargetCurrency);
        Assert.Equal(1500000m, fxRecord.SourceAmount);
        Assert.Equal(1000m, fxRecord.TargetAmount);

        // Verify double-entry balancing per currency side
        var entries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == transaction.Id)
            .ToListAsync();

        Assert.Equal(4, entries.Count);

        var ngnEntries = entries.Where(e => e.Currency == Currency.NGN).ToList();
        var usdtEntries = entries.Where(e => e.Currency == Currency.USDT).ToList();

        var ngnDebits = ngnEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var ngnCredits = ngnEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);

        var usdtDebits = usdtEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var usdtCredits = usdtEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);

        // NGN side balances independently
        Assert.Equal(1500000m, ngnDebits);
        Assert.Equal(1500000m, ngnCredits);

        // USDT side balances independently
        Assert.Equal(1000m, usdtDebits);
        Assert.Equal(1000m, usdtCredits);
    }

    [Fact]
    public async Task ReverseTransaction_ShouldCreateOffsettingEntriesAndRejectDuplicateReversals()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var userA = $"user_a_{Guid.NewGuid():N}";
        var userB = $"user_b_{Guid.NewGuid():N}";

        var walletA = await walletService.GetOrCreateIndividualWalletAsync(userA, Currency.NGN);
        var walletB = await walletService.GetOrCreateIndividualWalletAsync(userB, Currency.NGN);

        walletA.Credit(50000m);
        await dbContext.SaveChangesAsync();

        var accountA = await dbContext.LedgerAccounts.FirstAsync(l => l.WalletId == walletA.Id);
        var accountB = await dbContext.LedgerAccounts.FirstAsync(l => l.WalletId == walletB.Id);

        var originalTxn = await postingService.PostSingleCurrencyTransactionAsync(
            accountA.Id,
            accountB.Id,
            amount: 20000m,
            currency: Currency.NGN,
            transactionType: LedgerTransactionType.PeerTransfer);

        // Act 1 - Reverse transaction
        var reversalTxn = await postingService.ReverseTransactionAsync(originalTxn.Id, "User requested refund");

        // Assert 1 - Original transaction marked Reversed
        var reloadedOriginal = await dbContext.LedgerTransactions.FindAsync(originalTxn.Id);
        Assert.Equal(LedgerTransactionStatus.Reversed, reloadedOriginal!.Status);

        // Balances restored to initial state
        var reloadedA = await dbContext.Wallets.FindAsync(walletA.Id);
        var reloadedB = await dbContext.Wallets.FindAsync(walletB.Id);
        Assert.Equal(50000m, reloadedA!.AvailableBalance);
        Assert.Equal(0m, reloadedB!.AvailableBalance);

        // Act 2 & Assert 2 - Duplicate reversal rejected
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.ReverseTransactionAsync(originalTxn.Id, "Duplicate reversal request"));
    }

    [Fact]
    public async Task IdempotencyService_ScopedUniquenessAndConflictResolution()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var idempotencyService = new IdempotencyService(dbContext);

        var userA = $"user_a_{Guid.NewGuid():N}";
        var userB = $"user_b_{Guid.NewGuid():N}";
        var sharedKey = $"key_{Guid.NewGuid():N}";
        var opTransfer = "Transfer";
        var opWithdrawal = "Withdrawal";

        var payloadA1 = "{\"amount\": 100, \"recipient\": \"bob\"}";
        var payloadA2 = "{\"amount\": 999, \"recipient\": \"charlie\"}"; // Different payload
        var payloadB = "{\"amount\": 500, \"recipient\": \"dave\"}";

        // 1. User A + Transfer + Key X + Request A -> succeeds
        var recA1 = await idempotencyService.CreateRecordAsync(sharedKey, opTransfer, payloadA1, userId: userA);
        Assert.NotNull(recA1);

        // 2. User A + Transfer + Key X + Same Request -> Returns original result
        var recA1Retry = await idempotencyService.CreateRecordAsync(sharedKey, opTransfer, payloadA1, userId: userA);
        Assert.Equal(recA1.Id, recA1Retry.Id);

        // 3. User A + Transfer + Key X + Different Request -> Throws IdempotencyConflictException
        var conflictEx = await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            idempotencyService.CreateRecordAsync(sharedKey, opTransfer, payloadA2, userId: userA));
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", conflictEx.Code);
        Assert.Equal(sharedKey, conflictEx.IdempotencyKey);

        // 4. User B + Transfer + Key X + Independent Request -> succeeds (scoped per user)
        var recB = await idempotencyService.CreateRecordAsync(sharedKey, opTransfer, payloadB, userId: userB);
        Assert.NotNull(recB);
        Assert.NotEqual(recA1.Id, recB.Id);

        // 5. User A + Different Operation (Withdrawal) + Key X -> succeeds (scoped per operation)
        var recOp = await idempotencyService.CreateRecordAsync(sharedKey, opWithdrawal, payloadA1, userId: userA);
        Assert.NotNull(recOp);
        Assert.NotEqual(recA1.Id, recOp.Id);
    }

    [Fact]
    public async Task ConcurrencyProtection_TwoConcurrentDebitsFromSameWallet_OneSucceeds_OneFails_BalanceNeverNegative()
    {
        // Arrange
        var userId = $"concurrent_user_{Guid.NewGuid():N}";
        var receiver1 = $"concurrent_rec1_{Guid.NewGuid():N}";
        var receiver2 = $"concurrent_rec2_{Guid.NewGuid():N}";

        await using var initDb = CreateDbContext();
        var initWalletService = new WalletService(initDb);

        var senderWallet = await initWalletService.GetOrCreateIndividualWalletAsync(userId, Currency.NGN);
        var recWallet1 = await initWalletService.GetOrCreateIndividualWalletAsync(receiver1, Currency.NGN);
        var recWallet2 = await initWalletService.GetOrCreateIndividualWalletAsync(receiver2, Currency.NGN);

        senderWallet.Credit(100000m); // Initial balance = ₦100,000
        await initDb.SaveChangesAsync();

        var senderAccId = (await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == senderWallet.Id)).Id;
        var recAccId1 = (await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == recWallet1.Id)).Id;
        var recAccId2 = (await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == recWallet2.Id)).Id;

        // Act - Trigger 2 concurrent debits for ₦80,000 each (Total ₦160,000 requested)
        var task1 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new LedgerPostingService(db);
            return await service.PostSingleCurrencyTransactionAsync(senderAccId, recAccId1, 80000m, Currency.NGN, LedgerTransactionType.PeerTransfer);
        });

        var task2 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new LedgerPostingService(db);
            return await service.PostSingleCurrencyTransactionAsync(senderAccId, recAccId2, 80000m, Currency.NGN, LedgerTransactionType.PeerTransfer);
        });

        var results = await Task.WhenAll(task1.ExecutingTaskWithoutException(), task2.ExecutingTaskWithoutException());

        // Assert - Exactly 1 task succeeded and 1 failed
        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // Verify final wallet balance in DB
        await using var verifyDb = CreateDbContext();
        var finalSender = await verifyDb.Wallets.FindAsync(senderWallet.Id);
        Assert.Equal(20000m, finalSender!.AvailableBalance); // 100k - 80k = 20k, NEVER negative!
    }
}

internal static class TaskTestExtensions
{
    public static async Task<(bool IsSuccess, Exception? Exception)> ExecutingTaskWithoutException<T>(this Task<T> task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }
}
