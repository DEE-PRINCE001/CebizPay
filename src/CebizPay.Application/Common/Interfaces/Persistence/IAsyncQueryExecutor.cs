using System.Linq.Expressions;

namespace CebizPay.Application.Common.Interfaces.Persistence;

/// <summary>
/// Contract for executing asynchronous query operations without direct framework coupling.
/// </summary>
public interface IAsyncQueryExecutor
{
    /// <summary>Asynchronously creates a List from an IQueryable.</summary>
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>Asynchronously returns the first element or a default value.</summary>
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>Asynchronously returns the first element matching a predicate or a default value.</summary>
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Asynchronously determines whether any element satisfies a condition.</summary>
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>Asynchronously determines whether any element satisfies a predicate.</summary>
    Task<bool> AnyAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Asynchronously returns the number of elements.</summary>
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>Asynchronously returns the number of elements satisfying a predicate.</summary>
    Task<int> CountAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}
