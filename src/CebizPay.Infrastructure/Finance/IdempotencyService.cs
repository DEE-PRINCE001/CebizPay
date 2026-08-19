using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Security;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// PostgreSQL-backed idempotency service for money-moving operation deduplication.
/// Idempotency scope: Actor (UserId / OrganizationId) + Operation + IdempotencyKey.
/// </summary>
public sealed class IdempotencyService : IIdempotencyService
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotencyService"/>.
    /// </summary>
    public IdempotencyService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<IdempotencyRecord?> GetRecordAsync(
        string idempotencyKey,
        string operation,
        string? userId = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var key = idempotencyKey.Trim();
        var op = operation.Trim();

        return await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r =>
                r.IdempotencyKey == key &&
                r.Operation == op &&
                r.UserId == userId &&
                r.OrganizationId == organizationId, cancellationToken);
    }

    /// <summary>
    /// Duration after which an abandoned in-flight processing record is considered stale and recoverable.
    /// </summary>
    public static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc/>
    public async Task<IdempotencyRecord> CreateRecordAsync(
        string idempotencyKey,
        string operation,
        string requestPayload,
        string? userId = null,
        Guid? organizationId = null,
        bool autoSave = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));

        var key = idempotencyKey.Trim();
        var op = operation.Trim();
        var requestHash = HashUtility.ComputeSha256(requestPayload);

        var existing = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r =>
                r.IdempotencyKey == key &&
                r.Operation == op &&
                r.UserId == userId &&
                r.OrganizationId == organizationId, cancellationToken);

        if (existing != null)
        {
            if (existing.RequestHash != requestHash)
            {
                throw new IdempotencyConflictException(key, $"Idempotency key conflict: key '{key}' was previously used with a different request payload.");
            }

            // Recover stale in-flight operations that exceeded timeout or previously failed attempts
            if (existing.Status == IdempotencyStatus.Failed ||
                (existing.Status == IdempotencyStatus.Processing && (DateTime.UtcNow - existing.CreatedAtUtc) > ProcessingTimeout))
            {
                existing.MarkProcessing();
                if (autoSave)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return existing;
        }

        var record = new IdempotencyRecord(key, op, requestHash, userId, organizationId);
        _dbContext.IdempotencyRecords.Add(record);

        if (autoSave)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is Npgsql.PostgresException pgEx &&
                pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
            {
                var existingRetry = await _dbContext.IdempotencyRecords
                    .FirstOrDefaultAsync(r =>
                        r.IdempotencyKey == key &&
                        r.Operation == op &&
                        r.UserId == userId &&
                        r.OrganizationId == organizationId, cancellationToken);

                if (existingRetry != null)
                {
                    if (existingRetry.RequestHash != requestHash)
                    {
                        throw new IdempotencyConflictException(key, $"Idempotency key conflict: key '{key}' was previously used with a different request payload.");
                    }

                    if (existingRetry.Status == IdempotencyStatus.Failed ||
                        (existingRetry.Status == IdempotencyStatus.Processing && (DateTime.UtcNow - existingRetry.CreatedAtUtc) > ProcessingTimeout))
                    {
                        existingRetry.MarkProcessing();
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    return existingRetry;
                }
                throw;
            }
        }

        return record;
    }

    /// <inheritdoc/>
    public async Task CompleteRecordAsync(Guid recordId, string responseJson, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.IdempotencyRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record != null)
        {
            record.Complete(responseJson);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task FailRecordAsync(Guid recordId, string? errorJson = null, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.IdempotencyRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record != null)
        {
            record.Fail(errorJson);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

