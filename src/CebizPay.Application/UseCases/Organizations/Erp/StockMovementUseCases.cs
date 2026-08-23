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
/// Command to receive incoming stock into inventory.
/// </summary>
public sealed record StockInCommand(
    Guid InventoryItemId,
    Guid OrganizationId,
    decimal Quantity,
    decimal UnitCost,
    string Reference,
    string? Reason = null) : IRequest<Guid>;

/// <summary>
/// Validator for StockInCommand.
/// </summary>
public sealed class StockInCommandValidator : AbstractValidator<StockInCommand>
{
    /// <summary>
    /// Initializes validation rules for StockInCommand.
    /// </summary>
    public StockInCommandValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty().WithMessage("InventoryItemId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Stock in quantity must be greater than zero.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Reference is required.").MaximumLength(100);
    }
}

/// <summary>
/// Handler for StockInCommand.
/// </summary>
public sealed class StockInCommandHandler : IRequestHandler<StockInCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="StockInCommandHandler"/>.
    /// </summary>
    public StockInCommandHandler(
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
    public async Task<Guid> Handle(StockInCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot perform stock transactions while organization status is suspended.");
        }

        var normalizedRef = request.Reference.Trim();
        var refExists = await _dbContext.StockMovements.AnyAsync(
            m => m.OrganizationId == request.OrganizationId && m.Reference == normalizedRef,
            cancellationToken);

        if (refExists)
        {
            throw new InvalidOperationException($"A stock movement with reference '{normalizedRef}' already exists in this organization.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.InventoryItemId && i.OrganizationId == request.OrganizationId && !i.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item '{request.InventoryItemId}' was not found in this organization.");

        if (item.Status != InventoryItemStatus.Active)
        {
            throw new InvalidOperationException($"Cannot receive stock for inactive or discontinued item '{item.Sku}'.");
        }

        var activePolicy = await _dbContext.InventoryValuationPolicies.FirstOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId && p.IsActive,
            cancellationToken);

        var policyMethod = activePolicy?.Method ?? ValuationMethod.Wac;
        var policyVersion = activePolicy?.Version ?? 1;

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        item.ApplyStockIn(request.Quantity, request.UnitCost);

        var movement = new StockMovement(
            request.OrganizationId,
            item.Id,
            StockMovementType.StockIn,
            request.Quantity,
            normalizedRef,
            policyMethod,
            policyVersion,
            actorUserId,
            request.UnitCost,
            Math.Round(request.Quantity * request.UnitCost, 4),
            request.Reason);

        _dbContext.StockMovements.Add(movement);

        if (policyMethod == ValuationMethod.Fifo)
        {
            var costLayer = new InventoryCostLayer(
                request.OrganizationId,
                item.Id,
                movement.Id,
                request.Quantity,
                request.UnitCost,
                now);
            _dbContext.InventoryCostLayers.Add(costLayer);
        }

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StockReceived,
            resourceType: AuditResourceTypes.StockMovement,
            resourceId: movement.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                movement.Id,
                movement.InventoryItemId,
                movement.Quantity,
                movement.UnitCost,
                movement.TotalCost,
                movement.Reference,
                movement.ValuationMethod,
                movement.ValuationPolicyVersion,
                NewQuantity = item.CurrentQuantity,
                NewAverageCost = item.CurrentAverageCost
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StockReceivedDomainEvent(
            movement.Id,
            item.Id,
            request.OrganizationId,
            request.Quantity,
            request.UnitCost,
            normalizedRef,
            policyMethod,
            policyVersion,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return movement.Id;
    }
}

/// <summary>
/// Command to issue outgoing stock from inventory (fulfillment, sales, internal usage).
/// </summary>
public sealed record StockOutCommand(
    Guid InventoryItemId,
    Guid OrganizationId,
    decimal Quantity,
    string Reference,
    string? Reason = null) : IRequest<Guid>;

/// <summary>
/// Validator for StockOutCommand.
/// </summary>
public sealed class StockOutCommandValidator : AbstractValidator<StockOutCommand>
{
    /// <summary>
    /// Initializes validation rules for StockOutCommand.
    /// </summary>
    public StockOutCommandValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty().WithMessage("InventoryItemId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Stock out quantity must be greater than zero.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Reference is required.").MaximumLength(100);
    }
}

