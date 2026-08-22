using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Domain unit tests for <see cref="VirtualAccount"/> entity invariants and state transitions.
/// </summary>
public sealed class VirtualAccountDomainTests
{
    [Fact]
    public void CreateIndividual_WithValidData_CreatesActiveVirtualAccount()
    {
        // Act
        var va = VirtualAccount.CreateIndividual(
            individualId: "usr_123",
            provider: PaymentProvider.Flutterwave,
            accountNumber: "0123456789",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerReference: "flw_va_999");

        // Assert
        Assert.NotEqual(Guid.Empty, va.Id);
        Assert.Equal("usr_123", va.IndividualId);
        Assert.Null(va.OrganizationId);
        Assert.Equal(PaymentProvider.Flutterwave, va.Provider);
        Assert.Equal("0123456789", va.AccountNumber);
        Assert.Equal("John Doe", va.AccountName);
        Assert.Equal("035", va.BankCode);
        Assert.Equal("Wema Bank", va.BankName);
        Assert.Equal(Currency.NGN, va.Currency);
        Assert.Equal(VirtualAccountStatus.Active, va.Status);
        Assert.Equal("flw_va_999", va.ProviderReference);
    }

    [Fact]
    public void CreateOrganization_WithValidData_CreatesActiveVirtualAccount()
    {
        var orgId = Guid.NewGuid();

        // Act
        var va = VirtualAccount.CreateOrganization(
            organizationId: orgId,
            provider: PaymentProvider.Paystack,
            accountNumber: "9876543210",
            accountName: "Acme Corp",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerReference: "pstk_cust_123");

        // Assert
        Assert.NotEqual(Guid.Empty, va.Id);
        Assert.Null(va.IndividualId);
        Assert.Equal(orgId, va.OrganizationId);
        Assert.Equal(PaymentProvider.Paystack, va.Provider);
        Assert.Equal("9876543210", va.AccountNumber);
        Assert.Equal("Acme Corp", va.AccountName);
        Assert.Equal(VirtualAccountStatus.Active, va.Status);
    }

    [Fact]
    public void Create_WithNonTransactionalCurrency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            VirtualAccount.CreateIndividual(
                individualId: "usr_123",
                provider: PaymentProvider.Flutterwave,
                accountNumber: "0123456789",
                accountName: "John Doe",
                bankCode: "035",
                bankName: "Wema Bank",
                currency: Currency.USD));
    }

    [Fact]
    public void StatusTransitions_UpdateStateAndTimestamp()
    {
        var va = VirtualAccount.CreateIndividual(
            individualId: "usr_123",
            provider: PaymentProvider.Flutterwave,
            accountNumber: "0123456789",
            accountName: "John Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN);

        Assert.Equal(VirtualAccountStatus.Active, va.Status);

        va.MarkSuspended();
        Assert.Equal(VirtualAccountStatus.Suspended, va.Status);
        Assert.NotNull(va.UpdatedAtUtc);

        va.MarkActive();
        Assert.Equal(VirtualAccountStatus.Active, va.Status);

        va.MarkClosed();
        Assert.Equal(VirtualAccountStatus.Closed, va.Status);
    }
}
