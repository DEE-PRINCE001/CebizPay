using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure service implementing 4-digit transaction PIN security rules using BCrypt hashing.
/// Server-side bcrypt-hashed PIN, 3 failed attempts -> 15-minute debit lockout window.
/// </summary>
public sealed class TransactionPinService : ITransactionPinService
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initializes a new instance of <see cref="TransactionPinService"/>.
    /// </summary>
    public TransactionPinService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, string? Error)> SetPinAsync(string userId, string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
        {
            return (false, "PIN must be exactly 4 numeric digits.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        user.TransactionPinHash = BCrypt.Net.BCrypt.HashPassword(pin);
        user.FailedPinAttempts = 0;
        user.PinLockoutEndUtc = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return (true, null);
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, bool IsLocked, string? Error)> VerifyPinAsync(string userId, string pin, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, false, "User not found.");
        }

        if (string.IsNullOrEmpty(user.TransactionPinHash))
        {
            return (false, false, "Transaction PIN has not been set.");
        }

        // Check if currently locked out
        if (user.PinLockoutEndUtc.HasValue && user.PinLockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining = Math.Ceiling((user.PinLockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            return (false, true, $"Transaction PIN debit lock active due to 3 failed attempts. Try again in {remaining} minutes.");
        }

        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(pin, user.TransactionPinHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            isValid = false;
        }

        if (!isValid)
        {
            user.FailedPinAttempts++;
            if (user.FailedPinAttempts >= 3)
            {
                user.PinLockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                await _userManager.UpdateAsync(user);
                return (false, true, "Transaction PIN debit lock activated for 15 minutes due to 3 failed attempts.");
            }

            await _userManager.UpdateAsync(user);
            return (false, false, $"Invalid transaction PIN. Attempts remaining: {3 - user.FailedPinAttempts}.");
        }

        // Reset failed PIN attempts on clean match
        if (user.FailedPinAttempts > 0 || user.PinLockoutEndUtc.HasValue)
        {
            user.FailedPinAttempts = 0;
            user.PinLockoutEndUtc = null;
            await _userManager.UpdateAsync(user);
        }

        return (true, false, null);
    }
}
