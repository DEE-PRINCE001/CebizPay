using FluentValidation;

namespace CebizPay.Application.UseCases.Auth.RegisterPhone;

/// <summary>
/// Validator for RegisterPhoneCommand.
/// </summary>
public sealed class RegisterPhoneCommandValidator : AbstractValidator<RegisterPhoneCommand>
{
    /// <summary>
    /// Initializes validation rules for RegisterPhoneCommand.
    /// </summary>
    public RegisterPhoneCommandValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Invalid phone number format.");

        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage("DeviceId is required for device rate-limiting.");
    }
}
