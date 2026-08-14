using CebizPay.Application.UseCases.Wallet.Transfer;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class PeerTransferCommandValidatorTests
{
    private readonly PeerTransferCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPassValidation()
    {
        // Arrange
        var command = new PeerTransferCommand(
            RecipientIdentifier: "recipient@example.com",
            Amount: 5000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRecipientIdentifier_ShouldFail(string recipient)
    {
        var command = new PeerTransferCommand(
            RecipientIdentifier: recipient,
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: "key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PeerTransferCommand.RecipientIdentifier));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void Validate_NonPositiveAmount_ShouldFail(decimal amount)
    {
        var command = new PeerTransferCommand(
            RecipientIdentifier: "user@example.com",
            Amount: amount,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: "key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PeerTransferCommand.Amount));
    }

    [Theory]
    [InlineData("USD")]  // Reporting-only
    [InlineData("EUR")]  // Reporting-only
    [InlineData("INVALID")]
    public void Validate_NonTransactionalCurrency_ShouldFail(string currency)
    {
        var command = new PeerTransferCommand(
            RecipientIdentifier: "user@example.com",
            Amount: 1000m,
            Currency: currency,
            TransactionPin: "1234",
            IdempotencyKey: "key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PeerTransferCommand.Currency));
    }

    [Theory]
    [InlineData("123")]     // 3 digits
    [InlineData("12345")]   // 5 digits
    [InlineData("abcd")]    // Non-numeric
    [InlineData("")]        // Empty
    public void Validate_InvalidTransactionPin_ShouldFail(string pin)
    {
        var command = new PeerTransferCommand(
            RecipientIdentifier: "user@example.com",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: pin,
            IdempotencyKey: "key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PeerTransferCommand.TransactionPin));
    }

    [Fact]
    public void Validate_MissingIdempotencyKey_ShouldFail()
    {
        var command = new PeerTransferCommand(
            RecipientIdentifier: "user@example.com",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: "");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PeerTransferCommand.IdempotencyKey));
    }
}
