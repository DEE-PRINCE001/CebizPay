using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class BankTransferFeePolicyTests
{
    [Fact]
    public void Create_FreePolicy_ShouldSucceedWithNullFeeLimits()
    {
        // Act
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Free,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        // Assert
        Assert.Equal(FeePolicyMode.Free, policy.Mode);
        Assert.Null(policy.PercentageRate);
        Assert.Null(policy.MinimumFee);
        Assert.Null(policy.MaximumFee);
        Assert.True(policy.IsEnabled);
        Assert.Equal(1, policy.Version);
        Assert.Equal("admin-123", policy.CreatedByUserId);
        Assert.Null(policy.DeactivatedAtUtc);
    }

    [Fact]
    public void Create_PercentagePolicy_ShouldSucceedWithValidInvariants()
    {
        // Act
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.015m, // 1.5%
            minimumFee: 50m,
            maximumFee: 1000m,
            version: 2,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        // Assert
        Assert.Equal(FeePolicyMode.Percentage, policy.Mode);
        Assert.Equal(0.015m, policy.PercentageRate);
        Assert.Equal(50m, policy.MinimumFee);
        Assert.Equal(1000m, policy.MaximumFee);
        Assert.True(policy.IsEnabled);
        Assert.Equal(2, policy.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Create_PercentagePolicy_InvalidPercentageRate_ShouldThrow(decimal percentageRate)
    {
        Assert.Throws<ArgumentException>(() => BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Percentage,
            percentageRate: percentageRate,
            minimumFee: 10m,
            maximumFee: 100m,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow));
    }

    [Fact]
    public void Create_PercentagePolicy_MaximumFeeLessThanMinimumFee_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.01m,
            minimumFee: 100m,
            maximumFee: 50m, // Invalid: max < min
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow));
    }

    [Fact]
    public void CalculateFee_FreePolicy_ShouldAlwaysReturnZero()
    {
        // Arrange
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Free,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        // Act & Assert
        Assert.Equal(0m, policy.CalculateFee(1000m));
        Assert.Equal(0m, policy.CalculateFee(500000m));
    }

    [Theory]
    [InlineData(1000, 50)]   // 1.5% of 1,000 = 15 -> clamped to min 50
    [InlineData(10000, 150)] // 1.5% of 10,000 = 150 -> exact
    [InlineData(100000, 1000)] // 1.5% of 100,000 = 1,500 -> clamped to max 1000
    public void CalculateFee_PercentagePolicy_ShouldClampToMinAndMax(decimal transferAmount, decimal expectedFee)
    {
        // Arrange: 1.5% fee with Min ₦50 and Max ₦1000
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.015m,
            minimumFee: 50m,
            maximumFee: 1000m,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        // Act
        var calculated = policy.CalculateFee(transferAmount);

        // Assert
        Assert.Equal(expectedFee, calculated);
    }

    [Fact]
    public void CalculateFee_Rounding_ShouldUseMidpointRoundingAwayFromZero()
    {
        // Arrange: 1.5% fee with Min 0 and Max 1000
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.015m,
            minimumFee: 0m,
            maximumFee: 1000m,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        // 100.33 * 0.015 = 1.50495 -> rounds to 1.50
        Assert.Equal(1.50m, policy.CalculateFee(100.33m));

        // 100.37 * 0.015 = 1.50555 -> rounds to 1.51
        Assert.Equal(1.51m, policy.CalculateFee(100.37m));
    }

    [Fact]
    public void CalculateFee_DeactivatedPolicy_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Free,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        policy.Deactivate();

        // Act & Assert
        Assert.False(policy.IsEnabled);
        Assert.NotNull(policy.DeactivatedAtUtc);
        Assert.Throws<InvalidOperationException>(() => policy.CalculateFee(100m));
    }

    [Theory]
    [InlineData(Currency.NGN, 100.37, 1.51)]
    [InlineData(Currency.INTERNATIONAL_NGN, 100.37, 1.51)]
    [InlineData(Currency.USDT, 100.37, 1.51)]
    public void CalculateFee_AllV1TransactionalCurrencies_ShouldUse2DecimalPlaces(Currency currency, decimal transferAmount, decimal expectedFee)
    {
        // Arrange: 1.5% fee with Min 0 and Max 1000
        var policy = BankTransferFeePolicy.Create(
            mode: FeePolicyMode.Percentage,
            percentageRate: 0.015m,
            minimumFee: 0m,
            maximumFee: 1000m,
            version: 1,
            createdByUserId: "admin-123",
            effectiveFrom: DateTime.UtcNow);

        // Act
        var calculatedFee = policy.CalculateFee(transferAmount, currency);

        // Assert
        Assert.Equal(expectedFee, calculatedFee);
        Assert.Equal(2, currency.GetDecimalPlaces());
    }
}
