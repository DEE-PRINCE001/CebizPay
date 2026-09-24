using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.UseCases.Wallet.Transfer;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

/// <summary>
/// Integration tests verifying that wallet transfer transactions execute reliably
/// under NpgsqlRetryingExecutionStrategy using IApplicationDbContext.ExecuteInTransactionAsync.
/// </summary>
public sealed class TransferExecutionStrategyIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public TransferExecutionStrategyIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateRetryingDbContext()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            })
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task RawBeginTransactionAsync_UnderRetryingExecutionStrategy_ThrowsInvalidOperationException()
    {
        await using var dbContext = CreateRetryingDbContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
#pragma warning disable CS0618
            await using var tx = await ((IApplicationDbContext)dbContext).BeginTransactionAsync();
#pragma warning restore CS0618
            await dbContext.Wallets.FirstOrDefaultAsync();
            await tx.CommitAsync();
        });

        Assert.Contains("NpgsqlRetryingExecutionStrategy", ex.Message);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_UnderRetryingExecutionStrategy_CommitsAndFlushesPendingChanges()
    {
        await using var dbContext = CreateRetryingDbContext();
        var walletService = new WalletService(dbContext);
        var userId = $"user_{Guid.NewGuid():N}";

        var wallet = await dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var created = await walletService.GetOrCreateIndividualWalletAsync(userId, Currency.NGN, ct);
            created.Credit(25000m);
            return created;
        });

        Assert.NotNull(wallet);

        await using var verifyContext = CreateRetryingDbContext();
        var persistedWallet = await verifyContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == Currency.NGN);
        Assert.NotNull(persistedWallet);
        Assert.Equal(25000m, persistedWallet.AvailableBalance);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_UnderRetryingExecutionStrategy_RollsBackOnException()
    {
        await using var dbContext = CreateRetryingDbContext();
        var walletService = new WalletService(dbContext);
        var userId = $"user_{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                var created = await walletService.GetOrCreateIndividualWalletAsync(userId, Currency.NGN, ct);
                created.Credit(50000m);
                await dbContext.SaveChangesAsync(ct);

                throw new InvalidOperationException("Simulated domain failure to trigger rollback.");
            });
        });

        await using var verifyContext = CreateRetryingDbContext();
        var persistedWallet = await verifyContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == Currency.NGN);
        Assert.Null(persistedWallet);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_PeerTransferCoreWorkflow_ExecutesUnderRetryingExecutionStrategy()
    {
        await using var dbContext = CreateRetryingDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);
        var idempotencyService = new IdempotencyService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var receiverId = $"user_receiver_{Guid.NewGuid():N}";

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        var receiverWallet = await walletService.GetOrCreateIndividualWalletAsync(receiverId, Currency.NGN);

        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var reference = $"CBZPT-{Guid.NewGuid():N}"[..18];

        var result = await dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var requestPayload = JsonSerializer.Serialize(new
            {
                RecipientId = receiverId,
                Amount = 30000m,
                Currency = "NGN",
                SourceWalletId = senderWallet.Id
            });

            var idempotencyRecord = await idempotencyService.CreateRecordAsync(
                idempotencyKey, "PeerTransfer", requestPayload, senderId, null, autoSave: true, cancellationToken: ct);

            var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN, ct);

            var ledgerTxn = await postingService.PostPeerTransferCoreAsync(
                senderWalletId: senderWallet.Id,
                recipientWalletId: receiverWallet.Id,
                platformFeeAccountId: feeAccount.Id,
                transferAmount: 30000m,
                feeAmount: 0m,
                currency: Currency.NGN,
                reference: reference,
                idempotencyKey: idempotencyKey,
                description: "Peer transfer under execution strategy",
                cancellationToken: ct);

            var response = new PeerTransferResponseDto(
                TransactionReference: ledgerTxn.Reference,
                Status: "COMPLETED",
                Amount: 30000m,
                Currency: "NGN",
                FeeAmount: 0m,
                TotalDebited: 30000m,
                RecipientDisplay: receiverId,
                AppliedFeePolicyVersion: null,
                CreatedAtUtc: ledgerTxn.CreatedAtUtc);

            idempotencyRecord.Complete(JsonSerializer.Serialize(response));
            await dbContext.SaveChangesAsync(ct);

            return response;
        });

        Assert.NotNull(result);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(reference, result.TransactionReference);

        await using var verifyContext = CreateRetryingDbContext();
        var refreshedSender = await verifyContext.Wallets.FirstAsync(w => w.Id == senderWallet.Id);
        var refreshedReceiver = await verifyContext.Wallets.FirstAsync(w => w.Id == receiverWallet.Id);

        Assert.Equal(70000m, refreshedSender.AvailableBalance);
        Assert.Equal(30000m, refreshedReceiver.AvailableBalance);
    }
}
