using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

/// <summary>
/// Domain unit tests for <see cref="PlatformFeePolicy"/> aggregate entity.
/// Validates mathematical precision (decimal-only arithmetic, midpoint rounding away from zero),
/// fee breakdown allocations for CustomerPays/DeductFromFunds/PlatformAbsorbs, and parameter validation.
/// </summary>
public sealed class PlatformFeePolicyTests
{
    [Fact]
    public void Create_FreePolicy_ShouldCalculateZeroFee()
    {
        // Arrange & Act
        var policy = PlatformFeePolicy.CreateFree(
            operationType: FeeOperationType.VirtualAccountFunding,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        // Assert
        Assert.Equal(FeeCalculationMethod.Free, policy.CalculationMethod);
        Assert.Equal(0m, policy.CalculateFee(50000m));

        var breakdown = policy.CalculateBreakdown(50000m);
        Assert.Equal(50000m, breakdown.Amount);
        Assert.Equal(0m, breakdown.Fee);
        Assert.Equal(50000m, breakdown.TotalCustomerCharge);
        Assert.Equal(50000m, breakdown.NetBeneficiaryCredit);
        Assert.Equal(0m, breakdown.PlatformFeeCost);
    }

    [Fact]
    public void Create_FixedFeePolicy_ShouldCalculateExactFixedFee()
    {
        // Arrange
        var policy = PlatformFeePolicy.CreateFixed(
            operationType: FeeOperationType.BankTransfer,
            fixedAmount: 35.00m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        // Act
        var fee = policy.CalculateFee(10000m);
        var breakdown = policy.CalculateBreakdown(10000m);

        // Assert
        Assert.Equal(35.00m, fee);
        Assert.Equal(10000m, breakdown.Amount);
        Assert.Equal(35.00m, breakdown.Fee);
        Assert.Equal(10035.00m, breakdown.TotalCustomerCharge);
        Assert.Equal(10000m, breakdown.NetBeneficiaryCredit);
        Assert.Equal(0m, breakdown.PlatformFeeCost);
    }

    [Fact]
    public void Create_PercentagePolicy_ShouldCalculateFeeWithMidpointRoundingAwayFromZero()
    {
        // Arrange: 1.5% fee on 7,500 NGN -> 7500 * (1.5 / 100) = 112.50 NGN
        var policy = PlatformFeePolicy.CreatePercentage(
            operationType: FeeOperationType.CardFunding,
            percentageRate: 1.5m,
            feeBearer: FeeBearer.DeductFromFunds,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        // Act
        var fee = policy.CalculateFee(7500m);
        var breakdown = policy.CalculateBreakdown(7500m);

        // Assert
        Assert.Equal(112.50m, fee);
        Assert.Equal(7500m, breakdown.Amount);
        Assert.Equal(112.50m, breakdown.Fee);
        Assert.Equal(7500m, breakdown.TotalCustomerCharge);
        Assert.Equal(7387.50m, breakdown.NetBeneficiaryCredit);
        Assert.Equal(0m, breakdown.PlatformFeeCost);
    }

    [Fact]
    public void Create_PercentageWithCap_BelowFloor_ShouldApplyMinimumFee()
    {
        // Arrange: 1.5% with min 50 NGN and max 2000 NGN.
        // 1000 NGN * (1.5 / 100) = 15 NGN -> below min -> 50 NGN
        var policy = PlatformFeePolicy.CreatePercentageWithCap(
            operationType: FeeOperationType.BankTransfer,
            percentageRate: 1.5m,
            minimumFee: 50.00m,
            maximumFee: 2000.00m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        // Act
        var fee = policy.CalculateFee(1000m);

        // Assert
        Assert.Equal(50.00m, fee);
    }

    [Fact]
    public void Create_PercentageWithCap_AboveCap_ShouldApplyMaximumFee()
    {
        // Arrange: 1.5% with min 50 NGN and max 2000 NGN.
        // 500,000 NGN * (1.5 / 100) = 7500 NGN -> above max -> 2000 NGN
        var policy = PlatformFeePolicy.CreatePercentageWithCap(
            operationType: FeeOperationType.BankTransfer,
            percentageRate: 1.5m,
            minimumFee: 50.00m,
            maximumFee: 2000.00m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        // Act
        var fee = policy.CalculateFee(500000m);

        // Assert
        Assert.Equal(2000.00m, fee);
    }

    [Fact]
    public void Create_PlatformAbsorbs_ShouldZeroCustomerDebitAndRecordAbsorbedFee()
    {
        // Arrange: Fixed 100 NGN absorbed by platform
        var policy = PlatformFeePolicy.CreateFixed(
            operationType: FeeOperationType.VirtualAccountFunding,
            fixedAmount: 100.00m,
            feeBearer: FeeBearer.PlatformAbsorbs,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        // Act
        var breakdown = policy.CalculateBreakdown(20000m);

        // Assert
        Assert.Equal(20000m, breakdown.Amount);
        Assert.Equal(100.00m, breakdown.Fee);
        Assert.Equal(20000m, breakdown.TotalCustomerCharge);
        Assert.Equal(20000m, breakdown.NetBeneficiaryCredit);
        Assert.Equal(100.00m, breakdown.PlatformFeeCost);
    }

    [Fact]
    public void Deactivate_ActivePolicy_ShouldSetIsEnabledFalseAndRecordTimestamp()
    {
        // Arrange
        var policy = PlatformFeePolicy.CreateFixed(
            operationType: FeeOperationType.PeerTransfer,
            fixedAmount: 10m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-001");

        Assert.True(policy.IsEnabled);
        Assert.Null(policy.DeactivatedAtUtc);

        // Act
        policy.Deactivate();

        // Assert
        Assert.False(policy.IsEnabled);
        Assert.NotNull(policy.DeactivatedAtUtc);
        Assert.NotNull(policy.UpdatedAtUtc);
    }

    [Fact]
    public void Create_NonTransactionalCurrency_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PlatformFeePolicy.CreateFixed(
                operationType: FeeOperationType.PeerTransfer,
                fixedAmount: 10m,
                feeBearer: FeeBearer.CustomerPays,
                currency: Currency.EUR,
                version: 1,
                createdByUserId: "admin-001"));
    }
}
