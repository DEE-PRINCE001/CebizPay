using CebizPay.Domain.Vas.Enums;

namespace CebizPay.Application.Common.Interfaces.Vas;

/// <summary>
/// Service responsible for enforcing the 120-second duplicate purchase prevention window
/// across identical phone number + product + amount parameters.
/// </summary>
public interface IVasDuplicateGuard
{
    /// <summary>
    /// Attempts to acquire an atomic 120-second duplicate purchase lock.
    /// Returns true if lock was successfully acquired (no duplicate within window), false if duplicate detected.
    /// </summary>
    Task<bool> TryAcquireDuplicateLockAsync(
        VasType type,
        string phoneNumber,
        decimal amount,
        VasNetwork network,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the duplicate lock early if transaction was aborted prior to commitment (e.g. invalid PIN).
    /// </summary>
    Task ReleaseDuplicateLockAsync(
        VasType type,
        string phoneNumber,
        decimal amount,
        VasNetwork network,
        string? productCode = null,
        CancellationToken cancellationToken = default);
}
