using CebizPay.Application.UseCases.Vas.Commands.PurchaseAirtime;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public class PurchaseAirtimeCommandValidatorTests
{
    private readonly PurchaseAirtimeCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = new PurchaseAirtimeCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            Amount: 1000m,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-1");

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidCommandWithoutNetwork_PassesValidation()
    {
        var command = new PurchaseAirtimeCommand(
            PhoneNumber: "08031234567",
            Network: null,
            Amount: 1000m,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-1");

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(49.99)]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_AmountBelowMinimum50_FailsValidation(decimal amount)
    {
        var command = new PurchaseAirtimeCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            Amount: amount,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-1");

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Amount));
    }

    [Fact]
    public void Validate_AmountAboveMaximum50000_FailsValidation()
    {
        var command = new PurchaseAirtimeCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            Amount: 50000.01m,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-1");

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Amount));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("abcd")]
    [InlineData("")]
    public void Validate_InvalidPinFormat_FailsValidation(string pin)
    {
        var command = new PurchaseAirtimeCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            Amount: 500m,
            TransactionPin: pin,
            IdempotencyKey: "test-idemp-1");

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.TransactionPin));
    }

    [Theory]
    [InlineData("INVALID_NET")]
    [InlineData("XYZ")]
    public void Validate_InvalidNetwork_FailsValidation(string network)
    {
        var command = new PurchaseAirtimeCommand(
            PhoneNumber: "08031234567",
            Network: network,
            Amount: 500m,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-1");

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Network));
    }
}
