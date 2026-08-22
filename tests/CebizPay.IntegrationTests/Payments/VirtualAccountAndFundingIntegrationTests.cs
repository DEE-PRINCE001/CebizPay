using System.Text.Json;
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
using CebizPay.Infrastructure.Payments.Funding;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Payments.VirtualAccounts;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL Testcontainers integration tests for Dedicated Virtual Accounts, Inbound Funding ingestion,
/// Card Funding checkout, and Central Double-Entry Ledger posting.
/// </summary>
public sealed class VirtualAccountAndFundingIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public VirtualAccountAndFundingIntegrationTests(InfrastructureFixture fixture)
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
    public async Task DedicatedVirtualAccount_ProvisionAndInboundDeposit_ShouldCreditLedgerAndWalletInPostgreSQL()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);

        var userId = $"usr_va_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, $"{userId} NGN WALLET", Currency.NGN);
        dbContext.LedgerAccounts.Add(ledgerAccount);

        var profile = new IndividualProfile(userId, "Alice", "Smith");
        dbContext.IndividualProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var flwProvider = Substitute.For<IVirtualAccountProvider>();
        flwProvider.Provider.Returns(PaymentProvider.Flutterwave);
        var accountNumber = $"012{Random.Shared.Next(1000000, 9999999)}";

        flwProvider.CreateVirtualAccountAsync(Arg.Any<VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>())
            .Returns(VirtualAccountCreationResult.Success(accountNumber, "Alice Smith", "035", "Wema Bank", "flw_va_ref_1"));

        var vaService = new VirtualAccountService(
            new[] { flwProvider },
            dbContext,
            outboxService,
            NullLogger<VirtualAccountService>.Instance);

        // Step 1: Provision DVA
        var vaDto = await vaService.ProvisionIndividualVirtualAccountAsync(userId, Currency.NGN, PaymentProvider.Flutterwave);
        Assert.NotNull(vaDto);
        Assert.Equal(accountNumber, vaDto.AccountNumber);

        // Verify PostgreSQL persistence
        var vaEntity = await dbContext.VirtualAccounts.FirstOrDefaultAsync(v => v.AccountNumber == accountNumber);
        Assert.NotNull(vaEntity);
        Assert.Equal(VirtualAccountStatus.Active, vaEntity.Status);

        // Step 2: Webhook Ingestion for Inbound Deposit
        var signatureVerifier = Substitute.For<IWebhookSignatureVerifier>();
        signatureVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var flwOptions = Options.Create(new FlutterwaveOptions
        {
            WebhookSecretHash = "flw_test_secret",
            SecretKey = "FLWSECK_TEST"
        });
        var pstkOptions = Options.Create(new PaystackOptions
        {
            WebhookSecret = "pstk_test_secret",
            SecretKey = "sk_test_paystack"
        });

        var processor = new WebhookProcessor(
            signatureVerifier,
            dbContext,
            ledgerService,
            outboxService,
            flwOptions,
            pstkOptions,
            NullLogger<WebhookProcessor>.Instance);

        var depositAmount = 75000.00m;
        var depositEventId = $"flw_dep_evt_{Guid.NewGuid():N}";
        var webhookPayload = JsonSerializer.Serialize(new
        {
            @event = "charge.completed",
            data = new
            {
                id = Random.Shared.Next(100000, 999999),
                tx_ref = depositEventId,
                account_number = accountNumber,
                amount = depositAmount,
                currency = "NGN",
                status = "SUCCESSFUL"
            }
        });

        var headers = new Dictionary<string, string> { { "verif-hash", "flw_test_secret" } };

        // Act: Ingest deposit webhook
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Flutterwave, webhookPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        // Verify PostgreSQL wallet balance
        var updatedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(updatedWallet);
        Assert.Equal(depositAmount, updatedWallet.AvailableBalance);

        // Verify Central Double-Entry Ledger Transactions & Entries
        var fundingTx = await dbContext.FundingTransactions.FirstOrDefaultAsync(f => f.VirtualAccountId == vaEntity.Id);
        Assert.NotNull(fundingTx);
        Assert.Equal(FundingTransactionStatus.Completed, fundingTx.Status);
        Assert.NotNull(fundingTx.LedgerTransactionId);

        var ledgerTxn = await dbContext.LedgerTransactions.FindAsync(fundingTx.LedgerTransactionId.Value);
        Assert.NotNull(ledgerTxn);
        Assert.Equal(LedgerTransactionType.VirtualAccountDeposit, ledgerTxn.TransactionType);
        Assert.Equal(LedgerTransactionStatus.Completed, ledgerTxn.Status);

        var entries = await dbContext.LedgerEntries.Where(e => e.LedgerTransactionId == ledgerTxn.Id).ToListAsync();
        Assert.Equal(2, entries.Count);

        var debitEntry = entries.Single(e => e.Direction == LedgerEntryDirection.Debit);
        var creditEntry = entries.Single(e => e.Direction == LedgerEntryDirection.Credit);

        Assert.Equal(depositAmount, debitEntry.Amount);
        Assert.Equal(depositAmount, creditEntry.Amount);
        Assert.Equal(ledgerAccount.Id, creditEntry.LedgerAccountId);
    }

    [Fact]
    public async Task CardFunding_InitializeAndWebhookReconcile_ShouldCreditWalletInPostgreSQL()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);

        var userId = $"usr_card_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, $"{userId} NGN WALLET", Currency.NGN);
        dbContext.LedgerAccounts.Add(ledgerAccount);
        await dbContext.SaveChangesAsync();

        var cardProvider = Substitute.For<ICardPaymentProvider>();
        cardProvider.Provider.Returns(PaymentProvider.Paystack);
        cardProvider.InitializeCardPaymentAsync(Arg.Any<CardPaymentInitializationRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardPaymentInitializationResult.Success("https://checkout.paystack.com/pay/abc", "pstk_acc_code", "pstk_card_ref"));

        var cardService = new CardFundingService(
            new[] { cardProvider },
            dbContext,
            ledgerService,
            outboxService,
            NullLogger<CardFundingService>.Instance);

        var fundingAmount = 30000.00m;
        var initResponse = await cardService.InitializeCardFundingAsync(
            walletId: wallet.Id,
            amount: fundingAmount,
            currency: Currency.NGN,
            provider: PaymentProvider.Paystack,
            callbackUrl: "https://cebizpay.com/paystack/callback");

        Assert.NotNull(initResponse);
        var cardRef = initResponse.Reference;

        var signatureVerifier = Substitute.For<IWebhookSignatureVerifier>();
        signatureVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var flwOptions = Options.Create(new FlutterwaveOptions());
        var pstkOptions = Options.Create(new PaystackOptions { WebhookSecret = "pstk_secret_test", SecretKey = "sk_test_123" });

        var processor = new WebhookProcessor(
            signatureVerifier,
            dbContext,
            ledgerService,
            outboxService,
            flwOptions,
            pstkOptions,
            NullLogger<WebhookProcessor>.Instance);

        var webhookPayload = JsonSerializer.Serialize(new
        {
            @event = "charge.success",
            data = new
            {
                reference = cardRef,
                amount = (long)(fundingAmount * 100), // in kobo
                currency = "NGN",
                status = "success"
            }
        });

        var headers = new Dictionary<string, string> { { "x-paystack-signature", "pstk_secret_test" } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Paystack, webhookPayload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var updatedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(updatedWallet);
        Assert.Equal(fundingAmount, updatedWallet.AvailableBalance);

        var fundingTx = await dbContext.FundingTransactions.FindAsync(initResponse.FundingTransactionId);
        Assert.NotNull(fundingTx);
        Assert.Equal(FundingTransactionStatus.Completed, fundingTx.Status);
    }
}
