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
    /// <summary>View platform and organization audit logs.</summary>
    public const string AuditView = "Audit.View";

    // Staff & Workforce Management (Org-scoped)
    /// <summary>View organization staff list and profiles.</summary>
    public const string StaffView = "Staff.View";
    /// <summary>Manage organization staff members.</summary>
    public const string StaffManage = "Staff.Manage";
    /// <summary>Directly create / onboard organization staff members.</summary>
    public const string StaffCreate = "Staff.Create";
    /// <summary>Assign or reassign staff department, role, or salary level.</summary>
    public const string StaffAssign = "Staff.Assign";
    /// <summary>Reactivate suspended or terminated staff members.</summary>
    public const string StaffReactivate = "Staff.Reactivate";
    /// <summary>Terminate / offboard organization staff members.</summary>
    public const string StaffTerminate = "Staff.Terminate";
    /// <summary>Send staff invitations.</summary>
    public const string StaffInvite = "Staff.Invite";

    // Department & Role Management (Org-scoped)
    /// <summary>Manage organization departments.</summary>
    public const string DepartmentsManage = "Departments.Manage";
    /// <summary>Manage organization workforce roles.</summary>
    public const string RolesManage = "Roles.Manage";
    /// <summary>Manage organization salary levels.</summary>
    public const string SalaryLevelsManage = "SalaryLevels.Manage";

    // Recruitment & Job Postings (Org-scoped)
    /// <summary>View organization job postings and applications.</summary>
    public const string RecruitmentView = "Recruitment.View";
    /// <summary>Create draft job postings.</summary>
    public const string RecruitmentCreate = "Recruitment.Create";
    /// <summary>Manage and edit job postings.</summary>
    public const string RecruitmentManage = "Recruitment.Manage";
    /// <summary>Review, shortlist, reject, or accept candidate applications.</summary>
    public const string RecruitmentReview = "Recruitment.Review";
    /// <summary>Publish, close, or cancel job postings.</summary>
    public const string RecruitmentClose = "Recruitment.Close";

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
    /// <summary>Execute outbound bank transfers.</summary>
    public const string WalletTransferBank = "Wallet.TransferBank";

    // Loan & PIN Management
    /// <summary>Approve or decline organization loan applications.</summary>
    public const string LoanDecide = "Loan.Decide";
    /// <summary>View loan applications, plans, and contracts.</summary>
    public const string LoanView = "Loan.View";
    /// <summary>Create loan applications.</summary>
    public const string LoanCreate = "Loan.Create";
    /// <summary>Approve loan applications.</summary>
    public const string LoanApprove = "Loan.Approve";
    /// <summary>Manage organization corporate loan plans.</summary>
    public const string LoanManagePlan = "Loan.ManagePlan";
    /// <summary>View loan repayment schedules and tracking.</summary>
    public const string LoanRepaymentView = "Loan.RepaymentView";
    /// <summary>Manage transaction PIN settings.</summary>
    public const string PinManage = "Pin.Manage";

    // Savings
    /// <summary>View savings plans and savings accounts.</summary>
    public const string SavingsView = "Savings.View";
    /// <summary>Create a new savings plan or open a savings account.</summary>
    public const string SavingsCreate = "Savings.Create";
    /// <summary>Contribute funds to an active savings account.</summary>
    public const string SavingsContribute = "Savings.Contribute";
    /// <summary>Withdraw funds from a savings account.</summary>
    public const string SavingsWithdraw = "Savings.Withdraw";
    /// <summary>Manage organization corporate savings plans.</summary>
    public const string SavingsManagePlan = "Savings.ManagePlan";
    /// <summary>Manage platform savings interest policies (Super Admin only).</summary>
    public const string SavingsManagePolicy = "Savings.ManagePolicy";

    // Thrift / Ajo / Esusu
    /// <summary>View thrift groups, cycles, and member positions.</summary>
    public const string ThriftView = "Thrift.View";
    /// <summary>Create a thrift group.</summary>
    public const string ThriftCreate = "Thrift.Create";
    /// <summary>Invite members to a thrift group.</summary>
    public const string ThriftInvite = "Thrift.Invite";
    /// <summary>Manage thrift group configuration and position locking.</summary>
    public const string ThriftManage = "Thrift.Manage";
    /// <summary>Contribute funds to a thrift cycle.</summary>
    public const string ThriftContribute = "Thrift.Contribute";
    /// <summary>View thrift payout history and status.</summary>
    public const string ThriftPayoutView = "Thrift.PayoutView";

    // Announcements
    /// <summary>Publish platform-wide announcements.</summary>
    public const string AnnouncementsPublishPlatform = "Announcements.Publish.Platform";
    /// <summary>Publish organization workplace announcements.</summary>
    public const string AnnouncementsPublishWorkplace = "Announcements.Publish.Workplace";

    // ERP Management & Catalogs
    /// <summary>View organization ERP reports and financial metrics.</summary>
    public const string ErpView = "Erp.View";
    /// <summary>Manage ERP settings and configurations.</summary>
    public const string ErpManage = "Erp.Manage";
    /// <summary>View inventory items, stock levels, and movements.</summary>
    public const string InventoryView = "Inventory.View";
    /// <summary>Create and manage inventory items.</summary>
    public const string InventoryManage = "Inventory.Manage";
    /// <summary>Execute inventory stock in, stock out, and adjustments.</summary>
    public const string InventoryAdjust = "Inventory.Adjust";
    /// <summary>Configure organization inventory valuation policies (WAC / FIFO).</summary>
    public const string InventoryValuationManage = "Inventory.ValuationManage";
    /// <summary>View organization services catalog.</summary>
    public const string ServiceView = "Service.View";
    /// <summary>Create, update, and manage services catalog.</summary>
    public const string ServiceManage = "Service.Manage";
    /// <summary>View organization suppliers.</summary>
    public const string SupplierView = "Supplier.View";
    /// <summary>Create, update, and manage suppliers.</summary>
    public const string SupplierManage = "Supplier.Manage";
    /// <summary>View organization customers.</summary>
    public const string CustomerView = "Customer.View";
    /// <summary>Create, update, and manage customers.</summary>
    public const string CustomerManage = "Customer.Manage";
    /// <summary>View purchase and sales orders.</summary>
    public const string OrdersView = "Orders.View";
    /// <summary>Create, confirm, fulfill, receive, or cancel orders.</summary>
    public const string OrdersManage = "Orders.Manage";
    /// <summary>View operating expenses.</summary>
    public const string ExpensesView = "Expenses.View";
    /// <summary>Create, approve, pay, or cancel operating expenses.</summary>
    public const string ExpensesManage = "Expenses.Manage";
    /// <summary>View invoices and customer billing.</summary>
    public const string InvoicesView = "Invoices.View";
    /// <summary>Create, issue, pay, or cancel invoices.</summary>
    public const string InvoicesManage = "Invoices.Manage";
    /// <summary>View payment receipts.</summary>
    public const string ReceiptsView = "Receipts.View";
    /// <summary>View company vouchers.</summary>
    public const string CompanyVouchersView = "CompanyVouchers.View";
    /// <summary>Create company vouchers.</summary>
    public const string CompanyVouchersCreate = "CompanyVouchers.Create";
    /// <summary>Approve company vouchers.</summary>
    public const string CompanyVouchersApprove = "CompanyVouchers.Approve";
    /// <summary>Pay or settle company vouchers.</summary>
    public const string CompanyVouchersPay = "CompanyVouchers.Pay";
    /// <summary>View ERP financial and operational reports.</summary>
    public const string ReportsView = "Reports.View";

    // Platform Fee Policy (Super Admin only)
    /// <summary>
    /// Create, activate, or deactivate platform peer-transfer fee policies.
    /// Super Admin only — not granted to ordinary Admins or Auditors by default.
    /// </summary>
    public const string FeesManagePeerTransferPolicy = "Fees.ManagePeerTransferPolicy";
    /// <summary>
    /// Create, activate, or deactivate platform bank-transfer fee policies.
    /// Super Admin only — not granted to ordinary Admins or Auditors by default.
    /// </summary>
    public const string FeesManageBankTransferPolicy = "Fees.ManageBankTransferPolicy";

    // Value-Added Services (VAS - Airtime & Data)
    /// <summary>View VAS transactions and catalog bundles.</summary>
    public const string VasView = "Vas.View";
    /// <summary>Purchase airtime and mobile data bundles.</summary>
    public const string VasPurchase = "Vas.Purchase";

    /// <summary>
    /// Read-only permissions for platform auditor / view-only roles.
    /// </summary>
    public static readonly IReadOnlySet<string> ReadOnlyAdminPermissions = new HashSet<string>
    {
        KycView, KybView, OrganizationsView, TransactionsView, PayrollLogsView, AuditView,
        StaffView, PayrollView, WalletView, ErpView, LoanView, LoanRepaymentView,
        SavingsView, ThriftView, ThriftPayoutView, VasView, RecruitmentView,
        InventoryView, ServiceView, SupplierView, CustomerView,
        OrdersView, ExpensesView, InvoicesView, ReceiptsView,
        CompanyVouchersView, ReportsView
    };

    /// <summary>
    /// Default permissions assigned to Organization Owner (ORG_SUPER_ADMIN / CEO).
    /// </summary>
    public static readonly IReadOnlySet<string> OrgSuperAdminPermissions = new HashSet<string>
    {
        KybView, StaffView, StaffManage, StaffCreate, StaffAssign, StaffReactivate, StaffTerminate, StaffInvite,
        DepartmentsManage, RolesManage, SalaryLevelsManage,
        RecruitmentView, RecruitmentCreate, RecruitmentManage, RecruitmentReview, RecruitmentClose,
        PayrollView, PayrollExecute,
        WalletView, WalletFund, WalletTransfer, WalletTransferBank,
        LoanDecide, LoanView, LoanCreate, LoanApprove, LoanManagePlan, LoanRepaymentView,
        SavingsView, SavingsCreate, SavingsContribute, SavingsWithdraw, SavingsManagePlan,
        ThriftView, ThriftCreate, ThriftInvite, ThriftManage, ThriftContribute, ThriftPayoutView,
        VasView, VasPurchase,
        InventoryView, InventoryManage, InventoryAdjust, InventoryValuationManage,
        ServiceView, ServiceManage, SupplierView, SupplierManage, CustomerView, CustomerManage,
        OrdersView, OrdersManage, ExpensesView, ExpensesManage, InvoicesView, InvoicesManage, ReceiptsView,
        CompanyVouchersView, CompanyVouchersCreate, CompanyVouchersApprove, CompanyVouchersPay, ReportsView,
        PinManage, AnnouncementsPublishWorkplace, ErpView, ErpManage
    };

    /// <summary>
    /// Default permissions assigned to Finance Manager role.
    /// </summary>
    public static readonly IReadOnlySet<string> FinanceManagerPermissions = new HashSet<string>
    {
        WalletView, WalletFund, WalletTransfer, WalletTransferBank, PayrollView, PayrollExecute, ErpView, TransactionsView,
        LoanView, LoanRepaymentView, SavingsView, SavingsManagePlan, ThriftView, ThriftPayoutView, VasView, VasPurchase,
        InventoryView, InventoryManage, InventoryAdjust, InventoryValuationManage,
        ServiceView, ServiceManage, SupplierView, SupplierManage, CustomerView, CustomerManage,
        OrdersView, OrdersManage, ExpensesView, ExpensesManage, InvoicesView, InvoicesManage, ReceiptsView,
        CompanyVouchersView, CompanyVouchersCreate, CompanyVouchersApprove, CompanyVouchersPay, ReportsView
    };

    /// <summary>
    /// Default permissions assigned to HR Manager role.
    /// </summary>
    public static readonly IReadOnlySet<string> HrManagerPermissions = new HashSet<string>
    {
        StaffView, StaffManage, StaffCreate, StaffAssign, StaffReactivate, StaffTerminate, StaffInvite,
        DepartmentsManage, RolesManage, SalaryLevelsManage,
        RecruitmentView, RecruitmentCreate, RecruitmentManage, RecruitmentReview, RecruitmentClose,
        AnnouncementsPublishWorkplace,
        LoanView, SavingsView, ThriftView
    };
}
