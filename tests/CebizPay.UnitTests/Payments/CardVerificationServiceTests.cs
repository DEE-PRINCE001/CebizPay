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
/// Unit tests for <see cref="CardVerificationService"/> initialization, token extraction, and micro-charge auto-refund.
/// </summary>
public sealed class CardVerificationServiceTests
{
    private readonly ICardPaymentProvider _flwProvider = Substitute.For<ICardPaymentProvider>();
    private readonly IPaymentRoutingService _routingService = Substitute.For<IPaymentRoutingService>();
    private readonly ISavedCardService _savedCardService = Substitute.For<ISavedCardService>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    public CardVerificationServiceTests()
    {
        _flwProvider.Provider.Returns(PaymentProvider.Flutterwave);
        _routingService.ResolvePrimaryProvider(PaymentCapability.CardFunding).Returns(PaymentProvider.Flutterwave);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private CardVerificationService CreateService(ApplicationDbContext dbContext)
    {
        return new CardVerificationService(
            new[] { _flwProvider },
            _routingService,
            _savedCardService,
            dbContext,
            _outbox,
            NullLogger<CardVerificationService>.Instance);
    }

    [Fact]
    public async Task InitializeCardVerificationAsync_CreatesPendingVerificationAndReturnsAuthUrl()
    {
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_ver_01", Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        _flwProvider.VerifyCardAsync(Arg.Any<CardVerificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardVerificationResult.Success("https://checkout.flutterwave.com/v3/hosted/pay/ver123", "flw_ref_ver_01"));

        var service = CreateService(db);

        var result = await service.InitializeCardVerificationAsync(
            walletId: wallet.Id,
            userId: "usr_ver_01",
            email: "user@example.com",
            callbackUrl: "https://cebizpay.com/verify-callback");

        Assert.NotNull(result);
        Assert.Equal("https://checkout.flutterwave.com/v3/hosted/pay/ver123", result.AuthorizationUrl);
        Assert.Equal("Pending", result.Status);

        _outbox.Received(1).Write(Arg.Any<CardVerificationInitiatedDomainEvent>());
    }

    [Fact]
    public async Task CompleteCardVerificationAsync_WhenSuccessful_SavesCardAndTriggersMicroChargeRefund()
    {
        using var db = CreateDbContext();
        var wallet = Wallet.CreateIndividualWallet("usr_ver_02", Currency.NGN);
        db.Wallets.Add(wallet);

        var verification = CardVerification.Create("usr_ver_02", wallet.Id, PaymentProvider.Flutterwave, "CBZVR-REF-99", 50m, Currency.NGN);
        db.CardVerifications.Add(verification);
        await db.SaveChangesAsync();

        _flwProvider.GetCardPaymentStatusAsync("CBZVR-REF-99", Arg.Any<CancellationToken>())
            .Returns(PaymentProviderResult.Success("FLW-TX-99", "{\"flw_ref\":\"FLW-TOK-99\"}"));

        var savedCardDto = new SavedCardResponseDto(
            Id: Guid.NewGuid(),
            UserId: "usr_ver_02",
            WalletId: wallet.Id,
            Provider: "Flutterwave",
            Last4: "1234",
            Brand: "Visa",
            ExpiryMonth: "12",
            ExpiryYear: "2030",
            CardHolderName: null,
            Status: "Active",
            IsDefault: true,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

        _savedCardService.SaveCardTokenAsync(
            userId: "usr_ver_02",
            walletId: wallet.Id,
            provider: PaymentProvider.Flutterwave,
            providerToken: Arg.Any<string>(),
            last4: Arg.Any<string>(),
            brand: Arg.Any<string>(),
            expiryMonth: Arg.Any<string?>(),
            expiryYear: Arg.Any<string?>(),
            cardHolderName: Arg.Any<string?>(),
            providerCustomerReference: Arg.Any<string?>(),
            isDefault: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(savedCardDto);

        _flwProvider.RefundCardPaymentAsync(Arg.Any<CardRefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(CardRefundResult.Success("FLW-REFUND-99"));

        var service = CreateService(db);

        var result = await service.CompleteCardVerificationAsync("CBZVR-REF-99");

        Assert.NotNull(result);
        Assert.Equal("Refunded", result.Status);
        Assert.Equal(savedCardDto.Id, result.SavedCardId);

        _outbox.Received(1).Write(Arg.Any<CardVerificationCompletedDomainEvent>());
    }
}
