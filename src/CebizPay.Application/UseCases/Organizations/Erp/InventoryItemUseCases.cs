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
/// Command to create a new inventory item.
/// </summary>
public sealed record CreateInventoryItemCommand(
    Guid OrganizationId,
    string Sku,
    string Name,
    string UnitOfMeasure,
    decimal SellingPrice,
    string? Description = null,
    string? Category = null,
    decimal ReorderLevel = 0,
    Currency Currency = Currency.NGN,
    decimal InitialQuantity = 0,
    decimal InitialUnitCost = 0) : IRequest<Guid>;

/// <summary>
/// Validator for CreateInventoryItemCommand.
/// </summary>
public sealed class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateInventoryItemCommand.
    /// </summary>
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required.").MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Item name is required.").MaximumLength(200);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().WithMessage("Unit of measure is required.").MaximumLength(50);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0).WithMessage("Selling price cannot be negative.");
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).WithMessage("Reorder level cannot be negative.");
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0).WithMessage("Initial quantity cannot be negative.");
        RuleFor(x => x.InitialUnitCost).GreaterThanOrEqualTo(0).WithMessage("Initial unit cost cannot be negative.");
    }
}

/// <summary>
/// Handler for CreateInventoryItemCommand.
/// </summary>
public sealed class CreateInventoryItemCommandHandler : IRequestHandler<CreateInventoryItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateInventoryItemCommandHandler"/>.
    /// </summary>
    public CreateInventoryItemCommandHandler(
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
    public async Task<Guid> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot create inventory items while organization status is suspended.");
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        var skuExists = await _dbContext.InventoryItems.AnyAsync(
            i => i.OrganizationId == request.OrganizationId && i.Sku == normalizedSku && !i.IsDeleted,
            cancellationToken);

        if (skuExists)
        {
            throw new InvalidOperationException($"An inventory item with SKU '{normalizedSku}' already exists in this organization.");
        }

        var item = new InventoryItem(
            request.OrganizationId,
            normalizedSku,
            request.Name,
            request.UnitOfMeasure,
            request.SellingPrice,
            request.Description,
            request.Category,
            request.ReorderLevel,
            request.Currency,
            request.InitialQuantity,
            request.InitialUnitCost);

        _dbContext.InventoryItems.Add(item);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        // If initial stock is provided, record initial stock movement and FIFO cost layer
        if (request.InitialQuantity > 0)
        {
            var activePolicy = await _dbContext.InventoryValuationPolicies.FirstOrDefaultAsync(
                p => p.OrganizationId == request.OrganizationId && p.IsActive,
                cancellationToken);

            var policyMethod = activePolicy?.Method ?? ValuationMethod.Wac;
            var policyVersion = activePolicy?.Version ?? 1;

            var movement = new StockMovement(
                request.OrganizationId,
                item.Id,
                StockMovementType.StockIn,
                request.InitialQuantity,
                $"INIT-{Guid.NewGuid():N}"[..20],
                policyMethod,
                policyVersion,
                actorUserId,
                request.InitialUnitCost,
                Math.Round(request.InitialQuantity * request.InitialUnitCost, 4),
                "Initial inventory opening balance");

            _dbContext.StockMovements.Add(movement);

            if (policyMethod == ValuationMethod.Fifo)
            {
                var costLayer = new InventoryCostLayer(
                    request.OrganizationId,
                    item.Id,
                    movement.Id,
                    request.InitialQuantity,
                    request.InitialUnitCost,
                    now);
                _dbContext.InventoryCostLayers.Add(costLayer);
            }
        }

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.InventoryItemCreated,
            resourceType: AuditResourceTypes.InventoryItem,
            resourceId: item.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                item.Id,
                item.Sku,
                item.Name,
                item.UnitOfMeasure,
                item.SellingPrice,
                item.CurrentQuantity,
                item.CurrentAverageCost
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new InventoryItemCreatedDomainEvent(
            item.Id,
            request.OrganizationId,
            item.Sku,
            item.Name,
            actorUserId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}

/// <summary>
/// Command to update inventory item metadata.
/// </summary>
public sealed record UpdateInventoryItemCommand(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string UnitOfMeasure,
    decimal SellingPrice,
    string? Description = null,
    string? Category = null,
    decimal ReorderLevel = 0) : IRequest<Guid>;

/// <summary>
/// Validator for UpdateInventoryItemCommand.
/// </summary>
public sealed class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateInventoryItemCommand.
    /// </summary>
    public UpdateInventoryItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Item ID is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Item name is required.").MaximumLength(200);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().WithMessage("Unit of measure is required.").MaximumLength(50);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0).WithMessage("Selling price cannot be negative.");
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).WithMessage("Reorder level cannot be negative.");
    }
}

