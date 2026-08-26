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

/// <summary>Command to create a purchase order.</summary>
public sealed record CreatePurchaseOrderCommand(
    Guid OrganizationId,
    Guid SupplierId,
    DateTime OrderDate,
    DateTime? ExpectedDeliveryDate,
    Currency Currency,
    string? Notes,
    List<PurchaseOrderItemRequest> Items) : IRequest<Guid>;

/// <summary>Command to confirm a draft purchase order.</summary>
public sealed record ConfirmPurchaseOrderCommand(
    Guid OrganizationId,
    Guid PurchaseOrderId) : IRequest<Unit>;

/// <summary>Command to receive quantities for a purchase order item line.</summary>
public sealed record ReceivePurchaseOrderItemCommand(
    Guid OrganizationId,
    Guid PurchaseOrderId,
    Guid ItemId,
    decimal QuantityReceived) : IRequest<Unit>;

/// <summary>Command to cancel a purchase order.</summary>
public sealed record CancelPurchaseOrderCommand(
    Guid OrganizationId,
    Guid PurchaseOrderId) : IRequest<Unit>;

/// <summary>Query to retrieve paged purchase orders.</summary>
public sealed record GetPurchaseOrdersQuery(
    Guid OrganizationId,
    PurchaseOrderStatus? Status = null,
    Guid? SupplierId = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<PurchaseOrderDto>>;

/// <summary>Query to retrieve purchase order details by ID.</summary>
public sealed record GetPurchaseOrderByIdQuery(
    Guid OrganizationId,
    Guid PurchaseOrderId) : IRequest<PurchaseOrderDto>;

// ==========================================
// Handlers
// ==========================================

/// <summary>Handler for <see cref="CreatePurchaseOrderCommand"/>.</summary>
public sealed class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CreatePurchaseOrderCommandHandler(
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

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
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
            throw new ArgumentException("Purchase order must contain at least one line item.", nameof(request));
        }

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId && s.OrganizationId == request.OrganizationId && !s.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier '{request.SupplierId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var po = new PurchaseOrder(
            request.OrganizationId,
            orderNumber,
            supplier.Id,
            userId,
            request.OrderDate,
            request.ExpectedDeliveryDate,
            request.Currency,
            request.Notes);

        foreach (var item in request.Items)
        {
            po.AddItem(item.Description, item.Quantity, item.UnitPrice, item.InventoryItemId, item.ServiceId);
        }

        _dbContext.PurchaseOrders.Add(po);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.PurchaseOrderCreated,
            AuditResourceTypes.PurchaseOrder,
            po.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Created purchase order '{po.OrderNumber}' for supplier '{supplier.Name}' with total {po.TotalAmount} {po.Currency}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new PurchaseOrderCreatedDomainEvent(
            po.Id,
            po.OrganizationId,
            po.OrderNumber,
            po.SupplierId,
            po.TotalAmount,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return po.Id;
    }
}

