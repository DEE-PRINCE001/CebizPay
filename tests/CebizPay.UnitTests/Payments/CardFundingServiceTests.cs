using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Payments.Funding;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="CardFundingService"/> initialization and reconciliation.
/// </summary>
public sealed class CardFundingServiceTests
{
    private readonly ICardPaymentProvider _flwCardProvider = Substitute.For<ICardPaymentProvider>();
    private readonly ICardPaymentProvider _pstkCardProvider = Substitute.For<ICardPaymentProvider>();
    private readonly ILedgerPostingService _ledgerPosting = Substitute.For<ILedgerPostingService>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    public CardFundingServiceTests()
    {
        _flwCardProvider.Provider.Returns(PaymentProvider.Flutterwave);
        _pstkCardProvider.Provider.Returns(PaymentProvider.Paystack);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private CardFundingService CreateService(ApplicationDbContext dbContext)
    {
        return new CardFundingService(
            new[] { _flwCardProvider, _pstkCardProvider },
            dbContext,
            _ledgerPosting,
            _outbox,
            NullLogger<CardFundingService>.Instance);
    }

    [Fact]
    public async Task InitializeCardFundingAsync_WithValidWallet_CreatesPendingTransactionAndReturnsUrl()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_card_1", Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        _flwCardProvider.InitializeCardPaymentAsync(Arg.Any<CardPaymentInitializationRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardPaymentInitializationResult.Success("https://checkout.flutterwave.com/pay/abc123xyz", null, "CBZCD-REF"));

        var service = CreateService(db);

        // Act
        var response = await service.InitializeCardFundingAsync(
            walletId: wallet.Id,
            amount: 15000.00m,
            currency: Currency.NGN,
            provider: PaymentProvider.Flutterwave,
            callbackUrl: "https://cebizpay.com/callback");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("https://checkout.flutterwave.com/pay/abc123xyz", response.AuthorizationUrl);
        Assert.Equal("Flutterwave", response.Provider);

        var persisted = await db.FundingTransactions.FirstOrDefaultAsync(f => f.Id == response.FundingTransactionId);
        Assert.NotNull(persisted);
        Assert.Equal(wallet.Id, persisted.WalletId);
        Assert.Equal(15000.00m, persisted.Amount);
        Assert.Equal(FundingChannel.Card, persisted.FundingChannel);
        Assert.Equal(FundingTransactionStatus.Pending, persisted.Status);

        _outbox.Received(1).Write(Arg.Any<CardFundingInitiatedDomainEvent>());
    }

    [Fact]
    public async Task InitializeCardFundingAsync_WhenWalletInactive_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_suspended", Currency.NGN);
        wallet.Freeze();
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InitializeCardFundingAsync(
                walletId: wallet.Id,
                amount: 5000m,
                currency: Currency.NGN,
                provider: PaymentProvider.Flutterwave,
                callbackUrl: "https://cebizpay.com/callback"));
    }

    [Fact]
    public async Task ReconcileCardFundingAsync_WhenProviderConfirmsSuccess_CreditsLedgerAndCompletesFunding()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_rec_1", Currency.NGN);
        db.Wallets.Add(wallet);

        var funding = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: "CBZCD-REC123",
            fundingChannel: FundingChannel.Card,
            amount: 10000m,
            currency: Currency.NGN);
        db.FundingTransactions.Add(funding);
        await db.SaveChangesAsync();

        _flwCardProvider.GetCardPaymentStatusAsync("CBZCD-REC123", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("flw_tx_999"));

        var ledgerTxn = new LedgerTransaction(LedgerTransactionType.CardFunding, "FND-CBZCD-REC123", null, null);
        _ledgerPosting.PostInboundFundingCreditCoreAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<decimal>(), Arg.Any<Currency>(), Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<FundingChannel>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((ledgerTxn, funding));

        var service = CreateService(db);

        // Act
        var result = await service.ReconcileCardFundingAsync(funding.Id);

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        _outbox.Received(1).Write(Arg.Any<CardFundingCompletedDomainEvent>());
    }

    [Fact]
    public async Task ReconcileCardFundingAsync_WhenProviderConfirmsFailure_MarksFundingFailed()
    {
        // Arrange
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_rec_2", Currency.NGN);
        db.Wallets.Add(wallet);

        var funding = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Paystack,
            providerTransactionReference: "CBZCD-FAIL123",
            fundingChannel: FundingChannel.Card,
            amount: 2000m,
            currency: Currency.NGN);
        db.FundingTransactions.Add(funding);
        await db.SaveChangesAsync();

        _pstkCardProvider.GetCardPaymentStatusAsync("CBZCD-FAIL123", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.BusinessFailure("CARD_DECLINED", "Card has expired"));

        var service = CreateService(db);

        // Act
        var result = await service.ReconcileCardFundingAsync(funding.Id);

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);

        var updated = await db.FundingTransactions.FindAsync(funding.Id);
        Assert.NotNull(updated);
        Assert.Equal(FundingTransactionStatus.Failed, updated.Status);
        Assert.Equal("Card has expired", updated.FailureReason);

        _outbox.Received(1).Write(Arg.Any<CardFundingFailedDomainEvent>());
    }
}
