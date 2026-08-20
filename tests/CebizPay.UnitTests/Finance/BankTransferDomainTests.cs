using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class BankTransferDomainTests
{
    [Fact]
    public void CreatePending_ValidInputs_ShouldInitializeInPendingStatus()
    {
        // Arrange
        var ledgerTxId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        // Act
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: walletId,
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            amount: 50000m,
            currency: Currency.NGN,
            feeAmount: 500m,
            feePolicyId: policyId,
            feePolicyVersion: 1,
            reference: "CBZBT-TEST001");

        // Assert
        Assert.NotEqual(Guid.Empty, transfer.Id);
        Assert.Equal(ledgerTxId, transfer.LedgerTransactionId);
        Assert.Equal(walletId, transfer.SenderWalletId);
        Assert.Equal("058", transfer.DestinationBankCode);
        Assert.Equal("0123456789", transfer.DestinationAccountNumber);
        Assert.Equal("John Doe", transfer.DestinationAccountName);
        Assert.Equal(50000m, transfer.Amount);
        Assert.Equal(Currency.NGN, transfer.Currency);
        Assert.Equal(500m, transfer.FeeAmount);
        Assert.Equal(50500m, transfer.TotalDebited);
        Assert.Equal(policyId, transfer.FeePolicyId);
        Assert.Equal(1, transfer.FeePolicyVersion);
        Assert.Equal(BankTransferStatus.Pending, transfer.Status);
        Assert.Equal("CBZBT-TEST001", transfer.Reference);
        Assert.Null(transfer.CompletedAtUtc);
        Assert.Null(transfer.FailedAtUtc);
        Assert.Null(transfer.FailureReason);
        Assert.Null(transfer.ProviderReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CreatePending_InvalidAmount_ShouldThrowArgumentException(decimal invalidAmount)
    {
        Assert.Throws<ArgumentException>(() => BankTransfer.CreatePending(
            ledgerTransactionId: Guid.NewGuid(),
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            amount: invalidAmount,
            currency: Currency.NGN,
            feeAmount: 0m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CBZBT-TEST"));
    }

    [Fact]
    public void CreatePending_NegativeFee_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BankTransfer.CreatePending(
            ledgerTransactionId: Guid.NewGuid(),
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            amount: 1000m,
            currency: Currency.NGN,
            feeAmount: -50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CBZBT-TEST"));
    }

    [Fact]
    public void CreatePending_ReportingOnlyCurrency_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BankTransfer.CreatePending(
            ledgerTransactionId: Guid.NewGuid(),
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "John Doe",
            amount: 1000m,
            currency: Currency.USD, // Reporting only
            feeAmount: 0m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CBZBT-TEST"));
    }

    [Fact]
    public void StateTransitions_PendingToProcessing_ShouldSucceed()
    {
        var transfer = CreateSampleTransfer();

        transfer.MarkProcessing();

        Assert.Equal(BankTransferStatus.Processing, transfer.Status);
    }

    [Fact]
    public void StateTransitions_ProcessingToCompleted_ShouldSucceed()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkProcessing();

        transfer.MarkCompleted(providerReference: "PROV-TX-999");

        Assert.Equal(BankTransferStatus.Completed, transfer.Status);
        Assert.NotNull(transfer.CompletedAtUtc);
        Assert.Equal("PROV-TX-999", transfer.ProviderReference);
    }

    [Fact]
    public void StateTransitions_ProcessingToFailed_ShouldSucceed()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkProcessing();

        transfer.MarkFailed("Account not found at destination bank");

        Assert.Equal(BankTransferStatus.Failed, transfer.Status);
        Assert.NotNull(transfer.FailedAtUtc);
        Assert.Equal("Account not found at destination bank", transfer.FailureReason);
    }

    [Fact]
    public void StateTransitions_ProcessingToUnknown_ShouldSucceed()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkProcessing();

        transfer.MarkUnknown("Provider gateway timeout");

        Assert.Equal(BankTransferStatus.Unknown, transfer.Status);
        Assert.Equal("Provider gateway timeout", transfer.FailureReason);
    }

    [Fact]
    public void StateTransitions_UnknownToCompleted_ShouldSucceed()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkProcessing();
        transfer.MarkUnknown("Gateway timeout");

        transfer.MarkCompleted(providerReference: "RECONCILED-001");

        Assert.Equal(BankTransferStatus.Completed, transfer.Status);
        Assert.Equal("RECONCILED-001", transfer.ProviderReference);
    }

    [Fact]
    public void StateTransitions_UnknownToFailed_ShouldSucceed()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkProcessing();
        transfer.MarkUnknown("Gateway timeout");

        transfer.MarkFailed("Reconciled as rejected by destination bank");

        Assert.Equal(BankTransferStatus.Failed, transfer.Status);
        Assert.Equal("Reconciled as rejected by destination bank", transfer.FailureReason);
    }

    [Fact]
    public void StateTransitions_CompletedToAnyOtherState_ShouldThrowInvalidOperationException()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkCompleted();

        Assert.Throws<InvalidOperationException>(() => transfer.MarkProcessing());
        Assert.Throws<InvalidOperationException>(() => transfer.MarkCompleted());
        Assert.Throws<InvalidOperationException>(() => transfer.MarkFailed("reason"));
        Assert.Throws<InvalidOperationException>(() => transfer.MarkUnknown());
    }

    [Fact]
    public void StateTransitions_FailedToAnyOtherState_ShouldThrowInvalidOperationException()
    {
        var transfer = CreateSampleTransfer();
        transfer.MarkFailed("Initial failure");

        Assert.Throws<InvalidOperationException>(() => transfer.MarkProcessing());
        Assert.Throws<InvalidOperationException>(() => transfer.MarkCompleted());
        Assert.Throws<InvalidOperationException>(() => transfer.MarkFailed("second fail"));
        Assert.Throws<InvalidOperationException>(() => transfer.MarkUnknown());
    }

    [Theory]
    [InlineData("0123456789", "******6789")]
    [InlineData("1234", "****")]
    [InlineData("12345", "*2345")]
    public void GetMaskedAccountNumber_ShouldMaskCorrectly(string rawAccount, string expectedMasked)
    {
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: Guid.NewGuid(),
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: rawAccount,
            destinationAccountName: null,
            amount: 1000m,
            currency: Currency.NGN,
            feeAmount: 0m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "CBZBT-TEST");

        Assert.Equal(expectedMasked, transfer.GetMaskedAccountNumber());
    }

    private static BankTransfer CreateSampleTransfer() =>
        BankTransfer.CreatePending(
            ledgerTransactionId: Guid.NewGuid(),
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            destinationAccountName: "Sample Beneficiary",
            amount: 25000m,
            currency: Currency.NGN,
            feeAmount: 250m,
            feePolicyId: Guid.NewGuid(),
            feePolicyVersion: 1,
            reference: $"CBZBT-{Guid.NewGuid():N}"[..18]);
}
