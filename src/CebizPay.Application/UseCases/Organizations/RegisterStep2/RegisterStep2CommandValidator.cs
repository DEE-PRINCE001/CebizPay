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
            .NotEmpty().WithMessage("CAC Number is required.");

        RuleFor(x => x.LogoUrl)
            .NotEmpty().WithMessage("Logo URL is required.");

        RuleFor(x => x.CacCertificateUrl)
            .NotEmpty().WithMessage("CAC Certificate URL is required.");
    }
}
