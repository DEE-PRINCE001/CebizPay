using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Comprehensive resilience and concurrency integration tests verifying asynchronous webhook ingestion,
/// worker batch processing, automated provider failover chains, and duplicate concurrency protection.
/// </summary>
public sealed class AsyncWebhookAndFailoverResilienceTests
{
    private readonly IWebhookSignatureVerifier _signatureVerifier = Substitute.For<IWebhookSignatureVerifier>();
    private readonly ILedgerPostingService _ledgerPostingService = Substitute.For<ILedgerPostingService>();
    private readonly IPlatformFeePolicyService _feePolicyService = Substitute.For<IPlatformFeePolicyService>();
    private readonly IOutboxService _outboxService = Substitute.For<IOutboxService>();
    private readonly IPaymentRoutingService _routingService = Substitute.For<IPaymentRoutingService>();
    private readonly IPaymentProviderFactory _providerFactory = Substitute.For<IPaymentProviderFactory>();

    private readonly IPaymentProvider _monnifyProvider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentProvider _flutterwaveProvider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentProvider _paystackProvider = Substitute.For<IPaymentProvider>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private WebhookProcessor CreateWebhookProcessor(ApplicationDbContext dbContext)
    {
        _signatureVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        return new WebhookProcessor(
            _signatureVerifier,
            dbContext,
            _ledgerPostingService,
            _feePolicyService,
            _outboxService,
            Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions { WebhookSecretHash = "test_flw_hash" }),
            Microsoft.Extensions.Options.Options.Create(new PaystackOptions { WebhookSecret = "test_pstk_secret" }),
            Microsoft.Extensions.Options.Options.Create(new MonnifyOptions { WebhookSecret = "test_monnify_secret" }),
            NullLogger<WebhookProcessor>.Instance);
    }

    private PaymentFailoverService CreateFailoverService(ApplicationDbContext dbContext)
    {
        _providerFactory.GetProvider(PaymentProvider.Monnify).Returns(_monnifyProvider);
        _providerFactory.GetProvider(PaymentProvider.Flutterwave).Returns(_flutterwaveProvider);
        _providerFactory.GetProvider(PaymentProvider.Paystack).Returns(_paystackProvider);

        _routingService.GetNextFallbackProvider(PaymentCapability.BankTransfer, PaymentProvider.Monnify)
            .Returns(PaymentProvider.Flutterwave);
        _routingService.GetNextFallbackProvider(PaymentCapability.BankTransfer, PaymentProvider.Flutterwave)
            .Returns(PaymentProvider.Paystack);
        _routingService.GetNextFallbackProvider(PaymentCapability.BankTransfer, PaymentProvider.Paystack)
            .Returns((PaymentProvider?)null);

        return new PaymentFailoverService(
            _providerFactory,
            _routingService,
            dbContext,
            _ledgerPostingService,
            _outboxService,
            NullLogger<PaymentFailoverService>.Instance);
    }

    [Fact]
    public async Task AsyncWebhookIngestion_ShouldSaveReceivedEventAndReturnImmediately()
    {
        // Arrange
        await using var db = CreateDbContext();
        var processor = CreateWebhookProcessor(db);

        var payload = JsonSerializer.Serialize(new
        {
            event_type = "SUCCESSFUL_DISBURSEMENT",
            eventData = new
            {
                reference = "REF-ASYNC-001",
                transactionReference = "MNF-TX-101",
                amount = 15000m,
                currency = "NGN",
                status = "SUCCESS"
            }
        });

        // Act: Ingestion path (executed in HTTP endpoint)
        var result = await processor.IngestWebhookAsync(PaymentProvider.Monnify, payload, new Dictionary<string, string>());

        // Assert: Fast HTTP acknowledge
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var savedEvent = await db.WebhookEvents.FirstOrDefaultAsync(w => w.ProviderEventId == result.ProviderEventId);
        Assert.NotNull(savedEvent);
        Assert.Equal(WebhookEventStatus.Received, savedEvent.Status);
        Assert.Null(savedEvent.ProcessedAtUtc);

        // Financial ledger is NOT touched during ingestion
        await _ledgerPostingService.DidNotReceive().PostBankTransferReversalCoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsyncWebhookProcessing_WorkerClaimsAndReconcilesFinancialState()
    {
        // Arrange
        await using var db = CreateDbContext();
        var processor = CreateWebhookProcessor(db);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Wanda Maximoff",
            amount: 20000m,
            currency: Currency.NGN,
            feeAmount: 100m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "REF-ASYNC-002");
        transfer.MarkProcessing();
        db.BankTransfers.Add(transfer);

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: "CBZBT-REF-ASYNC-002-A1-FLW",
            amount: 20000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new
        {
            @event = "transfer.completed",
            data = new
            {
                id = 998877,
                reference = "CBZBT-REF-ASYNC-002-A1-FLW",
                status = "SUCCESSFUL",
                amount = 20000m,
                currency = "NGN"
            }
        });

        // 1. Ingestion
        await processor.IngestWebhookAsync(PaymentProvider.Flutterwave, payload, new Dictionary<string, string>());
        var savedEvent = await db.WebhookEvents.FirstAsync(w => w.Provider == PaymentProvider.Flutterwave);

        // 2. Asynchronous financial execution (by background worker)
        var processResult = await processor.ProcessFinancialWebhookEventAsync(savedEvent.Id);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, processResult.Status);

        var refreshedTransfer = await db.BankTransfers.FindAsync(transfer.Id);
        Assert.NotNull(refreshedTransfer);
        Assert.Equal(BankTransferStatus.Completed, refreshedTransfer.Status);

        var refreshedAttempt = await db.PaymentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(refreshedAttempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, refreshedAttempt.Status);

        var refreshedEvent = await db.WebhookEvents.FindAsync(savedEvent.Id);
        Assert.NotNull(refreshedEvent);
        Assert.Equal(WebhookEventStatus.Processed, refreshedEvent.Status);
    }

    [Fact]
    public async Task ConcurrentDuplicateWebhooks_ShouldResultInExactlyOneFinancialExecution()
    {
        // Arrange
        await using var db = CreateDbContext();
        var processor = CreateWebhookProcessor(db);

        var payload = JsonSerializer.Serialize(new
        {
            event_type = "SUCCESSFUL_DISBURSEMENT",
            eventData = new
            {
                reference = "REF-CONC-001",
                transactionReference = "MNF-DUP-999",
                amount = 5000m,
                currency = "NGN",
                status = "SUCCESS"
            }
        });

        // Act: Simulate concurrent duplicate webhook delivery
        var task1 = processor.IngestWebhookAsync(PaymentProvider.Monnify, payload, new Dictionary<string, string>());
        var task2 = processor.IngestWebhookAsync(PaymentProvider.Monnify, payload, new Dictionary<string, string>());

        var results = await Task.WhenAll(task1, task2);

        // Assert: One must be Processed and one must be Duplicate
        var statuses = results.Select(r => r.Status).ToList();
        Assert.Contains(WebhookProcessingStatus.Processed, statuses);
        Assert.Contains(WebhookProcessingStatus.Duplicate, statuses);

        // Exactly 1 webhook event record persisted
        var eventCount = await db.WebhookEvents.CountAsync();
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task FailoverChain_MonnifyFail_FlutterwaveFail_PaystackSuccess()
    {
        // Arrange
        await using var db = CreateDbContext();
        var failoverService = CreateFailoverService(db);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Stephen Strange",
            amount: 10000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CHAIN-001");
        transfer.MarkProcessing();
        db.BankTransfers.Add(transfer);

        // Initial attempt (Monnify) failed with TechnicalFailure
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-CHAIN-001-A1-MONNIFY",
            amount: 10000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("TECHNICAL_FAILURE", "Monnify connection timeout");
        db.PaymentAttempts.Add(attempt1);
        await db.SaveChangesAsync();

        // Fallback 1: Flutterwave returns TechnicalFailure
        _flutterwaveProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.TechnicalFailure("FLW_500", "Internal gateway error"));

        // Fallback 2: Paystack returns Success
        _paystackProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("PSTK-SUCCESS-777"));

        // Act 1: Failover to Flutterwave
        var failover1Result = await failoverService.FailoverAsync(ledgerTxnId);
        Assert.True(failover1Result.Succeeded);
        Assert.Equal(PaymentProvider.Flutterwave, failover1Result.FallbackProvider);

        // Act 2: Failover to Paystack
        var failover2Result = await failoverService.FailoverAsync(ledgerTxnId);
        Assert.True(failover2Result.Succeeded);
        Assert.Equal(PaymentProvider.Paystack, failover2Result.FallbackProvider);

        // Assert: 3 total attempts, final transfer Completed
        var attempts = await db.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTxnId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync();

        Assert.Equal(3, attempts.Count);
        Assert.Equal(PaymentProvider.Monnify, attempts[0].Provider);
        Assert.Equal(PaymentAttemptStatus.Failed, attempts[0].Status);

        Assert.Equal(PaymentProvider.Flutterwave, attempts[1].Provider);
        Assert.Equal(PaymentAttemptStatus.Failed, attempts[1].Status);

        Assert.Equal(PaymentProvider.Paystack, attempts[2].Provider);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempts[2].Status);

        var updatedTransfer = await db.BankTransfers.FindAsync(transfer.Id);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(BankTransferStatus.Completed, updatedTransfer.Status);

        // Transfer was NOT reversed because it ultimately succeeded
        await _ledgerPostingService.DidNotReceive().PostBankTransferReversalCoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailoverChain_FallbackBusinessFailure_MustReverseLedgerAndHaltChain()
    {
        // Arrange
        await using var db = CreateDbContext();
        var failoverService = CreateFailoverService(db);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Carol Danvers",
            amount: 10000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CHAIN-002");
        transfer.MarkProcessing();
        db.BankTransfers.Add(transfer);

        // Monnify failed with TechnicalFailure
        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "CBZBT-CHAIN-002-A1-MONNIFY",
            amount: 10000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkFailed("TECHNICAL_FAILURE", "Monnify network disconnect");
        db.PaymentAttempts.Add(attempt1);
        await db.SaveChangesAsync();

        // Fallback Flutterwave returns BusinessFailure (Account Invalid)
        _flutterwaveProvider
            .InitializePaymentAsync(Arg.Any<PaymentAttempt>(), Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.BusinessFailure("INVALID_ACCOUNT", "Destination account number does not exist"));

        // Act: Failover to Flutterwave
        var result = await failoverService.FailoverAsync(ledgerTxnId);

        // Assert: Chain halted, ledger reversed
        Assert.True(result.Succeeded); // The failover execution itself completed and handled the failure

        await _ledgerPostingService.Received(1).PostBankTransferReversalCoreAsync(
            transfer.Id,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var attempts = await db.PaymentAttempts
            .Where(p => p.LedgerTransactionId == ledgerTxnId)
            .ToListAsync();

        // Must NOT have attempted Paystack after business rejection
        Assert.Equal(2, attempts.Count);
        Assert.DoesNotContain(attempts, a => a.Provider == PaymentProvider.Paystack);
    }
}
