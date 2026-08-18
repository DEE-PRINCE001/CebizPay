using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using CebizPay.Infrastructure.Services;
using CebizPay.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

public sealed class RemediationConcurrencyTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public RemediationConcurrencyTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private static bool _dbEnsured;
    private static readonly object DbLock = new();

    private ApplicationDbContext CreateDbContext()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);

        lock (DbLock)
        {
            if (!_dbEnsured)
            {
                context.Database.EnsureCreated();
                _dbEnsured = true;
            }
        }

        return context;
    }

    [Fact]
    public async Task Test1_ConcurrentDoubleSpend_ShouldAllowOnlyOneTransaction()
    {
        // Arrange: Wallet balance = 100 NGN
        var senderUserId = $"double_spend_sender_{Guid.NewGuid():N}";
        var recipientUserId = $"double_spend_rec_{Guid.NewGuid():N}";

        await using var initDb = CreateDbContext();
        var walletService = new WalletService(initDb);

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderUserId, Currency.NGN);
        var recipientWallet = await walletService.GetOrCreateIndividualWalletAsync(recipientUserId, Currency.NGN);

        senderWallet.Credit(100m);
        await initDb.SaveChangesAsync();

        var senderAcc = await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == senderWallet.Id);
        var recipientAcc = await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == recipientWallet.Id);

        // Act: 10 simultaneous transfer tasks attempting to debit 100 NGN each
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new LedgerPostingService(db);
            try
            {
                var txn = await service.PostSingleCurrencyTransactionAsync(
                    senderAcc.Id,
                    recipientAcc.Id,
                    amount: 100m,
                    currency: Currency.NGN,
                    transactionType: LedgerTransactionType.PeerTransfer);
                return (Success: true, Transaction: txn, Exception: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Transaction: (LedgerTransaction?)null, Exception: ex);
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: Exactly 1 succeeds, 9 fail due to insufficient funds
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(9, failureCount);

        await using var verifyDb = CreateDbContext();
        var updatedSender = await verifyDb.Wallets.FindAsync(senderWallet.Id);
        var updatedRecipient = await verifyDb.Wallets.FindAsync(recipientWallet.Id);

        Assert.Equal(0m, updatedSender!.AvailableBalance);
        Assert.Equal(100m, updatedRecipient!.AvailableBalance);

        // Verify ledger entries are balanced
        var entries = await verifyDb.LedgerEntries.ToListAsync();
        var totalDebits = entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var totalCredits = entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
        Assert.Equal(totalDebits, totalCredits);
    }

    [Fact]
    public async Task Test2_OpposingTransfers_ShouldNotProduceDeadlocks()
    {
        // Arrange: Wallet A = 10,000 NGN, Wallet B = 10,000 NGN
        var userA = $"opposing_a_{Guid.NewGuid():N}";
        var userB = $"opposing_b_{Guid.NewGuid():N}";

        await using var initDb = CreateDbContext();
        var walletService = new WalletService(initDb);

        var walletA = await walletService.GetOrCreateIndividualWalletAsync(userA, Currency.NGN);
        var walletB = await walletService.GetOrCreateIndividualWalletAsync(userB, Currency.NGN);

        walletA.Credit(10000m);
        walletB.Credit(10000m);
        await initDb.SaveChangesAsync();

        var accA = await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == walletA.Id);
        var accB = await initDb.LedgerAccounts.FirstAsync(l => l.WalletId == walletB.Id);

        // Act: 20 concurrent tasks: 10 doing A -> B, 10 doing B -> A
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new LedgerPostingService(db);
            var isAtoB = i % 2 == 0;
            var fromAcc = isAtoB ? accA.Id : accB.Id;
            var toAcc = isAtoB ? accB.Id : accA.Id;

            return await service.PostSingleCurrencyTransactionAsync(
                fromAcc, toAcc, amount: 50m, currency: Currency.NGN, transactionType: LedgerTransactionType.PeerTransfer);
        })).ToArray();

        var completedTxns = await Task.WhenAll(tasks);

        // Assert: All 20 transfers completed cleanly without PostgreSQL 40P01 deadlocks
        Assert.Equal(20, completedTxns.Length);
        Assert.All(completedTxns, t => Assert.Equal(LedgerTransactionStatus.Completed, t.Status));

        await using var verifyDb = CreateDbContext();
        var updatedA = await verifyDb.Wallets.FindAsync(walletA.Id);
        var updatedB = await verifyDb.Wallets.FindAsync(walletB.Id);

        Assert.Equal(20000m, updatedA!.AvailableBalance + updatedB!.AvailableBalance);
    }

    [Fact]
    public async Task Test3_ConcurrentDuplicateIdempotencyRequests_ShouldExecuteOnce()
    {
        // Arrange
        var idempotencyKey = $"idemp_concurrent_{Guid.NewGuid():N}";
        var operation = "PeerTransfer";
        var payload = "{\"amount\": 500, \"currency\": \"NGN\"}";
        var userId = $"user_idemp_{Guid.NewGuid():N}";

        // Act: 10 simultaneous CreateRecordAsync calls with identical key and payload
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new IdempotencyService(db);
            return await service.CreateRecordAsync(idempotencyKey, operation, payload, userId: userId);
        })).ToArray();

        var records = await Task.WhenAll(tasks);

        // Assert: Every task received the exact same IdempotencyRecord ID
        var firstRecordId = records[0].Id;
        Assert.All(records, r => Assert.Equal(firstRecordId, r.Id));

        await using var verifyDb = CreateDbContext();
        var countInDb = await verifyDb.IdempotencyRecords.CountAsync(r => r.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, countInDb);
    }

    [Fact]
    public async Task Test4_OutboxWorkers_MultiInstance_ShouldNotDuplicateMessages()
    {
        // Arrange: Insert 10 unprocessed outbox messages
        await using var initDb = CreateDbContext();
        for (int i = 0; i < 10; i++)
        {
            var msg = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = $"TestEvent_{i}",
                Content = $"{{\"index\": {i}}}",
                OccurredOnUtc = DateTime.UtcNow
            };
            initDb.OutboxMessages.Add(msg);
        }
        await initDb.SaveChangesAsync();

        var publishedCount = 0;
        var eventPublisherMock = Substitute.For<Application.Common.Interfaces.Messaging.IEventPublisher>();
        eventPublisherMock.PublishAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref publishedCount);
                return Task.CompletedTask;
            });

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => CreateDbContext());
        serviceCollection.AddScoped(_ => eventPublisherMock);
        var provider = serviceCollection.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Act: Run 3 OutboxPublisherWorker instances concurrently against the same PostgreSQL database
        var worker1 = new OutboxPublisherWorker(scopeFactory, NullLogger<OutboxPublisherWorker>.Instance);
        var worker2 = new OutboxPublisherWorker(scopeFactory, NullLogger<OutboxPublisherWorker>.Instance);
        var worker3 = new OutboxPublisherWorker(scopeFactory, NullLogger<OutboxPublisherWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var w1Task = worker1.StartAsync(cts.Token);
        var w2Task = worker2.StartAsync(cts.Token);
        var w3Task = worker3.StartAsync(cts.Token);

        await Task.Delay(2000);
        cts.Cancel();

        try { await Task.WhenAll(w1Task, w2Task, w3Task); } catch (OperationCanceledException) { }

        // Assert: Every message was published exactly once across all 3 workers
        await using var verifyDb = CreateDbContext();
        var processedMessages = await verifyDb.OutboxMessages.Where(m => m.ProcessedOnUtc != null).ToListAsync();
        Assert.Equal(10, processedMessages.Count);
        Assert.Equal(10, publishedCount);
    }

    [Fact]
    public async Task Test5_PINBruteForceConcurrency_ShouldLockoutUserAtThreshold()
    {
        // Arrange
        var userId = $"pin_user_{Guid.NewGuid():N}";
        await using var initDb = CreateDbContext();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            Email = $"{userId}@test.com",
            TransactionPinHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            FailedPinAttempts = 0
        };
        initDb.Users.Add(user);
        await initDb.SaveChangesAsync();

        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManagerMock = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);

        // Act: 10 concurrent invalid PIN verification attempts
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var pinSvc = new TransactionPinService(userManagerMock, db);
            var res = await pinSvc.VerifyPinAsync(userId, "0000");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return res;
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: User is locked out and failed attempt counter is correctly managed
        await using var verifyDb = CreateDbContext();
        var updatedUser = await verifyDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.PinLockoutEndUtc);
        Assert.True(updatedUser.PinLockoutEndUtc > DateTime.UtcNow);
        Assert.Contains(results, r => r.IsLocked);
    }

    [Fact]
    public async Task Test6_FeeAccountConcurrency_ShouldBeSafeAndUnique()
    {
        // Act: 10 concurrent calls to GetOrCreatePlatformFeeAccountAsync for NGN
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new LedgerPostingService(db);
            return await service.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);
        })).ToArray();

        var accounts = await Task.WhenAll(tasks);

        // Assert: Exactly 1 system fee account created for NGN
        var firstAccountId = accounts[0].Id;
        Assert.All(accounts, a => Assert.Equal(firstAccountId, a.Id));

        await using var verifyDb = CreateDbContext();
        var feeAccountCount = await verifyDb.LedgerAccounts
            .CountAsync(l => l.AccountType == LedgerAccountType.FeeRevenue && l.Currency == Currency.NGN);

        Assert.Equal(1, feeAccountCount);
    }

    [Fact]
    public async Task Test7_CrossCurrencyConcurrency_OpposingTransfers_ShouldNotDeadlock()
    {
        // Arrange: Wallet USDT = 10,000, Wallet NGN = 10,000
        var userUSDT = $"fx_user_usdt_{Guid.NewGuid():N}";
        var userNGN = $"fx_user_ngn_{Guid.NewGuid():N}";

        await using var initDb = CreateDbContext();
        var walletService = new WalletService(initDb);

        var walletUSDT = await walletService.GetOrCreateIndividualWalletAsync(userUSDT, Currency.USDT);
        var walletNGN = await walletService.GetOrCreateIndividualWalletAsync(userNGN, Currency.NGN);

        walletUSDT.Credit(10000m);
        walletNGN.Credit(10000m);
        await initDb.SaveChangesAsync();

        // Act: 10 concurrent opposing cross-currency FX conversions
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var postingService = new LedgerPostingService(db);
            var isUsdtToNgn = i % 2 == 0;
            var src = isUsdtToNgn ? walletUSDT.Id : walletNGN.Id;
            var tgt = isUsdtToNgn ? walletNGN.Id : walletUSDT.Id;
            var srcAmt = isUsdtToNgn ? 10m : 1500m;
            var tgtAmt = isUsdtToNgn ? 1500m : 10m;
            var rate = 150m;

            return await postingService.PostCrossCurrencyTransactionAsync(
                sourceWalletId: src,
                targetWalletId: tgt,
                sourceAmount: srcAmt,
                targetAmount: tgtAmt,
                rate: rate,
                rateProvider: "TestProvider",
                rateTimestamp: DateTime.UtcNow);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: All 10 FX conversions succeeded without deadlocks
        Assert.Equal(10, results.Length);
        Assert.All(results, r => Assert.Equal(LedgerTransactionStatus.Completed, r.Transaction.Status));

        await using var verifyDb = CreateDbContext();
        var ledgerEntries = await verifyDb.LedgerEntries.ToListAsync();
        var totalDebitsUSDT = ledgerEntries.Where(e => e.Currency == Currency.USDT && e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var totalCreditsUSDT = ledgerEntries.Where(e => e.Currency == Currency.USDT && e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
        Assert.Equal(totalDebitsUSDT, totalCreditsUSDT);
    }
}
