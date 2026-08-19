using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Command to create a salary level.
/// </summary>
public sealed record CreateSalaryLevelCommand(
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency = "NGN") : IRequest<Guid>;

/// <summary>
/// Validator for CreateSalaryLevelCommand.
/// </summary>
public sealed class CreateSalaryLevelCommandValidator : AbstractValidator<CreateSalaryLevelCommand>
{
    private static readonly string[] AllowedCurrencies = ["NGN", "INT-NGN", "USDT", "USD", "GHS", "EUR", "INR"];

    /// <summary>
    /// Initializes validation rules for CreateSalaryLevelCommand.
    /// Supported V1 currencies: NGN, Int-NGN, USDT.
    /// </summary>
    public CreateSalaryLevelCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.LevelName).NotEmpty().WithMessage("LevelName is required.").MaximumLength(100);
        RuleFor(x => x.BaseAmount).GreaterThanOrEqualTo(0).WithMessage("BaseAmount cannot be negative.");
        RuleFor(x => x.Currency).Must(c => AllowedCurrencies.Contains(c.ToUpperInvariant()))
            .WithMessage("Currency must be a supported V1 currency.");
    }
}

/// <summary>
/// Handler for CreateSalaryLevelCommand.
/// </summary>
public sealed class CreateSalaryLevelCommandHandler : IRequestHandler<CreateSalaryLevelCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateSalaryLevelCommandHandler"/>.
    /// </summary>
    public CreateSalaryLevelCommandHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(CreateSalaryLevelCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot configure HRIS structure while organization status is suspended.");
        }

        var salaryLevel = new SalaryLevel(request.OrganizationId, request.LevelName, request.BaseAmount, request.Currency);
        _dbContext.SalaryLevels.Add(salaryLevel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return salaryLevel.Id;
    }
}