/// <summary>
/// Handler for UpdateInventoryItemCommand.
/// </summary>
public sealed class UpdateInventoryItemCommandHandler : IRequestHandler<UpdateInventoryItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateInventoryItemCommandHandler"/>.
    /// </summary>
    public UpdateInventoryItemCommandHandler(
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
    public async Task<Guid> Handle(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot update inventory items while organization status is suspended.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.Id && i.OrganizationId == request.OrganizationId && !i.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item '{request.Id}' was not found in this organization.");

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            item.Id,
            item.Name,
            item.Description,
            item.Category,
            item.UnitOfMeasure,
            item.ReorderLevel,
            item.SellingPrice
        });

        item.Update(
            request.Name,
            request.Description,
            request.Category,
            request.UnitOfMeasure,
            request.ReorderLevel,
            request.SellingPrice);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            item.Id,
            item.Name,
            item.Description,
            item.Category,
            item.UnitOfMeasure,
            item.ReorderLevel,
            item.SellingPrice
        });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.InventoryItemUpdated,
            resourceType: AuditResourceTypes.InventoryItem,
            resourceId: item.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new InventoryItemUpdatedDomainEvent(
            item.Id,
            request.OrganizationId,
            item.Sku,
            item.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}

/// <summary>
/// Command to soft-delete / deactivate an inventory item.
/// </summary>
public sealed record DeleteInventoryItemCommand(Guid Id, Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Handler for DeleteInventoryItemCommand.
/// </summary>
public sealed class DeleteInventoryItemCommandHandler : IRequestHandler<DeleteInventoryItemCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteInventoryItemCommandHandler"/>.
    /// </summary>
    public DeleteInventoryItemCommandHandler(
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
    public async Task<bool> Handle(DeleteInventoryItemCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot delete inventory items while organization status is suspended.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.Id && i.OrganizationId == request.OrganizationId && !i.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item '{request.Id}' was not found in this organization.");

        item.SoftDelete();

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.InventoryItemDeactivated,
            resourceType: AuditResourceTypes.InventoryItem,
            resourceId: item.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                item.Id,
                item.Sku,
                item.Status,
                item.IsDeleted
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new InventoryItemDeactivatedDomainEvent(
            item.Id,
            request.OrganizationId,
            item.Sku,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}

/// <summary>
/// Query to list inventory items with search, filter, and pagination.
/// </summary>
public sealed record GetInventoryItemsQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    string? Category = null,
    StockStatus? StockStatus = null,
    InventoryItemStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<InventoryItemDto>>;

/// <summary>
/// Validator for GetInventoryItemsQuery.
/// </summary>
public sealed class GetInventoryItemsQueryValidator : AbstractValidator<GetInventoryItemsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetInventoryItemsQuery.
    /// </summary>
    public GetInventoryItemsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetInventoryItemsQuery.
/// </summary>
public sealed class GetInventoryItemsQueryHandler : IRequestHandler<GetInventoryItemsQuery, PagedResult<InventoryItemDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetInventoryItemsQueryHandler"/>.
    /// </summary>
    public GetInventoryItemsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<InventoryItemDto>> Handle(GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.InventoryItems.Where(i => i.OrganizationId == request.OrganizationId && !i.IsDeleted);

        if (request.Status.HasValue)
        {
            query = query.Where(i => i.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var cat = request.Category.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(i => i.Category != null && i.Category.ToLower() == cat);
#pragma warning restore CA1862, CA1304, CA1311
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(i => i.Sku.ToLower().Contains(search) || i.Name.ToLower().Contains(search) || (i.Description != null && i.Description.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        if (request.StockStatus.HasValue)
        {
            query = request.StockStatus.Value switch
            {
                StockStatus.OutOfStock => query.Where(i => i.CurrentQuantity <= 0),
                StockStatus.LowStock => query.Where(i => i.CurrentQuantity > 0 && i.CurrentQuantity <= i.ReorderLevel),
                StockStatus.InStock => query.Where(i => i.CurrentQuantity > i.ReorderLevel),
                _ => query
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(i => i.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(i => new InventoryItemDto(
            i.Id,
            i.OrganizationId,
            i.Sku,
            i.Name,
            i.Description,
            i.Category,
            i.UnitOfMeasure,
            i.Currency,
            i.CurrentQuantity,
            i.ReorderLevel,
            i.CurrentAverageCost,
            i.SellingPrice,
            i.GetTotalWacValuation(),
            i.GetStockStatus(),
            i.Status,
            i.CreatedAtUtc,
            i.UpdatedAtUtc)).ToList();

        return new PagedResult<InventoryItemDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get single inventory item details.
/// </summary>
public sealed record GetInventoryItemByIdQuery(Guid Id, Guid OrganizationId) : IRequest<InventoryItemDto?>;

/// <summary>
/// Handler for GetInventoryItemByIdQuery.
/// </summary>
public sealed class GetInventoryItemByIdQueryHandler : IRequestHandler<GetInventoryItemByIdQuery, InventoryItemDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetInventoryItemByIdQueryHandler"/>.
    /// </summary>
    public GetInventoryItemByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<InventoryItemDto?> Handle(GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.Id && i.OrganizationId == request.OrganizationId && !i.IsDeleted,
            cancellationToken);

        if (item == null)
        {
            return null;
        }

        return new InventoryItemDto(
            item.Id,
            item.OrganizationId,
            item.Sku,
            item.Name,
            item.Description,
            item.Category,
            item.UnitOfMeasure,
            item.Currency,
            item.CurrentQuantity,
            item.ReorderLevel,
            item.CurrentAverageCost,
            item.SellingPrice,
            item.GetTotalWacValuation(),
            item.GetStockStatus(),
            item.Status,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }
}
