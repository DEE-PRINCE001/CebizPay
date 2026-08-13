namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Application service for mobile OTP generation, verification, and rate limiting.
/// Constraint: Max 3 requests per device per 15 minutes.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates and sends an OTP to the given phone number with rate-limiting check.
    /// </summary>
    Task<(bool Success, string Code, string? Error)> RequestOtpAsync(string phoneNumber, string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the OTP code for the phone number.
    /// </summary>
    Task<bool> VerifyOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
