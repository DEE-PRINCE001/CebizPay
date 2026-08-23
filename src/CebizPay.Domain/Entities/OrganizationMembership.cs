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
    /// <summary>Optional assigned department ID.</summary>
    public Guid? DepartmentId { get; private set; }
    /// <summary>Optional assigned workforce job role ID.</summary>
    public Guid? WorkforceRoleId { get; private set; }
    /// <summary>Optional assigned salary level structure ID.</summary>
    public Guid? SalaryLevelId { get; private set; }
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
    public OrganizationMembership(
        string userId,
        Guid organizationId,
        MembershipRoleType role = MembershipRoleType.Member,
        Guid? departmentId = null,
        Guid? workforceRoleId = null,
        Guid? salaryLevelId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        Id = Guid.NewGuid();
        UserId = userId;
        OrganizationId = organizationId;
        Role = role;
        DepartmentId = departmentId;
        WorkforceRoleId = workforceRoleId;
        SalaryLevelId = salaryLevelId;
        Status = MembershipStatus.Active;
        JoinedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns or updates the workforce structure for this membership.
    /// </summary>
    public void AssignWorkforceDetails(Guid? departmentId, Guid? workforceRoleId, Guid? salaryLevelId)
    {
        DepartmentId = departmentId;
        WorkforceRoleId = workforceRoleId;
        SalaryLevelId = salaryLevelId;
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

    /// <summary>
    /// Checks whether this membership possesses a specific organization-level permission.
    /// </summary>
    public bool HasPermission(string permission)
    {
        if (permission == Permissions.Permissions.WalletTransfer || permission == Permissions.Permissions.PayrollExecute ||
            permission == Permissions.Permissions.LoanManagePlan || permission == Permissions.Permissions.SavingsManagePlan ||
            permission == Permissions.Permissions.ThriftManage)
        {
            return Role == MembershipRoleType.Owner || Role == MembershipRoleType.Admin || Role == MembershipRoleType.PayrollManager;
        }
        if (permission == Permissions.Permissions.LoanApprove || permission == Permissions.Permissions.LoanDecide)
        {
            return Role == MembershipRoleType.Owner || Role == MembershipRoleType.Admin;
        }
        if (permission == Permissions.Permissions.PayrollView || permission == Permissions.Permissions.WalletView ||
            permission == Permissions.Permissions.LoanView || permission == Permissions.Permissions.LoanRepaymentView ||
            permission == Permissions.Permissions.LoanCreate || permission == Permissions.Permissions.SavingsView ||
            permission == Permissions.Permissions.SavingsCreate || permission == Permissions.Permissions.SavingsContribute ||
            permission == Permissions.Permissions.SavingsWithdraw || permission == Permissions.Permissions.ThriftView ||
            permission == Permissions.Permissions.ThriftCreate || permission == Permissions.Permissions.ThriftInvite ||
            permission == Permissions.Permissions.ThriftContribute || permission == Permissions.Permissions.ThriftPayoutView)
        {
            return true;
        }
        return Role == MembershipRoleType.Owner;
    }
}
