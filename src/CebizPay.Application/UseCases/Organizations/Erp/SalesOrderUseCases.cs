using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.Common.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

#pragma warning disable CS1591, CA1862, CA1304, CA1311

// ==========================================
// Commands & Queries
// ==========================================

/// <summary>Command to create a sales order.</summary>
public sealed record CreateSalesOrderCommand(
    Guid OrganizationId,
    Guid CustomerId,
    DateTime OrderDate,
    DateTime? ExpectedFulfillmentDate,
    Currency Currency,
    string? Notes,
    List<SalesOrderItemRequest> Items) : IRequest<Guid>;

/// <summary>Command to confirm a draft sales order.</summary>
public sealed record ConfirmSalesOrderCommand(
    Guid OrganizationId,
    Guid SalesOrderId) : IRequest<Unit>;

/// <summary>Command to fulfill quantities on a sales order item line.</summary>
public sealed record FulfillSalesOrderItemCommand(
    Guid OrganizationId,
    Guid SalesOrderId,
    Guid ItemId,
    decimal QuantityFulfilled) : IRequest<Unit>;

/// <summary>Command to cancel a sales order.</summary>
public sealed record CancelSalesOrderCommand(
    Guid OrganizationId,
    Guid SalesOrderId) : IRequest<Unit>;

/// <summary>Query to retrieve paged sales orders.</summary>
public sealed record GetSalesOrdersQuery(
    Guid OrganizationId,
    SalesOrderStatus? Status = null,
    Guid? CustomerId = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<SalesOrderDto>>;

/// <summary>Query to retrieve sales order details by ID.</summary>
public sealed record GetSalesOrderByIdQuery(
    Guid OrganizationId,
    Guid SalesOrderId) : IRequest<SalesOrderDto>;

// ==========================================
// Handlers
// ==========================================

/// <summary>Handler for <see cref="CreateSalesOrderCommand"/>.</summary>
public sealed class CreateSalesOrderCommandHandler : IRequestHandler<CreateSalesOrderCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CreateSalesOrderCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Guid> Handle(CreateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("Sales order must contain at least one line item.", nameof(request));
        }

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.OrganizationId == request.OrganizationId && !c.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{request.CustomerId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var orderNumber = $"SO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var so = new SalesOrder(
            request.OrganizationId,
            orderNumber,
            customer.Id,
            userId,
            request.OrderDate,
            request.ExpectedFulfillmentDate,
            request.Currency,
            request.Notes);

        foreach (var item in request.Items)
        {
            so.AddItem(item.Description, item.Quantity, item.UnitPrice, item.InventoryItemId, item.ServiceId);
        }

        _dbContext.SalesOrders.Add(so);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.SalesOrderCreated,
            AuditResourceTypes.SalesOrder,
            so.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Created sales order '{so.OrderNumber}' for customer '{customer.Name}' with total {so.TotalAmount} {so.Currency}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new SalesOrderCreatedDomainEvent(
            so.Id,
            so.OrganizationId,
            so.OrderNumber,
            so.CustomerId,
            so.TotalAmount,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return so.Id;
    }
}

