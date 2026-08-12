using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CebizPay.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ-backed implementation of the <see cref="IEventPublisher"/> interface.
/// Encapsulates connection lifecycle, serialization, and exchange publishing.
/// </summary>
public sealed partial class RabbitMQEventPublisher : IEventPublisher
{
    private readonly RabbitMQOptions _options;
    private readonly ILogger<RabbitMQEventPublisher> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMQEventPublisher"/> class.
    /// </summary>
    public RabbitMQEventPublisher(
        IOptions<RabbitMQOptions> options,
        ILogger<RabbitMQEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            using var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var messageType = typeof(T).Name;
            var routingKey = messageType.ToLowerInvariant();
            var jsonContent = JsonSerializer.Serialize(message, SerializerOptions);
            var body = Encoding.UTF8.GetBytes(jsonContent);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString()
            };

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            LogPublishSuccess(_logger, messageType, routingKey, _options.ExchangeName);
        }
        catch (Exception ex)
        {
            LogPublishError(_logger, typeof(T).Name, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Successfully published message of type '{MessageType}' with RoutingKey '{RoutingKey}' to exchange '{Exchange}'.")]
    private static partial void LogPublishSuccess(ILogger logger, string messageType, string routingKey, string exchange);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to publish event of type '{MessageType}' to RabbitMQ.")]
    private static partial void LogPublishError(ILogger logger, string messageType, Exception exception);
}
