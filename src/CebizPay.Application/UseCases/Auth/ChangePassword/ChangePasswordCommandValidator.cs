using FluentValidation;

namespace CebizPay.Application.UseCases.Auth.ChangePassword;

/// <summary>
/// Validator for ChangePasswordCommand enforcing web and mobile password policies.
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>
    /// Initializes validation rules for ChangePasswordCommand.
    /// </summary>
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Must((cmd, newPwd) =>
            {
                if (cmd.IsMobile)
                {
                    // Mobile: min 7 chars, upper, lower, digit, symbol
                    return newPwd.Length >= 7 &&
                           System.Text.RegularExpressions.Regex.IsMatch(newPwd, @"[A-Z]") &&
                           System.Text.RegularExpressions.Regex.IsMatch(newPwd, @"[a-z]") &&
                           System.Text.RegularExpressions.Regex.IsMatch(newPwd, @"[0-9]") &&
                           System.Text.RegularExpressions.Regex.IsMatch(newPwd, @"[\W_]");
                }

                // Web/Admin/Org: minimum 8 characters
                return newPwd.Length >= 8;
            })
            .WithMessage("New password does not satisfy security requirements for the platform.");
    }
}
