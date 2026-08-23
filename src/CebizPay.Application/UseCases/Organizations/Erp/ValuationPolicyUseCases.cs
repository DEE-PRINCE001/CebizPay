using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

/// <summary>
/// Query to get the active inventory valuation policy for an organization.
/// </summary>
public sealed record GetValuationPolicyQuery(Guid OrganizationId) : IRequest<InventoryValuationPolicyDto>;

/// <summary>
/// Handler for GetValuationPolicyQuery.
/// </summary>
public sealed class GetValuationPolicyQueryHandler : IRequestHandler<GetValuationPolicyQuery, InventoryValuationPolicyDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetValuationPolicyQueryHandler"/>.
    /// </summary>
    public GetValuationPolicyQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<InventoryValuationPolicyDto> Handle(GetValuationPolicyQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var activePolicy = await _dbContext.InventoryValuationPolicies.FirstOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId && p.IsActive,
            cancellationToken);

        if (activePolicy == null)
        {
            // Seed initial default WAC policy (Version 1)
            var actorUserId = _currentUserService.UserId ?? "SYSTEM";
            activePolicy = InventoryValuationPolicy.CreateInitialDefault(request.OrganizationId, actorUserId, DateTime.UtcNow);
            _dbContext.InventoryValuationPolicies.Add(activePolicy);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new InventoryValuationPolicyDto(
            activePolicy.Id,
            activePolicy.OrganizationId,
            activePolicy.Method,
            activePolicy.Version,
            activePolicy.EffectiveFromUtc,
            activePolicy.DeactivatedAtUtc,
            activePolicy.IsActive,
            activePolicy.CreatedByUserId,
            activePolicy.CreatedAtUtc);
    }
}

/// <summary>
/// Command to change or activate an inventory valuation policy method (WAC / FIFO) for an organization.
/// </summary>
public sealed record SetValuationPolicyCommand(
    Guid OrganizationId,
    ValuationMethod Method) : IRequest<InventoryValuationPolicyDto>;

/// <summary>
/// Validator for SetValuationPolicyCommand.
/// </summary>
public sealed class SetValuationPolicyCommandValidator : AbstractValidator<SetValuationPolicyCommand>
{
    /// <summary>
    /// Initializes validation rules for SetValuationPolicyCommand.
    /// </summary>
    public SetValuationPolicyCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for SetValuationPolicyCommand.
/// </summary>
public sealed class SetValuationPolicyCommandHandler : IRequestHandler<SetValuationPolicyCommand, InventoryValuationPolicyDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="SetValuationPolicyCommandHandler"/>.
    /// </summary>
    public SetValuationPolicyCommandHandler(
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
    public async Task<InventoryValuationPolicyDto> Handle(SetValuationPolicyCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot configure valuation policies while organization status is suspended.");
        }

        var existingPolicies = await _dbContext.InventoryValuationPolicies
            .Where(p => p.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);

        var currentActive = existingPolicies.FirstOrDefault(p => p.IsActive);
        if (currentActive != null && currentActive.Method == request.Method)
        {
            return new InventoryValuationPolicyDto(
                currentActive.Id,
                currentActive.OrganizationId,
                currentActive.Method,
                currentActive.Version,
                currentActive.EffectiveFromUtc,
                currentActive.DeactivatedAtUtc,
                currentActive.IsActive,
                currentActive.CreatedByUserId,
                currentActive.CreatedAtUtc);
        }

        var now = DateTime.UtcNow;
        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var nextVersion = existingPolicies.Count > 0 ? existingPolicies.Max(p => p.Version) + 1 : 1;

        if (currentActive != null)
        {
            currentActive.Deactivate(now);
        }

        var newPolicy = InventoryValuationPolicy.CreateNextVersion(
            request.OrganizationId,
            request.Method,
            nextVersion,
            actorUserId,
            now);

        _dbContext.InventoryValuationPolicies.Add(newPolicy);

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.InventoryValuationPolicyChanged,
            resourceType: AuditResourceTypes.InventoryValuationPolicy,
            resourceId: newPolicy.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                newPolicy.Id,
                newPolicy.Method,
                newPolicy.Version,
                newPolicy.EffectiveFromUtc,
                newPolicy.IsActive
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new InventoryValuationPolicyChangedDomainEvent(
            newPolicy.Id,
            request.OrganizationId,
            newPolicy.Method,
            newPolicy.Version,
            actorUserId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InventoryValuationPolicyDto(
            newPolicy.Id,
            newPolicy.OrganizationId,
            newPolicy.Method,
            newPolicy.Version,
            newPolicy.EffectiveFromUtc,
            newPolicy.DeactivatedAtUtc,
            newPolicy.IsActive,
            newPolicy.CreatedByUserId,
            newPolicy.CreatedAtUtc);
    }
}
