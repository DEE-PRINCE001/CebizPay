using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Authoritative audit log persistence service.
/// Implements centralized logging, sensitive data redaction, and request context capture.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditSanitizer _sanitizer;
    private readonly IAuditContextAccessor _contextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditLogService"/>.
    /// </summary>
    public AuditLogService(
        IApplicationDbContext dbContext,
        IAuditSanitizer sanitizer,
        IAuditContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _sanitizer = sanitizer;
        _contextAccessor = contextAccessor;
    }

    /// <inheritdoc/>
    public AuditLog CreateAuditEntry(
        string action,
        string resourceType,
        string? resourceId = null,
        object? before = null,
        object? after = null,
        Guid? organizationId = null,
        string? details = null,
        string? actorId = null)
    {
        var resolvedActorId = !string.IsNullOrWhiteSpace(actorId)
            ? actorId
            : (_contextAccessor.ActorId ?? "SYSTEM");

        var resolvedOrgId = organizationId ?? _contextAccessor.OrganizationId;
        var beforeJson = before is not null ? _sanitizer.Sanitize(before) : null;
        var afterJson = after is not null
            ? _sanitizer.Sanitize(after)
            : (details is not null ? _sanitizer.SanitizeJsonString(details) : null);

        var auditLog = AuditLog.Create(
            actorId: resolvedActorId,
            action: action,
            resourceType: resourceType,
            resourceId: resourceId,
            organizationId: resolvedOrgId,
            beforeJson: beforeJson,
            afterJson: afterJson,
            ipAddress: _contextAccessor.IpAddress,
            userAgent: _contextAccessor.UserAgent,
            correlationId: _contextAccessor.CorrelationId);

        _dbContext.AuditLogs.Add(auditLog);
        return auditLog;
    }

    /// <inheritdoc/>
    public async Task<AuditLog> LogAsync(
        string action,
        string resourceType,
        string? resourceId = null,
        object? before = null,
        object? after = null,
        Guid? organizationId = null,
        string? details = null,
        string? actorId = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = CreateAuditEntry(
            action: action,
            resourceType: resourceType,
            resourceId: resourceId,
            before: before,
            after: after,
            organizationId: organizationId,
            details: details,
            actorId: actorId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return auditLog;
    }
}
