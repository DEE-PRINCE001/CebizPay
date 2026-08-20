using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Domain unit tests for <see cref="PaymentAttempt"/> aggregate lifecycle, state machine transitions, and invariants.
/// </summary>
public sealed class PaymentAttemptDomainTests
{
    private static readonly Guid SampleLedgerTxId = Guid.NewGuid();
    private const string SampleRequestRef = "CBZPA-ABC123XYZ";
    private const decimal SampleAmount = 5000.00m;
    private const Currency SampleCurrency = Currency.NGN;

    [Fact]
    public void Create_WithValidParameters_ShouldInitializeInCreatedStatus()
    {
        // Act
        var attempt = PaymentAttempt.Create(
            SampleLedgerTxId,
            PaymentProvider.Flutterwave,
            attemptNumber: 1,
            SampleRequestRef,
            SampleAmount,
            SampleCurrency,
            safeMetadata: "{\"source\":\"api_v1\"}");

        // Assert
        Assert.NotEqual(Guid.Empty, attempt.Id);
        Assert.Equal(SampleLedgerTxId, attempt.LedgerTransactionId);
        Assert.Equal(PaymentProvider.Flutterwave, attempt.Provider);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(PaymentAttemptStatus.Created, attempt.Status);
        Assert.Equal(SampleRequestRef, attempt.RequestReference);
        Assert.Equal(SampleAmount, attempt.Amount);
        Assert.Equal(SampleCurrency, attempt.Currency);
        Assert.Null(attempt.ProviderReference);
        Assert.Null(attempt.StartedAtUtc);
        Assert.Null(attempt.CompletedAtUtc);
        Assert.Null(attempt.FailureCode);
        Assert.Null(attempt.FailureReason);
        Assert.Equal("{\"source\":\"api_v1\"}", attempt.SafeMetadata);
        Assert.True(attempt.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Create_WithInvalidAttemptNumber_ShouldThrowArgumentException(int invalidAttemptNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            PaymentAttempt.Create(
                SampleLedgerTxId,
                PaymentProvider.Paystack,
                invalidAttemptNumber,
                SampleRequestRef,
                SampleAmount,
                SampleCurrency));
    }

    [Fact]
    public void Create_WithEmptyLedgerTransactionId_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PaymentAttempt.Create(
                Guid.Empty,
                PaymentProvider.Flutterwave,
                1,
                SampleRequestRef,
                SampleAmount,
                SampleCurrency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Create_WithNonPositiveAmount_ShouldThrowArgumentException(decimal invalidAmount)
    {
        Assert.Throws<ArgumentException>(() =>
            PaymentAttempt.Create(
                SampleLedgerTxId,
                PaymentProvider.Flutterwave,
                1,
                SampleRequestRef,
                invalidAmount,
                SampleCurrency));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidRequestReference_ShouldThrowArgumentException(string? invalidRef)
    {
        Assert.Throws<ArgumentException>(() =>
            PaymentAttempt.Create(
                SampleLedgerTxId,
                PaymentProvider.Flutterwave,
                1,
                invalidRef!,
                SampleAmount,
                SampleCurrency));
    }

    [Theory]
    [InlineData(Currency.USD)]
    [InlineData(Currency.EUR)]
    [InlineData(Currency.GHS)]
    [InlineData(Currency.INR)]
    public void Create_WithReportingOnlyCurrency_ShouldThrowArgumentException(Currency reportingCurrency)
    {
        Assert.Throws<ArgumentException>(() =>
            PaymentAttempt.Create(
                SampleLedgerTxId,
                PaymentProvider.Flutterwave,
                1,
                SampleRequestRef,
                SampleAmount,
                reportingCurrency));
    }

    [Fact]
    public void MarkProcessing_FromCreated_ShouldTransitionToProcessing()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        var startTime = DateTime.UtcNow;

        // Act
        attempt.MarkProcessing(startTime);

        // Assert
        Assert.Equal(PaymentAttemptStatus.Processing, attempt.Status);
        Assert.Equal(startTime, attempt.StartedAtUtc);
    }

    [Fact]
    public void MarkProcessing_FromInvalidStatus_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();
        attempt.MarkSucceeded("FLW-REF-001");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkProcessing());
    }

    [Fact]
    public void MarkSucceeded_FromProcessing_ShouldTransitionToSucceeded()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();

