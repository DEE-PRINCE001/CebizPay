#pragma warning disable CA1848, CA1873, CS1591
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
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
/// Background worker and RabbitMQ consumer that listens for <see cref="PaymentAttemptFailedEvent"/>
/// and asynchronously executes automated provider failover according to the capability routing policy
/// (e.g. Monnify -> Flutterwave -> Paystack).
/// </summary>
public sealed partial class PaymentFailoverWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMQOptions _options;
    private readonly ILogger<PaymentFailoverWorker> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>RabbitMQ queue name for automated payment failover dispatch.</summary>
    public const string QueueName = "cebizpay.payments.failover";

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentFailoverWorker"/> class.
    /// </summary>
    public PaymentFailoverWorker(
        IServiceScopeFactory scopeFactory,
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMQOptions> options,
        ILogger<PaymentFailoverWorker> logger)
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

                var routingKey = nameof(PaymentAttemptFailedEvent).ToLowerInvariant();
                await channel.QueueBindAsync(
                    queue: QueueName,
                    exchange: _options.ExchangeName,
                    routingKey: routingKey,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var bodyBytes = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(bodyBytes);

                        await ProcessMessageAsync(json, stoppingToken).ConfigureAwait(false);

                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    catch (JsonException ex)
                    {
                        // Poison message: invalid JSON cannot be recovered by retrying
                        LogPoisonMessageRejected(_logger, ex);
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken).ConfigureAwait(false);
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

    /// <summary>
    /// Processes a single failover message consumed from RabbitMQ.
    /// Internal for unit test verification.
    /// </summary>
    public async Task ProcessMessageAsync(string json, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<PaymentAttemptFailedEvent>(json, SerializerOptions);
        if (@event == null || @event.LedgerTransactionId == Guid.Empty)
        {
            throw new JsonException("PaymentAttemptFailedEvent payload is empty or missing LedgerTransactionId.");
        }

        // Rule: Business Failure is terminal and must never enter automatic failover
        if (!string.IsNullOrWhiteSpace(@event.FailureCode) && IsBusinessFailureCode(@event.FailureCode))
        {
            LogBusinessFailureSkipped(_logger, @event.LedgerTransactionId, @event.FailureCode);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var failoverService = scope.ServiceProvider.GetRequiredService<IPaymentFailoverService>();

        // Re-read authoritative database state before triggering fallback provider dispatch
        var bankTransfer = await db.BankTransfers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.LedgerTransactionId == @event.LedgerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (bankTransfer == null)
        {
            LogBankTransferNotFound(_logger, @event.LedgerTransactionId);
            return;
        }

        if (bankTransfer.Status is BankTransferStatus.Completed or BankTransferStatus.Failed)
        {
            LogTransferAlreadyTerminal(_logger, @event.LedgerTransactionId, bankTransfer.Status.ToString());
            return;
        }

        var attempts = await db.PaymentAttempts
            .AsNoTracking()
            .Where(p => p.LedgerTransactionId == @event.LedgerTransactionId)
            .OrderBy(p => p.AttemptNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (attempts.Any(p => p.Status == PaymentAttemptStatus.Succeeded))
        {
            LogTransferAlreadySucceeded(_logger, @event.LedgerTransactionId);
            return;
        }

        var latestAttempt = attempts.LastOrDefault();
        if (latestAttempt != null && latestAttempt.Status is PaymentAttemptStatus.Unknown or PaymentAttemptStatus.Processing)
        {
            LogAttemptIndeterminate(_logger, @event.LedgerTransactionId, latestAttempt.Status.ToString());
            return;
        }

        // Trigger failover via authoritative IPaymentFailoverService
        LogExecutingFailover(_logger, @event.LedgerTransactionId, @event.Provider.ToString(), @event.AttemptNumber);
        var result = await failoverService.FailoverAsync(@event.LedgerTransactionId, cancellationToken).ConfigureAwait(false);
        LogFailoverResult(_logger, @event.LedgerTransactionId, result.Succeeded, result.FallbackProvider?.ToString() ?? "None", result.ErrorMessage ?? "Success");
    }

    private static bool IsBusinessFailureCode(string code)
    {
        return string.Equals(code, "BUSINESS_REJECTION", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "INVALID_ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "INVALID_BANK", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "BLOCKED_ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "ACCOUNT_BLOCKED", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "INSUFFICIENT_FUNDS", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "LIMIT_EXCEEDED", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "PAYMENT_FAILED", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "PaymentFailoverWorker starting up...")]
    private static partial void LogWorkerStarting(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "PaymentFailoverWorker actively subscribed to queue {QueueName}")]
    private static partial void LogWorkerSubscribed(ILogger logger, string queueName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "PaymentFailoverWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed processing failover event with routing key {RoutingKey}. Rejecting message.")]
    private static partial void LogEventProcessingError(ILogger logger, string routingKey, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Poison message encountered in failover queue. Dead-lettering without requeue.")]
    private static partial void LogPoisonMessageRejected(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "RabbitMQ consumer connection lost in PaymentFailoverWorker. Reconnecting in 5s...")]
    private static partial void LogConsumerConnectionLost(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Skipping failover for {LedgerTransactionId}: failure code '{FailureCode}' is a business rejection.")]
    private static partial void LogBusinessFailureSkipped(ILogger logger, Guid ledgerTransactionId, string failureCode);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "BankTransfer not found for LedgerTransactionId {LedgerTransactionId}. Cannot failover.")]
    private static partial void LogBankTransferNotFound(ILogger logger, Guid ledgerTransactionId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Skipping failover for {LedgerTransactionId}: BankTransfer is already in terminal state '{Status}'.")]
    private static partial void LogTransferAlreadyTerminal(ILogger logger, Guid ledgerTransactionId, string status);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Skipping failover for {LedgerTransactionId}: an attempt has already succeeded.")]
    private static partial void LogTransferAlreadySucceeded(ILogger logger, Guid ledgerTransactionId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "Skipping failover for {LedgerTransactionId}: latest attempt has indeterminate status '{Status}'. Reconciliation required.")]
    private static partial void LogAttemptIndeterminate(ILogger logger, Guid ledgerTransactionId, string status);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Executing automated failover for {LedgerTransactionId} after provider {FailedProvider} attempt {AttemptNumber} technical failure.")]
    private static partial void LogExecutingFailover(ILogger logger, Guid ledgerTransactionId, string failedProvider, int attemptNumber);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Automated failover for {LedgerTransactionId} completed: Succeeded={Succeeded}, FallbackProvider={FallbackProvider}, Result={Result}")]
    private static partial void LogFailoverResult(ILogger logger, Guid ledgerTransactionId, bool succeeded, string fallbackProvider, string result);
}
