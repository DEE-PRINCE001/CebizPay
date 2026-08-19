using CebizPay.Domain.Entities;

namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Centralized service for generating and persisting immutable audit log entries across the application.
/// Ensures contextual metadata and sensitive data redactions are applied consistently.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records an audit log entry and saves it to the database.
    /// </summary>
    Task<AuditLog> LogAsync(
        string action,
        string resourceType,
        string? resourceId = null,
        object? before = null,
        object? after = null,
        Guid? organizationId = null,
        string? details = null,
        string? actorId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and tracks an audit log entry within the active unit-of-work / DbContext ChangeTracker
    /// without calling SaveChangesAsync immediately. This ensures the audit record is committed
    /// atomically as part of an ambient business/financial transaction.
    /// </summary>
    AuditLog CreateAuditEntry(
        string action,
        string resourceType,
        string? resourceId = null,
        object? before = null,
        object? after = null,
        Guid? organizationId = null,
        string? details = null,
        string? actorId = null);
}
