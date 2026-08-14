namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Provides the identity of the currently authenticated user from the HTTP request context.
/// Abstraction over IHttpContextAccessor to keep Application layer free of ASP.NET Core hosting dependencies.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the authenticated user's ID (from JWT NameIdentifier claim), or null if unauthenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Returns true if there is an authenticated user in the current request context.
    /// </summary>
    bool IsAuthenticated { get; }
}
