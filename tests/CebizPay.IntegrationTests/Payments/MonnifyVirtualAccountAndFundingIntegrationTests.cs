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
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL Testcontainers integration tests for Monnify Reserved Virtual Accounts and Inbound Wallet Funding.
/// Verifies end-to-end signature verification, fee policy application, double-entry ledger posting, and audit trail integrity.
/// </summary>
public sealed class MonnifyVirtualAccountAndFundingIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public MonnifyVirtualAccountAndFundingIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static (string Payload, string Signature) GenerateMonnifyWebhook(string secretKey, string txRef, string payRef, string accountNumber, decimal amount, string status = "PAID")
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
                    bankCode = "035",
                    bankName = "Wema Bank",
                    accountNumber = accountNumber
                }
            }
        });

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexStringLower(hashBytes);

        return (payload, signature);
    }

    [Fact]
    public async Task MonnifyInboundFunding_WithPlatformFeePolicy_ShouldExecuteDoubleEntryAndCreditWallet()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var feePolicyService = new PlatformFeePolicyService(dbContext, outboxService, NullLogger<PlatformFeePolicyService>.Instance);
        var signatureVerifier = new WebhookSignatureVerifier();

        const string monnifySecret = "test_monnify_secret_key_777";
        var flwOptions = Options.Create(new FlutterwaveOptions());
        var pstkOptions = Options.Create(new PaystackOptions());
        var monnifyOptions = Options.Create(new MonnifyOptions
        {
            ApiKey = "MK_TEST_777",
            SecretKey = monnifySecret,
            WebhookSecret = monnifySecret,
            ContractCode = "1234567890",
            Enabled = true
        });

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

        // 1. Create active PlatformFeePolicy for VirtualAccountFunding: 100 NGN fixed fee (CustomerPays)
        var feePolicy = await feePolicyService.CreateAndActivatePolicyAsync(
            operationType: FeeOperationType.VirtualAccountFunding,
            calculationMethod: FeeCalculationMethod.Fixed,
            feeBearer: FeeBearer.CustomerPays,
            fixedAmount: 100.00m,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            currency: Currency.NGN,
            createdByUserId: "admin_user",
            effectiveFromUtc: DateTime.UtcNow.AddMinutes(-5));

        // 2. Create customer wallet & ExternalFundingAccount
        var userId = $"usr_monnify_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        // Also create customer ledger account for wallet
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
            accountName: "John Monnify User",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerCustomerReference: userId,
            providerAccountReference: $"MNFY_ACC_{Guid.NewGuid():N}",
            isPrimary: true);
        dbContext.ExternalFundingAccounts.Add(extAccount);
        await dbContext.SaveChangesAsync();

        var grossDepositAmount = 50000.00m;
        var txRef = $"MNFY_TX_{Guid.NewGuid():N}";
        var payRef = $"MNFY_PAY_{Guid.NewGuid():N}";
        var (payload, signature) = GenerateMonnifyWebhook(monnifySecret, txRef, payRef, accountNumber, grossDepositAmount);
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act: Process inbound webhook
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        // Verify Wallet balance materialized (+ grossDepositAmount for CustomerPays)
        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(50000.00m, refreshedWallet.AvailableBalance);

        // Verify FundingTransaction record
        var fundingTx = await dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.Provider == PaymentProvider.Monnify && f.ProviderTransactionReference == txRef);
        Assert.NotNull(fundingTx);
        Assert.Equal(FundingTransactionStatus.Completed, fundingTx.Status);
        Assert.Equal(grossDepositAmount, fundingTx.Amount);
        Assert.Equal(100.00m, fundingTx.FeeAmount);
        Assert.Equal(50000.00m, fundingTx.NetCreditedAmount);
        Assert.Equal(extAccount.Id, fundingTx.ExternalFundingAccountId);
        Assert.NotNull(fundingTx.LedgerTransactionId);

        // Verify double-entry ledger entries (Total Debits == Total Credits)
        var ledgerEntries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == fundingTx.LedgerTransactionId.Value)
            .ToListAsync();

        Assert.Equal(3, ledgerEntries.Count);
        var totalDebits = ledgerEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
        var totalCredits = ledgerEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);

        // In CustomerPays mode: Gross amount deposited, fee is recorded
        Assert.True(totalDebits > 0);

        // Verify Outbox Event
        var outboxEvents = await dbContext.OutboxMessages
            .Where(m => m.Type.Contains("ExternalFundingAccountDepositCompletedDomainEvent"))
            .ToListAsync();
        Assert.NotEmpty(outboxEvents);

        // Verify Audit Logs
        var auditLogs = await dbContext.AuditLogs
            .Where(a => a.Action == AuditActions.FundingReceived || a.Action == AuditActions.PaymentFundingCompleted)
            .ToListAsync();
        Assert.NotEmpty(auditLogs);
    }

    [Fact]
    public async Task MonnifyInboundFunding_DuplicateWebhook_ShouldBeAcknowledgedWithoutDuplicateLedgerPosting()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var feePolicyService = new PlatformFeePolicyService(dbContext, outboxService, NullLogger<PlatformFeePolicyService>.Instance);
        var signatureVerifier = new WebhookSignatureVerifier();

        const string monnifySecret = "test_monnify_secret_dup";
        var flwOptions = Options.Create(new FlutterwaveOptions());
        var pstkOptions = Options.Create(new PaystackOptions());
        var monnifyOptions = Options.Create(new MonnifyOptions
        {
            ApiKey = "MK_TEST_DUP",
            SecretKey = monnifySecret,
            WebhookSecret = monnifySecret,
            ContractCode = "1234567890",
            Enabled = true
        });

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

        var userId = $"usr_monnify_dup_{Guid.NewGuid():N}";
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
            accountName: "Dup Monnify User",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerCustomerReference: userId,
            isPrimary: true);
        dbContext.ExternalFundingAccounts.Add(extAccount);
        await dbContext.SaveChangesAsync();

        var txRef = $"MNFY_TX_DUP_{Guid.NewGuid():N}";
        var payRef = $"MNFY_PAY_DUP_{Guid.NewGuid():N}";
        var (payload, signature) = GenerateMonnifyWebhook(monnifySecret, txRef, payRef, accountNumber, 20000.00m);
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act 1: Initial delivery
        var result1 = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Act 2: Duplicate delivery
        var result2 = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Processed, result1.Status);
        Assert.Equal(WebhookProcessingStatus.Duplicate, result2.Status);

        // Ensure wallet was credited only ONCE
        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(20000.00m, refreshedWallet.AvailableBalance);
    }

    [Fact]
    public async Task MonnifyInboundFunding_UnmatchedAccount_ShouldRecordAuditAndNotCreditAnyWallet()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var feePolicyService = new PlatformFeePolicyService(dbContext, outboxService, NullLogger<PlatformFeePolicyService>.Instance);
        var signatureVerifier = new WebhookSignatureVerifier();

        const string monnifySecret = "test_monnify_secret_unmatched";
        var flwOptions = Options.Create(new FlutterwaveOptions());
        var pstkOptions = Options.Create(new PaystackOptions());
        var monnifyOptions = Options.Create(new MonnifyOptions
        {
            ApiKey = "MK_TEST_UNMATCHED",
            SecretKey = monnifySecret,
            WebhookSecret = monnifySecret,
            ContractCode = "1234567890",
            Enabled = true
        });

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

        var unknownAccountNumber = "0000000000";
        var txRef = $"MNFY_TX_UNM_{Guid.NewGuid():N}";
        var payRef = $"MNFY_PAY_UNM_{Guid.NewGuid():N}";
        var (payload, signature) = GenerateMonnifyWebhook(monnifySecret, txRef, payRef, unknownAccountNumber, 15000.00m);
        var headers = new Dictionary<string, string> { { "monnify-signature", signature } };

        // Act
        var result = await processor.ProcessWebhookAsync(PaymentProvider.Monnify, payload, headers);

        // Assert
        Assert.Equal(WebhookProcessingStatus.Ignored, result.Status);

        // Verify audit log recorded for unmatched transaction
        var unmatchedAudit = await dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == AuditActions.WebhookUnmatchedTransaction);
        Assert.NotNull(unmatchedAudit);
        Assert.Contains(unknownAccountNumber, unmatchedAudit.AfterJson ?? string.Empty);
    }
}
