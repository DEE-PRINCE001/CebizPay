using RabbitMQ.Client;

namespace CebizPay.Infrastructure.Messaging;

/// <summary>
/// Infrastructure contract for managing a persistent, shared RabbitMQ connection lifecycle.
/// Prevents connection-per-publish anti-patterns and handles automatic reconnection.
/// </summary>
public interface IRabbitMqConnectionProvider : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Retrieves or establishes the active persistent RabbitMQ connection.
    /// </summary>
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