/// <summary>Handler for <see cref="ConfirmPurchaseOrderCommand"/>.</summary>
public sealed class ConfirmPurchaseOrderCommandHandler : IRequestHandler<ConfirmPurchaseOrderCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public ConfirmPurchaseOrderCommandHandler(
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

    public async Task<Unit> Handle(ConfirmPurchaseOrderCommand request, CancellationToken cancellationToken)
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

        var po = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId && p.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order '{request.PurchaseOrderId}' not found.");

        po.Confirm();

        var userId = _currentUser.UserId ?? "system";
        var auditLog = AuditLog.Create(
            userId,
            AuditActions.PurchaseOrderConfirmed,
            AuditResourceTypes.PurchaseOrder,
            po.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Confirmed purchase order '{po.OrderNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new PurchaseOrderConfirmedDomainEvent(
            po.Id,
            po.OrganizationId,
            po.OrderNumber,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="ReceivePurchaseOrderItemCommand"/>.</summary>
public sealed class ReceivePurchaseOrderItemCommandHandler : IRequestHandler<ReceivePurchaseOrderItemCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public ReceivePurchaseOrderItemCommandHandler(
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

    public async Task<Unit> Handle(ReceivePurchaseOrderItemCommand request, CancellationToken cancellationToken)
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

        var po = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId && p.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order '{request.PurchaseOrderId}' not found.");

        var line = await _dbContext.PurchaseOrderItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.PurchaseOrderId == request.PurchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order item '{request.ItemId}' not found.");

        po.ReceiveItemQuantity(request.ItemId, request.QuantityReceived);

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        // If line is linked to inventory, automatically receive stock into inventory
        if (line.InventoryItemId.HasValue)
        {
            var item = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == line.InventoryItemId.Value && i.OrganizationId == request.OrganizationId, cancellationToken)
                ?? throw new KeyNotFoundException($"Inventory item '{line.InventoryItemId.Value}' not found.");

            // Resolve valuation policy
            var policy = await _dbContext.InventoryValuationPolicies
                .Where(p => p.OrganizationId == request.OrganizationId && p.IsActive)
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(cancellationToken);

            var valuationMethod = policy?.Method ?? ValuationMethod.Wac;
            var policyVersion = policy?.Version ?? 1;

            // Apply stock in
            item.ApplyStockIn(request.QuantityReceived, line.UnitPrice);

            var movement = new StockMovement(
                request.OrganizationId,
                item.Id,
                StockMovementType.StockIn,
                request.QuantityReceived,
                $"REC-{po.OrderNumber}-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
                valuationMethod,
                policyVersion,
                userId,
                unitCost: line.UnitPrice,
                totalCost: Math.Round(request.QuantityReceived * line.UnitPrice, 2),
                reason: $"Received via Purchase Order {po.OrderNumber}");

            _dbContext.StockMovements.Add(movement);

            if (valuationMethod == ValuationMethod.Fifo)
            {
                var costLayer = new InventoryCostLayer(
                    request.OrganizationId,
                    item.Id,
                    movement.Id,
                    request.QuantityReceived,
                    line.UnitPrice,
                    now);
                _dbContext.InventoryCostLayers.Add(costLayer);
            }
        }

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.PurchaseOrderReceived,
            AuditResourceTypes.PurchaseOrder,
            po.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Received {request.QuantityReceived} on line '{line.Description}' for purchase order '{po.OrderNumber}'. Status is now '{po.Status}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new PurchaseOrderReceivedDomainEvent(
            po.Id,
            po.OrganizationId,
            po.OrderNumber,
            po.Status,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="CancelPurchaseOrderCommand"/>.</summary>
public sealed class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CancelPurchaseOrderCommandHandler(
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

    public async Task<Unit> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var po = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId && p.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order '{request.PurchaseOrderId}' not found.");

        po.Cancel();

        var userId = _currentUser.UserId ?? "system";
        var auditLog = AuditLog.Create(
            userId,
            AuditActions.PurchaseOrderCancelled,
            AuditResourceTypes.PurchaseOrder,
            po.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Cancelled purchase order '{po.OrderNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new PurchaseOrderCancelledDomainEvent(
            po.Id,
            po.OrganizationId,
            po.OrderNumber,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="GetPurchaseOrdersQuery"/>.</summary>
public sealed class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetPurchaseOrdersQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PagedResult<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var query = _dbContext.PurchaseOrders
            .Where(p => p.OrganizationId == request.OrganizationId);

        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }

        if (request.SupplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == request.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(p => p.OrderNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(p => p.Id).ToList();
        var allItems = await _dbContext.PurchaseOrderItems
            .Where(i => orderIds.Contains(i.PurchaseOrderId))
            .ToListAsync(cancellationToken);

        var itemsByOrder = allItems.GroupBy(i => i.PurchaseOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = orders.Select(p => new PurchaseOrderDto(
            p.Id,
            p.OrganizationId,
            p.OrderNumber,
            p.SupplierId,
            p.OrderDate,
            p.ExpectedDeliveryDate,
            p.Status,
            p.Subtotal,
            p.VatAmount,
            p.TotalAmount,
            p.Currency,
            p.Notes,
            p.CreatedByUserId,
            p.CreatedAtUtc,
            p.UpdatedAtUtc,
            (itemsByOrder.TryGetValue(p.Id, out var lines) ? lines : new List<PurchaseOrderItem>())
                .Select(i => new PurchaseOrderItemDto(
                    i.Id,
                    i.PurchaseOrderId,
                    i.InventoryItemId,
                    i.ServiceId,
                    i.Description,
                    i.Quantity,
                    i.ReceivedQuantity,
                    i.UnitPrice,
                    i.TotalAmount)).ToList())).ToList();

        return new PagedResult<PurchaseOrderDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>Handler for <see cref="GetPurchaseOrderByIdQuery"/>.</summary>
public sealed class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var p = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(po => po.Id == request.PurchaseOrderId && po.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order '{request.PurchaseOrderId}' not found.");

        var lines = await _dbContext.PurchaseOrderItems
            .Where(i => i.PurchaseOrderId == p.Id)
            .ToListAsync(cancellationToken);

        return new PurchaseOrderDto(
            p.Id,
            p.OrganizationId,
            p.OrderNumber,
            p.SupplierId,
            p.OrderDate,
            p.ExpectedDeliveryDate,
            p.Status,
            p.Subtotal,
            p.VatAmount,
            p.TotalAmount,
            p.Currency,
            p.Notes,
            p.CreatedByUserId,
            p.CreatedAtUtc,
            p.UpdatedAtUtc,
            lines.Select(i => new PurchaseOrderItemDto(
                i.Id,
                i.PurchaseOrderId,
                i.InventoryItemId,
                i.ServiceId,
                i.Description,
                i.Quantity,
                i.ReceivedQuantity,
                i.UnitPrice,
                i.TotalAmount)).ToList());
    }
}

#pragma warning restore CA1862, CA1304, CA1311
