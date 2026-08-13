namespace CebizPay.Domain.Permissions;

/// <summary>
/// Authoritative catalog of granular permissions across CebizPay platform and organization domains.
/// </summary>
public static class Permissions
{
    // KYC / KYB Admin Review
    /// <summary>View KYC documents and profiles.</summary>
    public const string KycView = "Kyc.View";
    /// <summary>Approve or reject KYC submissions.</summary>
    public const string KycReview = "Kyc.Review";
    /// <summary>View KYB detail submissions and organization compliance profiles.</summary>
    public const string KybView = "Kyb.View";
    /// <summary>Approve or reject KYB submissions.</summary>
    public const string KybReview = "Kyb.Review";

    // Organization Admin Management
    /// <summary>View organizations and detail profiles.</summary>
    public const string OrganizationsView = "Organizations.View";
    /// <summary>Suspend organizations.</summary>
    public const string OrganizationsSuspend = "Organizations.Suspend";
    /// <summary>Reactivate suspended organizations.</summary>
    public const string OrganizationsReactivate = "Organizations.Reactivate";
    /// <summary>Grant organization profile edit permission.</summary>
    public const string OrganizationsGrantEditPermission = "Organizations.GrantEditPermission";

    // Platform Admin Management
    /// <summary>Invite new administrative users.</summary>
    public const string AdminsInvite = "Admins.Invite";
    /// <summary>Delete administrative user profiles.</summary>
    public const string AdminsDelete = "Admins.Delete";
    /// <summary>Activate or deactivate administrative profiles.</summary>
    public const string AdminsToggleStatus = "Admins.ToggleStatus";
    /// <summary>Grant or revoke delegated permissions for admins.</summary>
    public const string AdminsManagePermissions = "Admins.ManagePermissions";

    // Auditing / Logs
    /// <summary>View platform financial transaction logs.</summary>
    public const string TransactionsView = "Transactions.View";
    /// <summary>View platform payroll logs.</summary>
    public const string PayrollLogsView = "PayrollLogs.View";

    // Staff & Workforce Management (Org-scoped)
    /// <summary>View organization staff list and profiles.</summary>
    public const string StaffView = "Staff.View";
    /// <summary>Manage organization staff members.</summary>
    public const string StaffManage = "Staff.Manage";
    /// <summary>Send staff invitations.</summary>
    public const string StaffInvite = "Staff.Invite";

    // Department & Role Management (Org-scoped)
    /// <summary>Manage organization departments.</summary>
    public const string DepartmentsManage = "Departments.Manage";
    /// <summary>Manage organization workforce roles.</summary>
    public const string RolesManage = "Roles.Manage";
    /// <summary>Manage organization salary levels.</summary>
    public const string SalaryLevelsManage = "SalaryLevels.Manage";

    // Payroll Operations (Org-scoped)
    /// <summary>View organization payroll runs.</summary>
    public const string PayrollView = "Payroll.View";
    /// <summary>Execute outbound organization payroll runs.</summary>
    public const string PayrollExecute = "Payroll.Execute";

    // Wallet Operations
    /// <summary>View wallet details and balances.</summary>
    public const string WalletView = "Wallet.View";
    /// <summary>Fund wallet balance.</summary>
    public const string WalletFund = "Wallet.Fund";
    /// <summary>Execute outbound wallet transfers.</summary>
    public const string WalletTransfer = "Wallet.Transfer";

    // Loan & PIN Management
    /// <summary>Approve or decline organization loan applications.</summary>
    public const string LoanDecide = "Loan.Decide";
    /// <summary>Manage transaction PIN settings.</summary>
    public const string PinManage = "Pin.Manage";

    // Announcements
    /// <summary>Publish platform-wide announcements.</summary>
    public const string AnnouncementsPublishPlatform = "Announcements.Publish.Platform";
    /// <summary>Publish organization workplace announcements.</summary>
    public const string AnnouncementsPublishWorkplace = "Announcements.Publish.Workplace";

    // ERP Management
    /// <summary>View organization ERP reports and financial metrics.</summary>
    public const string ErpView = "Erp.View";
    /// <summary>Manage ERP settings and configurations.</summary>
    public const string ErpManage = "Erp.Manage";

    /// <summary>
    /// Default read-only permissions assigned to Read-Only Admin / Auditor role.
    /// </summary>
    public static readonly IReadOnlySet<string> ReadOnlyAdminPermissions = new HashSet<string>
    {
        KycView, KybView, OrganizationsView, TransactionsView, PayrollLogsView,
        StaffView, PayrollView, WalletView, ErpView
    };

    /// <summary>
    /// Default permissions assigned to Organization Owner (ORG_SUPER_ADMIN / CEO).
    /// </summary>
    public static readonly IReadOnlySet<string> OrgSuperAdminPermissions = new HashSet<string>
    {
        KybView, StaffView, StaffManage, StaffInvite, DepartmentsManage, RolesManage,
        SalaryLevelsManage, PayrollView, PayrollExecute, WalletView, WalletFund, WalletTransfer,
        LoanDecide, PinManage, AnnouncementsPublishWorkplace, ErpView, ErpManage
    };

    /// <summary>
    /// Default permissions assigned to Finance Manager role.
    /// </summary>
    public static readonly IReadOnlySet<string> FinanceManagerPermissions = new HashSet<string>
    {
        WalletView, WalletFund, WalletTransfer, PayrollView, PayrollExecute, ErpView, TransactionsView
    };

    /// <summary>
    /// Default permissions assigned to HR Manager role.
    /// </summary>
    public static readonly IReadOnlySet<string> HrManagerPermissions = new HashSet<string>
    {
        StaffView, StaffManage, StaffInvite, DepartmentsManage, RolesManage, SalaryLevelsManage, AnnouncementsPublishWorkplace
    };
}
