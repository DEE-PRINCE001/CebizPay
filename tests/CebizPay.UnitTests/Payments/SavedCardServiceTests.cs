using CebizPay.Application.Common.Interfaces.Messaging;
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
/// Unit tests for <see cref="SavedCardService"/> token storage, user-scoping, default toggling, and revocation.
/// </summary>
public sealed class SavedCardServiceTests
{
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private SavedCardService CreateService(ApplicationDbContext dbContext)
    {
        return new SavedCardService(
            dbContext,
            _outbox,
            NullLogger<SavedCardService>.Instance);
    }

    [Fact]
    public async Task SaveCardTokenAsync_WhenFirstCard_SetsAsDefault()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var wallet = Wallet.CreateIndividualWallet("usr_sc_01", Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var result = await service.SaveCardTokenAsync(
            userId: "usr_sc_01",
            walletId: wallet.Id,
            provider: PaymentProvider.Flutterwave,
            providerToken: "flw_token_first",
            last4: "4242",
            brand: "Visa",
            expiryMonth: "12",
            expiryYear: "2028");

        Assert.NotNull(result);
        Assert.True(result.IsDefault);
        Assert.Equal("4242", result.Last4);
        Assert.Equal("Active", result.Status);

        _outbox.Received(1).Write(Arg.Any<SavedCardCreatedDomainEvent>());
    }

    [Fact]
    public async Task SaveCardTokenAsync_WhenDuplicateToken_UpdatesExistingAndReturns()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var wallet = Wallet.CreateIndividualWallet("usr_sc_02", Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var first = await service.SaveCardTokenAsync("usr_sc_02", wallet.Id, PaymentProvider.Paystack, "pstk_tok_dup", "1111", "Mastercard");
        var second = await service.SaveCardTokenAsync("usr_sc_02", wallet.Id, PaymentProvider.Paystack, "pstk_tok_dup", "1111", "Mastercard");

        Assert.Equal(first.Id, second.Id);
        var count = await db.SavedCards.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SetDefaultCardAsync_UpdatesDefaultFlagAndResetsOthers()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var wallet = Wallet.CreateIndividualWallet("usr_sc_03", Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var card1 = await service.SaveCardTokenAsync("usr_sc_03", wallet.Id, PaymentProvider.Flutterwave, "flw_tok_1", "1234", "Visa", isDefault: true);
        var card2 = await service.SaveCardTokenAsync("usr_sc_03", wallet.Id, PaymentProvider.Paystack, "pstk_tok_2", "5678", "Mastercard", isDefault: false);

        var updatedCard2 = await service.SetDefaultCardAsync(card2.Id, "usr_sc_03");

        Assert.True(updatedCard2.IsDefault);

        var refreshedCard1 = await db.SavedCards.FindAsync(card1.Id);
        Assert.False(refreshedCard1!.IsDefault);
    }

    [Fact]
    public async Task RevokeSavedCardAsync_MarksCardRevoked()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var wallet = Wallet.CreateIndividualWallet("usr_sc_04", Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var card = await service.SaveCardTokenAsync("usr_sc_04", wallet.Id, PaymentProvider.Flutterwave, "flw_tok_rev", "9999", "Visa");
        var revoked = await service.RevokeSavedCardAsync(card.Id, "usr_sc_04");

        Assert.Equal("Revoked", revoked.Status);
        _outbox.Received(1).Write(Arg.Any<SavedCardRevokedDomainEvent>());

        // Active list should not include revoked card
        var activeCards = await service.GetSavedCardsForUserAsync("usr_sc_04");
        Assert.Empty(activeCards);
    }
}
