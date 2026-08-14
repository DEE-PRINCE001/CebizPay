using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;

namespace CebizPay.Infrastructure.Persistence.Outbox;

/// <summary>
/// Infrastructure implementation of IOutboxService that persists OutboxMessage entities to PostgreSQL.
/// </summary>
public sealed class OutboxService : IOutboxService
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OutboxService"/>.
    /// </summary>
    public OutboxService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public void Write<TEvent>(TEvent domainEvent) where TEvent : class
    {
        var eventType = typeof(TEvent).Name;

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = eventType,
            Content = JsonSerializer.Serialize(domainEvent),
            OccurredOnUtc = DateTime.UtcNow
        };

        _dbContext.OutboxMessages.Add(message);
    }
}
