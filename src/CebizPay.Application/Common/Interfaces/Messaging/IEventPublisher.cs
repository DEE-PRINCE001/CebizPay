namespace CebizPay.Application.Common.Interfaces.Messaging;

/// <summary>
/// Defines the contract for publishing events or messages.
/// Enables loosely-coupled communication between different parts of the application.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a message or event asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the message to publish.</typeparam>
    /// <param name="message">The message or event instance to publish.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default);
}