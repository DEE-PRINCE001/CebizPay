using FluentValidation;

namespace CebizPay.Application.UseCases.Organizations.RegisterStep2;

/// <summary>
/// Validator for RegisterStep2Command.
/// </summary>
public sealed class RegisterStep2CommandValidator : AbstractValidator<RegisterStep2Command>
{
    /// <summary>
    /// Initializes validation rules for RegisterStep2Command.
    /// </summary>
    public RegisterStep2CommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("OrganizationId is required.");

        RuleFor(x => x.CacNumber)
            .MaximumLength(32)
            .When(x => !string.IsNullOrWhiteSpace(x.CacNumber))
            .WithMessage("CAC Number cannot exceed 32 characters.");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(2048)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl))
            .WithMessage("Logo URL cannot exceed 2048 characters.");

        RuleFor(x => x.CacCertificateUrl)
            .MaximumLength(2048)
            .When(x => !string.IsNullOrWhiteSpace(x.CacCertificateUrl))
            .WithMessage("CAC Certificate URL cannot exceed 2048 characters.");
    }
}
