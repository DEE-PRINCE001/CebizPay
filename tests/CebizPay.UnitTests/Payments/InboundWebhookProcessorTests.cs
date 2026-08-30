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

    private readonly IOptions<MonnifyOptions> _monnifyOptions = Options.Create(new MonnifyOptions
    {
        WebhookSecret = "mnfy_secret_123",
        SecretKey = "mnfy_secret_123",
        ApiKey = "mnfy_key_123",
        ContractCode = "1234567890",
        Enabled = true
    });

    private readonly IPlatformFeePolicyService _feePolicyService = Substitute.For<IPlatformFeePolicyService>();

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
            _feePolicyService,
            _outbox,
            _flwOptions,
            _pstkOptions,
            _monnifyOptions,
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
        _ledgerPosting.PostCardFundingCreditCoreAsync(
            walletId: wallet.Id,
            grossAmount: 10000m,
            feeAmount: Arg.Any<decimal>(),
            netCreditedAmount: Arg.Any<decimal>(),
            providerFeeAmount: Arg.Any<decimal>(),
            currency: Currency.NGN,
            provider: PaymentProvider.Paystack,
            providerTransactionReference: "CBZCD-PSTK123",
            providerEventReference: Arg.Any<string?>(),
            feePolicyId: Arg.Any<Guid?>(),
            feePolicyVersion: Arg.Any<int?>(),
            feeBearer: Arg.Any<FeeBearer?>(),
            description: Arg.Any<string?>(),
            cancellationToken: Arg.Any<CancellationToken>())
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
        await _ledgerPosting.Received(1).PostCardFundingCreditCoreAsync(
            wallet.Id, 10000m, Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>(), Currency.NGN, PaymentProvider.Paystack, "CBZCD-PSTK123", Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int?>(), Arg.Any<FeeBearer?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhookAsync_MonnifyExternalFundingAccountDeposit_CreditsWalletWithFeePolicy()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_monnify_fund", Currency.NGN);
        db.Wallets.Add(wallet);

        var extAccount = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "7820987654",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerCustomerReference: "usr_monnify_fund",
            providerAccountReference: "MNFY_ACC_100",
            isPrimary: true);
        db.ExternalFundingAccounts.Add(extAccount);
        await db.SaveChangesAsync();

        var feePolicy = PlatformFeePolicy.CreateFixed(
            operationType: FeeOperationType.VirtualAccountFunding,
            fixedAmount: 150m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin");
        _feePolicyService.GetActivePolicyAsync(FeeOperationType.VirtualAccountFunding, Arg.Any<CancellationToken>())
            .Returns(feePolicy);

        var ledgerTxn = new LedgerTransaction(LedgerTransactionType.VirtualAccountDeposit, "FND-EXT-MNFY_TX_001", null, null);
        var fundingTx = FundingTransaction.CreateWithExternalAccount(
            wallet.Id, extAccount.Id, PaymentProvider.Monnify, "MNFY_TX_001", "mnfy_evt_1", FundingChannel.VirtualAccount,
            10000m, 150m, 10000m, 0m, feePolicy.Id, 1, FeeBearer.CustomerPays, Currency.NGN);

        _ledgerPosting.PostExternalFundingAccountCreditCoreAsync(
            wallet.Id, extAccount.Id, 10000m, 150m, 10000m, 0m, Currency.NGN, PaymentProvider.Monnify, "MNFY_TX_001",
            Arg.Any<string?>(), feePolicy.Id, feePolicy.Version, feePolicy.FeeBearer, FundingChannel.VirtualAccount, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((ledgerTxn, fundingTx));

        var processor = CreateProcessor(db);

        var payload = JsonSerializer.Serialize(new
        {
            eventType = "SUCCESSFUL_TRANSACTION",
            eventData = new
            {
                transactionReference = "MNFY_TX_001",
                paymentReference = "MNFY_PAY_001",
                amountPaid = 10000m,
                totalPayable = 10000m,
                settlementAmount = 9900m,
                paymentStatus = "PAID",
                currencyCode = "NGN",
                destinationAccountInformation = new
                {
                    bankCode = "035",
                    bankName = "Wema Bank",
                    accountNumber = "7820987654"
                }
            }
        });

        var headers = new Dictionary<string, string> { { "monnify-signature", "mnfy_secret_123" } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);
        await _ledgerPosting.Received(1).PostExternalFundingAccountCreditCoreAsync(
            wallet.Id, extAccount.Id, 10000m, 150m, 10000m, 0m, Currency.NGN, PaymentProvider.Monnify, "MNFY_TX_001",
            Arg.Any<string?>(), feePolicy.Id, feePolicy.Version, feePolicy.FeeBearer, FundingChannel.VirtualAccount, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhookAsync_MonnifyUnmatchedAccount_RecordsUnmatchedAuditAndReturnsIgnored()
    {
        // Arrange
        using var db = CreateDbContext();
        var processor = CreateProcessor(db);

        var payload = JsonSerializer.Serialize(new
        {
            eventType = "SUCCESSFUL_TRANSACTION",
            eventData = new
            {
                transactionReference = "MNFY_UNKNOWN_TX",
                paymentReference = "MNFY_UNKNOWN_PAY",
                amountPaid = 50000m,
                paymentStatus = "PAID",
                currencyCode = "NGN",
                destinationAccountInformation = new
                {
                    bankCode = "035",
                    bankName = "Wema Bank",
                    accountNumber = "9999999999" // Unknown account
                }
            }
        });

        var headers = new Dictionary<string, string> { { "monnify-signature", "mnfy_secret_123" } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Ignored, result.Status);

        // Verify audit log
        var audits = await db.AuditLogs
            .Where(a => a.Action == CebizPay.Domain.Auditing.AuditActions.WebhookUnmatchedTransaction)
            .ToListAsync();
        Assert.Single(audits);
    }
}