        // Act
        attempt.MarkSucceeded("FLW-SUCCESS-999", safeMetadata: "{\"channel\":\"card\"}");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("FLW-SUCCESS-999", attempt.ProviderReference);
        Assert.NotNull(attempt.CompletedAtUtc);
        Assert.Equal("{\"channel\":\"card\"}", attempt.SafeMetadata);
    }

    [Fact]
    public void MarkSucceeded_FromUnknown_ShouldTransitionToSucceeded()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Paystack, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();
        attempt.MarkUnknown("Gateway timeout");

        // Act (Reconciliation confirms success)
        attempt.MarkSucceeded("PSTK-RECON-OK-123");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("PSTK-RECON-OK-123", attempt.ProviderReference);
        Assert.NotNull(attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkSucceeded_WithEmptyProviderReference_ShouldThrowArgumentException(string? invalidRef)
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => attempt.MarkSucceeded(invalidRef!));
    }

    [Fact]
    public void MarkSucceeded_FromCreated_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkSucceeded("FLW-001"));
    }

    [Fact]
    public void MarkFailed_FromProcessing_ShouldTransitionToFailed()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();

        // Act
        attempt.MarkFailed("ERR_INSUFFICIENT_FUNDS", "Account has insufficient balance", safeMetadata: "{\"resp_code\":\"51\"}");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal("ERR_INSUFFICIENT_FUNDS", attempt.FailureCode);
        Assert.Equal("Account has insufficient balance", attempt.FailureReason);
        Assert.NotNull(attempt.CompletedAtUtc);
        Assert.Equal("{\"resp_code\":\"51\"}", attempt.SafeMetadata);
    }

    [Fact]
    public void MarkFailed_FromUnknown_ShouldTransitionToFailed()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Paystack, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();
        attempt.MarkUnknown("Timeout after 30s");

        // Act (Reconciliation confirms failure)
        attempt.MarkFailed("ERR_DECLINED", "Card was declined by bank");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal("ERR_DECLINED", attempt.FailureCode);
        Assert.Equal("Card was declined by bank", attempt.FailureReason);
        Assert.NotNull(attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkFailed_WithEmptyReason_ShouldThrowArgumentException(string? invalidReason)
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => attempt.MarkFailed("ERR_CODE", invalidReason!));
    }

    [Fact]
    public void MarkUnknown_FromProcessing_ShouldTransitionToUnknown()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();

        // Act
        attempt.MarkUnknown("Read timeout reached during gateway dispatch");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Unknown, attempt.Status);
        Assert.Equal("Read timeout reached during gateway dispatch", attempt.FailureReason);
        Assert.Null(attempt.CompletedAtUtc); // Not a terminal state yet
    }

    [Fact]
    public void MarkUnknown_FromCreated_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkUnknown("Premature timeout"));
    }

    [Fact]
    public void MarkCancelled_FromCreated_ShouldTransitionToCancelled()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);

        // Act
        attempt.MarkCancelled("Cancelled prior to gateway submission");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Cancelled, attempt.Status);
        Assert.Equal("Cancelled prior to gateway submission", attempt.FailureReason);
        Assert.NotNull(attempt.CompletedAtUtc);
    }

    [Fact]
    public void MarkCancelled_FromUnknown_ShouldTransitionToCancelled()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Paystack, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();
        attempt.MarkUnknown("Provider unreachable");

        // Act
        attempt.MarkCancelled("Abandoned after reconciliation timeout");

        // Assert
        Assert.Equal(PaymentAttemptStatus.Cancelled, attempt.Status);
        Assert.Equal("Abandoned after reconciliation timeout", attempt.FailureReason);
        Assert.NotNull(attempt.CompletedAtUtc);
    }

    [Fact]
    public void MarkCancelled_FromProcessing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkCancelled("Cannot cancel in-flight request"));
    }

    [Fact]
    public void TerminalStateInvariants_SucceededAttemptCannotTransitionFurther()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();
        attempt.MarkSucceeded("FLW-001");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkProcessing());
        Assert.Throws<InvalidOperationException>(() => attempt.MarkSucceeded("FLW-002"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkFailed("ERR", "Failed"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkUnknown("Unknown"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkCancelled("Cancelled"));
    }

    [Fact]
    public void TerminalStateInvariants_FailedAttemptCannotTransitionFurther()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkProcessing();
        attempt.MarkFailed("ERR_DECLINED", "Card declined");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkProcessing());
        Assert.Throws<InvalidOperationException>(() => attempt.MarkSucceeded("FLW-002"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkFailed("ERR", "Failed"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkUnknown("Unknown"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkCancelled("Cancelled"));
    }

    [Fact]
    public void TerminalStateInvariants_CancelledAttemptCannotTransitionFurther()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(SampleLedgerTxId, PaymentProvider.Flutterwave, 1, SampleRequestRef, SampleAmount, SampleCurrency);
        attempt.MarkCancelled("User cancelled");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => attempt.MarkProcessing());
        Assert.Throws<InvalidOperationException>(() => attempt.MarkSucceeded("FLW-002"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkFailed("ERR", "Failed"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkUnknown("Unknown"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkCancelled("Cancelled"));
    }
}
