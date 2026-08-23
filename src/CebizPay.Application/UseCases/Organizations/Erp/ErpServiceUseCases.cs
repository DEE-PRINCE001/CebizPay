using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

/// <summary>
/// Command to create a new ERP billable service offering.
/// </summary>
public sealed record CreateErpServiceCommand(
    Guid OrganizationId,
    string Code,
    string Name,
    decimal UnitPrice,
    string? Description = null,
    Currency Currency = Currency.NGN) : IRequest<Guid>;

/// <summary>
/// Validator for CreateErpServiceCommand.
/// </summary>
public sealed class CreateErpServiceCommandValidator : AbstractValidator<CreateErpServiceCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateErpServiceCommand.
    /// </summary>
    public CreateErpServiceCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Service code is required.").MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Service name is required.").MaximumLength(200);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
    }
}

/// <summary>
/// Handler for CreateErpServiceCommand.
/// </summary>
public sealed class CreateErpServiceCommandHandler : IRequestHandler<CreateErpServiceCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateErpServiceCommandHandler"/>.
    /// </summary>
    public CreateErpServiceCommandHandler(
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
    public async Task<Guid> Handle(CreateErpServiceCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot create services while organization status is suspended.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeExists = await _dbContext.ErpServices.AnyAsync(
            s => s.OrganizationId == request.OrganizationId && s.Code == normalizedCode && !s.IsDeleted,
            cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A service with code '{normalizedCode}' already exists in this organization.");
        }

        var service = new ErpService(
            request.OrganizationId,
            normalizedCode,
            request.Name,
            request.UnitPrice,
            request.Description,
            request.Currency);

        _dbContext.ErpServices.Add(service);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.ServiceCreated,
            resourceType: AuditResourceTypes.ErpService,
            resourceId: service.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                service.Id,
                service.Code,
                service.Name,
                service.UnitPrice,
                service.Currency
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new ErpServiceCreatedDomainEvent(
            service.Id,
            request.OrganizationId,
            service.Code,
            service.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return service.Id;
    }
}

/// <summary>
/// Command to update an existing ERP service.
/// </summary>
public sealed record UpdateErpServiceCommand(
    Guid Id,
    Guid OrganizationId,
    string Name,
    decimal UnitPrice,
    string? Description = null) : IRequest<Guid>;

/// <summary>
/// Validator for UpdateErpServiceCommand.
/// </summary>
public sealed class UpdateErpServiceCommandValidator : AbstractValidator<UpdateErpServiceCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateErpServiceCommand.
    /// </summary>
    public UpdateErpServiceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Service ID is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Service name is required.").MaximumLength(200);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
    }
}

/// <summary>
/// Handler for UpdateErpServiceCommand.
/// </summary>
public sealed class UpdateErpServiceCommandHandler : IRequestHandler<UpdateErpServiceCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateErpServiceCommandHandler"/>.
    /// </summary>
    public UpdateErpServiceCommandHandler(
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
    public async Task<Guid> Handle(UpdateErpServiceCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot update services while organization status is suspended.");
        }

        var service = await _dbContext.ErpServices.FirstOrDefaultAsync(
            s => s.Id == request.Id && s.OrganizationId == request.OrganizationId && !s.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Service '{request.Id}' was not found in this organization.");

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            service.Id,
            service.Name,
            service.Description,
            service.UnitPrice
        });

        service.Update(request.Name, request.Description, request.UnitPrice);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            service.Id,
            service.Name,
            service.Description,
            service.UnitPrice
        });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.ServiceUpdated,
            resourceType: AuditResourceTypes.ErpService,
            resourceId: service.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new ErpServiceUpdatedDomainEvent(
            service.Id,
            request.OrganizationId,
            service.Code,
            service.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return service.Id;
    }
}

/// <summary>
/// Command to soft-delete / deactivate an ERP service.
/// </summary>
public sealed record DeleteErpServiceCommand(Guid Id, Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Handler for DeleteErpServiceCommand.
/// </summary>
public sealed class DeleteErpServiceCommandHandler : IRequestHandler<DeleteErpServiceCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteErpServiceCommandHandler"/>.
    /// </summary>
    public DeleteErpServiceCommandHandler(
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
    public async Task<bool> Handle(DeleteErpServiceCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot delete services while organization status is suspended.");
        }

        var service = await _dbContext.ErpServices.FirstOrDefaultAsync(
            s => s.Id == request.Id && s.OrganizationId == request.OrganizationId && !s.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Service '{request.Id}' was not found in this organization.");

        service.SoftDelete();

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.ServiceDeleted,
            resourceType: AuditResourceTypes.ErpService,
            resourceId: service.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                service.Id,
                service.Code,
                service.Status,
                service.IsDeleted
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new ErpServiceDeletedDomainEvent(
            service.Id,
            request.OrganizationId,
            service.Code,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}

/// <summary>
/// Query to list ERP services with search and pagination.
/// </summary>
public sealed record GetErpServicesQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    ErpServiceStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<ErpServiceDto>>;

/// <summary>
/// Validator for GetErpServicesQuery.
/// </summary>
public sealed class GetErpServicesQueryValidator : AbstractValidator<GetErpServicesQuery>
{
    /// <summary>
    /// Initializes validation rules for GetErpServicesQuery.
    /// </summary>
    public GetErpServicesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetErpServicesQuery.
/// </summary>
public sealed class GetErpServicesQueryHandler : IRequestHandler<GetErpServicesQuery, PagedResult<ErpServiceDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetErpServicesQueryHandler"/>.
    /// </summary>
    public GetErpServicesQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ErpServiceDto>> Handle(GetErpServicesQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.ErpServices.Where(s => s.OrganizationId == request.OrganizationId && !s.IsDeleted);

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(s => s.Code.ToLower().Contains(search) || s.Name.ToLower().Contains(search) || (s.Description != null && s.Description.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var services = await query
            .OrderBy(s => s.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = services.Select(s => new ErpServiceDto(
            s.Id,
            s.OrganizationId,
            s.Code,
            s.Name,
            s.Description,
            s.UnitPrice,
            s.Currency,
            s.Status,
            s.CreatedAtUtc,
            s.UpdatedAtUtc)).ToList();

        return new PagedResult<ErpServiceDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get single ERP service details.
/// </summary>
public sealed record GetErpServiceByIdQuery(Guid Id, Guid OrganizationId) : IRequest<ErpServiceDto?>;

/// <summary>
/// Handler for GetErpServiceByIdQuery.
/// </summary>
public sealed class GetErpServiceByIdQueryHandler : IRequestHandler<GetErpServiceByIdQuery, ErpServiceDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetErpServiceByIdQueryHandler"/>.
    /// </summary>
    public GetErpServiceByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<ErpServiceDto?> Handle(GetErpServiceByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var service = await _dbContext.ErpServices.FirstOrDefaultAsync(
            s => s.Id == request.Id && s.OrganizationId == request.OrganizationId && !s.IsDeleted,
            cancellationToken);

        if (service == null)
        {
            return null;
        }

        return new ErpServiceDto(
            service.Id,
            service.OrganizationId,
            service.Code,
            service.Name,
            service.Description,
            service.UnitPrice,
            service.Currency,
            service.Status,
            service.CreatedAtUtc,
            service.UpdatedAtUtc);
    }
}
