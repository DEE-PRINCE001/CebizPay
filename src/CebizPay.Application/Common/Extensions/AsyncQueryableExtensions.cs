using System.Linq.Expressions;
using CebizPay.Application.Common.Interfaces.Persistence;

namespace CebizPay.Application.Common.Extensions;

/// <summary>
/// Async LINQ extension methods for IQueryable without direct EF Core references.
/// Falls back gracefully to standard LINQ if no external executor is configured (e.g. in-memory unit tests).
/// </summary>
public static class AsyncQueryableExtensions
{
    private static IAsyncQueryExecutor? _executor;

    /// <summary>
    /// Sets the global async query executor implementation.
    /// </summary>
    public static void SetExecutor(IAsyncQueryExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>Asynchronously creates a List from an IQueryable.</summary>
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _executor != null ? _executor.ToListAsync(source, cancellationToken) : Task.FromResult(source.ToList());
    }

    /// <summary>Asynchronously returns the first element or a default value.</summary>
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _executor != null ? _executor.FirstOrDefaultAsync(source, cancellationToken) : Task.FromResult(source.FirstOrDefault());
    }

    /// <summary>Asynchronously returns the first element matching a predicate or a default value.</summary>
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return _executor != null ? _executor.FirstOrDefaultAsync(source, predicate, cancellationToken) : Task.FromResult(source.FirstOrDefault(predicate.Compile()));
    }

    /// <summary>Asynchronously determines whether any element satisfies a condition.</summary>
    public static Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _executor != null ? _executor.AnyAsync(source, cancellationToken) : Task.FromResult(source.Any());
    }

    /// <summary>Asynchronously determines whether any element satisfies a predicate.</summary>
    public static Task<bool> AnyAsync<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return _executor != null ? _executor.AnyAsync(source, predicate, cancellationToken) : Task.FromResult(source.Any(predicate.Compile()));
    }

    /// <summary>Asynchronously returns the number of elements.</summary>
    public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _executor != null ? _executor.CountAsync(source, cancellationToken) : Task.FromResult(source.Count());
    }

    /// <summary>Asynchronously returns the number of elements satisfying a predicate.</summary>
    public static Task<int> CountAsync<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return _executor != null ? _executor.CountAsync(source, predicate, cancellationToken) : Task.FromResult(source.Count(predicate.Compile()));
    }
}
