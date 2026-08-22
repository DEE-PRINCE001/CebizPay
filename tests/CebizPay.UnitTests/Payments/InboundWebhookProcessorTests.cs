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
/// Unit tests for <see cref="WebhookProcessor"/> handling inbound virtual account deposits and card funding payments.
/// </summary>
public sealed class InboundWebhookProcessorTests
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

    public InboundWebhookProcessorTests()
    {
        _signatureVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private WebhookProcessor CreateProcessor(ApplicationDbContext dbContext)
    {
        return new WebhookProcessor(
            _signatureVerifier,
            dbContext,
            _ledgerPosting,
            _outbox,
            _flwOptions,
            _pstkOptions,
            NullLogger<WebhookProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessWebhookAsync_FlutterwaveInboundVirtualAccountDeposit_CreditsWallet()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_dva_1", Currency.NGN);
        db.Wallets.Add(wallet);

        var va = VirtualAccount.CreateIndividual(
            individualId: "usr_dva_1",
            provider: PaymentProvider.Flutterwave,
            accountNumber: "0123456789",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN);
        db.VirtualAccounts.Add(va);
        await db.SaveChangesAsync();

        var ledgerTxn = new LedgerTransaction(LedgerTransactionType.VirtualAccountDeposit, "FND-0123456789", null, null);
        var fundingTx = FundingTransaction.Create(wallet.Id, va.Id, PaymentProvider.Flutterwave, "flw_dep_1", FundingChannel.VirtualAccount, 50000m, Currency.NGN);
        _ledgerPosting.PostInboundFundingCreditCoreAsync(
            wallet.Id, va.Id, 50000m, Currency.NGN, PaymentProvider.Flutterwave, Arg.Any<string>(), FundingChannel.VirtualAccount, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((ledgerTxn, fundingTx));

        var processor = CreateProcessor(db);

        var payload = JsonSerializer.Serialize(new
        {
            @event = "charge.completed",
            data = new
            {
                id = 998877,
                tx_ref = "FLW-DEP-998877",
                account_number = "0123456789",
                amount = 50000.00m,
                currency = "NGN",
                status = "SUCCESSFUL"
            }
        });

        var headers = new Dictionary<string, string> { { "verif-hash", "flw_secret_hash_123" } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);
        await _ledgerPosting.Received(1).PostInboundFundingCreditCoreAsync(
            wallet.Id, va.Id, 50000m, Currency.NGN, PaymentProvider.Flutterwave, Arg.Any<string>(), FundingChannel.VirtualAccount, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaystackCardFundingSuccess_CreditsWallet()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_card_fund", Currency.NGN);
        db.Wallets.Add(wallet);

        var fundingTx = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Paystack,
            providerTransactionReference: "CBZCD-PSTK123",
            fundingChannel: FundingChannel.Card,
            amount: 10000m,
            currency: Currency.NGN);
        db.FundingTransactions.Add(fundingTx);
        await db.SaveChangesAsync();

        var ledgerTxn = new LedgerTransaction(LedgerTransactionType.CardFunding, "FND-CBZCD-PSTK123", null, null);
        _ledgerPosting.PostInboundFundingCreditCoreAsync(
            wallet.Id, null, 10000m, Currency.NGN, PaymentProvider.Paystack, "CBZCD-PSTK123", FundingChannel.Card, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((ledgerTxn, fundingTx));

        var processor = CreateProcessor(db);

        var payload = JsonSerializer.Serialize(new
        {
            @event = "charge.success",
            data = new
            {
                reference = "CBZCD-PSTK123",
                amount = 1000000, // 10,000 NGN in kobo
                currency = "NGN",
                status = "success"
            }
        });

        var headers = new Dictionary<string, string> { { "x-paystack-signature", "pstk_secret_123" } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Paystack, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);
        await _ledgerPosting.Received(1).PostInboundFundingCreditCoreAsync(
            wallet.Id, null, 10000m, Currency.NGN, PaymentProvider.Paystack, "CBZCD-PSTK123", FundingChannel.Card, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
