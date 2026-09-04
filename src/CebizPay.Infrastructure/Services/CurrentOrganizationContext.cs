using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Provides tenant context and isolation checks for the current HTTP request.
/// </summary>
public sealed class CurrentOrganizationContext : ICurrentOrganizationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApplicationDbContext _dbContext;
    private Guid? _cachedOrgId;
    private bool _hasResolvedOrgId;

    /// <summary>
    /// Initializes a new instance of <see cref="CurrentOrganizationContext"/>.
    /// </summary>
    public CurrentOrganizationContext(IHttpContextAccessor httpContextAccessor, IApplicationDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public Guid? CurrentOrganizationId
    {
        get
        {
            if (_hasResolvedOrgId)
            {
                return _cachedOrgId;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                _cachedOrgId = null;
                _hasResolvedOrgId = true;
                return null;
            }

            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _cachedOrgId = null;
                _hasResolvedOrgId = true;
                return null;
            }

            // Extract candidate organization ID from header or claim
            Guid? candidateOrgId = null;
            if (httpContext.Request.Headers.TryGetValue("X-Organization-Id", out var headerVal) &&
                Guid.TryParse(headerVal.ToString(), out var headerOrgId) &&
                headerOrgId != Guid.Empty)
            {
                candidateOrgId = headerOrgId;
            }
            else
            {
                var claimVal = httpContext.User.FindFirstValue("OrganizationId");
                if (Guid.TryParse(claimVal, out var claimOrgId) && claimOrgId != Guid.Empty)
                {
                    candidateOrgId = claimOrgId;
                }
            }

            if (!candidateOrgId.HasValue)
            {
                _cachedOrgId = null;
                _hasResolvedOrgId = true;
                return null;
            }

            var targetOrgId = candidateOrgId.Value;

            // Server-side authorization verification:
            // 1. SuperAdmin can operate across any organization
            var isSuperAdmin = _dbContext.AdminProfiles
                .Any(a => a.UserId == userId && a.IsActive && a.Role == AdminRoleType.SuperAdmin);

            if (isSuperAdmin)
            {
                _cachedOrgId = targetOrgId;
                _hasResolvedOrgId = true;
                return _cachedOrgId;
            }

            // 2. Active organization membership
            var hasActiveMembership = _dbContext.OrganizationMemberships
                .Any(m => m.UserId == userId && m.OrganizationId == targetOrgId && m.Status == MembershipStatus.Active);

            if (hasActiveMembership)
            {
                _cachedOrgId = targetOrgId;
            }
            else
            {
                // Client-supplied organization is unauthorized; resolve to null (do not grant access)
                _cachedOrgId = null;
            }

            _hasResolvedOrgId = true;
            return _cachedOrgId;
        }
    }

    /// <inheritdoc/>
    public bool IsInOrganizationContext => CurrentOrganizationId.HasValue;

    /// <inheritdoc/>
    public async Task<bool> HasAccessToOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            return false;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        // Check if user is SuperAdmin
        var isSuperAdmin = await _dbContext.AdminProfiles
            .AnyAsync(a => a.UserId == userId && a.IsActive && a.Role == AdminRoleType.SuperAdmin, cancellationToken);

        if (isSuperAdmin)
        {
            return true;
        }

        // Check active organization membership
        var hasActiveMembership = await _dbContext.OrganizationMemberships
            .AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active, cancellationToken);

        return hasActiveMembership;
    }

    /// <inheritdoc/>
    public async Task<OrganizationMembership?> GetCurrentMembershipAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty) return null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true) return null;

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;

        return await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(Guid organizationId, string permission, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(permission)) return false;

        if (await IsSuperAdminAsync(cancellationToken)) return true;

        var membership = await GetCurrentMembershipAsync(organizationId, cancellationToken);
        if (membership == null) return false;

        return membership.HasPermission(permission);
    }

    /// <inheritdoc/>
    public async Task<bool> HasRoleAsync(Guid organizationId, MembershipRoleType[] allowedRoles, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || allowedRoles == null || allowedRoles.Length == 0) return false;

        if (await IsSuperAdminAsync(cancellationToken)) return true;

        var membership = await GetCurrentMembershipAsync(organizationId, cancellationToken);
        if (membership == null) return false;

        return allowedRoles.Contains(membership.Role);
    }

    /// <inheritdoc/>
    public async Task<bool> IsSuperAdminAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true) return false;

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return false;

        return await _dbContext.AdminProfiles
            .AnyAsync(a => a.UserId == userId && a.IsActive && !a.IsDeleted && a.Role == AdminRoleType.SuperAdmin, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AdminProfile?> GetCurrentAdminProfileAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true) return null;

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;

        return await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsActive && !a.IsDeleted, cancellationToken);
    }
}

