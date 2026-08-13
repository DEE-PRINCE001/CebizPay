namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Domain/Application contract for transaction PIN security management.
/// Rules: 4-digit PIN, hashed server-side, 3 failed attempts trigger 15-minute debit lock.
/// </summary>
public interface ITransactionPinService
{
    /// <summary>
    /// Sets or updates a 4-digit transaction PIN for a user.
    /// </summary>
    Task<(bool Succeeded, string? Error)> SetPinAsync(string userId, string pin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a 4-digit transaction PIN.
    /// Tracks failed attempts. Lockout occurs after 3 consecutive failed attempts for 15 minutes.
    /// </summary>
    Task<(bool Succeeded, bool IsLocked, string? Error)> VerifyPinAsync(string userId, string pin, CancellationToken cancellationToken = default);
}
