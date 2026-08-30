using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Funding;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL Testcontainers integration tests for tokenized saved cards, recurring charge execution,
/// platform fee policies, card refunds, and central double-entry ledger verification.
/// </summary>
public sealed class CardFundingIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public CardFundingIntegrationTests(InfrastructureFixture fixture)
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
    public async Task SavedCard_StoreAndCharge_ShouldPostDoubleEntryLedgerCreditAndIncreaseWalletBalance()
    {
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);
        var feePolicyService = new PlatformFeePolicyService(dbContext, outboxService, NullLogger<PlatformFeePolicyService>.Instance);
        var savedCardService = new SavedCardService(dbContext, outboxService, NullLogger<SavedCardService>.Instance);

        var userId = $"usr_card_it_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, $"{userId} NGN WALLET", Currency.NGN);
        dbContext.LedgerAccounts.Add(ledgerAccount);
        await dbContext.SaveChangesAsync();

        // 1. Store tokenized saved card
        var savedCardDto = await savedCardService.SaveCardTokenAsync(
            userId: userId,
            walletId: wallet.Id,
            provider: PaymentProvider.Flutterwave,
            providerToken: "flw_token_pg_test",
            last4: "4242",
            brand: "Visa",
            expiryMonth: "12",
            expiryYear: "2030",
            isDefault: true);

        Assert.NotNull(savedCardDto);
        Assert.True(savedCardDto.IsDefault);

        // 2. Set up card provider mock
        var cardProvider = Substitute.For<ICardPaymentProvider>();
        cardProvider.Provider.Returns(PaymentProvider.Flutterwave);
        cardProvider.ChargeSavedCardAsync(Arg.Any<CardSavedChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardChargeResult.Success("FLW-TX-SAVED-100"));

        var routingService = Substitute.For<IPaymentRoutingService>();
        routingService.ResolvePrimaryProvider(PaymentCapability.CardFunding).Returns(PaymentProvider.Flutterwave);

        var cardFundingService = new CardFundingService(
            new[] { cardProvider },
            routingService,
            feePolicyService,
            dbContext,
            ledgerService,
            outboxService,
            NullLogger<CardFundingService>.Instance);

        // 3. Charge saved card for 12,000 NGN
        const decimal chargeAmount = 12000.00m;
        var chargeResponse = await cardFundingService.ChargeSavedCardAsync(
            savedCardId: savedCardDto.Id,
            amount: chargeAmount,
            currency: Currency.NGN,
            idempotencyKey: $"idem_charge_{Guid.NewGuid():N}",
            actorUserId: userId);

        Assert.NotNull(chargeResponse);
        Assert.Equal("Completed", chargeResponse.Status);
        Assert.Equal(chargeAmount, chargeResponse.GrossAmount);

        // 4. Verify Wallet balance updated in PostgreSQL
        var refreshedWallet = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.NotNull(refreshedWallet);
        Assert.Equal(chargeAmount, refreshedWallet.AvailableBalance);

        // 5. Verify double-entry ledger entries
        var fundingTx = await dbContext.FundingTransactions.FindAsync(chargeResponse.FundingTransactionId);
        Assert.NotNull(fundingTx);
        Assert.Equal(FundingTransactionStatus.Completed, fundingTx.Status);

        var ledgerTxn = await dbContext.LedgerTransactions.FindAsync(fundingTx.LedgerTransactionId);
        Assert.NotNull(ledgerTxn);

        var entries = await dbContext.LedgerEntries.Where(e => e.LedgerTransactionId == ledgerTxn.Id).ToListAsync();
        Assert.Equal(2, entries.Count);

        var debitEntry = entries.Single(e => e.Direction == LedgerEntryDirection.Debit);
        var creditEntry = entries.Single(e => e.Direction == LedgerEntryDirection.Credit);

        Assert.Equal(chargeAmount, debitEntry.Amount);
        Assert.Equal(chargeAmount, creditEntry.Amount);
        Assert.Equal(ledgerAccount.Id, creditEntry.LedgerAccountId);
    }

    [Fact]
    public async Task CardRefund_WhenWalletHasFunds_ReversesLedgerAndDebitsWalletBalance()
    {
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerService = new LedgerPostingService(dbContext);

        var userId = $"usr_refund_it_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, $"{userId} NGN WALLET", Currency.NGN);
        dbContext.LedgerAccounts.Add(ledgerAccount);
        await dbContext.SaveChangesAsync();

        // Simulate existing funding credit of 20,000 NGN
        const decimal initialDeposit = 20000.00m;
        var (depositTxn, fundingTx) = await ledgerService.PostCardFundingCreditCoreAsync(
            walletId: wallet.Id,
            grossAmount: initialDeposit,
            feeAmount: 0m,
            netCreditedAmount: initialDeposit,
            providerFeeAmount: 0m,
            currency: Currency.NGN,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: $"FLW-ORIG-{Guid.NewGuid():N}",
            providerEventReference: null,
            feePolicyId: null,
            feePolicyVersion: null,
            feeBearer: null);

        var walletAfterDeposit = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.Equal(initialDeposit, walletAfterDeposit!.AvailableBalance);

        // Set up card refund service
        var cardProvider = Substitute.For<ICardPaymentProvider>();
        cardProvider.Provider.Returns(PaymentProvider.Flutterwave);
        cardProvider.RefundCardPaymentAsync(Arg.Any<CardRefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardRefundResult.Success("FLW-REFUND-SUCCESS-01"));

        var refundService = new CardRefundService(
            new[] { cardProvider },
            dbContext,
            ledgerService,
            outboxService,
            NullLogger<CardRefundService>.Instance);

        // Execute partial refund of 7,500 NGN
        const decimal refundAmount = 7500.00m;
        var refundResponse = await refundService.RequestCardRefundAsync(
            fundingTransactionId: fundingTx.Id,
            amount: refundAmount,
            reason: "Defective service refund",
            idempotencyKey: $"idem_ref_{Guid.NewGuid():N}",
            actorUserId: userId);

        Assert.NotNull(refundResponse);
        Assert.Equal("Succeeded", refundResponse.Status);
        Assert.Equal("FLW-REFUND-SUCCESS-01", refundResponse.ProviderRefundReference);

        // Verify wallet debited by refund amount
        var walletAfterRefund = await dbContext.Wallets.FindAsync(wallet.Id);
        Assert.Equal(initialDeposit - refundAmount, walletAfterRefund!.AvailableBalance);

        // Verify reversal ledger entries
        var refundEntity = await dbContext.CardRefunds.FindAsync(refundResponse.Id);
        Assert.NotNull(refundEntity);
        Assert.NotNull(refundEntity.LedgerTransactionId);

        var reversalEntries = await dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == refundEntity.LedgerTransactionId.Value)
            .ToListAsync();

        Assert.Equal(2, reversalEntries.Count);
        var refundDebit = reversalEntries.Single(e => e.Direction == LedgerEntryDirection.Debit);
        var refundCredit = reversalEntries.Single(e => e.Direction == LedgerEntryDirection.Credit);

        Assert.Equal(refundAmount, refundDebit.Amount);
        Assert.Equal(ledgerAccount.Id, refundDebit.LedgerAccountId); // Customer wallet debited
        Assert.Equal(refundAmount, refundCredit.Amount); // Inbound clearing credited
    }
}
