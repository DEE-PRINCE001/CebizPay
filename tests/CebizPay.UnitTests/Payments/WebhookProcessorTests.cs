using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="WebhookProcessor"/> testing ingestion, deduplication, amount matching, and reconciliation.
/// </summary>
public sealed class WebhookProcessorTests
{
    private readonly IWebhookSignatureVerifier _signatureVerifier = Substitute.For<IWebhookSignatureVerifier>();
    private readonly ILedgerPostingService _ledgerPosting = Substitute.For<ILedgerPostingService>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    private readonly IOptions<FlutterwaveOptions> _flwOptions = Options.Create(new FlutterwaveOptions
    {
        WebhookSecretHash = "flw_secret_hash_123",
        SecretKey = "FLWSECK_TEST"
    });

    private readonly IOptions<PaystackOptions> _pstkOptions = Options.Create(new PaystackOptions
    {
        WebhookSecret = "pstk_secret_123",
        SecretKey = "sk_test_paystack"
    });

    private readonly IOptions<MonnifyOptions> _monnifyOptions = Options.Create(new MonnifyOptions
    {
        WebhookSecret = "mnfy_secret_123",
        SecretKey = "mnfy_secret_123",
        ApiKey = "mnfy_key_123",
        ContractCode = "1234567890",
        Enabled = true
    });

    private readonly IPlatformFeePolicyService _feePolicyService = Substitute.For<IPlatformFeePolicyService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private WebhookProcessor CreateProcessor(ApplicationDbContext dbContext)
    {
        return new WebhookProcessor(
            _signatureVerifier,
            dbContext,
            _ledgerPosting,
            _feePolicyService,
            _outbox,
            _flwOptions,
            _pstkOptions,
            _monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessWebhook_InvalidSignature_ShouldReturnInvalidSignatureResult()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var processor = CreateProcessor(dbContext);

        _signatureVerifier
            .VerifySignature(PaymentProvider.Flutterwave, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(false);

        const string rawPayload = """{"event":"charge.completed","data":{"id":1234}}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, rawPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.InvalidSignature, result.Status);
    }

    [Fact]
    public async Task ProcessWebhook_DuplicateEvent_ShouldAcknowledgeWithoutReexecuting()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var processor = CreateProcessor(dbContext);

        _signatureVerifier
            .VerifySignature(PaymentProvider.Flutterwave, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        // Seed existing webhook event
        var existingEvent = WebhookEvent.Create(
            provider: PaymentProvider.Flutterwave,
            providerEventId: "flw_evt_9999_SUCCESSFUL",
            eventType: "transfer.completed");
        existingEvent.MarkProcessed();
        dbContext.WebhookEvents.Add(existingEvent);
        await dbContext.SaveChangesAsync();

        const string rawPayload = """{"event":"transfer.completed","data":{"id":9999,"status":"SUCCESSFUL","reference":"REF-123","amount":5000,"currency":"NGN"}}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, rawPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Duplicate, result.Status);
        Assert.Equal("flw_evt_9999_SUCCESSFUL", result.ProviderEventId);
    }

    [Fact]
    public async Task ProcessWebhook_SuccessfulTransfer_ShouldMarkAttemptSucceededAndCompleteBankTransfer()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var processor = CreateProcessor(dbContext);

        _signatureVerifier
            .VerifySignature(PaymentProvider.Flutterwave, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-001-1",
            amount: 5000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "John Doe",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "TR-001");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        const string rawPayload = """{"event":"transfer.completed","data":{"id":8888,"status":"SUCCESSFUL","reference":"CBZPA-TR-001-1","amount":5000,"currency":"NGN"}}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, rawPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);
        Assert.Equal(attempt.Id, result.PaymentAttemptId);

        var updatedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, updatedAttempt.Status);

        var updatedTransfer = await dbContext.BankTransfers.FindAsync(bankTransfer.Id);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(BankTransferStatus.Completed, updatedTransfer.Status);

        var webhookEvent = await dbContext.WebhookEvents.FirstOrDefaultAsync(w => w.PaymentAttemptId == attempt.Id);
        Assert.NotNull(webhookEvent);
        Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
    }

    [Fact]
    public async Task ProcessWebhook_FailedTransfer_ShouldMarkAttemptFailedAndTriggerLedgerReversal()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var processor = CreateProcessor(dbContext);

        _signatureVerifier
            .VerifySignature(PaymentProvider.Paystack, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Paystack,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-002-1",
            amount: 1000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);

        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0987654321",
            destinationAccountName: "Jane Smith",
            amount: 1000m,
            currency: Currency.NGN,
            feeAmount: 25m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "TR-002");
        bankTransfer.MarkProcessing();
        dbContext.BankTransfers.Add(bankTransfer);

        await dbContext.SaveChangesAsync();

        const string rawPayload = """{"event":"transfer.failed","data":{"reference":"CBZPA-TR-002-1","transfer_code":"TRF_failed_99","amount":100000,"currency":"NGN","status":"failed"}}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Paystack, rawPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var updatedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(PaymentAttemptStatus.Failed, updatedAttempt.Status);

        // Verify financial reversal invocation
        await _ledgerPosting.Received(1).PostBankTransferReversalCoreAsync(
            bankTransfer.Id,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhook_AmountMismatch_ShouldRejectAndMarkWebhookFailed()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var processor = CreateProcessor(dbContext);

        _signatureVerifier
            .VerifySignature(PaymentProvider.Flutterwave, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var ledgerTxnId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZPA-TR-003-1",
            amount: 5000m, // Expected 5000
            currency: Currency.NGN);
        attempt.MarkProcessing();
        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        // Webhook arrives with amount 1000 instead of 5000
        const string rawPayload = """{"event":"transfer.completed","data":{"id":7777,"status":"SUCCESSFUL","reference":"CBZPA-TR-003-1","amount":1000,"currency":"NGN"}}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, rawPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Error, result.Status);
        Assert.Contains("Amount mismatch", result.Message);

        var unaffectedAttempt = await dbContext.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(unaffectedAttempt);
        Assert.Equal(PaymentAttemptStatus.Processing, unaffectedAttempt.Status); // Untouched
    }
}
