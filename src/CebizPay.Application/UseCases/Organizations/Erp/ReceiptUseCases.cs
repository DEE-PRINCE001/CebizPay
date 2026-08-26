using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

#pragma warning disable CS1591, CA1862, CA1304, CA1311

// ==========================================
// Queries
// ==========================================

/// <summary>Query to retrieve paged payment receipts.</summary>
public sealed record GetReceiptsQuery(
    Guid OrganizationId,
    Guid? CustomerId = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<ErpReceiptDto>>;

/// <summary>Query to retrieve receipt details by ID.</summary>
public sealed record GetReceiptByIdQuery(
    Guid OrganizationId,
    Guid ReceiptId) : IRequest<ErpReceiptDto>;

/// <summary>Query to retrieve receipt details by invoice ID.</summary>
public sealed record GetReceiptByInvoiceIdQuery(
    Guid OrganizationId,
    Guid InvoiceId) : IRequest<ErpReceiptDto>;

// ==========================================
// Handlers
// ==========================================

/// <summary>Handler for <see cref="GetReceiptsQuery"/>.</summary>
public sealed class GetReceiptsQueryHandler : IRequestHandler<GetReceiptsQuery, PagedResult<ErpReceiptDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetReceiptsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PagedResult<ErpReceiptDto>> Handle(GetReceiptsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var query = _dbContext.ErpReceipts
            .Where(r => r.OrganizationId == request.OrganizationId);

        if (request.CustomerId.HasValue)
        {
            query = query.Where(r => r.CustomerId == request.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(r => r.ReceiptNumber.ToLower().Contains(term) || r.Reference.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.PaymentDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ErpReceiptDto(
                r.Id,
                r.OrganizationId,
                r.ReceiptNumber,
                r.InvoiceId,
                r.CustomerId,
                r.Amount,
                r.Currency,
                r.PaymentDate,
                r.SettlementMethod,
                r.Reference,
                r.Notes,
                r.CreatedByUserId,
                r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ErpReceiptDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>Handler for <see cref="GetReceiptByIdQuery"/>.</summary>
public sealed class GetReceiptByIdQueryHandler : IRequestHandler<GetReceiptByIdQuery, ErpReceiptDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetReceiptByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<ErpReceiptDto> Handle(GetReceiptByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var r = await _dbContext.ErpReceipts
            .FirstOrDefaultAsync(r => r.Id == request.ReceiptId && r.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Receipt '{request.ReceiptId}' not found.");

        return new ErpReceiptDto(
            r.Id,
            r.OrganizationId,
            r.ReceiptNumber,
            r.InvoiceId,
            r.CustomerId,
            r.Amount,
            r.Currency,
            r.PaymentDate,
            r.SettlementMethod,
            r.Reference,
            r.Notes,
            r.CreatedByUserId,
            r.CreatedAtUtc);
    }
}

/// <summary>Handler for <see cref="GetReceiptByInvoiceIdQuery"/>.</summary>
public sealed class GetReceiptByInvoiceIdQueryHandler : IRequestHandler<GetReceiptByInvoiceIdQuery, ErpReceiptDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetReceiptByInvoiceIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<ErpReceiptDto> Handle(GetReceiptByInvoiceIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var r = await _dbContext.ErpReceipts
            .FirstOrDefaultAsync(r => r.InvoiceId == request.InvoiceId && r.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Receipt for invoice '{request.InvoiceId}' not found.");

        return new ErpReceiptDto(
            r.Id,
            r.OrganizationId,
            r.ReceiptNumber,
            r.InvoiceId,
            r.CustomerId,
            r.Amount,
            r.Currency,
            r.PaymentDate,
            r.SettlementMethod,
            r.Reference,
            r.Notes,
            r.CreatedByUserId,
            r.CreatedAtUtc);
    }
}

#pragma warning restore CA1862, CA1304, CA1311
