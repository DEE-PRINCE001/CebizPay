using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class WalletTests
{
    [Theory]
    [InlineData(Currency.NGN)]
    [InlineData(Currency.INTERNATIONAL_NGN)]
    [InlineData(Currency.USDT)]
    public void CreateIndividualWallet_TransactionalV1Currencies_ShouldSucceed(Currency currency)
    {
        // Act
        var wallet = Wallet.CreateIndividualWallet("user-123", currency);

        // Assert
        Assert.Equal("user-123", wallet.IndividualId);
        Assert.Null(wallet.OrganizationId);
        Assert.Equal(currency, wallet.Currency);
        Assert.Equal(0m, wallet.AvailableBalance);
        Assert.Equal(WalletStatus.Active, wallet.Status);
    }

    [Theory]
    [InlineData(Currency.USD)]
    [InlineData(Currency.GHS)]
    [InlineData(Currency.EUR)]
    [InlineData(Currency.INR)]
    public void CreateIndividualWallet_ReportingOnlyCurrencies_ShouldBeRejected(Currency currency)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Wallet.CreateIndividualWallet("user-123", currency));
        Assert.Contains("reporting-only currency", ex.Message);
    }

    [Fact]
    public void Credit_PositiveAmount_ShouldIncreaseBalance()
    {
        // Arrange
        var wallet = Wallet.CreateIndividualWallet("user-123", Currency.USDT);

        // Act
        wallet.Credit(500m);

        // Assert
        Assert.Equal(500m, wallet.AvailableBalance);
    }

    [Fact]
    public void Debit_ValidAmount_ShouldDecreaseBalance()
    {
        // Arrange
        var wallet = Wallet.CreateIndividualWallet("user-123", Currency.NGN);
        wallet.Credit(1000m);

        // Act
        wallet.Debit(400m);

        // Assert
        Assert.Equal(600m, wallet.AvailableBalance);
    }

    [Fact]
    public void Debit_AmountGreaterThanBalance_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var wallet = Wallet.CreateIndividualWallet("user-123", Currency.NGN);
        wallet.Credit(200m);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Debit(300m));
        Assert.Contains("Insufficient available balance", ex.Message);
        Assert.Equal(200m, wallet.AvailableBalance);
    }

    [Fact]
    public void Debit_FrozenWallet_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var wallet = Wallet.CreateIndividualWallet("user-123", Currency.NGN);
        wallet.Credit(500m);
        wallet.Freeze();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => wallet.Debit(100m));
    }
}
