using CebizPay.Application.Common.Utils;
using FluentValidation;

namespace CebizPay.Application.UseCases.Vas.Commands.PurchaseData;

/// <summary>
/// Validator for <see cref="PurchaseDataCommand"/>.
/// Enforces valid recipient phone number, non-empty product code, positive amount, and 4-digit PIN format.
/// </summary>
public sealed class PurchaseDataCommandValidator : AbstractValidator<PurchaseDataCommand>
{
    /// <summary>
    /// Initializes validation rules for data bundle purchases.
    /// </summary>
    public PurchaseDataCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Recipient phone number is required.")
            .Must(PhoneNormalizer.IsValidNigerianPhoneNumber)
            .WithMessage("Invalid Nigerian mobile phone number format.");

        RuleFor(x => x.Network)
            .NotEmpty().WithMessage("Network operator is required.")
            .Must(n => n.Equals("MTN", StringComparison.OrdinalIgnoreCase) ||
                       n.Equals("AIRTEL", StringComparison.OrdinalIgnoreCase) ||
                       n.Equals("GLO", StringComparison.OrdinalIgnoreCase) ||
                       n.Equals("9MOBILE", StringComparison.OrdinalIgnoreCase) ||
                       n.Equals("NINEMOBILE", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Invalid network operator. Allowed values: MTN, AIRTEL, GLO, 9MOBILE.");

        RuleFor(x => x.ProductCode)
            .NotEmpty().WithMessage("Data bundle product code is required.")
            .MaximumLength(64).WithMessage("Product code cannot exceed 64 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Data bundle amount must be greater than zero.");

        RuleFor(x => x.TransactionPin)
            .NotEmpty().WithMessage("Transaction PIN is required.")
            .Matches(@"^\d{4}$").WithMessage("Transaction PIN must be exactly 4 numeric digits.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key header is required.")
            .MaximumLength(128).WithMessage("Idempotency-Key cannot exceed 128 characters.");
    }
}