/// <summary>Handler for <see cref="ConfirmSalesOrderCommand"/>.</summary>
public sealed class ConfirmSalesOrderCommandHandler : IRequestHandler<ConfirmSalesOrderCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public ConfirmSalesOrderCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Unit> Handle(ConfirmSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        var so = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId && s.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sales order '{request.SalesOrderId}' not found.");

        so.Confirm();

        var userId = _currentUser.UserId ?? "system";
        var auditLog = AuditLog.Create(
            userId,
            AuditActions.SalesOrderConfirmed,
            AuditResourceTypes.SalesOrder,
            so.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Confirmed sales order '{so.OrderNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new SalesOrderConfirmedDomainEvent(
            so.Id,
            so.OrganizationId,
            so.OrderNumber,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="FulfillSalesOrderItemCommand"/>.</summary>
public sealed class FulfillSalesOrderItemCommandHandler : IRequestHandler<FulfillSalesOrderItemCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public FulfillSalesOrderItemCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Unit> Handle(FulfillSalesOrderItemCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        var so = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId && s.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sales order '{request.SalesOrderId}' not found.");

        var line = await _dbContext.SalesOrderItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.SalesOrderId == request.SalesOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sales order item '{request.ItemId}' not found.");

        so.FulfillItemQuantity(request.ItemId, request.QuantityFulfilled);

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        // If line is linked to inventory, fulfill stock from inventory
        if (line.InventoryItemId.HasValue)
        {
            var item = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == line.InventoryItemId.Value && i.OrganizationId == request.OrganizationId, cancellationToken)
                ?? throw new KeyNotFoundException($"Inventory item '{line.InventoryItemId.Value}' not found.");

            var policy = await _dbContext.InventoryValuationPolicies
                .Where(p => p.OrganizationId == request.OrganizationId && p.IsActive)
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(cancellationToken);

            var valuationMethod = policy?.Method ?? ValuationMethod.Wac;
            var policyVersion = policy?.Version ?? 1;

            decimal unitCost;
            decimal totalCost;

            if (valuationMethod == ValuationMethod.Fifo)
            {
                var activeLayers = await _dbContext.InventoryCostLayers
                    .Where(l => l.InventoryItemId == item.Id && l.RemainingQuantity > 0)
                    .OrderBy(l => l.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                var remainingToConsume = request.QuantityFulfilled;
                decimal consumedCostSum = 0m;

                foreach (var layer in activeLayers)
                {
                    if (remainingToConsume <= 0) break;
                    var consumed = layer.Consume(remainingToConsume);
                    consumedCostSum += consumed * layer.UnitCost;
                    remainingToConsume -= consumed;
                }

                if (remainingToConsume > 0)
                {
                    throw new InvalidOperationException($"Insufficient FIFO cost layers to fulfill requested quantity {request.QuantityFulfilled}.");
                }

                totalCost = Math.Round(consumedCostSum, 2);
                unitCost = Math.Round(totalCost / request.QuantityFulfilled, 4);
            }
            else
            {
                unitCost = item.CurrentAverageCost;
                totalCost = Math.Round(request.QuantityFulfilled * unitCost, 2);
            }

            // Decrement item quantity
            item.ApplyStockOut(request.QuantityFulfilled);

            var movement = new StockMovement(
                request.OrganizationId,
                item.Id,
                StockMovementType.StockOut,
                request.QuantityFulfilled,
                $"FULFILL-{so.OrderNumber}-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
                valuationMethod,
                policyVersion,
                userId,
                unitCost: unitCost,
                totalCost: totalCost,
                reason: $"Fulfilled via Sales Order {so.OrderNumber}");

            _dbContext.StockMovements.Add(movement);
        }

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.SalesOrderFulfilled,
            AuditResourceTypes.SalesOrder,
            so.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Fulfilled {request.QuantityFulfilled} on line '{line.Description}' for sales order '{so.OrderNumber}'. Status is now '{so.Status}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new SalesOrderFulfilledDomainEvent(
            so.Id,
            so.OrganizationId,
            so.OrderNumber,
            so.Status,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="CancelSalesOrderCommand"/>.</summary>
public sealed class CancelSalesOrderCommandHandler : IRequestHandler<CancelSalesOrderCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CancelSalesOrderCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Unit> Handle(CancelSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var so = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId && s.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sales order '{request.SalesOrderId}' not found.");

        so.Cancel();

        var userId = _currentUser.UserId ?? "system";
        var auditLog = AuditLog.Create(
            userId,
            AuditActions.SalesOrderCancelled,
            AuditResourceTypes.SalesOrder,
            so.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Cancelled sales order '{so.OrderNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new SalesOrderCancelledDomainEvent(
            so.Id,
            so.OrganizationId,
            so.OrderNumber,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="GetSalesOrdersQuery"/>.</summary>
public sealed class GetSalesOrdersQueryHandler : IRequestHandler<GetSalesOrdersQuery, PagedResult<SalesOrderDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetSalesOrdersQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PagedResult<SalesOrderDto>> Handle(GetSalesOrdersQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var query = _dbContext.SalesOrders
            .Where(s => s.OrganizationId == request.OrganizationId);

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(s => s.CustomerId == request.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s => s.OrderNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(s => s.Id).ToList();
        var allItems = await _dbContext.SalesOrderItems
            .Where(i => orderIds.Contains(i.SalesOrderId))
            .ToListAsync(cancellationToken);

        var itemsByOrder = allItems.GroupBy(i => i.SalesOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = orders.Select(s => new SalesOrderDto(
            s.Id,
            s.OrganizationId,
            s.OrderNumber,
            s.CustomerId,
            s.OrderDate,
            s.ExpectedFulfillmentDate,
            s.Status,
            s.Subtotal,
            s.VatAmount,
            s.TotalAmount,
            s.Currency,
            s.Notes,
            s.CreatedByUserId,
            s.CreatedAtUtc,
            s.UpdatedAtUtc,
            (itemsByOrder.TryGetValue(s.Id, out var lines) ? lines : new List<SalesOrderItem>())
                .Select(i => new SalesOrderItemDto(
                    i.Id,
                    i.SalesOrderId,
                    i.InventoryItemId,
                    i.ServiceId,
                    i.Description,
                    i.Quantity,
                    i.FulfilledQuantity,
                    i.UnitPrice,
                    i.TotalAmount)).ToList())).ToList();

        return new PagedResult<SalesOrderDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>Handler for <see cref="GetSalesOrderByIdQuery"/>.</summary>
public sealed class GetSalesOrderByIdQueryHandler : IRequestHandler<GetSalesOrderByIdQuery, SalesOrderDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetSalesOrderByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<SalesOrderDto> Handle(GetSalesOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var s = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(so => so.Id == request.SalesOrderId && so.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sales order '{request.SalesOrderId}' not found.");

        var lines = await _dbContext.SalesOrderItems
            .Where(i => i.SalesOrderId == s.Id)
            .ToListAsync(cancellationToken);

        return new SalesOrderDto(
            s.Id,
            s.OrganizationId,
            s.OrderNumber,
            s.CustomerId,
            s.OrderDate,
            s.ExpectedFulfillmentDate,
            s.Status,
            s.Subtotal,
            s.VatAmount,
            s.TotalAmount,
            s.Currency,
            s.Notes,
            s.CreatedByUserId,
            s.CreatedAtUtc,
            s.UpdatedAtUtc,
            lines.Select(i => new SalesOrderItemDto(
                i.Id,
                i.SalesOrderId,
                i.InventoryItemId,
                i.ServiceId,
                i.Description,
                i.Quantity,
                i.FulfilledQuantity,
                i.UnitPrice,
                i.TotalAmount)).ToList());
    }
}

#pragma warning restore CA1862, CA1304, CA1311
