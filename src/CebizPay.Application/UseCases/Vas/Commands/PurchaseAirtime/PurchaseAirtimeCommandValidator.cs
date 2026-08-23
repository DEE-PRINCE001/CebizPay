using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Vas.Enums;
using FluentValidation;

namespace CebizPay.Application.UseCases.Vas.Commands.PurchaseAirtime;

/// <summary>
/// Validator for <see cref="PurchaseAirtimeCommand"/>.
/// Enforces Nigerian mobile phone number validity, minimum ₦50 and maximum ₦50,000 limits, and 4-digit PIN format.
/// </summary>
public sealed class PurchaseAirtimeCommandValidator : AbstractValidator<PurchaseAirtimeCommand>
{
    /// <summary>
    /// Initializes validation rules for airtime purchases.
    /// </summary>
    public PurchaseAirtimeCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Recipient phone number is required.")
            .Must(PhoneNormalizer.IsValidNigerianPhoneNumber)
            .WithMessage("Invalid Nigerian mobile phone number format.");

        RuleFor(x => x.Network)
            .Must(net => Enum.TryParse<VasNetwork>(net!.Replace("9mobile", "NineMobile", StringComparison.OrdinalIgnoreCase), true, out _))
            .WithMessage("Network must be one of MTN, AIRTEL, GLO, or 9MOBILE.")
            .When(x => !string.IsNullOrWhiteSpace(x.Network));

        RuleFor(x => x.Amount)
            .InclusiveBetween(50m, 50000m)
            .WithMessage("Airtime purchase amount must be between ₦50 and ₦50,000.");

        RuleFor(x => x.TransactionPin)
            .NotEmpty().WithMessage("Transaction PIN is required.")
            .Matches(@"^\d{4}$").WithMessage("Transaction PIN must be exactly 4 numeric digits.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key header is required.")
            .MaximumLength(128).WithMessage("Idempotency-Key cannot exceed 128 characters.");
    }
}
