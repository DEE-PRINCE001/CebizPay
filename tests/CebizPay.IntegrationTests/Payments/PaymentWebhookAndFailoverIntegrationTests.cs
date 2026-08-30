using System.Globalization;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
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
/// PostgreSQL Testcontainers integration tests for Webhook processing, deduplication, and provider failover coordination.
/// </summary>
public sealed class PaymentWebhookAndFailoverIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public PaymentWebhookAndFailoverIntegrationTests(InfrastructureFixture fixture)
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
    public async Task WebhookDeduplication_RealPostgreSql_ShouldPreventDuplicateEntries()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var signatureVerifier = new WebhookSignatureVerifier();
        var ledgerService = Substitute.For<ILedgerPostingService>();

        var flwOptions = Options.Create(new FlutterwaveOptions
        {
            WebhookSecretHash = "flw_secret_test_hash",
            SecretKey = "FLWSECK_TEST"
        });
        var pstkOptions = Options.Create(new PaystackOptions
        {
            WebhookSecret = "pstk_secret_test_hash",
            SecretKey = "sk_test_pstk"
        });
        var monnifyOptions = Options.Create(new MonnifyOptions
        {
            WebhookSecret = "mnfy_secret_test_hash",
            SecretKey = "mnfy_secret_test_hash"
        });
        var feePolicyService = Substitute.For<IPlatformFeePolicyService>();

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

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-INT-001-1",
            amount: 5000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        var payload = """{"event":"transfer.completed","data":{"id":55555,"status":"SUCCESSFUL","reference":"CBZPA-INT-001-1","amount":5000,"currency":"NGN"}}""";
        var headers = new Dictionary<string, string> { { "verif-hash", "flw_secret_test_hash" } };

        // Act 1: First ingestion
        var result1 = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, payload, headers);

        // Act 2: Duplicate ingestion
        var result2 = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result1.Status);
        Assert.Equal(WebhookProcessingStatus.Duplicate, result2.Status);

        var eventsInDb = await dbContext.WebhookEvents
            .Where(w => w.Provider == PaymentProvider.Flutterwave && w.ProviderEventId == "flw_evt_55555_SUCCESSFUL")
            .ToListAsync();

        Assert.Single(eventsInDb);
    }

    [Fact]
    public async Task WebhookFailure_WithRealLedgerPostingService_ShouldRestoreWalletBalance()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var signatureVerifier = new WebhookSignatureVerifier();
        var walletService = new WalletService(dbContext);
        var ledgerPostingService = new LedgerPostingService(dbContext);

        var flwOptions = Options.Create(new FlutterwaveOptions { WebhookSecretHash = "flw_secret", SecretKey = "flw_secret" });
        var pstkOptions = Options.Create(new PaystackOptions { WebhookSecret = "pstk_secret", SecretKey = "pstk_secret" });
        var monnifyOptions = Options.Create(new MonnifyOptions { WebhookSecret = "mnfy_secret", SecretKey = "mnfy_secret" });
        var feePolicyService = Substitute.For<IPlatformFeePolicyService>();

        var processor = new WebhookProcessor(
            signatureVerifier,
            dbContext,
            ledgerPostingService,
            feePolicyService,
            outboxService,
            flwOptions,
            pstkOptions,
            monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);

        // 1. Create wallet with initial funds
        var senderWallet = await walletService.GetOrCreateIndividualWalletAsync($"user_wh_{Guid.NewGuid():N}", Currency.NGN);
        senderWallet.Credit(10000m);
        await dbContext.SaveChangesAsync();

        // 2. Execute Bank Transfer debit via core ledger posting service
        var clearingAccount = await ledgerPostingService.GetOrCreateBankTransferClearingAccountAsync(Currency.NGN);
        var feeAccount = await ledgerPostingService.GetOrCreatePlatformFeeAccountAsync(Currency.NGN);

        var (ledgerTxn, bankTransfer) = await ledgerPostingService.PostBankTransferDebitCoreAsync(
            senderWalletId: senderWallet.Id,
            clearingAccountId: clearingAccount.Id,
            platformFeeAccountId: feeAccount.Id,
            transferAmount: 3000m,
            feeAmount: 50m,
            currency: Currency.NGN,
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "David Miller",
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"TRF-{Guid.NewGuid():N}",
            idempotencyKey: null,
            description: "Bank transfer test");

        // Record Attempt #1 in Processing status
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxn.Id,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: $"CBZPA-{bankTransfer.Reference}-1",
            amount: 3000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        await dbContext.SaveChangesAsync();

        // Wallet balance before webhook failure should be 10000 - 3050 = 6950
        var refreshedSender = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.NotNull(refreshedSender);
        Assert.Equal(6950m, refreshedSender.AvailableBalance);

        // 3. Ingest failure webhook from Flutterwave
        var failPayload = string.Format(
            CultureInfo.InvariantCulture,
            """{{"event":"transfer.completed","data":{{"id":66666,"status":"FAILED","reference":"{0}","amount":3000,"currency":"NGN","complete_message":"Destination account is invalid"}}}}""",
            attempt.RequestReference);
        var headers = new Dictionary<string, string> { { "verif-hash", "flw_secret" } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, failPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        // Wallet balance must be exactly restored (+3050 back to 10000)
        var restoredWallet = await dbContext.Wallets.FindAsync(senderWallet.Id);
        Assert.NotNull(restoredWallet);
        Assert.Equal(10000m, restoredWallet.AvailableBalance);

        // BankTransfer status must be FAILED
        var refreshedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(refreshedTransfer);
        Assert.Equal(BankTransferStatus.Failed, refreshedTransfer.Status);

        // PaymentAttempt status must be FAILED
        var refreshedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(refreshedAttempt);
        Assert.Equal(PaymentAttemptStatus.Failed, refreshedAttempt.Status);

        // Outbox must contain BankTransferFailedEvent
        var outboxEvents = await dbContext.OutboxMessages.ToListAsync();
        Assert.Contains(outboxEvents, o => o.Type.Contains("BankTransferFailedEvent"));
    }

    [Fact]
    public async Task PaymentFailover_RealPostgreSql_ShouldPersistAttempt2AndAudit()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = Substitute.For<ILedgerPostingService>();

        var paystackProvider = Substitute.For<IPaymentProvider>();
        paystackProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("PSTK_AUTHSUCC_9988"));

        var providerFactory = Substitute.For<IPaymentProviderFactory>();
        providerFactory.GetProvider(PaymentProvider.Paystack).Returns(paystackProvider);

        var failoverService = new PaymentFailoverService(
            providerFactory,
            dbContext,
            ledgerService,
            outboxService,
            NullLogger<PaymentFailoverService>.Instance);

        var ledgerTxnId = Guid.NewGuid();

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "9876543210",
            destinationAccountName: "Emma Stone",
            amount: 8000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"TRF-FO-{Guid.NewGuid():N}");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        // Attempt #1: Flutterwave technical failure
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: $"CBZPA-{bankTransfer.Reference}-1",
            amount: 8000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("GATEWAY_504_GATEWAY_TIMEOUT", "Gateway timeout");
        dbContext.PaymentAttempts.Add(attempt1);

        await dbContext.SaveChangesAsync();

        // Act
        var result = await failoverService.FailoverAsync(ledgerTxnId);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(PaymentProvider.Paystack, result.FallbackProvider);
        Assert.Equal(PaymentProviderResultStatus.Success, result.ResultStatus);

        var allAttempts = await dbContext.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTxnId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, allAttempts.Count);
        Assert.Equal(1, allAttempts[0].AttemptNumber);
        Assert.Equal(PaymentAttemptStatus.Failed, allAttempts[0].Status);

        Assert.Equal(2, allAttempts[1].AttemptNumber);
        Assert.Equal(PaymentProvider.Paystack, allAttempts[1].Provider);
        Assert.Equal(PaymentAttemptStatus.Succeeded, allAttempts[1].Status);
        Assert.Equal("PSTK_AUTHSUCC_9988", allAttempts[1].ProviderReference);

        // Outbox must contain ProviderFailoverStartedEvent & ProviderFailoverSucceededEvent
        var outboxMessages = await dbContext.OutboxMessages.ToListAsync();
        Assert.Contains(outboxMessages, o => o.Type.Contains("ProviderFailoverStartedEvent"));
        Assert.Contains(outboxMessages, o => o.Type.Contains("ProviderFailoverSucceededEvent"));
    }
}
