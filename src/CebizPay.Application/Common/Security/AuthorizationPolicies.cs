namespace CebizPay.Application.Common.Security;

/// <summary>
/// Authoritative named authorization policy constants for CebizPay platform and tenant operations.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires active platform SuperAdmin role.</summary>
    public const string RequireSuperAdmin = "RequireSuperAdmin";

    /// <summary>Requires active platform SuperAdmin or delegated Compliance Admin.</summary>
    public const string RequireComplianceAdmin = "RequireComplianceAdmin";

    /// <summary>Requires active platform SuperAdmin or delegated Finance Admin.</summary>
    public const string RequireFinanceAdmin = "RequireFinanceAdmin";

    /// <summary>Requires any active platform administrative role (SuperAdmin or Admin).</summary>
    public const string RequirePlatformAdmin = "RequirePlatformAdmin";

    /// <summary>Requires platform Auditor / read-only administrative role or higher.</summary>
    public const string RequireAuditor = "RequireAuditor";

    /// <summary>Requires organization treasury approval capability (Owner, Admin, or Finance/Payroll Manager).</summary>
    public const string RequireOrganizationFinanceApproval = "RequireOrganizationFinanceApproval";

    /// <summary>Requires organization payroll execution capability (Owner, Admin, or Finance/Payroll Manager).</summary>
    public const string RequirePayrollExecution = "RequirePayrollExecution";

    /// <summary>Requires organization workforce management capability (Owner, Admin, or HR Manager).</summary>
    public const string RequireWorkforceManagement = "RequireWorkforceManagement";
}
