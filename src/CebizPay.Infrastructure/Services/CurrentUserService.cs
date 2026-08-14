using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Security;
using Microsoft.AspNetCore.Http;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// ASP.NET Core HTTP context-based implementation of ICurrentUserService.
/// Extracts the authenticated user's ID from the NameIdentifier JWT claim.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="CurrentUserService"/>.
    /// </summary>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string? UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <inheritdoc/>
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
