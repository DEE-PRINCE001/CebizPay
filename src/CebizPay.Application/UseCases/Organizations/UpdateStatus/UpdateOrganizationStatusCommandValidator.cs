using FluentValidation;

namespace CebizPay.Application.UseCases.Organizations.UpdateStatus;

/// <summary>
/// Validator for UpdateOrganizationStatusCommand.
/// </summary>
public sealed class UpdateOrganizationStatusCommandValidator : AbstractValidator<UpdateOrganizationStatusCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateOrganizationStatusCommand.
    /// </summary>
    public UpdateOrganizationStatusCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("OrganizationId is required.");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("Valid OrganizationStatus is required.");
    }
}
