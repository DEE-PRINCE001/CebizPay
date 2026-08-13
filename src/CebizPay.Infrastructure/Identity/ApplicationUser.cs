using Microsoft.AspNetCore.Identity;

namespace CebizPay.Infrastructure.Identity;

/// <summary>
/// Unified ASP.NET Core Identity user model for CebizPay.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Hashed 4-digit transaction PIN.</summary>
    public string? TransactionPinHash { get; set; }

    /// <summary>Failed transaction PIN verification attempts counter.</summary>
    public int FailedPinAttempts { get; set; }

    /// <summary>15-minute transaction PIN debit lockout expiration timestamp.</summary>
    public DateTime? PinLockoutEndUtc { get; set; }

    /// <summary>Serialized list of last 3 password hashes to prevent password reuse.</summary>
    public string PasswordHistoryJson { get; set; } = "[]";
}
