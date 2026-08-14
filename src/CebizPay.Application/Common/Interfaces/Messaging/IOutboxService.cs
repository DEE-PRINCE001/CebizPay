namespace CebizPay.Application.Common.Interfaces.Messaging;

/// <summary>
/// Application abstraction for enqueuing domain events into the persistent outbox table.
/// Encapsulates OutboxMessage creation to preserve Clean Architecture boundary.
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Enqueues a domain event for outbox persistence within the active unit of work transaction.
    /// </summary>
    /// <typeparam name="TEvent">The event payload type.</typeparam>
    /// <param name="domainEvent">The domain event object to serialize and persist.</param>
    void Write<TEvent>(TEvent domainEvent) where TEvent : class;
}
