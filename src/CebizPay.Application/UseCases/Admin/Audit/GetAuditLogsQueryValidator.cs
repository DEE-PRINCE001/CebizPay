using FluentValidation;

namespace CebizPay.Application.UseCases.Admin.Audit;

/// <summary>
/// Validator for <see cref="GetAuditLogsQuery"/>.
/// </summary>
public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    /// <summary>
    /// Initializes a new instance of <see cref="GetAuditLogsQueryValidator"/>.
    /// </summary>
    public GetAuditLogsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc.Value <= x.ToUtc.Value)
            .WithMessage("FromUtc date must be less than or equal to ToUtc date.");
    }
}
