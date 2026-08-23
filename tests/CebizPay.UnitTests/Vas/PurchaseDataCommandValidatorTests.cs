using CebizPay.Application.UseCases.Vas.Commands.PurchaseData;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public class PurchaseDataCommandValidatorTests
{
    private readonly PurchaseDataCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = new PurchaseDataCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            ProductCode: "MTN-1GB",
            Amount: 280m,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-2");

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_NonPositiveAmount_FailsValidation(decimal amount)
    {
        var command = new PurchaseDataCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            ProductCode: "MTN-1GB",
            Amount: amount,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-2");

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingProductCode_FailsValidation(string productCode)
    {
        var command = new PurchaseDataCommand(
            PhoneNumber: "08031234567",
            Network: "MTN",
            ProductCode: productCode,
            Amount: 280m,
            TransactionPin: "1234",
            IdempotencyKey: "test-idemp-2");

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ProductCode));
    }
}
