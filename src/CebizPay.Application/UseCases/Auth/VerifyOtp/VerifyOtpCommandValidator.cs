using FluentValidation;

namespace CebizPay.Application.UseCases.Auth.VerifyOtp;

/// <summary>
/// Validator for VerifyOtpCommand enforcing mobile password rules.
/// Rule: min 7 characters, uppercase, lowercase, number, symbol.
/// </summary>
public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    /// <summary>
    /// Initializes validation rules for VerifyOtpCommand.
    /// </summary>
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("OTP Code is required.")
            .Length(6).WithMessage("OTP Code must be 6 digits.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Valid email format required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(7).WithMessage("Mobile password must be at least 7 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
            .Matches(@"[\W_]").WithMessage("Password must contain at least one symbol.");
    }
}
