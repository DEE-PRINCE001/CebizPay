using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

/// <summary>
/// Command to create a new organization supplier.
/// </summary>
public sealed record CreateSupplierCommand(
    Guid OrganizationId,
    string Reference,
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? TaxIdentifier = null) : IRequest<Guid>;

/// <summary>
/// Validator for CreateSupplierCommand.
/// </summary>
public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateSupplierCommand.
    /// </summary>
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Supplier reference is required.").MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Supplier name is required.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Invalid email address.");
    }
}

/// <summary>
/// Handler for CreateSupplierCommand.
/// </summary>
public sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateSupplierCommandHandler"/>.
    /// </summary>
    public CreateSupplierCommandHandler(
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
    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot create suppliers while organization status is suspended.");
        }

        var normalizedRef = request.Reference.Trim().ToUpperInvariant();
        var refExists = await _dbContext.Suppliers.AnyAsync(
            s => s.OrganizationId == request.OrganizationId && s.Reference == normalizedRef && !s.IsDeleted,
            cancellationToken);

        if (refExists)
        {
            throw new InvalidOperationException($"A supplier with reference '{normalizedRef}' already exists in this organization.");
        }

        var supplier = new Supplier(
            request.OrganizationId,
            normalizedRef,
            request.Name,
            request.Email,
            request.Phone,
            request.Address,
            request.TaxIdentifier);

        _dbContext.Suppliers.Add(supplier);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.SupplierCreated,
            resourceType: AuditResourceTypes.Supplier,
            resourceId: supplier.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                supplier.Id,
                supplier.Reference,
                supplier.Name,
                supplier.Email,
                supplier.Phone,
                supplier.Address,
                supplier.TaxIdentifier
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new SupplierCreatedDomainEvent(
            supplier.Id,
            request.OrganizationId,
            supplier.Reference,
            supplier.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}

/// <summary>
/// Command to update an existing supplier.
/// </summary>
public sealed record UpdateSupplierCommand(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? TaxIdentifier = null) : IRequest<Guid>;

/// <summary>
/// Validator for UpdateSupplierCommand.
/// </summary>
public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateSupplierCommand.
    /// </summary>
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Supplier ID is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Supplier name is required.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Invalid email address.");
    }
}

/// <summary>
/// Handler for UpdateSupplierCommand.
/// </summary>
public sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateSupplierCommandHandler"/>.
    /// </summary>
    public UpdateSupplierCommandHandler(
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
    public async Task<Guid> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot update suppliers while organization status is suspended.");
        }

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(
            s => s.Id == request.Id && s.OrganizationId == request.OrganizationId && !s.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier '{request.Id}' was not found in this organization.");

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            supplier.Id,
            supplier.Name,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.TaxIdentifier
        });

        supplier.Update(
            request.Name,
            request.Email,
            request.Phone,
            request.Address,
            request.TaxIdentifier);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            supplier.Id,
            supplier.Name,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.TaxIdentifier
        });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.SupplierUpdated,
            resourceType: AuditResourceTypes.Supplier,
            resourceId: supplier.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new SupplierUpdatedDomainEvent(
            supplier.Id,
            request.OrganizationId,
            supplier.Reference,
            supplier.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}

/// <summary>
/// Command to soft-delete / deactivate a supplier.
/// </summary>
public sealed record DeleteSupplierCommand(Guid Id, Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Handler for DeleteSupplierCommand.
/// </summary>
public sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteSupplierCommandHandler"/>.
    /// </summary>
    public DeleteSupplierCommandHandler(
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
    public async Task<bool> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot delete suppliers while organization status is suspended.");
        }

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(
            s => s.Id == request.Id && s.OrganizationId == request.OrganizationId && !s.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier '{request.Id}' was not found in this organization.");

        supplier.SoftDelete();

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.SupplierDeleted,
            resourceType: AuditResourceTypes.Supplier,
            resourceId: supplier.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                supplier.Id,
                supplier.Reference,
                supplier.Status,
                supplier.IsDeleted
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new SupplierDeletedDomainEvent(
            supplier.Id,
            request.OrganizationId,
            supplier.Reference,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}

/// <summary>
/// Query to list suppliers with search and pagination.
/// </summary>
public sealed record GetSuppliersQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    SupplierStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<SupplierDto>>;

/// <summary>
/// Validator for GetSuppliersQuery.
/// </summary>
public sealed class GetSuppliersQueryValidator : AbstractValidator<GetSuppliersQuery>
{
    /// <summary>
    /// Initializes validation rules for GetSuppliersQuery.
    /// </summary>
    public GetSuppliersQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetSuppliersQuery.
/// </summary>
public sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSuppliersQueryHandler"/>.
    /// </summary>
    public GetSuppliersQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.Suppliers.Where(s => s.OrganizationId == request.OrganizationId && !s.IsDeleted);

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(s => s.Reference.ToLower().Contains(search) || s.Name.ToLower().Contains(search) || (s.Email != null && s.Email.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = suppliers.Select(s => new SupplierDto(
            s.Id,
            s.OrganizationId,
            s.Reference,
            s.Name,
            s.Email,
            s.Phone,
            s.Address,
            s.TaxIdentifier,
            s.Status,
            s.CreatedAtUtc,
            s.UpdatedAtUtc)).ToList();

        return new PagedResult<SupplierDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get single supplier details.
/// </summary>
public sealed record GetSupplierByIdQuery(Guid Id, Guid OrganizationId) : IRequest<SupplierDto?>;

/// <summary>
/// Handler for GetSupplierByIdQuery.
/// </summary>
public sealed class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSupplierByIdQueryHandler"/>.
    /// </summary>
    public GetSupplierByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(
            s => s.Id == request.Id && s.OrganizationId == request.OrganizationId && !s.IsDeleted,
            cancellationToken);

        if (supplier == null)
        {
            return null;
        }

        return new SupplierDto(
            supplier.Id,
            supplier.OrganizationId,
            supplier.Reference,
            supplier.Name,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.TaxIdentifier,
            supplier.Status,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc);
    }
}
