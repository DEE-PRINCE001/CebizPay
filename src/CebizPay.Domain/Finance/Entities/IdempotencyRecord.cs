using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Authoritative PostgreSQL-backed idempotency record for money-moving operations.
/// </summary>
public class IdempotencyRecord
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Client-supplied or generated Idempotency key.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>User ID string if user-scoped.</summary>
    public string? UserId { get; private set; }

    /// <summary>Organization ID if organization-scoped.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Operation or endpoint name.</summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>SHA-256 hash of the request payload.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>Processing status.</summary>
    public IdempotencyStatus Status { get; private set; } = IdempotencyStatus.Processing;

    /// <summary>Serialized result response JSON string.</summary>
    public string? ResponseJson { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Completion timestamp.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    private IdempotencyRecord() { } // EF Core

    /// <summary>
    /// Creates a new idempotency record.
    /// </summary>
    public IdempotencyRecord(string idempotencyKey, string operation, string requestHash, string? userId = null, Guid? organizationId = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        if (string.IsNullOrWhiteSpace(requestHash))
            throw new ArgumentException("RequestHash is required.", nameof(requestHash));

        Id = Guid.NewGuid();
        IdempotencyKey = idempotencyKey.Trim();
        Operation = operation.Trim();
        RequestHash = requestHash.Trim();
        UserId = userId;
        OrganizationId = organizationId;
        Status = IdempotencyStatus.Processing;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the idempotency record back to processing state for retry.
    /// </summary>
    public void MarkProcessing()
    {
        Status = IdempotencyStatus.Processing;
        ResponseJson = null;
        CompletedAtUtc = null;
    }

    /// <summary>
    /// Completes the idempotency record with serialized response JSON.
    /// </summary>
    public void Complete(string responseJson)
    {
        Status = IdempotencyStatus.Completed;
        ResponseJson = responseJson;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the idempotency record as failed.
    /// </summary>
    public void Fail(string? errorJson = null)
    {
        Status = IdempotencyStatus.Failed;
        ResponseJson = errorJson;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
