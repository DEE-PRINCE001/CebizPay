using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Audit;

/// <summary>
/// Handles <see cref="GetAuditLogsQuery"/>.
/// Enforces AuditView permissions, tenant isolation boundaries, efficient indexed querying, and pagination.
/// </summary>
public sealed class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _currentOrgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAuditLogsQueryHandler"/>.
    /// </summary>
    public GetAuditLogsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext currentOrgContext)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _currentOrgContext = currentOrgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        // Determine if user is platform admin with AuditView permission or SuperAdmin
        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == currentUserId && a.IsActive, cancellationToken);

        var isPlatformAdmin = adminProfile != null &&
            (adminProfile.Role == AdminRoleType.SuperAdmin || adminProfile.HasPermission(Permissions.AuditView));

        Guid? effectiveOrgId = null;

        if (isPlatformAdmin)
        {
            // Platform admin can query across all tenants or filter to specific tenant if requested
            effectiveOrgId = request.OrganizationId;
        }
        else
        {
            // Organization tenant user: enforce strict tenant boundary
            if (!_currentOrgContext.CurrentOrganizationId.HasValue)
            {
                throw new UnauthorizedAccessException("User does not have permission to view audit logs.");
            }

            var currentTenantOrgId = _currentOrgContext.CurrentOrganizationId.Value;

            // Reject attempt to manipulate cross-tenant organization ID
            if (request.OrganizationId.HasValue && request.OrganizationId.Value != currentTenantOrgId)
            {
                throw new UnauthorizedAccessException("Cross-tenant audit query is forbidden.");
            }

            effectiveOrgId = currentTenantOrgId;
        }

        var query = _dbContext.AuditLogs.AsQueryable();

        // Apply tenant isolation / organization filter
        if (effectiveOrgId.HasValue)
        {
            query = query.Where(a => a.OrganizationId == effectiveOrgId.Value);
        }

        // Apply optional multi-attribute filters (all backed by database indexes)
        if (request.FromUtc.HasValue)
        {
            query = query.Where(a => a.OccurredAtUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(a => a.OccurredAtUtc <= request.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ActorId))
        {
            query = query.Where(a => a.ActorId == request.ActorId);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action == request.Action);
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            query = query.Where(a => a.ResourceType == request.ResourceType);
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
        {
            query = query.Where(a => a.ResourceId == request.ResourceId);
        }

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            query = query.Where(a => a.CorrelationId == request.CorrelationId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.ActorId,
                a.OrganizationId,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.BeforeJson,
                a.AfterJson,
                a.IpAddress,
                a.UserAgent,
                a.CorrelationId,
                a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
