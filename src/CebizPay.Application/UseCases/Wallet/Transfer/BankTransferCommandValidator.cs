using CebizPay.Domain.Finance.Enums;
using FluentValidation;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// FluentValidation validator for <see cref="BankTransferCommand"/>.
/// Validates transfer parameters before domain execution.
/// </summary>
public sealed class BankTransferCommandValidator : AbstractValidator<BankTransferCommand>
{
    /// <summary>
    /// Initializes validation rules for <see cref="BankTransferCommand"/>.
    /// </summary>
    public BankTransferCommandValidator()
    {
        RuleFor(x => x.DestinationBankCode)
            .NotEmpty().WithMessage("Destination bank code is required.")
            .Length(3, 10).WithMessage("Destination bank code must be between 3 and 10 characters.");

        RuleFor(x => x.DestinationAccountNumber)
            .NotEmpty().WithMessage("Destination account number is required.")
            .Length(10).WithMessage("Destination account number must be exactly 10 digits.")
            .Matches(@"^\d{10}$").WithMessage("Destination account number must contain only numeric digits.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transfer amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Must(BeAValidTransactionalCurrency)
            .WithMessage("Currency must be a valid transactional currency (NGN, INTERNATIONAL_NGN, or USDT). Reporting currencies (USD, GHS, EUR, INR) are not supported for bank transfers.");

        RuleFor(x => x.TransactionPin)
            .NotEmpty().WithMessage("Transaction PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("Transaction PIN must be 4 to 6 numeric digits.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required.")
            .MaximumLength(256).WithMessage("IdempotencyKey cannot exceed 256 characters.");
    }

    private static bool BeAValidTransactionalCurrency(string currencyStr)
    {
        if (!Enum.TryParse<Currency>(currencyStr, ignoreCase: true, out var currency))
            return false;

        return currency.IsTransactionalV1();
    }
}
