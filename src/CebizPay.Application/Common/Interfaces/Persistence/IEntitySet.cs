namespace CebizPay.Application.Common.Interfaces.Persistence;

/// <summary>
/// Abstraction representing a queryable and modifiable domain entity set.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IEntitySet<T> : IQueryable<T> where T : class
{
    /// <summary>Adds a new entity to the set.</summary>
    void Add(T entity);

    /// <summary>Marks an entity as modified.</summary>
    void Update(T entity);

    /// <summary>Removes an entity from the set.</summary>
    void Remove(T entity);
}
