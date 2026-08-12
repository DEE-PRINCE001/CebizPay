namespace CebizPay.Application.Common.Interfaces.Caching;

/// <summary>
/// Defines the contract for caching operations.
/// Provides methods to retrieve, store, and remove cached data with expiration support.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key used to identify the value.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The cached value if found; otherwise, null.</returns>
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a value in the cache asynchronously with the specified expiration time.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key under which to store the value.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">The time span after which the cached value expires.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached value asynchronously.
    /// </summary>
    /// <param name="key">The cache key of the value to remove.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);
}