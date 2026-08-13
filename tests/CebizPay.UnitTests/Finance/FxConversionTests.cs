using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class FxConversionTests
{
    [Fact]
    public void CreateFxConversion_DifferentCurrenciesAndPositiveAmounts_ShouldSucceed()
    {
        // Arrange
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var fx = new FxConversion(
            txnId,
            Currency.NGN,
            Currency.USDT,
            sourceAmount: 1500000m,
            targetAmount: 1000m,
            rate: 0.00066667m,
            rateProvider: "CebizPayInternalFX",
            rateTimestamp: now);

        // Assert
        Assert.Equal(txnId, fx.LedgerTransactionId);
        Assert.Equal(Currency.NGN, fx.SourceCurrency);
        Assert.Equal(Currency.USDT, fx.TargetCurrency);
        Assert.Equal(1500000m, fx.SourceAmount);
        Assert.Equal(1000m, fx.TargetAmount);
        Assert.Equal(0.00066667m, fx.Rate);
    }

    [Fact]
    public void CreateFxConversion_SameSourceAndTargetCurrency_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FxConversion(
            Guid.NewGuid(),
            Currency.NGN,
            Currency.NGN,
            100m,
            100m,
            1m,
            "Internal",
            DateTime.UtcNow));
    }
}
