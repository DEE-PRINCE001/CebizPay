using FluentValidation;

namespace CebizPay.Application.UseCases.Auth.RefreshToken;

/// <summary>
/// Validation rules for RefreshTokenCommand.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Initializes a new instance of <see cref="RefreshTokenCommandValidator"/>.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("RefreshToken is required.");
    }
}
