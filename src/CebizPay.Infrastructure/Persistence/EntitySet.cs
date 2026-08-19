using System.Collections;
using System.Linq.Expressions;
using CebizPay.Application.Common.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Infrastructure adapter implementing <see cref="IEntitySet{T}"/> using EF Core's <see cref="DbSet{T}"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class EntitySet<T> : IEntitySet<T> where T : class
{
    private readonly DbSet<T> _set;

    /// <summary>
    /// Initializes a new instance of <see cref="EntitySet{T}"/>.
    /// </summary>
    public EntitySet(DbSet<T> set)
    {
        _set = set ?? throw new ArgumentNullException(nameof(set));
    }

    /// <inheritdoc/>
    public Type ElementType => ((IQueryable<T>)_set).ElementType;

    /// <inheritdoc/>
    public Expression Expression => ((IQueryable<T>)_set).Expression;

    /// <inheritdoc/>
    public IQueryProvider Provider => ((IQueryable<T>)_set).Provider;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_set).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public void Add(T entity) => _set.Add(entity);

    /// <inheritdoc/>
    public void Update(T entity) => _set.Update(entity);

    /// <inheritdoc/>
    public void Remove(T entity) => _set.Remove(entity);
}
