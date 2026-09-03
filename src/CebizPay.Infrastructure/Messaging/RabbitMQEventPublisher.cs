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
/// Reuses persistent IConnection from <see cref="IRabbitMqConnectionProvider"/> and safely manages channel lifecycles.
/// </summary>
public sealed partial class RabbitMQEventPublisher : IEventPublisher
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMQOptions _options;
    private readonly ILogger<RabbitMQEventPublisher> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMQEventPublisher"/> class.
    /// </summary>
    public RabbitMQEventPublisher(
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMQOptions> options,
        ILogger<RabbitMQEventPublisher> logger)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            string messageType;
            string jsonContent;

            if (message is string rawString)
            {
                jsonContent = rawString;
                messageType = InferEventType(rawString) ?? typeof(T).Name;
            }
            else
            {
                messageType = typeof(T).Name;
                jsonContent = JsonSerializer.Serialize(message, SerializerOptions);
            }

            var routingKey = messageType.ToLowerInvariant();
            var body = Encoding.UTF8.GetBytes(jsonContent);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
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

    private static string? InferEventType(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("$type", out var typeProp) ||
                root.TryGetProperty("Type", out typeProp) ||
                root.TryGetProperty("type", out typeProp) ||
                root.TryGetProperty("EventType", out typeProp) ||
                root.TryGetProperty("eventType", out typeProp))
            {
                var typeStr = typeProp.GetString();
                if (!string.IsNullOrWhiteSpace(typeStr))
                {
                    var commaIdx = typeStr.IndexOf(',', StringComparison.Ordinal);
                    return commaIdx > 0 ? typeStr[..commaIdx].Trim() : typeStr.Trim();
                }
            }

            // Heuristic detection based on unique domain event payload signatures
            if (root.TryGetProperty("PaymentAttemptId", out _) && root.TryGetProperty("FailureReason", out _))
                return "PaymentAttemptFailedEvent";
            if (root.TryGetProperty("PaymentAttemptId", out _) && root.TryGetProperty("ProviderReference", out _))
                return "PaymentAttemptSucceededEvent";
            if (root.TryGetProperty("PaymentAttemptId", out _) && root.TryGetProperty("Reason", out _))
                return "PaymentAttemptUnknownEvent";
            if (root.TryGetProperty("TransferId", out _) && root.TryGetProperty("ProviderReference", out _))
                return "BankTransferCompletedEvent";
            if (root.TryGetProperty("TransferId", out _) && root.TryGetProperty("Reason", out _))
                return "BankTransferFailedEvent";
            if (root.TryGetProperty("OrganizationId", out _) && root.TryGetProperty("NewStatus", out _))
                return "OrganizationStatusChangedDomainEvent";
            if (root.TryGetProperty("Application", out _) && root.TryGetProperty("OccurredOnUtc", out _))
                return "LoanApplicationApprovedDomainEvent";
            if (root.TryGetProperty("PayrollBatchId", out _))
                return "PayrollBatchCompletedDomainEvent";
            if (root.TryGetProperty("CycleId", out _) && root.TryGetProperty("UserId", out _))
                return "ThriftContributionMissedDomainEvent";
            if (root.TryGetProperty("AnnouncementId", out _))
                return "AnnouncementPublishedDomainEvent";

            return null;
        }
        catch
        {
            return null;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Successfully published message of type '{MessageType}' with RoutingKey '{RoutingKey}' to exchange '{Exchange}'.")]
    private static partial void LogPublishSuccess(ILogger logger, string messageType, string routingKey, string exchange);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to publish event of type '{MessageType}' to RabbitMQ.")]
    private static partial void LogPublishError(ILogger logger, string messageType, Exception exception);
}
