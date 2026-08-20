using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

public sealed class BankTransferIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public BankTransferIntegrationTests(InfrastructureFixture fixture)
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
    public async Task PostBankTransferDebitCoreAsync_FreePolicy_ShouldDebitSenderAndCreditClearingAccount()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var clearingAccount = await postingService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // Act
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var (ledgerTxn, bankTransfer) = await postingService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 35000m,
            feeAmount: 0m,
            currency: Currency.NGN,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"CBZBT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: Guid.NewGuid().ToString(),
            description: "Bank transfer free test");

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        // Assert
        Assert.NotNull(ledgerTxn);
        Assert.NotNull(bankTransfer);
        Assert.Equal(BankTransferStatus.Pending, bankTransfer.Status);
        Assert.Equal(35000m, bankTransfer.Amount);
        Assert.Equal(0m, bankTransfer.FeeAmount);
        Assert.Equal(35000m, bankTransfer.TotalDebited);

        var updatedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.Equal(65000m, updatedSender!.AvailableBalance);

        // Verify ledger entries
        var entries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == ledgerTxn.Id)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.Equal(35000m, entries.Single(e => e.Direction == LedgerEntryDirection.Debit).Amount);
        Assert.Equal(35000m, entries.Single(e => e.Direction == LedgerEntryDirection.Credit).Amount);
    }

    [Fact]
    public async Task PostBankTransferDebitCoreAsync_PercentagePolicyWithFee_ShouldDebitSenderAndCreditClearingAndFee()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var clearingAccount = await postingService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        var transferAmount = 50000m;
        var feeAmount = 500m; // 1%
        var totalDebit = transferAmount + feeAmount;

        // Act
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var (ledgerTxn, bankTransfer) = await postingService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: transferAmount,
            feeAmount: feeAmount,
            currency: Currency.NGN,
            destinationBankCode: "044",
            destinationAccountNumber: "9876543210",
            destinationAccountName: "Corporate Client",
            feePolicyId: Guid.NewGuid(),
            feePolicyVersion: 1,
            reference: $"CBZBT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: Guid.NewGuid().ToString(),
            description: "Bank transfer percentage fee test");

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        // Assert
        Assert.NotNull(bankTransfer);
        Assert.Equal(BankTransferStatus.Pending, bankTransfer.Status);
        Assert.Equal(50000m, bankTransfer.Amount);
        Assert.Equal(500m, bankTransfer.FeeAmount);
        Assert.Equal(50500m, bankTransfer.TotalDebited);

        var updatedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.Equal(49500m, updatedSender!.AvailableBalance);

        // Verify 3 balanced entries: 1 debit (50500), 2 credits (50000 + 500)
        var entries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == ledgerTxn.Id)
            .ToListAsync();

        Assert.Equal(3, entries.Count);
        var totalDebits = entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var totalCredits = entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);

        Assert.Equal(50500m, totalDebits);
        Assert.Equal(50500m, totalCredits);
    }

    [Fact]
    public async Task PostBankTransferDebitCoreAsync_InsufficientBalance_ShouldThrowAndLeaveStateUnmutated()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        senderWallet.Credit(1000m);
        await dbContext.SaveChangesAsync();

        var clearingAccount = await postingService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // Act & Assert
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 5000m, // Exceeds balance
            feeAmount: 50m,
            currency: Currency.NGN,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"CBZBT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: Guid.NewGuid().ToString(),
            description: "Bank transfer overbalance test"));

        await tx.RollbackAsync();

        var senderCheck = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.Equal(1000m, senderCheck!.AvailableBalance);
    }

    [Fact]
    public async Task PostBankTransferReversalCoreAsync_FailedTransfer_ShouldRestoreSenderBalanceAndPostBalancedReversalLedger()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var walletService = new WalletService(dbContext);
        var postingService = new LedgerPostingService(dbContext);

        var senderId = $"user_sender_{Guid.NewGuid():N}";
        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync(senderId, Currency.NGN);
        senderWallet.Credit(100000m);
        await dbContext.SaveChangesAsync();

        var clearingAccount = await postingService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await postingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // 1. Post initial debit
        await using var tx1 = await dbContext.Database.BeginTransactionAsync();

        var (debitTxn, bankTransfer) = await postingService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 20000m,
            feeAmount: 200m,
            currency: Currency.NGN,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"CBZBT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: Guid.NewGuid().ToString(),
            description: "Bank transfer to reverse");

        await dbContext.SaveChangesAsync();
        await tx1.CommitAsync();

        var balanceAfterDebit = (await dbContext.Wallets.FindAsync(senderWallet.Id))!.AvailableBalance;
        Assert.Equal(79800m, balanceAfterDebit);

        // 2. Post definitive failure and reversal
        await using var tx2 = await dbContext.Database.BeginTransactionAsync();

        var reversalTxn = await postingService.PostBankTransferReversalCoreAsync(
            bankTransfer.Id,
            "Beneficiary account number invalid or closed");

        await dbContext.SaveChangesAsync();
        await tx2.CommitAsync();

        // Assert
        Assert.NotNull(reversalTxn);
        Assert.Equal(LedgerTransactionType.Reversal, reversalTxn.TransactionType);
        Assert.Equal(LedgerTransactionStatus.Completed, reversalTxn.Status);

        var updatedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.Equal(BankTransferStatus.Failed, updatedTransfer!.Status);
        Assert.Equal("Beneficiary account number invalid or closed", updatedTransfer.FailureReason);
        Assert.NotNull(updatedTransfer.FailedAtUtc);

        // Sender balance must be fully restored (79800 + 20200 = 100000)
        var restoredSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.Equal(100000m, restoredSender!.AvailableBalance);

        // Verify reversal ledger entries are balanced
        var reversalEntries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == reversalTxn.Id)
            .ToListAsync();

        Assert.Equal(3, reversalEntries.Count);
        var reversalDebits = reversalEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var reversalCredits = reversalEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);

        Assert.Equal(20200m, reversalDebits);
        Assert.Equal(20200m, reversalCredits);
    }

    [Fact]
    public async Task BankTransferFeePolicyService_CreateAndSupersede_ShouldDeactivateOldPolicyAndActivateNewVersion()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var feePolicyService = new BankTransferFeePolicyService(dbContext);

        // 1. Create initial Free policy (v1)
        var policy1 = await feePolicyService.CreateAndActivatePolicyAsync(
            FeePolicyMode.Free,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            createdByUserId: "admin-super",
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, policy1.Version);
        Assert.True(policy1.IsEnabled);

        var active1 = await feePolicyService.GetActivePolicyAsync(CancellationToken.None);
        Assert.NotNull(active1);
        Assert.Equal(policy1.Id, active1.Id);

        // 2. Create superseding Percentage policy (v2)
        var policy2 = await feePolicyService.CreateAndActivatePolicyAsync(
            FeePolicyMode.Percentage,
            percentageRate: 0.015m,
            minimumFee: 50m,
            maximumFee: 1000m,
            createdByUserId: "admin-super",
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, policy2.Version);
        Assert.True(policy2.IsEnabled);

        // 3. Verify policy1 was deactivated
        var refreshedPolicy1 = await dbContext.BankTransferFeePolicies.FindAsync(policy1.Id);
        Assert.False(refreshedPolicy1!.IsEnabled);
        Assert.NotNull(refreshedPolicy1.DeactivatedAtUtc);

        // 4. Verify GetActivePolicyAsync returns policy2
        var active2 = await feePolicyService.GetActivePolicyAsync(CancellationToken.None);
        Assert.NotNull(active2);
        Assert.Equal(policy2.Id, active2.Id);
        Assert.Equal(2, active2.Version);
        Assert.Equal(FeePolicyMode.Percentage, active2.Mode);
    }
}
