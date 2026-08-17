using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure service implementing 4-digit transaction PIN security rules using BCrypt hashing.
/// Server-side bcrypt-hashed PIN, 3 failed attempts -> 15-minute debit lockout window.
/// </summary>
public sealed class TransactionPinService : ITransactionPinService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext? _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="TransactionPinService"/>.
    /// </summary>
    public TransactionPinService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext? dbContext = null)
    {
        _userManager = userManager;
        _dbContext = dbContext;
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
        // 1. Fetch user to verify hash & current lockout status
        ApplicationUser? initialUser = null;
        if (_dbContext != null)
        {
            initialUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        }
        else
        {
            initialUser = await _userManager.FindByIdAsync(userId);
        }

        if (initialUser == null)
        {
            return (false, false, "User not found.");
        }

        if (string.IsNullOrEmpty(initialUser.TransactionPinHash))
        {
            return (false, false, "Transaction PIN has not been set.");
        }

        // Check if currently locked out
        if (initialUser.PinLockoutEndUtc.HasValue && initialUser.PinLockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining = Math.Ceiling((initialUser.PinLockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            return (false, true, $"Transaction PIN debit lock active due to 3 failed attempts. Try again in {remaining} minutes.");
        }

        // 2. Perform BCrypt hash verification in memory BEFORE row lock
        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(pin, initialUser.TransactionPinHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            isValid = false;
        }

        // 3. Atomically update counter / lockout status in database
        if (_dbContext != null)
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var user = _dbContext.Database.IsNpgsql()
                ? await _dbContext.Users.FromSqlRaw("SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", userId).FirstOrDefaultAsync(cancellationToken)
                : await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                await tx.RollbackAsync(cancellationToken);
                return (false, false, "User not found.");
            }

            if (user.PinLockoutEndUtc.HasValue && user.PinLockoutEndUtc.Value > DateTime.UtcNow)
            {
                var remaining = Math.Ceiling((user.PinLockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
                await tx.RollbackAsync(cancellationToken);
                return (false, true, $"Transaction PIN debit lock active due to 3 failed attempts. Try again in {remaining} minutes.");
            }

            if (!isValid)
            {
                user.FailedPinAttempts++;
                if (user.FailedPinAttempts >= 3)
                {
                    user.PinLockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    _dbContext.Update(user);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return (false, true, "Transaction PIN debit lock activated for 15 minutes due to 3 failed attempts.");
                }

                _dbContext.Update(user);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return (false, false, $"Invalid transaction PIN. Attempts remaining: {3 - user.FailedPinAttempts}.");
            }

            // Reset failed PIN attempts on clean match
            if (user.FailedPinAttempts > 0 || user.PinLockoutEndUtc.HasValue)
            {
                user.FailedPinAttempts = 0;
                user.PinLockoutEndUtc = null;
                _dbContext.Update(user);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return (true, false, null);
        }
        else
        {
            if (!isValid)
            {
                initialUser.FailedPinAttempts++;
                if (initialUser.FailedPinAttempts >= 3)
                {
                    initialUser.PinLockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    await _userManager.UpdateAsync(initialUser);
                    return (false, true, "Transaction PIN debit lock activated for 15 minutes due to 3 failed attempts.");
                }

                await _userManager.UpdateAsync(initialUser);
                return (false, false, $"Invalid transaction PIN. Attempts remaining: {3 - initialUser.FailedPinAttempts}.");
            }

            if (initialUser.FailedPinAttempts > 0 || initialUser.PinLockoutEndUtc.HasValue)
            {
                initialUser.FailedPinAttempts = 0;
                initialUser.PinLockoutEndUtc = null;
                await _userManager.UpdateAsync(initialUser);
            }

            return (true, false, null);
        }
    }
}
