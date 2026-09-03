using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Referrals;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CebizPay.Workers;

/// <summary>
/// Background worker that consumes KYC and deposit domain events to evaluate referral qualification milestones.
/// Supports both KYC-first and deposit-first event ordering idempotently.
/// </summary>
public sealed partial class ReferralQualificationWorker : BackgroundService
{
    private const string QueueName = "cebizpay.referrals.qualification";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMQOptions _options;
    private readonly ILogger<ReferralQualificationWorker> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ReferralQualificationWorker"/>.
    /// </summary>
    public ReferralQualificationWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMQOptions> options,
        ILogger<ReferralQualificationWorker> logger)
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
                    nameof(KycStatusChangedDomainEvent).ToLowerInvariant(),
                    nameof(InboundVirtualAccountDepositCompletedDomainEvent).ToLowerInvariant(),
                    nameof(ExternalFundingAccountDepositCompletedDomainEvent).ToLowerInvariant(),
                    nameof(CardFundingCompletedDomainEvent).ToLowerInvariant()
                };

                foreach (var routingKey in routingKeys)
                {
                    await channel.QueueBindAsync(
                        queue: QueueName,
                        exchange: _options.ExchangeName,
                        routingKey: routingKey,
                        cancellationToken: stoppingToken).ConfigureAwait(false);
                }

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 20, global: false, cancellationToken: stoppingToken).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var routingKey = ea.RoutingKey;

                        await ProcessMessageAsync(routingKey, json, stoppingToken).ConfigureAwait(false);

                        await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogMessageProcessingFailed(_logger, ea.RoutingKey, ex);
                        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                LogWorkerStarted(_logger);

                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerConnectionFailed(_logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }

        LogWorkerStopped(_logger);
    }

    private async Task ProcessMessageAsync(string routingKey, string json, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var qualificationService = scope.ServiceProvider.GetRequiredService<IReferralQualificationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var kycRoutingKey = nameof(KycStatusChangedDomainEvent).ToLowerInvariant();
        var virtualAccountRoutingKey = nameof(InboundVirtualAccountDepositCompletedDomainEvent).ToLowerInvariant();
        var externalFundingRoutingKey = nameof(ExternalFundingAccountDepositCompletedDomainEvent).ToLowerInvariant();
        var cardFundingRoutingKey = nameof(CardFundingCompletedDomainEvent).ToLowerInvariant();

        if (routingKey.Equals(kycRoutingKey, StringComparison.OrdinalIgnoreCase))
        {
            var evt = JsonSerializer.Deserialize<KycStatusChangedDomainEvent>(json);
            if (evt != null && evt.NewStatus == KycStatus.Verified)
            {
                await qualificationService.EvaluateQualificationAsync(evt.UserId, cancellationToken);
            }
        }
        else if (routingKey.Equals(virtualAccountRoutingKey, StringComparison.OrdinalIgnoreCase) ||
                 routingKey.Equals(externalFundingRoutingKey, StringComparison.OrdinalIgnoreCase) ||
                 routingKey.Equals(cardFundingRoutingKey, StringComparison.OrdinalIgnoreCase))
        {
            // Resolve wallet ID to user ID
            Guid walletId = Guid.Empty;
            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.TryGetProperty("WalletId", out var walletIdProp))
                {
                    walletId = walletIdProp.GetGuid();
                }
            }

            if (walletId != Guid.Empty)
            {
                var wallet = await dbContext.Wallets
                    .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);

                if (wallet != null && !string.IsNullOrWhiteSpace(wallet.IndividualId))
                {
                    await qualificationService.EvaluateQualificationAsync(wallet.IndividualId, cancellationToken);
                }
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting ReferralQualificationWorker...")]
    private static partial void LogWorkerStarting(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "ReferralQualificationWorker connected and consuming.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "ReferralQualificationWorker connection error. Retrying in 5 seconds.")]
    private static partial void LogWorkerConnectionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "ReferralQualificationWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Error processing referral qualification message with routing key '{RoutingKey}'.")]
    private static partial void LogMessageProcessingFailed(ILogger logger, string routingKey, Exception exception);
}