/// <summary>
/// Handler for StockOutCommand.
/// </summary>
public sealed class StockOutCommandHandler : IRequestHandler<StockOutCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="StockOutCommandHandler"/>.
    /// </summary>
    public StockOutCommandHandler(
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
    public async Task<Guid> Handle(StockOutCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot perform stock transactions while organization status is suspended.");
        }

        var normalizedRef = request.Reference.Trim();
        var refExists = await _dbContext.StockMovements.AnyAsync(
            m => m.OrganizationId == request.OrganizationId && m.Reference == normalizedRef,
            cancellationToken);

        if (refExists)
        {
            throw new InvalidOperationException($"A stock movement with reference '{normalizedRef}' already exists in this organization.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.InventoryItemId && i.OrganizationId == request.OrganizationId && !i.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item '{request.InventoryItemId}' was not found in this organization.");

        if (item.Status != InventoryItemStatus.Active)
        {
            throw new InvalidOperationException($"Cannot issue stock for inactive or discontinued item '{item.Sku}'.");
        }

        if (item.CurrentQuantity < request.Quantity)
        {
            throw new InvalidOperationException($"Insufficient inventory available. Current: {item.CurrentQuantity}, requested: {request.Quantity}.");
        }

        var activePolicy = await _dbContext.InventoryValuationPolicies.FirstOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId && p.IsActive,
            cancellationToken);

        var policyMethod = activePolicy?.Method ?? ValuationMethod.Wac;
        var policyVersion = activePolicy?.Version ?? 1;

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        decimal? unitCost = null;
        decimal? totalCost = null;

        if (policyMethod == ValuationMethod.Fifo)
        {
            // FIFO layer consumption
            var layers = await _dbContext.InventoryCostLayers
                .Where(l => l.InventoryItemId == item.Id && l.RemainingQuantity > 0)
                .OrderBy(l => l.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var totalRemainingInLayers = layers.Sum(l => l.RemainingQuantity);
            if (totalRemainingInLayers < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient FIFO cost layers available. Available layers total: {totalRemainingInLayers}, requested: {request.Quantity}.");
            }

            decimal needed = request.Quantity;
            decimal totalFifoCost = 0;

            foreach (var layer in layers)
            {
                if (needed <= 0)
                {
                    break;
                }

                var consumed = layer.Consume(needed);
                totalFifoCost += consumed * layer.UnitCost;
                needed -= consumed;
            }

            totalCost = Math.Round(totalFifoCost, 4);
            unitCost = Math.Round(totalFifoCost / request.Quantity, 4);
        }
        else
        {
            // WAC valuation
            unitCost = item.CurrentAverageCost;
            totalCost = Math.Round(request.Quantity * item.CurrentAverageCost, 4);
        }

        item.ApplyStockOut(request.Quantity);

        var movement = new StockMovement(
            request.OrganizationId,
            item.Id,
            StockMovementType.StockOut,
            request.Quantity,
            normalizedRef,
            policyMethod,
            policyVersion,
            actorUserId,
            unitCost,
            totalCost,
            request.Reason);

        _dbContext.StockMovements.Add(movement);

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StockIssued,
            resourceType: AuditResourceTypes.StockMovement,
            resourceId: movement.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                movement.Id,
                movement.InventoryItemId,
                movement.Quantity,
                movement.UnitCost,
                movement.TotalCost,
                movement.Reference,
                movement.ValuationMethod,
                movement.ValuationPolicyVersion,
                NewQuantity = item.CurrentQuantity
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StockIssuedDomainEvent(
            movement.Id,
            item.Id,
            request.OrganizationId,
            request.Quantity,
            unitCost,
            normalizedRef,
            policyMethod,
            policyVersion,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return movement.Id;
    }
}

/// <summary>
/// Command to manually adjust inventory quantity and optional average cost.
/// </summary>
public sealed record StockAdjustmentCommand(
    Guid InventoryItemId,
    Guid OrganizationId,
    decimal QuantityDelta,
    string Reference,
    string Reason,
    decimal? NewAverageCost = null) : IRequest<Guid>;

/// <summary>
/// Validator for StockAdjustmentCommand.
/// </summary>
public sealed class StockAdjustmentCommandValidator : AbstractValidator<StockAdjustmentCommand>
{
    /// <summary>
    /// Initializes validation rules for StockAdjustmentCommand.
    /// </summary>
    public StockAdjustmentCommandValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty().WithMessage("InventoryItemId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.QuantityDelta).NotEqual(0).WithMessage("Adjustment quantity delta cannot be zero.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Reference is required.").MaximumLength(100);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Adjustment reason is mandatory.").MaximumLength(500);
    }
}

