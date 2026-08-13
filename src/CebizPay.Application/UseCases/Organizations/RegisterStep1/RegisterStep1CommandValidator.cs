using FluentValidation;

namespace CebizPay.Application.UseCases.Organizations.RegisterStep1;

/// <summary>
/// Validator for RegisterStep1Command.
/// </summary>
public sealed class RegisterStep1CommandValidator : AbstractValidator<RegisterStep1Command>
{
    /// <summary>
    /// Initializes validation rules for RegisterStep1Command.
    /// </summary>
    public RegisterStep1CommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Company email is required.")
            .EmailAddress().WithMessage("Valid email format is required.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Company phone is required.");

        RuleFor(x => x.OwnerUserId)
            .NotEmpty().WithMessage("Owner User ID is required.");
    }
}
