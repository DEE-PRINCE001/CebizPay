namespace CebizPay.Domain.Enums;

/// <summary>
/// Role of a user within an organization membership.
/// </summary>
public enum MembershipRoleType
{
    /// <summary>Organization owner / CEO (ORG_SUPER_ADMIN).</summary>
    Owner = 1,
    /// <summary>Organization admin role.</summary>
    Admin = 2,
    /// <summary>Standard organization member role.</summary>
    Member = 3,
    /// <summary>Finance / Payroll manager role (FINANCE_MANAGER).</summary>
    PayrollManager = 4,
    /// <summary>HR manager role (HR_MANAGER).</summary>
    HrManager = 5
}
