using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL integration tests ensuring WebhookProcessor operates reliably under NpgsqlRetryingExecutionStrategy.
/// Verifies that manual transactions executed via the execution strategy commit cleanly without throwing
/// InvalidOperationException and roll back safely on failure without leaving partial data.
/// </summary>
public sealed class WebhookExecutionStrategyTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public WebhookExecutionStrategyTests(InfrastructureFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<ApplicationDbContext> CreateRetryingDbContextAsync()
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

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static (string Payload, string Signature) GenerateMonnifyWebhook(
        string secretKey,
        string txRef,
        string payRef,
        string accountNumber,
        decimal amount,
        string status = "PAID")
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventType = "SUCCESSFUL_TRANSACTION",
            eventData = new
            {
                transactionReference = txRef,
                paymentReference = payRef,
                amountPaid = amount,
                totalPayable = amount,
                settlementAmount = amount,
                paymentStatus = status,
                currencyCode = "NGN",
                destinationAccountInformation = new
                {
                    bankCode = "232",
                    bankName = "Sterling Bank",
                    accountNumber = accountNumber
                }
            }
        });

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexStringLower(hashBytes);

        return (payload, signature);
    }

    private static WebhookProcessor CreateProcessor(ApplicationDbContext dbContext, string monnifySecret)
    {
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var feePolicyService = new PlatformFeePolicyService(dbContext, outboxService, NullLogger<PlatformFeePolicyService>.Instance);
        var signatureVerifier = new WebhookSignatureVerifier();

        var flwOptions = Options.Create(new FlutterwaveOptions());
        var pstkOptions = Options.Create(new PaystackOptions());
        var monnifyOptions = Options.Create(new MonnifyOptions
        {
            ApiKey = "MK_TEST_STRATEGY",
            SecretKey = monnifySecret,
            WebhookSecret = monnifySecret,
            ContractCode = "1234567890",
            Enabled = true
        });

        return new WebhookProcessor(
            signatureVerifier,
            dbContext,
            ledgerService,
            feePolicyService,
            outboxService,
            flwOptions,
            pstkOptions,
            monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessInboundVirtualAccountDeposit_UnderRetryingStrategy_CommitsCleanly()
    {
        // Arrange
        await using var dbContext = await CreateRetryingDbContextAsync();
        const string monnifySecret = "test_secret_exec_strat_va";
        var processor = CreateProcessor(dbContext, monnifySecret);

        var userId = $"usr_va_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var customerLedgerAccount = LedgerAccount.CreateWalletAccount(
            walletId: wallet.Id,
            accountName: $"Customer Wallet - {wallet.Id}",
            currency: Currency.NGN);
        dbContext.LedgerAccounts.Add(customerLedgerAccount);

        var accountNumber = $"{Random.Shared.Next(1000000000, 2000000000)}";
        var virtualAccount = VirtualAccount.CreateIndividual(
            individualId: userId,
            provider: PaymentProvider.Monnify,
            accountNumber: accountNumber,
            accountName: "Test VA User",
            bankCode: "232",
            bankName: "Sterling Bank",
            currency: Currency.NGN);
        dbContext.VirtualAccounts.Add(virtualAccount);
        await dbContext.SaveChangesAsync();

        var txRef = $"TX_VA_{Guid.NewGuid():N}"[..20];
        var payRef = $"PAY_VA_{Guid.NewGuid():N}"[..20];
        const decimal depositAmount = 7500.00m;

        var (payload, signature) = GenerateMonnifyWebhook(monnifySecret, txRef, payRef, accountNumber, depositAmount);
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert: Succeeded without NpgsqlRetryingExecutionStrategy InvalidOperationException
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(depositAmount, refreshedWallet.AvailableBalance);

        var fundingTx = await dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.WalletId == wallet.Id && f.VirtualAccountId == virtualAccount.Id);
        Assert.NotNull(fundingTx);
        Assert.Equal(FundingTransactionStatus.Completed, fundingTx.Status);
        Assert.Equal(depositAmount, fundingTx.Amount);

        var webhookEvent = await dbContext.WebhookEvents
            .FirstOrDefaultAsync(w => w.CorrelationReference == txRef || w.CorrelationReference == payRef || w.CorrelationReference == accountNumber);
        Assert.NotNull(webhookEvent);
        Assert.Equal(WebhookEventStatus.Processed, webhookEvent.Status);
    }

    [Fact]
    public async Task ProcessExternalFundingAccountDeposit_UnderRetryingStrategy_CommitsCleanly()
    {
        // Arrange
        await using var dbContext = await CreateRetryingDbContextAsync();
        const string monnifySecret = "test_secret_exec_strat_efa";
        var processor = CreateProcessor(dbContext, monnifySecret);

        var userId = $"usr_efa_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var customerLedgerAccount = LedgerAccount.CreateWalletAccount(
            walletId: wallet.Id,
            accountName: $"Customer Wallet - {wallet.Id}",
            currency: Currency.NGN);
        dbContext.LedgerAccounts.Add(customerLedgerAccount);

        var accountNumber = $"{Random.Shared.Next(1000000000, 2000000000)}";
        var extAccount = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: accountNumber,
            accountName: "Test EFA User",
            bankCode: "232",
            bankName: "Sterling Bank",
            currency: Currency.NGN,
            isPrimary: true);
        dbContext.ExternalFundingAccounts.Add(extAccount);
        await dbContext.SaveChangesAsync();

        var txRef = $"TX_EFA_{Guid.NewGuid():N}"[..20];
        var payRef = $"PAY_EFA_{Guid.NewGuid():N}"[..20];
        const decimal depositAmount = 12000.00m;

        var (payload, signature) = GenerateMonnifyWebhook(monnifySecret, txRef, payRef, accountNumber, depositAmount);
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(depositAmount, refreshedWallet.AvailableBalance);

        var fundingTx = await dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.WalletId == wallet.Id && f.ExternalFundingAccountId == extAccount.Id);
        Assert.NotNull(fundingTx);
        Assert.Equal(FundingTransactionStatus.Completed, fundingTx.Status);
    }

    [Fact]
    public async Task ProcessCardFunding_UnderRetryingStrategy_CommitsCleanly()
    {
        // Arrange
        await using var dbContext = await CreateRetryingDbContextAsync();
        const string monnifySecret = "test_secret_exec_strat_card";
        var processor = CreateProcessor(dbContext, monnifySecret);

        var userId = $"usr_card_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var customerLedgerAccount = LedgerAccount.CreateWalletAccount(
            walletId: wallet.Id,
            accountName: $"Customer Wallet - {wallet.Id}",
            currency: Currency.NGN);
        dbContext.LedgerAccounts.Add(customerLedgerAccount);

        const decimal cardAmount = 3000.00m;
        var cardRef = $"CRD_{Guid.NewGuid():N}"[..20];
        var fundingTx = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Monnify,
            providerTransactionReference: cardRef,
            fundingChannel: FundingChannel.Card,
            amount: cardAmount,
            currency: Currency.NGN);
        dbContext.FundingTransactions.Add(fundingTx);
        await dbContext.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new
        {
            eventType = "SUCCESSFUL_TRANSACTION",
            eventData = new
            {
                transactionReference = cardRef,
                paymentReference = cardRef,
                amountPaid = cardAmount,
                paymentStatus = "PAID",
                currencyCode = "NGN"
            }
        });

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(monnifySecret));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(cardAmount, refreshedWallet.AvailableBalance);

        var refreshedFunding = await dbContext.FundingTransactions.FindAsync(fundingTx.Id);
        Assert.NotNull(refreshedFunding);
        Assert.Equal(FundingTransactionStatus.Completed, refreshedFunding.Status);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_OnFailure_RollsBackWithNoPartialData()
    {
        // Arrange
        await using var dbContext = await CreateRetryingDbContextAsync();
        const string monnifySecret = "test_secret_exec_strat_rollback";
        var processor = CreateProcessor(dbContext, monnifySecret);

        var userId = $"usr_frozen_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        wallet.Freeze();
        dbContext.Wallets.Add(wallet);

        var customerLedgerAccount = LedgerAccount.CreateWalletAccount(
            walletId: wallet.Id,
            accountName: $"Frozen Wallet - {wallet.Id}",
            currency: Currency.NGN);
        dbContext.LedgerAccounts.Add(customerLedgerAccount);

        var accountNumber = $"{Random.Shared.Next(1000000000, 2000000000)}";
        var virtualAccount = VirtualAccount.CreateIndividual(
            individualId: userId,
            provider: PaymentProvider.Monnify,
            accountNumber: accountNumber,
            accountName: "Suspended User",
            bankCode: "232",
            bankName: "Sterling Bank",
            currency: Currency.NGN);
        dbContext.VirtualAccounts.Add(virtualAccount);
        await dbContext.SaveChangesAsync();

        var txRef = $"TX_ROLLBACK_{Guid.NewGuid():N}"[..20];
        var payRef = $"PAY_ROLLBACK_{Guid.NewGuid():N}"[..20];
        const decimal depositAmount = 5000.00m;

        var (payload, signature) = GenerateMonnifyWebhook(monnifySecret, txRef, payRef, accountNumber, depositAmount);
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert: Processing failed cleanly
        Assert.Equal(WebhookProcessingStatus.Error, result.Status);

        // Assert: Wallet balance was NOT updated
        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(0m, refreshedWallet.AvailableBalance);

        // Assert: No funding transaction was created/committed
        var fundingTx = await dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.WalletId == wallet.Id && f.VirtualAccountId == virtualAccount.Id);
        Assert.Null(fundingTx);

        // Assert: WebhookEvent released claim with error recorded
        var webhookEvent = await dbContext.WebhookEvents
            .FirstOrDefaultAsync(w => w.CorrelationReference == txRef || w.CorrelationReference == payRef || w.CorrelationReference == accountNumber);
        Assert.NotNull(webhookEvent);
        Assert.Equal(WebhookEventStatus.Received, webhookEvent.Status);
        Assert.NotNull(webhookEvent.ProcessingError);
        Assert.Contains("not active", webhookEvent.ProcessingError);
    }
}