/// <summary>
/// Handler for StockAdjustmentCommand.
/// </summary>
public sealed class StockAdjustmentCommandHandler : IRequestHandler<StockAdjustmentCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="StockAdjustmentCommandHandler"/>.
    /// </summary>
    public StockAdjustmentCommandHandler(
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
    public async Task<Guid> Handle(StockAdjustmentCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot adjust inventory while organization status is suspended.");
        }

        var normalizedRef = request.Reference.Trim();
        var refExists = await _dbContext.StockMovements.AnyAsync(
            m => m.OrganizationId == request.OrganizationId && m.Reference == normalizedRef,
            cancellationToken);

        if (refExists)
        {
            throw new InvalidOperationException($"A stock movement with reference '{normalizedRef}' already exists in this organization.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.InventoryItemId && i.OrganizationId == request.OrganizationId && !i.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item '{request.InventoryItemId}' was not found in this organization.");

        if (item.CurrentQuantity + request.QuantityDelta < 0)
        {
            throw new InvalidOperationException($"Adjustment would cause negative inventory. Current: {item.CurrentQuantity}, delta: {request.QuantityDelta}.");
        }

        var activePolicy = await _dbContext.InventoryValuationPolicies.FirstOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId && p.IsActive,
            cancellationToken);

        var policyMethod = activePolicy?.Method ?? ValuationMethod.Wac;
        var policyVersion = activePolicy?.Version ?? 1;

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        item.ApplyStockAdjustment(request.QuantityDelta, request.NewAverageCost);

        var movement = new StockMovement(
            request.OrganizationId,
            item.Id,
            StockMovementType.Adjustment,
            Math.Abs(request.QuantityDelta),
            normalizedRef,
            policyMethod,
            policyVersion,
            actorUserId,
            item.CurrentAverageCost,
            Math.Round(Math.Abs(request.QuantityDelta) * item.CurrentAverageCost, 4),
            request.Reason.Trim());

        _dbContext.StockMovements.Add(movement);

        if (policyMethod == ValuationMethod.Fifo)
        {
            if (request.QuantityDelta > 0)
            {
                // Create a new layer for positive adjustment
                var unitCost = request.NewAverageCost ?? item.CurrentAverageCost;
                var layer = new InventoryCostLayer(
                    request.OrganizationId,
                    item.Id,
                    movement.Id,
                    request.QuantityDelta,
                    unitCost,
                    now);
                _dbContext.InventoryCostLayers.Add(layer);
            }
            else
            {
                // Consume oldest layers for downward adjustment
                var layers = await _dbContext.InventoryCostLayers
                    .Where(l => l.InventoryItemId == item.Id && l.RemainingQuantity > 0)
                    .OrderBy(l => l.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                decimal needed = Math.Abs(request.QuantityDelta);
                foreach (var layer in layers)
                {
                    if (needed <= 0) break;
                    var consumed = layer.Consume(needed);
                    needed -= consumed;
                }
            }
        }

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StockAdjusted,
            resourceType: AuditResourceTypes.StockMovement,
            resourceId: movement.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                movement.Id,
                movement.InventoryItemId,
                QuantityDelta = request.QuantityDelta,
                movement.Reference,
                movement.Reason,
                NewQuantity = item.CurrentQuantity,
                NewAverageCost = item.CurrentAverageCost
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StockAdjustedDomainEvent(
            movement.Id,
            item.Id,
            request.OrganizationId,
            request.QuantityDelta,
            normalizedRef,
            request.Reason.Trim(),
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return movement.Id;
    }
}

/// <summary>
/// Query to list stock movements for an inventory item.
/// </summary>
public sealed record GetStockMovementsQuery(
    Guid InventoryItemId,
    Guid OrganizationId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<StockMovementDto>>;

/// <summary>
/// Validator for GetStockMovementsQuery.
/// </summary>
public sealed class GetStockMovementsQueryValidator : AbstractValidator<GetStockMovementsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetStockMovementsQuery.
    /// </summary>
    public GetStockMovementsQueryValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty().WithMessage("InventoryItemId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetStockMovementsQuery.
/// </summary>
public sealed class GetStockMovementsQueryHandler : IRequestHandler<GetStockMovementsQuery, PagedResult<StockMovementDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetStockMovementsQueryHandler"/>.
    /// </summary>
    public GetStockMovementsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<StockMovementDto>> Handle(GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var item = await _dbContext.InventoryItems.FirstOrDefaultAsync(
            i => i.Id == request.InventoryItemId && i.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item '{request.InventoryItemId}' was not found in this organization.");

        var query = _dbContext.StockMovements
            .Where(m => m.InventoryItemId == request.InventoryItemId && m.OrganizationId == request.OrganizationId);

        var totalCount = await query.CountAsync(cancellationToken);

        var movements = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = movements.Select(m => new StockMovementDto(
            m.Id,
            m.OrganizationId,
            m.InventoryItemId,
            item.Name,
            item.Sku,
            m.MovementType,
            m.Quantity,
            m.UnitCost,
            m.TotalCost,
            m.Reference,
            m.Reason,
            m.ValuationMethod,
            m.ValuationPolicyVersion,
            m.CreatedByUserId,
            m.CreatedAtUtc)).ToList();

        return new PagedResult<StockMovementDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
