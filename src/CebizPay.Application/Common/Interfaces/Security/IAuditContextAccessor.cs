namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Provides contextual metadata for the current request execution to attach to audit records
/// (actor user ID, active organization ID, IP address, user-agent, correlation ID).
/// </summary>
public interface IAuditContextAccessor
{
    /// <summary>Gets the current actor user identifier, if authenticated or available.</summary>
    string? ActorId { get; }

    /// <summary>Gets the currently active OrganizationId, if executing within an organization context.</summary>
    Guid? OrganizationId { get; }

    /// <summary>Gets the client IP address from the request context, if available.</summary>
    string? IpAddress { get; }

    /// <summary>Gets the client User-Agent header from the request context, if available.</summary>
    string? UserAgent { get; }

    /// <summary>Gets the correlation / trace ID for the current request, if available.</summary>
    string? CorrelationId { get; }
}
