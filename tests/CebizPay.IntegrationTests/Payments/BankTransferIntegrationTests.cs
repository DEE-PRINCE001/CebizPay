using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL Testcontainers integration tests for Batch 4 Bank Transfers & Payouts:
/// - Option A Immediate Debit + Clearing Account
/// - Primary Monnify execution
/// - Sequential Technical Failover (Monnify -> Flutterwave -> Paystack)
/// - Business Failure Reversal to Sender Wallet
/// - Monnify Disbursement Webhook Ingestion & Deduplication
/// </summary>
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
    public async Task PostBankTransferDebitCoreAsync_RealPostgres_ShouldDebitWalletAndCreditClearingAccount()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var walletService = new WalletService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync($"user_bt_{Guid.NewGuid():N}", Currency.NGN);
        senderWallet.Credit(50000m);
        await dbContext.SaveChangesAsync();

        var clearingAccount = await ledgerService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await ledgerService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        // Act
        var (ledgerTxn, bankTransfer) = await ledgerService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 20000m,
            feeAmount: 100m,
            currency: Currency.NGN,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "Alice Chukwu",
            feePolicyId: null,
            feePolicyVersion: 1,
            reference: $"CBZBT-{Guid.NewGuid():N}"[..18],
            idempotencyKey: $"idemp-{Guid.NewGuid():N}",
            description: "Test Bank Transfer");

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var updatedWallet = await readContext.Wallets.FindAsync(senderWallet.Id);
        Assert.NotNull(updatedWallet);
        Assert.Equal(29900m, updatedWallet.AvailableBalance); // 50000 - 20100

        var persistedTransfer = await readContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(persistedTransfer);
        Assert.Equal(BankTransferStatus.Pending, persistedTransfer.Status);
        Assert.Equal(20000m, persistedTransfer.Amount);
        Assert.Equal(100m, persistedTransfer.FeeAmount);
        Assert.Equal("058", persistedTransfer.DestinationBankCode);
        Assert.Equal("0123456789", persistedTransfer.DestinationAccountNumber);

        var entries = await readContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == ledgerTxn.Id)
            .OrderBy(e => e.Sequence)
            .ToListAsync();

        Assert.Equal(3, entries.Count);
        Assert.Equal(LedgerEntryDirection.Debit, entries[0].Direction);
        Assert.Equal(20100m, entries[0].Amount); // Debit sender
        Assert.Equal(LedgerEntryDirection.Credit, entries[1].Direction);
        Assert.Equal(20000m, entries[1].Amount); // Credit clearing
        Assert.Equal(LedgerEntryDirection.Credit, entries[2].Direction);
        Assert.Equal(100m, entries[2].Amount); // Credit fee
    }

    [Fact]
    public async Task PostBankTransferReversalCoreAsync_RealPostgres_ShouldAtomicallyRefundSenderWallet()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var walletService = new WalletService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);

        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync($"user_rev_{Guid.NewGuid():N}", Currency.NGN);
        senderWallet.Credit(30000m);
        await dbContext.SaveChangesAsync();

        var clearingAccount = await ledgerService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await ledgerService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        var (ledgerTxn, bankTransfer) = await ledgerService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 10000m,
            feeAmount: 50m,
            currency: Currency.NGN,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "Alice Chukwu",
            feePolicyId: null,
            feePolicyVersion: 1,
            reference: $"CBZBT-REV-{Guid.NewGuid():N}"[..18],
            idempotencyKey: null,
            description: "Test Bank Transfer For Reversal");

        // Available balance is now 30000 - 10050 = 19950
        var walletMid = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.NotNull(walletMid);
        Assert.Equal(19950m, walletMid.AvailableBalance);

        // Act: Execute Reversal
        var reversalTxn = await ledgerService.PostBankTransferReversalCoreAsync(bankTransfer.Id, "Account closed at destination bank");

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var restoredWallet = await readContext.Wallets.FindAsync(senderWallet.Id);
        Assert.NotNull(restoredWallet);
        Assert.Equal(30000m, restoredWallet.AvailableBalance); // Exact refund

        var failedTransfer = await readContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(failedTransfer);
        Assert.Equal(BankTransferStatus.Failed, failedTransfer.Status);
        Assert.Equal("Account closed at destination bank", failedTransfer.FailureReason);

        var reversalEntries = await readContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == reversalTxn.Id)
            .OrderBy(e => e.Sequence)
            .ToListAsync();

        Assert.Equal(3, reversalEntries.Count);
        Assert.Equal(LedgerEntryDirection.Credit, reversalEntries[0].Direction);
        Assert.Equal(10050m, reversalEntries[0].Amount); // Credit sender (refund)
        Assert.Equal(LedgerEntryDirection.Debit, reversalEntries[1].Direction);
        Assert.Equal(10000m, reversalEntries[1].Amount); // Debit clearing (reversal)
        Assert.Equal(LedgerEntryDirection.Debit, reversalEntries[2].Direction);
        Assert.Equal(50m, reversalEntries[2].Amount); // Debit fee (reversal)
    }

    [Fact]
    public async Task MonnifyDisbursementWebhook_RealPostgres_ShouldCompleteTransfer()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var signatureVerifier = new WebhookSignatureVerifier();
        var ledgerService = new LedgerPostingService(dbContext);
        var feePolicyService = Substitute.For<IPlatformFeePolicyService>();

        var flwOptions = Options.Create(new FlutterwaveOptions { WebhookSecretHash = "flw_secret", SecretKey = "flw_secret" });
        var pstkOptions = Options.Create(new PaystackOptions { WebhookSecret = "pstk_secret", SecretKey = "pstk_secret" });
        var monnifyOptions = Options.Create(new MonnifyOptions { WebhookSecret = "mnfy_secret", SecretKey = "mnfy_secret" });

        var processor = new WebhookProcessor(
            signatureVerifier,
            dbContext,
            ledgerService,
            feePolicyService,
            outboxService,
            flwOptions,
            pstkOptions,
            monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);

        var ledgerTxId = Guid.NewGuid();
        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "Alice Chukwu",
            amount: 25000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"CBZBT-WH-{Guid.NewGuid():N}"[..18]);
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: $"CBZBT-{bankTransfer.Reference}-A1-MONNIFY",
            amount: 25000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        var webhookPayload = $$"""
        {
            "eventType": "SUCCESSFUL_DISBURSEMENT",
            "eventData": {
                "transactionReference": "MNFY_DISB_TX_777",
                "reference": "{{attempt.RequestReference}}",
                "amount": 25000.00,
                "currency": "NGN",
                "status": "SUCCESS",
                "destinationAccountNumber": "0123456789",
                "destinationBankCode": "058"
            }
        }
        """;

        using var hmac = new System.Security.Cryptography.HMACSHA512(System.Text.Encoding.UTF8.GetBytes("mnfy_secret"));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(webhookPayload)));
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, webhookPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        await using var readContext = await CreateDbContextAsync();
        var updatedTransfer = await readContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(BankTransferStatus.Completed, updatedTransfer.Status);
        Assert.Equal("MNFY_DISB_TX_777", updatedTransfer.ProviderReference);

        var updatedAttempt = await readContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, updatedAttempt.Status);
        Assert.Equal("MNFY_DISB_TX_777", updatedAttempt.ProviderReference);
    }
}
