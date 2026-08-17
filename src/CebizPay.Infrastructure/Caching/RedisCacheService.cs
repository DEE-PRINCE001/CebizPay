using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CebizPay.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of the <see cref="ICacheService"/> interface.
/// Handles key prefixing, JSON serialization, and expiration management.
/// </summary>
public sealed partial class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheService"/> class.
    /// </summary>
    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _options = options.Value;
        _logger = logger;
    }

    private static DateTime _circuitBreakerUntilUtc = DateTime.MinValue;
    private static int _consecutiveFailures;
    private static readonly object LockObj = new();

    private static bool IsCircuitOpen()
    {
        lock (LockObj)
        {
            if (DateTime.UtcNow < _circuitBreakerUntilUtc)
            {
                return true;
            }

            return false;
        }
    }

    private static void RecordSuccess()
    {
        lock (LockObj)
        {
            _consecutiveFailures = 0;
            _circuitBreakerUntilUtc = DateTime.MinValue;
        }
    }

    private void RecordFailure()
    {
        lock (LockObj)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= 3)
            {
                _circuitBreakerUntilUtc = DateTime.UtcNow.AddSeconds(30);
                LogCircuitBreakerTripped(_logger, _consecutiveFailures);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (IsCircuitOpen())
        {
            return default;
        }

        try
        {
            var db = _connectionMultiplexer.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);
            var redisValue = await db.StringGetAsync(prefixedKey).ConfigureAwait(false);

            RecordSuccess();

            if (redisValue.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>((byte[])redisValue!, SerializerOptions);
        }
        catch (Exception ex)
        {
            RecordFailure();
            LogCacheGetError(_logger, key, ex);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (IsCircuitOpen())
        {
            return;
        }

        try
        {
            var db = _connectionMultiplexer.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);

            byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

            await db.StringSetAsync(prefixedKey, utf8Bytes, expiration).ConfigureAwait(false);

            RecordSuccess();
        }
        catch (Exception ex)
        {
            RecordFailure();
            LogCacheSetError(_logger, key, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (IsCircuitOpen())
        {
            return;
        }

        try
        {
            var db = _connectionMultiplexer.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);

            await db.KeyDeleteAsync(prefixedKey).ConfigureAwait(false);

            RecordSuccess();
        }
        catch (Exception ex)
        {
            RecordFailure();
            LogCacheRemoveError(_logger, key, ex);
        }
    }

    private string GetPrefixedKey(string key) => $"{_options.InstanceName}{key}";

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error retrieving key '{Key}' from Redis cache.")]
    private static partial void LogCacheGetError(ILogger logger, string key, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error setting key '{Key}' in Redis cache.")]
    private static partial void LogCacheSetError(ILogger logger, string key, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error removing key '{Key}' from Redis cache.")]
    private static partial void LogCacheRemoveError(ILogger logger, string key, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Redis circuit breaker TRIPPED for 30 seconds due to {Count} consecutive failures.")]
    private static partial void LogCircuitBreakerTripped(ILogger logger, int count);
}
