using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

public sealed class PeerTransferIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public PeerTransferIntegrationTests(InfrastructureFixture fixture)
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
    public async Task PostPeerTransferCoreAsync_FreePolicy_ShouldUpdateBalancesAndPostBalancedLedger()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var receiverId = $"user_receiver_{Guid.NewGuid():N}";

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        var receiverWallet = await walletService.GetOrCreateIndividualWalletAsync(receiverId, Currency.NGN);

        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // Act
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var transaction = await postingService.PostPeerTransferCoreAsync(
            senderWalletId: senderWallet.Id,
            recipientWalletId: receiverWallet.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 30000m,
            feeAmount: 0m, // Free policy
            currency: Currency.NGN,
            reference: $"CBZPT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: Guid.NewGuid().ToString(),
            description: "P2P Free Transfer Test");

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        // Assert
        Assert.NotNull(transaction);
        Assert.Equal(LedgerTransactionStatus.Completed, transaction.Status);

        var updatedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        var updatedReceiver = await dbContext.Wallets.FindAsync(receiverWallet.Id);

        Assert.Equal(70000m, updatedSender!.AvailableBalance);
        Assert.Equal(30000m, updatedReceiver!.AvailableBalance);

        // Verify 2 ledger entries for 0 fee
        var entries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == transaction.Id)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.Equal(30000m, entries.Single(e => e.Direction == LedgerEntryDirection.Debit).Amount);
        Assert.Equal(30000m, entries.Single(e => e.Direction == LedgerEntryDirection.Credit).Amount);
    }

    [Fact]
    public async Task PostPeerTransferCoreAsync_PercentagePolicyWithFee_ShouldPostThreeWayLedgerAndDebitFee()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var receiverId = $"user_receiver_{Guid.NewGuid():N}";

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        var receiverWallet = await walletService.GetOrCreateIndividualWalletAsync(receiverId, Currency.NGN);

        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // Transfer 50,000 NGN with 500 NGN fee (total debit = 50,500 NGN)
        var transferAmount = 50000m;
        var feeAmount = 500m;
        var totalDebit = transferAmount + feeAmount;

        // Act
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var transaction = await postingService.PostPeerTransferCoreAsync(
            senderWalletId: senderWallet.Id,
            recipientWalletId: receiverWallet.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: transferAmount,
            feeAmount: feeAmount,
            currency: Currency.NGN,
            reference: $"CBZPT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: Guid.NewGuid().ToString(),
            description: "P2P Fee Transfer Test");

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        // Assert
        var updatedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        var updatedReceiver = await dbContext.Wallets.FindAsync(receiverWallet.Id);

        Assert.Equal(100000m - totalDebit, updatedSender!.AvailableBalance); // 49,500 NGN
        Assert.Equal(50000m, updatedReceiver!.AvailableBalance);             // 50,000 NGN

        // Verify 3 ledger entries (Debit sender 50,500, Credit recipient 50,000, Credit fee 500)
        var entries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == transaction.Id)
            .ToListAsync();

        Assert.Equal(3, entries.Count);
        Assert.Equal(totalDebit, entries.Single(e => e.Direction == LedgerEntryDirection.Debit).Amount);
        Assert.Equal(transferAmount, entries.Single(e => e.Direction == LedgerEntryDirection.Credit && e.LedgerAccountId != feeAccount.Id).Amount);
        Assert.Equal(feeAmount, entries.Single(e => e.Direction == LedgerEntryDirection.Credit && e.LedgerAccountId == feeAccount.Id).Amount);
    }

    [Fact]
    public async Task PostPeerTransferCoreAsync_InsufficientFunds_ShouldThrowAndPreserveBalances()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var receiverId = $"user_receiver_{Guid.NewGuid():N}";

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        var receiverWallet = await walletService.GetOrCreateIndividualWalletAsync(receiverId, Currency.NGN);

        senderWallet.Credit(10000m);
        await dbContext.SaveChangesAsync();

        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // Attempting to transfer 10,000 with 100 fee requires 10,100 -> fails
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostPeerTransferCoreAsync(
                senderWalletId: senderWallet.Id,
                recipientWalletId: receiverWallet.Id,
                platformFeeAccountId: feeAccount.Id,
                transferAmount: 10000m,
                feeAmount: 100m,
                currency: Currency.NGN,
                reference: $"CBZPT-{Guid.NewGuid():N}"[..18],
                idempotencyKey: Guid.NewGuid().ToString(),
                description: "Overdraft Test"));

        await tx.RollbackAsync();

        Assert.Contains("Insufficient funds after lock", ex.Message);

        var updatedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.Equal(10000m, updatedSender!.AvailableBalance);
    }

    [Fact]
    public async Task FeePolicyService_VersionedPolicyCreation_DeactivatesPreviousActivePolicy()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var policyService = new FeePolicyService(dbContext);

        // Create initial Free policy
        var policyV1 = await policyService.CreateAndActivatePolicyAsync(
            mode: FeePolicyMode.Free,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            createdByUserId: "admin-1");

        var v1Version = policyV1.Version;
        Assert.True(policyV1.IsEnabled);

        var activePolicy1 = await policyService.GetActivePolicyAsync();
        Assert.NotNull(activePolicy1);
        Assert.Equal(v1Version, activePolicy1.Version);

        // Act: Create new Percentage policy
        var policyV2 = await policyService.CreateAndActivatePolicyAsync(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.01m,
            minimumFee: 50m,
            maximumFee: 500m,
            createdByUserId: "admin-1");

        // Assert
        Assert.Equal(v1Version + 1, policyV2.Version);
        Assert.True(policyV2.IsEnabled);

        var activePolicy2 = await policyService.GetActivePolicyAsync();
        Assert.NotNull(activePolicy2);
        Assert.Equal(v1Version + 1, activePolicy2.Version);
        Assert.Equal(FeePolicyMode.Percentage, activePolicy2.Mode);

        // Reload policy v1 from DB to confirm deactivation
        var reloadedV1 = await dbContext.PeerTransferFeePolicies.FindAsync(policyV1.Id);
        Assert.False(reloadedV1!.IsEnabled);
        Assert.NotNull(reloadedV1.DeactivatedAtUtc);
    }

    [Fact]
    public async Task FeePolicyVersioning_NewPolicyActivation_DoesNotMutateHistoricalIdempotentTransfers()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var policyService = new FeePolicyService(dbContext);
        var idempotencyService = new IdempotencyService(dbContext);

        // 1. Create Policy V1 (Free)
        var policyV1 = await policyService.CreateAndActivatePolicyAsync(
            mode: FeePolicyMode.Free,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            createdByUserId: "admin-1");

        Assert.Equal(1, policyV1.Version);

        // Simulate historical transfer executed under Policy V1
        var idempotencyKey = $"idemp-{Guid.NewGuid():N}";
        var historicalResponse = new CebizPay.Application.UseCases.Wallet.Transfer.PeerTransferResponseDto(
            TransactionReference: "CBZPT-HISTORICAL01",
            Status: "COMPLETED",
            Amount: 10000m,
            Currency: "NGN",
            FeeAmount: 0m,
            TotalDebited: 10000m,
            RecipientDisplay: "recipient@example.com",
            AppliedFeePolicyVersion: 1,
            CreatedAtUtc: DateTime.UtcNow);

        var requestPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            RecipientId = "user-rec-1",
            Amount = 10000m,
            Currency = "NGN",
            SourceWalletId = Guid.NewGuid(),
            FeePolicyVersion = 1
        });

        var record = await idempotencyService.CreateRecordAsync(
            idempotencyKey, "PeerTransfer", requestPayload, "user-sender-1", null);

        record.Complete(System.Text.Json.JsonSerializer.Serialize(historicalResponse));
        await dbContext.SaveChangesAsync();

        // 2. Policy V2 becomes active (Percentage 5%)
        var policyV2 = await policyService.CreateAndActivatePolicyAsync(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.05m,
            minimumFee: 100m,
            maximumFee: 1000m,
            createdByUserId: "admin-1");

        Assert.Equal(2, policyV2.Version);

        // 3. Retry the original idempotent transfer under active Policy V2
        var fetchedRecord = await idempotencyService.GetRecordAsync(
            idempotencyKey, "PeerTransfer", "user-sender-1", null);

        Assert.NotNull(fetchedRecord);
        Assert.Equal(Domain.Finance.Enums.IdempotencyStatus.Completed, fetchedRecord.Status);

        var cachedResponse = System.Text.Json.JsonSerializer.Deserialize<CebizPay.Application.UseCases.Wallet.Transfer.PeerTransferResponseDto>(fetchedRecord.ResponseJson!);

        // Assert: The original response is returned intact, retaining Version 1 and 0 Fee (not recalculated under V2!)
        Assert.NotNull(cachedResponse);
        Assert.Equal(1, cachedResponse.AppliedFeePolicyVersion);
        Assert.Equal(0m, cachedResponse.FeeAmount);
        Assert.Equal("CBZPT-HISTORICAL01", cachedResponse.TransactionReference);
    }
}
