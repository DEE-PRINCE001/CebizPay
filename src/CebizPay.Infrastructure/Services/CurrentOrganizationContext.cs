using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
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
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // Header takes precedence
            if (httpContext.Request.Headers.TryGetValue("X-Organization-Id", out var headerVal) &&
                Guid.TryParse(headerVal.ToString(), out var headerOrgId))
            {
                return headerOrgId;
            }

            // Fallback to claim
            var claimVal = httpContext.User.FindFirstValue("OrganizationId");
            if (Guid.TryParse(claimVal, out var claimOrgId))
            {
                return claimOrgId;
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsInOrganizationContext => CurrentOrganizationId.HasValue;

    /// <inheritdoc/>
    public async Task<bool> HasAccessToOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
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
}
