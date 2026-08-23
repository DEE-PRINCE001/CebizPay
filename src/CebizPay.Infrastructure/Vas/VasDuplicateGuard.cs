using System.Collections.Concurrent;
using System.Globalization;
using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Vas.Enums;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CebizPay.Infrastructure.Vas;

/// <summary>
/// Service implementing 120-second duplicate purchase prevention window for VAS operations.
/// Leverages atomic Redis keys with TTL, falling back gracefully to in-memory protection if Redis is unavailable.
/// </summary>
public sealed partial class VasDuplicateGuard : IVasDuplicateGuard
{
    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly ILogger<VasDuplicateGuard> _logger;
    private static readonly ConcurrentDictionary<string, DateTime> InMemoryGuard = new();

    /// <summary>
    /// Initializes a new instance of <see cref="VasDuplicateGuard"/>.
    /// </summary>
    public VasDuplicateGuard(
        ILogger<VasDuplicateGuard> logger,
        IConnectionMultiplexer? connectionMultiplexer = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionMultiplexer = connectionMultiplexer;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireDuplicateLockAsync(
        VasType type,
        string phoneNumber,
        decimal amount,
        VasNetwork network,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = PhoneNormalizer.NormalizeNational(phoneNumber);
        var key = BuildKey(type, normalizedPhone, amount, network, productCode);

        // 1. Try Redis atomic lock if available
        if (_connectionMultiplexer != null && _connectionMultiplexer.IsConnected)
        {
            try
            {
                var db = _connectionMultiplexer.GetDatabase();
                var setSuccess = await db.StringSetAsync(key, "1", TimeSpan.FromSeconds(120), When.NotExists).ConfigureAwait(false);

                if (!setSuccess)
                {
                    LogDuplicateBlocked(_logger, type.ToString(), VtuGate.VtuGateClient.MaskPhoneNumber(normalizedPhone), amount);
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                LogRedisFallbackWarning(_logger, ex.Message);
            }
        }

        // 2. In-memory fallback
        CleanExpiredInMemoryEntries();
        var now = DateTime.UtcNow;

        if (InMemoryGuard.TryGetValue(key, out var expiresAt) && expiresAt > now)
        {
            LogDuplicateBlocked(_logger, type.ToString(), VtuGate.VtuGateClient.MaskPhoneNumber(normalizedPhone), amount);
            return false;
        }

        InMemoryGuard[key] = now.AddSeconds(120);
        return true;
    }

    /// <inheritdoc/>
    public async Task ReleaseDuplicateLockAsync(
        VasType type,
        string phoneNumber,
        decimal amount,
        VasNetwork network,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = PhoneNormalizer.NormalizeNational(phoneNumber);
        var key = BuildKey(type, normalizedPhone, amount, network, productCode);

        InMemoryGuard.TryRemove(key, out _);

        if (_connectionMultiplexer != null && _connectionMultiplexer.IsConnected)
        {
            try
            {
                var db = _connectionMultiplexer.GetDatabase();
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                LogRedisFallbackWarning(_logger, ex.Message);
            }
        }
    }

    private static string BuildKey(VasType type, string normalizedPhone, decimal amount, VasNetwork network, string? productCode)
    {
        var prod = string.IsNullOrWhiteSpace(productCode) ? "none" : productCode.Trim().ToLowerInvariant();
        return $"vas:duplicate:{type.ToString().ToLowerInvariant()}:{normalizedPhone}:{amount.ToString("F2", CultureInfo.InvariantCulture)}:{network.ToString().ToLowerInvariant()}:{prod}";
    }

    private static void CleanExpiredInMemoryEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in InMemoryGuard)
        {
            if (kvp.Value <= now)
            {
                InMemoryGuard.TryRemove(kvp.Key, out _);
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "120-second duplicate VAS purchase blocked for type {Type}, phone {MaskedPhone}, amount {Amount}")]
    private static partial void LogDuplicateBlocked(ILogger logger, string type, string maskedPhone, decimal amount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Redis error encountered in VasDuplicateGuard, operating in fallback mode: {ErrorMessage}")]
    private static partial void LogRedisFallbackWarning(ILogger logger, string errorMessage);
}
