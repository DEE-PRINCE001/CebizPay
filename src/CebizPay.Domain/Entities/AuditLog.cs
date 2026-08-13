namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain entity representing immutable audit logs for administrative, compliance, and security operations.
/// </summary>
public class AuditLog
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Actor user ID who performed the action.</summary>
    public string ActorUserId { get; private set; } = string.Empty;
    /// <summary>Action event category/name (e.g., Kyc.Verify, Admin.GrantPermission).</summary>
    public string Action { get; private set; } = string.Empty;
    /// <summary>Target entity type (e.g., KycDocument, Organization, AdminProfile).</summary>
    public string EntityType { get; private set; } = string.Empty;
    /// <summary>Target entity ID.</summary>
    public string EntityId { get; private set; } = string.Empty;
    /// <summary>Optional JSON details payload.</summary>
    public string? DetailsJson { get; private set; }
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private AuditLog() { } // EF Core

    /// <summary>
    /// Creates a new audit log record.
    /// </summary>
    public AuditLog(string actorUserId, string action, string entityType, string entityId, string? detailsJson = null)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("EntityId is required.", nameof(entityId));

        Id = Guid.NewGuid();
        ActorUserId = actorUserId.Trim();
        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId.Trim();
        DetailsJson = detailsJson;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
