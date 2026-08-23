using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Command to update an organization salary level.
/// </summary>
public sealed record UpdateSalaryLevelCommand(
    Guid SalaryLevelId,
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency = "NGN") : IRequest<Guid>;

/// <summary>
/// Validator for UpdateSalaryLevelCommand.
/// </summary>
public sealed class UpdateSalaryLevelCommandValidator : AbstractValidator<UpdateSalaryLevelCommand>
{
    private static readonly string[] AllowedCurrencies = ["NGN", "INT-NGN", "USDT", "USD", "GHS", "EUR", "INR"];

    /// <summary>
    /// Initializes validation rules for UpdateSalaryLevelCommand.
    /// </summary>
    public UpdateSalaryLevelCommandValidator()
    {
        RuleFor(x => x.SalaryLevelId).NotEmpty().WithMessage("SalaryLevelId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.LevelName).NotEmpty().WithMessage("LevelName is required.").MaximumLength(100);
        RuleFor(x => x.BaseAmount).GreaterThanOrEqualTo(0).WithMessage("BaseAmount cannot be negative.");
        RuleFor(x => x.Currency).Must(c => AllowedCurrencies.Contains(c.ToUpperInvariant()))
            .WithMessage("Currency must be a supported V1 currency.");
    }
}

/// <summary>
/// Handler for UpdateSalaryLevelCommand.
/// </summary>
public sealed class UpdateSalaryLevelCommandHandler : IRequestHandler<UpdateSalaryLevelCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateSalaryLevelCommandHandler"/>.
    /// </summary>
    public UpdateSalaryLevelCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(UpdateSalaryLevelCommand request, CancellationToken cancellationToken)
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

        var salaryLevel = await _dbContext.SalaryLevels.FirstOrDefaultAsync(
            s => s.Id == request.SalaryLevelId && s.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Salary level {request.SalaryLevelId} not found in organization {request.OrganizationId}.");

        var trimmedName = request.LevelName.Trim();
        var lowerName = trimmedName.ToLowerInvariant();

#pragma warning disable CA1862, CA1304, CA1311
        var duplicateExists = await _dbContext.SalaryLevels.AnyAsync(
            s => s.OrganizationId == request.OrganizationId && s.Id != request.SalaryLevelId && s.LevelName.ToLower() == lowerName,
            cancellationToken);
#pragma warning restore CA1862, CA1304, CA1311

        if (duplicateExists)
        {
            throw new InvalidOperationException($"Another salary level with name '{trimmedName}' already exists in this organization.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { salaryLevel.Id, salaryLevel.LevelName, salaryLevel.BaseAmount, salaryLevel.Currency });

        salaryLevel.Update(trimmedName, request.BaseAmount, request.Currency);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new { salaryLevel.Id, salaryLevel.LevelName, salaryLevel.BaseAmount, salaryLevel.Currency });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.SalaryLevelUpdated,
            resourceType: AuditResourceTypes.SalaryLevel,
            resourceId: salaryLevel.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new SalaryLevelUpdatedDomainEvent(salaryLevel.Id, request.OrganizationId, salaryLevel.LevelName, salaryLevel.BaseAmount, salaryLevel.Currency, DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return salaryLevel.Id;
    }
}
