using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

/// <summary>
/// Domain unit tests for <see cref="ExternalFundingAccount"/> aggregate entity.
/// Validates domain invariants, lifecycle state machines, currency restrictions, and primary status logic.
/// </summary>
public sealed class ExternalFundingAccountTests
{
    [Fact]
    public void Create_ValidParameters_ShouldInstantiateActiveAccount()
    {
        // Arrange
        var walletId = Guid.NewGuid();

        // Act
        var account = ExternalFundingAccount.Create(
            walletId: walletId,
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerCustomerReference: "MNFY_CUST_001",
            providerAccountReference: "MNFY_ACC_001",
            isPrimary: true);

        // Assert
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(walletId, account.WalletId);
        Assert.Equal(PaymentProvider.Monnify, account.Provider);
        Assert.Equal("1234567890", account.AccountNumber);
        Assert.Equal("John Doe", account.AccountName);
        Assert.Equal("035", account.BankCode);
        Assert.Equal("Wema Bank", account.BankName);
        Assert.Equal(Currency.NGN, account.Currency);
        Assert.Equal(ExternalFundingAccountStatus.Active, account.Status);
        Assert.True(account.IsPrimary);
        Assert.Equal("MNFY_CUST_001", account.ProviderCustomerReference);
        Assert.Equal("MNFY_ACC_001", account.ProviderAccountReference);
        Assert.True(account.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyAccountNumber_ShouldThrowArgumentException(string accountNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalFundingAccount.Create(
                walletId: Guid.NewGuid(),
                provider: PaymentProvider.Monnify,
                accountNumber: accountNumber,
                accountName: "John Doe",
                bankCode: "035",
                bankName: "Wema Bank",
                currency: Currency.NGN));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyAccountName_ShouldThrowArgumentException(string accountName)
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalFundingAccount.Create(
                walletId: Guid.NewGuid(),
                provider: PaymentProvider.Monnify,
                accountNumber: "1234567890",
                accountName: accountName,
                bankCode: "035",
                bankName: "Wema Bank",
                currency: Currency.NGN));
    }

    [Fact]
    public void Create_EmptyWalletId_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalFundingAccount.Create(
                walletId: Guid.Empty,
                provider: PaymentProvider.Monnify,
                accountNumber: "1234567890",
                accountName: "John Doe",
                bankCode: "035",
                bankName: "Wema Bank",
                currency: Currency.NGN));
    }

    [Fact]
    public void Create_NonTransactionalCurrency_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalFundingAccount.Create(
                walletId: Guid.NewGuid(),
                provider: PaymentProvider.Monnify,
                accountNumber: "1234567890",
                accountName: "John Doe",
                bankCode: "035",
                bankName: "Wema Bank",
                currency: Currency.USD));
    }

    [Fact]
    public void SetPrimary_OnActiveAccount_ShouldSucceed()
    {
        // Arrange
        var account = ExternalFundingAccount.Create(
            walletId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: false);

        // Act
        account.SetPrimary(true);

        // Assert
        Assert.True(account.IsPrimary);
        Assert.NotNull(account.UpdatedAtUtc);
    }

    [Fact]
    public void SetPrimary_OnSuspendedAccount_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var account = ExternalFundingAccount.Create(
            walletId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: false);
        account.MarkSuspended();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => account.SetPrimary(true));
        Assert.Contains("Only Active accounts can be primary", ex.Message);
    }

    [Fact]
    public void SetPrimary_OnClosedAccount_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var account = ExternalFundingAccount.Create(
            walletId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: false);
        account.MarkClosed();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => account.SetPrimary(true));
        Assert.Contains("Only Active accounts can be primary", ex.Message);
    }

    [Fact]
    public void ClearPrimary_ShouldUnsetPrimary()
    {
        // Arrange
        var account = ExternalFundingAccount.Create(
            walletId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        // Act
        account.ClearPrimary();

        // Assert
        Assert.False(account.IsPrimary);
        Assert.NotNull(account.UpdatedAtUtc);
    }

    [Fact]
    public void MarkSuspended_WhenPrimary_ShouldRevokePrimary()
    {
        // Arrange
        var account = ExternalFundingAccount.Create(
            walletId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        // Act
        account.MarkSuspended();

        // Assert
        Assert.Equal(ExternalFundingAccountStatus.Suspended, account.Status);
        Assert.False(account.IsPrimary);
    }

    [Fact]
    public void MarkClosed_WhenPrimary_ShouldRevokePrimary()
    {
        // Arrange
        var account = ExternalFundingAccount.Create(
            walletId: Guid.NewGuid(),
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        // Act
        account.MarkClosed();

        // Assert
        Assert.Equal(ExternalFundingAccountStatus.Closed, account.Status);
        Assert.False(account.IsPrimary);
    }
}
