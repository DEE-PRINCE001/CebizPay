using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Explicit membership entity connecting a User to an Organization.
/// Supports multiple organization memberships per user.
/// Staff work suspension belongs strictly to the organization relationship and does NOT affect personal identity or personal wallet.
/// </summary>
public class OrganizationMembership
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>User ID string matching Identity ApplicationUser Id.</summary>
    public string UserId { get; private set; } = string.Empty;
    /// <summary>Organization ID.</summary>
    public Guid OrganizationId { get; private set; }
    /// <summary>Membership role within organization.</summary>
    public MembershipRoleType Role { get; private set; }
    /// <summary>Membership status (Active / Suspended).</summary>
    public MembershipStatus Status { get; private set; } = MembershipStatus.Active;
    /// <summary>Joined timestamp.</summary>
    public DateTime JoinedAtUtc { get; private set; }
    /// <summary>Suspension timestamp.</summary>
    public DateTime? SuspendedAtUtc { get; private set; }
    /// <summary>Reason for staff work suspension.</summary>
    public string? SuspensionReason { get; private set; }

    private OrganizationMembership() { } // EF Core

    /// <summary>
    /// Creates a new organization membership.
    /// </summary>
    public OrganizationMembership(string userId, Guid organizationId, MembershipRoleType role = MembershipRoleType.Member)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        Id = Guid.NewGuid();
        UserId = userId;
        OrganizationId = organizationId;
        Role = role;
        Status = MembershipStatus.Active;
        JoinedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspends staff work access for this organization relationship.
    /// Does NOT suspend user's personal identity or wallet.
    /// </summary>
    public void SuspendWorkAccess(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Suspension reason is required.", nameof(reason));

        Status = MembershipStatus.Suspended;
        SuspendedAtUtc = DateTime.UtcNow;
        SuspensionReason = reason.Trim();
    }

    /// <summary>
    /// Reactivates staff work access.
    /// </summary>
    public void ReactivateWorkAccess()
    {
        Status = MembershipStatus.Active;
        SuspendedAtUtc = null;
        SuspensionReason = null;
    }

    /// <summary>
    /// Changes the role within the organization.
    /// </summary>
    public void ChangeRole(MembershipRoleType newRole)
    {
        Role = newRole;
    }

    /// <summary>
    /// Returns true if membership is active.
    /// </summary>
    public bool IsActiveWorkplaceMember() => Status == MembershipStatus.Active;
}
