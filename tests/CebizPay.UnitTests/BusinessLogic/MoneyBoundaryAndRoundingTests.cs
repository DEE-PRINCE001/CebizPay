using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.BusinessLogic;

public sealed class MoneyBoundaryAndRoundingTests
{
    [Fact]
    public void Wallet_Credit_ZeroAmount_ThrowsArgumentException()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        Assert.Throws<ArgumentException>(() => wallet.Credit(0m));
    }

    [Fact]
    public void Wallet_Credit_NegativeAmount_ThrowsArgumentException()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        Assert.Throws<ArgumentException>(() => wallet.Credit(-100m));
    }

    [Fact]
    public void Wallet_Debit_ZeroAmount_ThrowsArgumentException()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        wallet.Credit(500m);
        Assert.Throws<ArgumentException>(() => wallet.Debit(0m));
    }

    [Fact]
    public void Wallet_Debit_NegativeAmount_ThrowsArgumentException()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        wallet.Credit(500m);
        Assert.Throws<ArgumentException>(() => wallet.Debit(-50m));
    }

    [Fact]
    public void Wallet_Debit_ExceedingAvailableBalance_ThrowsInvalidOperationException()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        wallet.Credit(100.00m);

        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Debit(100.01m));
        Assert.Contains("Insufficient available balance", ex.Message);
        Assert.Equal(100.00m, wallet.AvailableBalance);
    }

    [Fact]
    public void Wallet_Debit_ExactBalance_LeavesZeroBalance()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        wallet.Credit(250.75m);
        wallet.Debit(250.75m);

        Assert.Equal(0.00m, wallet.AvailableBalance);
    }

    [Fact]
    public void Wallet_Operations_OnFrozenWallet_ThrowsInvalidOperationException()
    {
        var wallet = Wallet.CreateIndividualWallet("user-1", Currency.NGN);
        wallet.Credit(500m);
        wallet.Freeze();

        Assert.Throws<InvalidOperationException>(() => wallet.Credit(100m));
        Assert.Throws<InvalidOperationException>(() => wallet.Debit(50m));

        wallet.Unfreeze();
        wallet.Debit(50m);
        Assert.Equal(450m, wallet.AvailableBalance);
    }

    [Fact]
    public void PlatformFeePolicy_Free_ReturnsZeroFee()
    {
        var policy = PlatformFeePolicy.CreateFree(
            FeeOperationType.PeerTransfer,
            FeeBearer.CustomerPays,
            Currency.NGN,
            1,
            "admin-1");

        var fee = policy.CalculateFee(50000m);
        Assert.Equal(0.00m, fee);
    }

    [Fact]
    public void PlatformFeePolicy_Fixed_ReturnsExactConfiguredFee()
    {
        var policy = PlatformFeePolicy.CreateFixed(
            FeeOperationType.BankTransfer,
            53.75m,
            FeeBearer.CustomerPays,
            Currency.NGN,
            1,
            "admin-1");

        var fee = policy.CalculateFee(1000m);
        Assert.Equal(53.75m, fee);
    }

    [Fact]
    public void PlatformFeePolicy_Percentage_RoundsAwayFromZero()
    {
        // 1.5% of 105.50 = 1.5825 -> rounded to 1.58
        // 1.5% of 115.00 = 1.725 -> rounded to 1.73 (AwayFromZero)
        var policy = PlatformFeePolicy.CreatePercentage(
            FeeOperationType.CardFunding,
            1.5m,
            FeeBearer.CustomerPays,
            Currency.NGN,
            1,
            "admin-1");

        Assert.Equal(1.58m, policy.CalculateFee(105.50m));
        Assert.Equal(1.73m, policy.CalculateFee(115.00m));
    }

    [Fact]
    public void PlatformFeePolicy_PercentageWithCap_ClampsToFloorAndCeiling()
    {
        var policy = PlatformFeePolicy.CreatePercentageWithCap(
            FeeOperationType.PeerTransfer,
            1.5m,
            minimumFee: 20.00m,
            maximumFee: 500.00m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-1");

        // 1.5% of 500 = 7.50 -> clamped to min 20.00
        Assert.Equal(20.00m, policy.CalculateFee(500m));

        // 1.5% of 10,000 = 150.00 -> within bounds
        Assert.Equal(150.00m, policy.CalculateFee(10000m));

        // 1.5% of 100,000 = 1,500.00 -> clamped to max 500.00
        Assert.Equal(500.00m, policy.CalculateFee(100000m));
    }

    [Fact]
    public void PlatformFeePolicy_FeeBearerBreakdown_PreservesMathematicalInvariance()
    {
        var policy = PlatformFeePolicy.CreateFixed(
            FeeOperationType.PeerTransfer,
            50.00m,
            FeeBearer.DeductFromFunds,
            Currency.NGN,
            1,
            "admin-1");

        var breakdown = policy.CalculateBreakdown(1000m);
        Assert.Equal(1000m, breakdown.Amount);
        Assert.Equal(50m, breakdown.Fee);
        Assert.Equal(1000m, breakdown.TotalCustomerCharge);
        Assert.Equal(950m, breakdown.NetBeneficiaryCredit);
        Assert.Equal(0m, breakdown.PlatformFeeCost);
        Assert.Equal(breakdown.TotalCustomerCharge, breakdown.NetBeneficiaryCredit + breakdown.Fee);
    }

    [Fact]
    public void ErpInvoice_MultipleLineItems_SubtotalAndVatCalculationAreDeterministic()
    {
        var invoice = new ErpInvoice(
            Guid.NewGuid(),
            "INV-2026-0001",
            Guid.NewGuid(),
            "user-creator",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            applyVat: true);

        // Line 1: 3 * 10.33 = 30.99
        invoice.AddItem("Item 1", 3m, 10.33m);
        // Line 2: 7 * 5.21 = 36.47
        invoice.AddItem("Item 2", 7m, 5.21m);

        // Expected subtotal = 30.99 + 36.47 = 67.46
        Assert.Equal(67.46m, invoice.Subtotal);

        // Statutory VAT 7.5% of 67.46 = 5.0595 -> 5.06
        Assert.Equal(5.06m, invoice.VatAmount);

        // Total = 67.46 + 5.06 = 72.52
        Assert.Equal(72.52m, invoice.TotalAmount);
    }

    [Fact]
    public void MonetaryCalculations_LargeAmounts_DoNotOverflow()
    {
        var wallet = Wallet.CreateOrganizationWallet(Guid.NewGuid(), Currency.NGN);
        wallet.Credit(100_000_000.00m);
        wallet.Credit(50_000_000.00m);

        Assert.Equal(150_000_000.00m, wallet.AvailableBalance);

        wallet.Debit(75_000_000.00m);
        Assert.Equal(75_000_000.00m, wallet.AvailableBalance);
    }
}
