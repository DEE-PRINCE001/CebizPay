using System.Globalization;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Domain.Communication.Events;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using CebizPay.Domain.Loans.Events;
using CebizPay.Domain.Payroll.Events;
using CebizPay.Domain.Thrift.Events;
using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CebizPay.Workers;

/// <summary>
/// Authoritative RabbitMQ consumer worker that subscribes to Outbox domain events
/// and dispatches multi-channel notifications asynchronously across bounded contexts.
/// </summary>
public sealed partial class NotificationDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMQOptions _options;
    private readonly ILogger<NotificationDispatcherWorker> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>RabbitMQ queue name for asynchronous notification dispatch.</summary>
    public const string QueueName = "cebizpay.notifications.dispatch";

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationDispatcherWorker"/>.
    /// </summary>
    public NotificationDispatcherWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMQOptions> options,
        ILogger<NotificationDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        _options = options?.Value ?? new RabbitMQOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarting(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(stoppingToken).ConfigureAwait(false);
                var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);

                await channel.ExchangeDeclareAsync(
                    exchange: _options.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                await channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                var routingKeys = new[]
                {
                    nameof(OrganizationStatusChangedDomainEvent).ToLowerInvariant(),
                    nameof(LoanApplicationApprovedDomainEvent).ToLowerInvariant(),
                    nameof(PayrollBatchCompletedDomainEvent).ToLowerInvariant(),
                    nameof(ThriftContributionMissedDomainEvent).ToLowerInvariant(),
                    nameof(AnnouncementPublishedDomainEvent).ToLowerInvariant()
                };

                foreach (var routingKey in routingKeys)
                {
                    await channel.QueueBindAsync(
                        queue: QueueName,
                        exchange: _options.ExchangeName,
                        routingKey: routingKey,
                        cancellationToken: stoppingToken).ConfigureAwait(false);
                }

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var bodyBytes = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(bodyBytes);
                        var routingKey = ea.RoutingKey;

                        await ProcessMessageAsync(routingKey, json, stoppingToken).ConfigureAwait(false);

                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogEventProcessingError(_logger, ea.RoutingKey, ex);
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                LogWorkerSubscribed(_logger, QueueName);

                // Keep worker alive until cancellation
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogConsumerConnectionLost(_logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }

        LogWorkerStopped(_logger);
    }

    private async Task ProcessMessageAsync(string routingKey, string json, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        if (routingKey.Equals(nameof(OrganizationStatusChangedDomainEvent).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<OrganizationStatusChangedDomainEvent>(json, SerializerOptions);
            if (@event != null && @event.NewStatus == OrganizationStatus.Suspended)
            {
                var members = await db.OrganizationMemberships
                    .Where(m => m.OrganizationId == @event.OrganizationId && m.Status == MembershipStatus.Active)
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var eventId = $"org-suspended-{@event.OrganizationId}-{@event.OccurredOnUtc:yyyyMMddHHmmss}";

                foreach (var userId in members)
                {
                    await dispatcher.DispatchAsync(new DispatchNotificationRequest(
                        EventId: eventId,
                        RecipientUserId: userId,
                        RecipientEmail: null,
                        RecipientPhoneNumber: null,
                        OrganizationId: @event.OrganizationId,
                        Type: NotificationType.OrganizationSuspended,
                        Priority: NotificationPriority.Critical,
                        TemplateParameters: new Dictionary<string, string>()), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else if (routingKey.Equals(nameof(LoanApplicationApprovedDomainEvent).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<LoanApplicationApprovedDomainEvent>(json, SerializerOptions);
            if (@event != null && @event.Application != null)
            {
                var eventId = $"loan-approved-{@event.Application.Id}";
                var parameters = new Dictionary<string, string>
                {
                    ["Amount"] = @event.Application.RequestedAmount.ToString("N2", CultureInfo.InvariantCulture),
                    ["Currency"] = "NGN"
                };

                await dispatcher.DispatchAsync(new DispatchNotificationRequest(
                    EventId: eventId,
                    RecipientUserId: @event.Application.ApplicantUserId,
                    RecipientEmail: null,
                    RecipientPhoneNumber: null,
                    OrganizationId: @event.Application.OrganizationId,
                    Type: NotificationType.LoanApproved,
                    Priority: NotificationPriority.High,
                    TemplateParameters: parameters), cancellationToken).ConfigureAwait(false);
            }
        }
        else if (routingKey.Equals(nameof(PayrollBatchCompletedDomainEvent).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<PayrollBatchCompletedDomainEvent>(json, SerializerOptions);
            if (@event != null)
            {
                var managers = await db.OrganizationMemberships
                    .Where(m => m.OrganizationId == @event.OrganizationId && m.Status == MembershipStatus.Active &&
                               (m.Role == MembershipRoleType.Owner || m.Role == MembershipRoleType.Admin || m.Role == MembershipRoleType.HrManager || m.Role == MembershipRoleType.PayrollManager))
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var eventId = $"payroll-completed-{@event.PayrollBatchId}";
                var parameters = new Dictionary<string, string>
                {
                    ["BatchReference"] = @event.BatchReference
                };

                foreach (var managerId in managers)
                {
                    await dispatcher.DispatchAsync(new DispatchNotificationRequest(
                        EventId: eventId,
                        RecipientUserId: managerId,
                        RecipientEmail: null,
                        RecipientPhoneNumber: null,
                        OrganizationId: @event.OrganizationId,
                        Type: NotificationType.PayrollCompleted,
                        Priority: NotificationPriority.High,
                        TemplateParameters: parameters), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else if (routingKey.Equals(nameof(ThriftContributionMissedDomainEvent).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<ThriftContributionMissedDomainEvent>(json, SerializerOptions);
            if (@event != null && !string.IsNullOrWhiteSpace(@event.UserId))
            {
                var eventId = $"thrift-missed-{@event.CycleId}-{@event.UserId}";
                await dispatcher.DispatchAsync(new DispatchNotificationRequest(
                    EventId: eventId,
                    RecipientUserId: @event.UserId,
                    RecipientEmail: null,
                    RecipientPhoneNumber: null,
                    OrganizationId: null,
                    Type: NotificationType.ThriftDelinquency,
                    Priority: NotificationPriority.High,
                    TemplateParameters: new Dictionary<string, string>()), cancellationToken).ConfigureAwait(false);
            }
        }
        else if (routingKey.Equals(nameof(AnnouncementPublishedDomainEvent).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            var @event = JsonSerializer.Deserialize<AnnouncementPublishedDomainEvent>(json, SerializerOptions);
            if (@event != null)
            {
                var eventId = $"announcement-{@event.AnnouncementId}";
                var parameters = new Dictionary<string, string>
                {
                    ["Title"] = @event.Title,
                    ["Description"] = @event.Description
                };

                if (@event.Scope == AnnouncementScope.Workplace && @event.OrganizationId.HasValue)
                {
                    // Workplace: Strictly members of the affected organization
                    var members = await db.OrganizationMemberships
                        .Where(m => m.OrganizationId == @event.OrganizationId.Value && m.Status == MembershipStatus.Active)
                        .Select(m => m.UserId)
                        .Distinct()
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var memberId in members)
                    {
                        await dispatcher.DispatchAsync(new DispatchNotificationRequest(
                            EventId: eventId,
                            RecipientUserId: memberId,
                            RecipientEmail: null,
                            RecipientPhoneNumber: null,
                            OrganizationId: @event.OrganizationId.Value,
                            Type: NotificationType.WorkplaceAnnouncement,
                            Priority: NotificationPriority.Normal,
                            TemplateParameters: parameters), cancellationToken).ConfigureAwait(false);
                    }
                }
                else if (@event.Scope == AnnouncementScope.Platform)
                {
                    // Platform: Broadcast to individual users in chunks
                    var userIds = await db.IndividualProfiles
                        .Select(p => p.UserId)
                        .Distinct()
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var userId in userIds)
                    {
                        await dispatcher.DispatchAsync(new DispatchNotificationRequest(
                            EventId: eventId,
                            RecipientUserId: userId,
                            RecipientEmail: null,
                            RecipientPhoneNumber: null,
                            OrganizationId: null,
                            Type: NotificationType.PlatformAnnouncement,
                            Priority: NotificationPriority.Normal,
                            TemplateParameters: parameters), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "NotificationDispatcherWorker starting up...")]
    private static partial void LogWorkerStarting(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "NotificationDispatcherWorker actively subscribed to queue {QueueName}")]
    private static partial void LogWorkerSubscribed(ILogger logger, string queueName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "NotificationDispatcherWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed processing event with routing key {RoutingKey}. Rejecting message.")]
    private static partial void LogEventProcessingError(ILogger logger, string routingKey, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "RabbitMQ consumer connection lost in NotificationDispatcherWorker. Reconnecting in 5s...")]
    private static partial void LogConsumerConnectionLost(ILogger logger, Exception exception);
}
