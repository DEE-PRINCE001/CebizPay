using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Security;
using Microsoft.AspNetCore.Http;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Extracts runtime request metadata from the ambient HTTP context to populate audit records.
/// </summary>
public sealed class AuditContextAccessor : IAuditContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _currentOrganizationContext;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditContextAccessor"/>.
    /// </summary>
    public AuditContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext currentOrganizationContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserService = currentUserService;
        _currentOrganizationContext = currentOrganizationContext;
    }

    /// <inheritdoc/>
    public string? ActorId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_currentUserService.UserId))
                return _currentUserService.UserId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is not null)
            {
                var claimId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.User.FindFirst("sub")?.Value;

                if (!string.IsNullOrWhiteSpace(claimId))
                    return claimId;
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public Guid? OrganizationId => _currentOrganizationContext.CurrentOrganizationId;

    /// <inheritdoc/>
    public string? IpAddress
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                    return firstIp;
            }

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    /// <inheritdoc/>
    public string? UserAgent
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
        }
    }

    /// <inheritdoc/>
    public string? CorrelationId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            if (httpContext.Items.TryGetValue("CorrelationId", out var itemVal) && itemVal is string itemStr && !string.IsNullOrWhiteSpace(itemStr))
                return itemStr;

            var headerVal = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerVal))
                return headerVal;

            return httpContext.TraceIdentifier;
        }
    }
}
