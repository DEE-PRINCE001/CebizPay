using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Domain unit tests for <see cref="FundingTransaction"/> aggregate lifecycle and invariants.
/// </summary>
public sealed class FundingTransactionDomainTests
{
    [Fact]
    public void Create_WithValidData_InitializesInPendingStatus()
    {
        var walletId = Guid.NewGuid();

        // Act
        var funding = FundingTransaction.Create(
            walletId: walletId,
            virtualAccountId: null,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: "CBZCD-123456",
            fundingChannel: FundingChannel.Card,
            amount: 25000.00m,
            currency: Currency.NGN);

        // Assert
        Assert.NotEqual(Guid.Empty, funding.Id);
        Assert.Equal(walletId, funding.WalletId);
        Assert.Null(funding.VirtualAccountId);
        Assert.Null(funding.LedgerTransactionId);
        Assert.Equal(PaymentProvider.Flutterwave, funding.Provider);
        Assert.Equal("CBZCD-123456", funding.ProviderTransactionReference);
        Assert.Equal(FundingChannel.Card, funding.FundingChannel);
        Assert.Equal(25000.00m, funding.Amount);
        Assert.Equal(Currency.NGN, funding.Currency);
        Assert.Equal(FundingTransactionStatus.Pending, funding.Status);
        Assert.Null(funding.CompletedAtUtc);
        Assert.Null(funding.FailedAtUtc);
    }

    [Fact]
    public void Create_WithNonPositiveAmount_ThrowsArgumentException()
    {
        var walletId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            FundingTransaction.Create(
                walletId: walletId,
                virtualAccountId: null,
                provider: PaymentProvider.Flutterwave,
                providerTransactionReference: "REF-123",
                fundingChannel: FundingChannel.Card,
                amount: 0,
                currency: Currency.NGN));
    }

    [Fact]
    public void MarkCompleted_SetsStatusAndLinksLedgerTxn()
    {
        var funding = FundingTransaction.Create(
            walletId: Guid.NewGuid(),
            virtualAccountId: null,
            provider: PaymentProvider.Paystack,
            providerTransactionReference: "pstk_tx_123",
            fundingChannel: FundingChannel.Card,
            amount: 1000m,
            currency: Currency.NGN);

        var ledgerTxId = Guid.NewGuid();
        funding.MarkCompleted(ledgerTxId);

        Assert.Equal(FundingTransactionStatus.Completed, funding.Status);
        Assert.Equal(ledgerTxId, funding.LedgerTransactionId);
        Assert.NotNull(funding.CompletedAtUtc);
    }

    [Fact]
    public void MarkFailed_SetsStatusAndFailureReason()
    {
        var funding = FundingTransaction.Create(
            walletId: Guid.NewGuid(),
            virtualAccountId: null,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: "flw_tx_123",
            fundingChannel: FundingChannel.Card,
            amount: 5000m,
            currency: Currency.NGN);

        funding.MarkFailed("Insufficient cardholder funds");

        Assert.Equal(FundingTransactionStatus.Failed, funding.Status);
        Assert.Equal("Insufficient cardholder funds", funding.FailureReason);
        Assert.NotNull(funding.FailedAtUtc);
    }
}
