using CebizPay.Domain.Finance.Enums;
using FluentValidation;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// FluentValidation validator for <see cref="PeerTransferCommand"/>.
/// Enforces request-level invariants before the handler executes.
/// </summary>
public sealed class PeerTransferCommandValidator : AbstractValidator<PeerTransferCommand>
{
    /// <summary>
    /// Initializes a new instance of <see cref="PeerTransferCommandValidator"/>.
    /// </summary>
    public PeerTransferCommandValidator()
    {
        RuleFor(x => x.RecipientIdentifier)
            .NotEmpty()
            .WithMessage("Recipient identifier (email or phone number) is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Transfer amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Must(BeAValidTransactionalCurrency)
            .WithMessage("Currency must be a supported V1 transactional currency: NGN, INTERNATIONAL_NGN, USDT.");

        RuleFor(x => x.TransactionPin)
            .NotEmpty()
            .WithMessage("Transaction PIN is required.")
            .Length(4)
            .WithMessage("Transaction PIN must be exactly 4 digits.")
            .Matches(@"^\d{4}$")
            .WithMessage("Transaction PIN must consist of exactly 4 numeric digits.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required.");
    }

    private static bool BeAValidTransactionalCurrency(string currency)
    {
        return Enum.TryParse<Currency>(currency, ignoreCase: true, out var parsed) && parsed.IsTransactionalV1();
    }
}
