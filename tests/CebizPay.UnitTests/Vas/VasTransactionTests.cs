using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Entities;
using CebizPay.Domain.Vas.Enums;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public class VasTransactionTests
{
    [Fact]
    public void CreateAirtime_WithValidArguments_InitializesCorrectlyInPendingStatus()
    {
        // Arrange & Act
        var txn = VasTransaction.CreateAirtime(
            reference: "CBZVAS-AIR-20260822-ABC1234567",
            userId: "usr_123",
            organizationId: null,
            walletId: Guid.NewGuid(),
            ledgerTransactionId: Guid.NewGuid(),
            phoneNumber: "08031234567",
            network: VasNetwork.Mtn,
            amount: 1000m,
            currency: Currency.NGN);

        // Assert
        Assert.Equal("CBZVAS-AIR-20260822-ABC1234567", txn.Reference);
        Assert.Equal(VasType.Airtime, txn.Type);
        Assert.Equal(VasNetwork.Mtn, txn.Network);
        Assert.Equal(1000m, txn.Amount);
        Assert.Equal(Currency.NGN, txn.Currency);
        Assert.Equal(VasTransactionStatus.Pending, txn.Status);
        Assert.Null(txn.CompletedAtUtc);
        Assert.Null(txn.ReversedAtUtc);
        Assert.Null(txn.FailureReason);
    }

    [Fact]
    public void CreateData_WithValidArguments_InitializesCorrectly()
    {
        // Arrange & Act
        var txn = VasTransaction.CreateData(
            reference: "CBZVAS-DAT-20260822-XYZ9876543",
            userId: "usr_456",
            organizationId: Guid.NewGuid(),
            walletId: Guid.NewGuid(),
            ledgerTransactionId: Guid.NewGuid(),
            phoneNumber: "08029876543",
            network: VasNetwork.Airtel,
            productCode: "AIRTEL-1GB",
            productName: "Airtel 1GB 30-Day",
            amount: 280m,
            currency: Currency.NGN);

        // Assert
        Assert.Equal("CBZVAS-DAT-20260822-XYZ9876543", txn.Reference);
        Assert.Equal(VasType.Data, txn.Type);
        Assert.Equal(VasNetwork.Airtel, txn.Network);
        Assert.Equal("AIRTEL-1GB", txn.ProductCode);
        Assert.Equal("Airtel 1GB 30-Day", txn.ProductName);
        Assert.Equal(280m, txn.Amount);
        Assert.Equal(VasTransactionStatus.Pending, txn.Status);
    }

    [Fact]
    public void MarkProcessing_WhenPending_TransitionsToProcessing()
    {
        // Arrange
        var txn = VasTransaction.CreateAirtime(
            "CBZVAS-AIR-1", "usr_1", null, Guid.NewGuid(), Guid.NewGuid(), "08031234567", VasNetwork.Mtn, 500m, Currency.NGN);

        // Act
        txn.MarkProcessing();

        // Assert
        Assert.Equal(VasTransactionStatus.Processing, txn.Status);
    }

    [Fact]
    public void MarkSucceeded_WhenProcessing_TransitionsToSucceededAndRecordsTimestamp()
    {
        // Arrange
        var txn = VasTransaction.CreateAirtime(
            "CBZVAS-AIR-2", "usr_1", null, Guid.NewGuid(), Guid.NewGuid(), "08031234567", VasNetwork.Mtn, 500m, Currency.NGN);
        txn.MarkProcessing();

        // Act
        txn.MarkSucceeded("VTU-REF-12345");

        // Assert
        Assert.Equal(VasTransactionStatus.Succeeded, txn.Status);
        Assert.Equal("VTU-REF-12345", txn.ProviderReference);
        Assert.NotNull(txn.CompletedAtUtc);
    }

    [Fact]
    public void MarkFailed_WhenProcessing_TransitionsToFailed()
    {
        // Arrange
        var txn = VasTransaction.CreateAirtime(
            "CBZVAS-AIR-3", "usr_1", null, Guid.NewGuid(), Guid.NewGuid(), "08031234567", VasNetwork.Mtn, 500m, Currency.NGN);
        txn.MarkProcessing();

        // Act
        txn.MarkFailed("INVALID_NUMBER", "Recipient phone number is inactive.");

        // Assert
        Assert.Equal(VasTransactionStatus.Failed, txn.Status);
        Assert.Equal("INVALID_NUMBER", txn.FailureCode);
        Assert.Equal("Recipient phone number is inactive.", txn.FailureReason);
    }

    [Fact]
    public void MarkUnknown_WhenProcessing_TransitionsToUnknown()
    {
        // Arrange
        var txn = VasTransaction.CreateAirtime(
            "CBZVAS-AIR-4", "usr_1", null, Guid.NewGuid(), Guid.NewGuid(), "08031234567", VasNetwork.Mtn, 500m, Currency.NGN);
        txn.MarkProcessing();

        // Act
        txn.MarkUnknown("Gateway timeout after 30 seconds.");

        // Assert
        Assert.Equal(VasTransactionStatus.Unknown, txn.Status);
        Assert.Equal("Gateway timeout after 30 seconds.", txn.FailureReason);
    }

    [Fact]
    public void MarkReversed_WhenFailedOrUnknown_TransitionsToReversed()
    {
        // Arrange
        var txn = VasTransaction.CreateAirtime(
            "CBZVAS-AIR-5", "usr_1", null, Guid.NewGuid(), Guid.NewGuid(), "08031234567", VasNetwork.Mtn, 500m, Currency.NGN);
        txn.MarkProcessing();
        txn.MarkFailed("ERR", "Failed");

        // Act
        txn.MarkReversed("Automatic reversal due to fulfillment failure.");

        // Assert
        Assert.Equal(VasTransactionStatus.Reversed, txn.Status);
        Assert.NotNull(txn.ReversedAtUtc);
        Assert.Equal("Automatic reversal due to fulfillment failure.", txn.FailureReason);
    }

    [Fact]
    public void GetMaskedPhoneNumber_MasksMiddleDigitsCorrectly()
    {
        // Arrange
        var txn = VasTransaction.CreateAirtime(
            "CBZVAS-AIR-6", "usr_1", null, Guid.NewGuid(), Guid.NewGuid(), "08031234567", VasNetwork.Mtn, 500m, Currency.NGN);

        // Act
        var masked = txn.GetMaskedPhoneNumber();

        // Assert
        Assert.Equal("0803***4567", masked);
    }
}
