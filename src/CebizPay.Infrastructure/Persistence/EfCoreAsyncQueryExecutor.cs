using System.Linq.Expressions;
using CebizPay.Application.Common.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Infrastructure implementation of <see cref="IAsyncQueryExecutor"/> using EF Core asynchronous query provider extensions.
/// </summary>
public sealed class EfCoreAsyncQueryExecutor : IAsyncQueryExecutor
{
    /// <inheritdoc/>
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);
        return EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, predicate, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EntityFrameworkQueryableExtensions.AnyAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> AnyAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);
        return EntityFrameworkQueryableExtensions.AnyAsync(query, predicate, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);
        return EntityFrameworkQueryableExtensions.CountAsync(query, predicate, cancellationToken);
    }
}
