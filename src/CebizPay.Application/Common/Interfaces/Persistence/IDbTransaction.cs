namespace CebizPay.Application.Common.Interfaces.Persistence;

/// <summary>
/// Database transaction abstraction for explicit unit-of-work transaction management.
/// </summary>
public interface IDbTransaction : IAsyncDisposable
{
    /// <summary>Commits the database transaction asynchronously.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the database transaction asynchronously.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
