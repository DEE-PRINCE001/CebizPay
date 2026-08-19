using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CebizPay.Infrastructure.Messaging;

/// <summary>
/// Thread-safe provider managing the singleton persistent RabbitMQ connection lifecycle.
/// </summary>
public sealed partial class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
    private readonly RabbitMQOptions _options;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="RabbitMqConnectionProvider"/>.
    /// </summary>
    public RabbitMqConnectionProvider(
        IOptions<RabbitMQOptions> options,
        ILogger<RabbitMqConnectionProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection != null)
            {
                try
                {
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogConnectionDisposeError(_logger, ex);
                }
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            LogConnecting(_logger, _options.HostName, _options.Port);
            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            LogConnected(_logger, _options.HostName, _options.Port);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connection != null)
        {
            try
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogConnectionDisposeError(_logger, ex);
            }
            _connection = null;
        }

        _connectionLock.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connection != null)
        {
            try
            {
                _connection.Dispose();
            }
            catch (Exception ex)
            {
                LogConnectionDisposeError(_logger, ex);
            }
            _connection = null;
        }

        _connectionLock.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connecting to RabbitMQ broker at {Host}:{Port}...")]
    private static partial void LogConnecting(ILogger logger, string host, int port);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Successfully connected to RabbitMQ broker at {Host}:{Port}.")]
    private static partial void LogConnected(ILogger logger, string host, int port);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Error disposing old RabbitMQ connection.")]
    private static partial void LogConnectionDisposeError(ILogger logger, Exception exception);
}
