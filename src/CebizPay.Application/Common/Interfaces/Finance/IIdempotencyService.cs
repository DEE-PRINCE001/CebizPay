using CebizPay.Domain.Finance.Entities;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Contract for PostgreSQL-backed idempotency service handling money-moving request deduplication.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks for an existing idempotency record by key within the specified actor/operation scope.
    /// </summary>
    Task<IdempotencyRecord?> GetRecordAsync(
        string idempotencyKey,
        string operation,
        string? userId = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to register an idempotency key. Throws if the key exists with a different request hash.
    /// </summary>
    Task<IdempotencyRecord> CreateRecordAsync(
        string idempotencyKey,
        string operation,
        string requestPayload,
        string? userId = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an idempotency record as completed with serialized result JSON.
    /// </summary>
    Task CompleteRecordAsync(Guid recordId, string responseJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an idempotency record as failed.
    /// </summary>
    Task FailRecordAsync(Guid recordId, string? errorJson = null, CancellationToken cancellationToken = default);
}
