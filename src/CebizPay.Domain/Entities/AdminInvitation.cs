using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain aggregate representation of an administrative user invitation.
/// Created by a Super Admin with a secure single-use 24-hour invitation token.
/// </summary>
public class AdminInvitation
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Normalized recipient email address.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Intended administrative role type.</summary>
    public AdminRoleType Role { get; private set; }

    /// <summary>Cryptographic SHA-256 hash of the 24-hour invitation token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Current invitation lifecycle status.</summary>
    public AdminInvitationStatus Status { get; private set; } = AdminInvitationStatus.Pending;

    /// <summary>Super Admin user ID who issued the invitation.</summary>
    public string InvitedByUserId { get; private set; } = string.Empty;

    /// <summary>Expiration timestamp (24 hours from creation).</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp when the invitation was successfully redeemed.</summary>
    public DateTime? RedeemedAtUtc { get; private set; }

    /// <summary>Identity user ID created upon redemption.</summary>
    public string? RedeemedByUserId { get; private set; }

    private AdminInvitation() { } // EF Core

    /// <summary>
    /// Creates a new admin invitation.
    /// </summary>
    public AdminInvitation(
        string email,
        AdminRoleType role,
        string tokenHash,
        string invitedByUserId,
        TimeSpan? validityWindow = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("TokenHash is required.", nameof(tokenHash));
        if (string.IsNullOrWhiteSpace(invitedByUserId))
            throw new ArgumentException("InvitedByUserId is required.", nameof(invitedByUserId));

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        Role = role;
        TokenHash = tokenHash.Trim();
        InvitedByUserId = invitedByUserId.Trim();
        Status = AdminInvitationStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(validityWindow ?? TimeSpan.FromHours(24));
    }

    /// <summary>
    /// Redeems the invitation and binds it to the newly created admin user ID.
    /// </summary>
    public void Redeem(string userId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (Status != AdminInvitationStatus.Pending)
            throw new InvalidOperationException($"Cannot redeem invitation with status '{Status}'.");

        if (now > ExpiresAtUtc)
        {
            Status = AdminInvitationStatus.Expired;
            throw new InvalidOperationException("Invitation has expired.");
        }

        Status = AdminInvitationStatus.Redeemed;
        RedeemedByUserId = userId.Trim();
        RedeemedAtUtc = now;
    }

    /// <summary>
    /// Cancels the invitation before redemption.
    /// </summary>
    public void Cancel(DateTime now)
    {
        if (Status != AdminInvitationStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel invitation with status '{Status}'.");

        Status = AdminInvitationStatus.Cancelled;
    }

    /// <summary>
    /// Marks the invitation as expired.
    /// </summary>
    public void MarkExpired()
    {
        if (Status == AdminInvitationStatus.Pending)
        {
            Status = AdminInvitationStatus.Expired;
        }
    }

    /// <summary>
    /// Checks if the invitation is expired or no longer pending.
    /// </summary>
    public bool IsExpired(DateTime now) => now > ExpiresAtUtc || Status != AdminInvitationStatus.Pending;
}
