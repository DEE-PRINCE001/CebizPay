using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain aggregate representation of a staff invitation.
/// Created by an organization to invite an individual user to join.
/// </summary>
public class StaffInvitation
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Inviting organization ID.</summary>
    public Guid OrganizationId { get; private set; }
    /// <summary>Target user email.</summary>
    public string Email { get; private set; } = string.Empty;
    /// <summary>Invitation secret token/code.</summary>
    public string InvitationCode { get; private set; } = string.Empty;
    /// <summary>Invitation status.</summary>
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;
    /// <summary>Expiration timestamp.</summary>
    public DateTime ExpiresAtUtc { get; private set; }
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Timestamp when recipient responded.</summary>
    public DateTime? RespondedAtUtc { get; private set; }

    private StaffInvitation() { } // EF Core

    /// <summary>
    /// Creates a new staff invitation.
    /// </summary>
    public StaffInvitation(Guid organizationId, string email, TimeSpan? validityWindow = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Target Email is required.", nameof(email));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Email = email.Trim().ToLowerInvariant();
        InvitationCode = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        Status = InvitationStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(validityWindow ?? TimeSpan.FromDays(7));
    }

    /// <summary>
    /// Accepts the staff invitation.
    /// </summary>
    public void Accept(DateTime now)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Cannot accept invitation with status {Status}.");
        if (now > ExpiresAtUtc)
            throw new InvalidOperationException("Invitation has expired.");

        Status = InvitationStatus.Accepted;
        RespondedAtUtc = now;
    }

    /// <summary>
    /// Rejects the staff invitation.
    /// </summary>
    public void Reject(DateTime now)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Cannot reject invitation with status {Status}.");

        Status = InvitationStatus.Rejected;
        RespondedAtUtc = now;
    }

    /// <summary>
    /// Cancels the staff invitation.
    /// </summary>
    public void Cancel()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel invitation with status {Status}.");

        Status = InvitationStatus.Cancelled;
    }

    /// <summary>
    /// Returns true if the invitation is expired or non-pending.
    /// </summary>
    public bool IsExpired(DateTime now) => now > ExpiresAtUtc || Status != InvitationStatus.Pending;
}
