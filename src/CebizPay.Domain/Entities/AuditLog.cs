namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain entity representing an immutable audit log record for administrative, financial, compliance, and security operations.
/// </summary>
public class AuditLog
{
    /// <summary>Unique identifier for the audit record.</summary>
    public Guid Id { get; private set; }

    /// <summary>Actor identifier (User ID or system actor) who performed the action.</summary>
    public string ActorId { get; private set; } = string.Empty;

    /// <summary>Optional Organization identifier for tenant-scoped operations.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Action event taxonomy name (e.g., USER_REGISTERED, PEER_TRANSFER_COMPLETED).</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Target resource type (e.g., USER, ORGANIZATION, WALLET, FEE_POLICY).</summary>
    public string ResourceType { get; private set; } = string.Empty;

    /// <summary>Optional identifier of the target resource.</summary>
    public string? ResourceId { get; private set; }

    /// <summary>Optional sanitized JSON snapshot of the state before the action.</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>Optional sanitized JSON snapshot of the state after the action.</summary>
    public string? AfterJson { get; private set; }

    /// <summary>Optional client IP address from which the action originated.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Optional User-Agent string from the client request.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Optional distributed correlation / trace identifier.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>UTC timestamp when the audited event occurred.</summary>
    public DateTime OccurredAtUtc { get; private set; }

    // Backwards-compatible aliases
    /// <summary>Gets the actor user ID (alias of <see cref="ActorId"/>).</summary>
    public string ActorUserId => ActorId;

    /// <summary>Gets the entity type (alias of <see cref="ResourceType"/>).</summary>
    public string EntityType => ResourceType;

    /// <summary>Gets the entity ID (alias of <see cref="ResourceId"/>).</summary>
    public string EntityId => ResourceId ?? string.Empty;

    /// <summary>Gets the creation timestamp (alias of <see cref="OccurredAtUtc"/>).</summary>
    public DateTime CreatedAtUtc => OccurredAtUtc;

    /// <summary>Gets the details payload (alias of <see cref="AfterJson"/>).</summary>
    public string? DetailsJson => AfterJson;

    private AuditLog() { } // EF Core

    /// <summary>
    /// Creates a new immutable audit log record.
    /// </summary>
    public static AuditLog Create(
        string actorId,
        string action,
        string resourceType,
        string? resourceId = null,
        Guid? organizationId = null,
        string? beforeJson = null,
        string? afterJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? correlationId = null,
        DateTime? occurredAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("ActorId is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("ResourceType is required.", nameof(resourceType));

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorId.Trim(),
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.Trim(),
            OrganizationId = organizationId,
            BeforeJson = string.IsNullOrWhiteSpace(beforeJson) ? null : beforeJson,
            AfterJson = string.IsNullOrWhiteSpace(afterJson) ? null : afterJson,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : (userAgent.Length > 500 ? userAgent[..500] : userAgent),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Backwards-compatible constructor for existing codebase usages.
    /// </summary>
    public AuditLog(
        string actorUserId,
        string action,
        string entityType,
        string entityId,
        string? detailsJson = null)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required.", nameof(entityType));

        Id = Guid.NewGuid();
        ActorId = actorUserId.Trim();
        Action = action.Trim();
        ResourceType = entityType.Trim();
        ResourceId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim();
        AfterJson = detailsJson;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
