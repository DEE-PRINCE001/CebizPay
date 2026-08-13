using System.Globalization;
using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Application.Common.Interfaces.Security;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of OTP generation, verification, and rate limiting backed by Redis.
/// Rate-limiting constraint: Max 3 request attempts per device per 15 minutes.
/// </summary>
public sealed class RedisOtpService : IOtpService
{
    private readonly ICacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisOtpService"/>.
    /// </summary>
    public RedisOtpService(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string Code, string? Error)> RequestOtpAsync(string phoneNumber, string deviceId, CancellationToken cancellationToken = default)
    {
        var rateLimitKey = $"otp_rate_limit:{deviceId}";
        var currentCount = await _cacheService.GetAsync<int?>(rateLimitKey, cancellationToken) ?? 0;

        if (currentCount >= 3)
        {
            return (false, string.Empty, "Maximum 3 OTP requests allowed per device per 15 minutes.");
        }

        // Increment count and set 15-minute sliding window
        await _cacheService.SetAsync(rateLimitKey, currentCount + 1, TimeSpan.FromMinutes(15), cancellationToken);

        // Generate 6-digit OTP
        var random = new Random();
        var code = random.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);

        var otpKey = $"otp_code:{phoneNumber}";
        await _cacheService.SetAsync(otpKey, code, TimeSpan.FromMinutes(5), cancellationToken);

        return (true, code, null);
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        var otpKey = $"otp_code:{phoneNumber}";
        var storedCode = await _cacheService.GetAsync<string>(otpKey, cancellationToken);

        if (string.IsNullOrEmpty(storedCode) || storedCode != code.Trim())
        {
            return false;
        }

        // Remove OTP code once verified
        await _cacheService.RemoveAsync(otpKey, cancellationToken);
        return true;
    }
}
