using CebizPay.Application.UseCases.Wallet.Transfer;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class BankTransferCommandValidatorTests
{
    private readonly BankTransferCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPassValidation()
    {
        var command = new BankTransferCommand(
            DestinationBankCode: "058",
            DestinationAccountNumber: "0123456789",
            Amount: 15000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("NGN")]
    [InlineData("INTERNATIONAL_NGN")]
    [InlineData("USDT")]
    [InlineData("ngn")]
    [InlineData("usdt")]
    public void Validate_AllTransactionalV1Currencies_ShouldPassValidation(string currency)
    {
        var command = new BankTransferCommand(
            DestinationBankCode: "058",
            DestinationAccountNumber: "0123456789",
            Amount: 500m,
            Currency: currency,
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("GHS")]
    [InlineData("EUR")]
    [InlineData("INR")]
    [InlineData("BTC")]
    [InlineData("INVALID")]
    public void Validate_ReportingOrInvalidCurrencies_ShouldFailValidation(string invalidCurrency)
    {
        var command = new BankTransferCommand(
            DestinationBankCode: "058",
            DestinationAccountNumber: "0123456789",
            Amount: 500m,
            Currency: invalidCurrency,
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BankTransferCommand.Currency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void Validate_NonPositiveAmount_ShouldFailValidation(decimal invalidAmount)
    {
        var command = new BankTransferCommand(
            DestinationBankCode: "058",
            DestinationAccountNumber: "0123456789",
            Amount: invalidAmount,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BankTransferCommand.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12")]
    public void Validate_InvalidDestinationBankCode_ShouldFailValidation(string invalidBankCode)
    {
        var command = new BankTransferCommand(
            DestinationBankCode: invalidBankCode,
            DestinationAccountNumber: "0123456789",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BankTransferCommand.DestinationBankCode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]    // 9 digits
    [InlineData("12345678901")]  // 11 digits
    [InlineData("012345678a")]  // non-numeric
    public void Validate_InvalidDestinationAccountNumber_ShouldFailValidation(string invalidAccountNumber)
    {
        var command = new BankTransferCommand(
            DestinationBankCode: "058",
            DestinationAccountNumber: invalidAccountNumber,
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: "1234",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BankTransferCommand.DestinationAccountNumber));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("1234567")]
    [InlineData("abcd")]
    public void Validate_InvalidPin_ShouldFailValidation(string invalidPin)
    {
        var command = new BankTransferCommand(
            DestinationBankCode: "058",
            DestinationAccountNumber: "0123456789",
            Amount: 1000m,
            Currency: "NGN",
            TransactionPin: invalidPin,
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BankTransferCommand.TransactionPin));
    }
}
