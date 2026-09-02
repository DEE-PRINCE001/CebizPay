# CebizPay API to UI Mapping Specification

## 1. Overview & Architecture Strategy

This document maps the **264 backend API endpoints** across **48 controllers** directly to user interface screens, forms, modals, tables, and workflows in the CebizPay frontend application.

The primary objective is to guarantee that every web-facing backend capability is fully represented in the frontend with zero orphaned endpoints and zero simulated mock data.

---

## 2. Feature-by-Feature API to UI Mapping

### 2.1 Authentication & User Onboarding

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **Email Login** | `POST /api/v1/auth/login` | `LoginPage` | `Account.png` (D007) | `{ email, password }`, loading indicator, rate-limit error banner |
| **MFA Verification** | `POST /api/v1/auth/mfa/verify` | `MfaModal` / `MfaVerifyPage` | `iPhone 14 Pro - 101.png` (D417) | `{ mfaChallengeToken, code }`, 60s countdown timer |
| **Toggle MFA** | `POST /api/v1/auth/mfa/toggle` | `SecuritySettingsTab` | `Profile-1.png` (D311) | `{ enable: boolean, currentPassword }`, toast notification |
| **Phone OTP Register** | `POST /api/v1/auth/register/phone` | `RegisterPhonePage` | `Create Form.png` (D075) | `{ phoneNumber }`, phone format validator |
| **Verify OTP & Finish** | `POST /api/v1/auth/register/otp/verify` | `VerifyOtpPage` | `Create Form.png` (D075) | `{ phoneNumber, otpCode, password, fullName }` |
| **Redeem Admin Invite** | `POST /api/v1/auth/admin/redeem-invite` | `RedeemAdminInvitePage` | `add New Admin.png` (D372) | `{ inviteToken, password, fullName }` |
| **Change Password** | `POST /api/v1/auth/change-password` | `SecuritySettingsTab` | `Profile.png` (D312) | `{ currentPassword, newPassword, confirmPassword }` |

---

### 2.2 Wallet Operations, Transfers & Funding

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **View Wallet Balance** | `GET /api/v1/virtual-accounts/primary` | `WalletOverviewPage` | `Organisations Wallet.png` (D274) | Account number, Bank name, Available/Ledger balance |
| **Resolve Destination Bank**| `GET /api/v1/wallet/transfer/resolve-account` | `BankTransferModal` | `Transfer to bank.png` (D352) | `{ bankCode, accountNumber }`, resolved account name badge |
| **Bank NIP Transfer** | `POST /api/v1/wallet/transfer/bank` | `BankTransferModal` | `Transfer to bank-1.png` (D347) | `{ destinationBankCode, destinationAccountNumber, amount, narration, transactionPin }`, Idempotency key |
| **P2P Peer Transfer** | `POST /api/v1/wallet/transfer/peer` | `PeerTransferModal` | `Transfer Fund.png` (D343) | `{ recipientPhoneOrEmail, amount, narration, transactionPin }`, Idempotency key |
| **View External Accounts**| `GET /api/v1/wallet/external-accounts` | `FundingSettingsTab` | `Wallet Org..png` (D357) | List of linked reserved Monnify virtual accounts |
| **Provision Monnify DVA** | `POST /api/v1/wallet/external-accounts/monnify` | `ProvisionAccountModal`| `Wallet Org.-1.png` (D356) | `{ currency, organizationId }`, copyable account details |
| **Set Primary Funding Acct**| `POST /api/v1/wallet/external-accounts/{id}/primary` | `FundingAccountsList` | `Wallet Org..png` (D357) | Target account ID, update active radio selector |
| **Card Funding Init** | `POST /api/v1/funding/card/initialize` | `CardFundingModal` | `Add card.png` (D022) | `{ amount, gateway: 'Flutterwave'|'Paystack', returnUrl }` |
| **Charge Saved Card** | `POST /api/v1/funding/card/charge-saved` | `ChargeSavedCardModal` | `iPhone 14 Pro - 98.png` (D421) | `{ savedCardId, amount, transactionPin }` |
| **List Saved Cards** | `GET /api/v1/saved-cards` | `SavedCardsSection` | `Card management.png` (D043) | List of tokenized cards (Masked PAN, Brand, Expiry) |
| **Set Default Card** | `POST /api/v1/saved-cards/{id}/default`| `SavedCardsSection` | `Card management.png` (D043) | Target card ID, active state badge |
| **Delete Saved Card** | `DELETE /api/v1/saved-cards/{id}` | `DeleteCardConfirmModal`| `Delete Subject.png` (D108) | Confirmation dialog, optimistic removal from state |

---

### 2.3 Value-Added Services (VAS / Bill Payments)

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **Detect Operator** | `GET /api/v1/vas/operators/detect` | `VasAirtimeDataModal` | `Create Form.png` (D075) | `{ phoneNumber }` -> returns detected telco (MTN, Airtel, Glo, 9mobile) |
| **Get Data Bundles** | `GET /api/v1/vas/data/bundles` | `VasDataBundleSelect` | `Categories.png` (D047) | `{ network }` -> list of available data plans (Price, Validity, Volume) |
| **Purchase Airtime** | `POST /api/v1/vas/airtime` | `AirtimePurchaseForm` | `Transfer Fund.png` (D343) | `{ phoneNumber, operator, amount, transactionPin }`, Idempotency key |
| **Purchase Data Bundle** | `POST /api/v1/vas/data` | `DataPurchaseForm` | `Transfer Fund.png` (D343) | `{ phoneNumber, operator, dataPlanCode, amount, transactionPin }`, Idempotency key |
| **Get VAS Transaction** | `GET /api/v1/vas/transactions/{id}` | `VasReceiptModal` | `done.png` (D408) | Transaction ID, token/PIN, recharge status |

---

### 2.4 Corporate Payroll & Payment Vouchers

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **Preview Payroll Run** | `POST /api/v1/org/payroll/calculate` | `PayrollPreviewModal` | `Pay by level.png` (D290) | `{ payPeriodMonth, payPeriodYear, departmentId? }` -> Dry-run total, tax, net pay |
| **Execute Payroll Batch**| `POST /api/v1/org/payroll/execute` | `PayrollExecuteDialog` | `Pay all.png` (D288) | `{ payPeriodMonth, payPeriodYear, transactionPin }`, enqueues background worker |
| **View Batch Progress** | `GET /api/v1/org/payroll/{batchId}` | `PayrollBatchStatusPage`| `Payroll(Schedule.png` (D302) | Polling state (`TotalEmployees`, `ProcessedCount`, `FailedCount`, `Status`) |
| **Retry Failed Items** | `POST /api/v1/org/payroll/{batchId}/retry-failed` | `PayrollBatchStatusPage`| `Payroll(Schedule-1.png` (D299) | Batch ID, retry progress indicator |
| **Cancel Pending Run** | `POST /api/v1/org/payroll/{batchId}/cancel` | `CancelPayrollConfirm` | `Delete Subject.png` (D108) | Batch ID, cancels if in `Pending` status |
| **Get Payment Voucher** | `GET /api/v1/org/payroll/vouchers/{id}`| `PaymentVoucherModal` | `view voucher.png` (D479) | Voucher ID, tenant isolation check, PDF export |
| **Update Voucher Meta** | `PUT /api/v1/org/payroll/vouchers/{id}`| `EditVoucherDrawer` | `Edit voucher.png` (D149) | `{ bankName, remarks, description }` |

---

### 2.5 Workforce, Staff & Department Management

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **List Staff Roster** | `GET /api/v1/org/staff` | `StaffRosterPage` | `Staff.png` (D331) | Search, department filter, salary level filter, pagination |
| **Invite Staff Member** | `POST /api/v1/org/staff/invite` | `InviteStaffModal` | `Add New Member.png` (D011) | `{ email, firstName, lastName, departmentId, roleId, salaryLevelId }` |
| **Accept Invitation** | `POST /api/v1/work/organisation/join` | `AcceptInvitePage` | `Invite code.png` (D248) | `{ inviteCode, password }` |
| **Suspend Staff** | `POST /api/v1/org/staff/{id}/suspend` | `SuspendStaffModal` | `Delete Subject-1.png` (D090) | Staff ID, reason for suspension |
| **List Departments** | `GET /api/v1/org/departments` | `DepartmentsPage` | `Departments.png` (D109) | Department list with staff count |
| **Create Department** | `POST /api/v1/org/departments` | `CreateDeptModal` | `Create Depts.png` (D074) | `{ name, description, managerId? }` |
| **List Salary Levels** | `GET /api/v1/org/salary-levels` | `SalaryLevelsPage` | `All lev.png` (D031) | List of compensation grades and step levels |
| **Create Salary Level** | `POST /api/v1/org/salary-levels` | `CreateSalaryLevelModal`| `Create Level.png` (D077) | `{ name, grade, baseSalary, allowances, deductions }` |
| **List Workforce Roles**| `GET /api/v1/org/roles` | `RolesAndPermissionsPage`| `Roles.png` (D320) | Role definitions and assigned staff |
| **Create Workforce Role**| `POST /api/v1/org/roles` | `CreateRoleModal` | `Add New Role.png` (D012) | `{ name, description, permissionIds: [] }` |

---

### 2.6 ERP: Inventory, Catalog & Procurement

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **List Inventory Items**| `GET /api/v1/org/inventory/items` | `InventoryItemsPage` | `items inventory.png` (D433) | Search, Category, StockStatus filter, Pagination |
| **Create Inventory Item**| `POST /api/v1/org/inventory/items` | `CreateItemDrawer` | `add items.png` (D374) | `{ sku, name, unitOfMeasure, sellingPrice, category, reorderLevel, initialQty }` |
| **Stock-In Movement** | `POST /api/v1/org/inventory/items/{id}/stock-in` | `StockInModal` | `Add Purchase.png` (D019) | `{ quantity, unitCost, reference, reason }` |
| **Stock-Out Movement** | `POST /api/v1/org/inventory/items/{id}/stock-out`| `StockOutModal` | `Add Sales 2.png` (D020) | `{ quantity, reference, reason }` |
| **Adjust Stock** | `POST /api/v1/org/inventory/items/{id}/adjust` | `StockAdjustModal` | `edit-1.png` (D409) | `{ quantityDelta, reason, newAverageCost }` |
| **Get Valuation Policy** | `GET /api/v1/org/inventory/valuation-policy` | `ValuationPolicyTab` | `Policy Type.png` (D305) | Current valuation method (`FIFO` / `WAC`) |
| **Set Valuation Policy** | `POST /api/v1/org/inventory/valuation-policy`| `ValuationPolicyTab` | `Policy Type.png` (D305) | `{ method: 'FIFO' | 'WeightedAverageCost' }` |

---

### 2.7 ERP: Invoicing, Receipts & Sales

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **List Invoices** | `GET /api/v1/org/invoices` | `InvoicesListPage` | `Invoice generator.png` (D251) | Metric split badges (Total, Open, Closed), Search, Pagination |
| **Create Invoice** | `POST /api/v1/org/invoices` | `CreateInvoicePage` | `Invoice generator.png` (D251) | `{ customerId, issueDate, dueDate, items: [{ description, qty, unitPrice }], taxRate }` |
| **Send Invoice Email** | `POST /api/v1/org/invoices/{id}/send` | `SendInvoiceDialog` | `Send Invoice.png` (D325) | Invoice ID, recipient email, optional custom note |
| **View Invoice Printable**| `GET /api/v1/org/invoices/{id}/render`| `InvoiceDetailsView` | `view invoice Order.png` (D475) | Printable HTML/PDF view, company branding |
| **List Sales Orders** | `GET /api/v1/org/orders` | `OrdersRegistryPage` | `order inventory.png` (D443) | Order status filter, customer search, date range filter |
| **Create Sales Order** | `POST /api/v1/org/orders` | `CreateOrderDrawer` | `Add ORDER.png` (D015) | `{ customerId, lineItems: [], paymentMode, notes }` |
| **List CRM Customers** | `GET /api/v1/org/customers` | `CustomersDirectoryPage`| `manage customer.png` (D435) | Customer list, receivables balance, order history count |
| **Add CRM Customer** | `POST /api/v1/org/customers` | `AddCustomerModal` | `Add customer.png` (D024) | `{ name, email, phone, company, billingAddress }` |
| **List Suppliers** | `GET /api/v1/org/suppliers` | `SuppliersDirectoryPage`| `supplier's inventory.png` (D471) | Vendor list, contact info, purchase order totals |
| **Add Supplier** | `POST /api/v1/org/suppliers` | `AddSupplierModal` | `ADD supplier.png` (D004) | `{ name, email, phone, address, taxId }` |

---

### 2.8 Corporate & Staff Loans

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **List Corporate Plans** | `GET /api/v1/org/loans/plans` | `LoanPlansManagementPage`| `Create plan.png` (D081) | Available corporate credit schemes, interest rates, tenor |
| **Create Loan Plan** | `POST /api/v1/org/loans/plans` | `CreateLoanPlanModal` | `Create plan.png` (D081) | `{ name, interestRate, maxTenorMonths, minAmount, maxAmount }` |
| **Apply for Staff Loan**| `POST /api/v1/work/loans/applications` | `StaffLoanApplicationModal`| `Loan Request.png` (D262) | `{ loanPlanId, requestedAmount, tenorMonths, reason }` |
| **View My Loans** | `GET /api/v1/work/loans/applications` | `StaffLoansHistoryPage` | `Loans View.png` (D265) | List of personal employee loan applications & repayment schedules |

---

### 2.9 Savings & Rotational Thrift (Ajo / Esusu)

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **Preview Savings Return**| `POST /api/v1/work/savings/preview` | `SavingsCalculatorWidget`| `Saving Plans.png` (D324) | `{ amount, tenorMonths, frequency }` -> Projected interest return |
| **Open Savings Account** | `POST /api/v1/work/savings` | `OpenSavingsModal` | `Saving Plans-1.png` (D321) | `{ targetAmount, targetDate, title, autoDebitEnabled }` |
| **Create Thrift Group** | `POST /api/v1/work/thrift` | `CreateThriftGroupModal` | `Manage Groups.png` (D267) | `{ name, contributionAmount, frequency, totalPositions }` |
| **Select Thrift Position**| `POST /api/v1/work/thrift/{id}/position` | `SelectPositionDialog` | `Manage Groups.png` (D267) | `{ positionNumber: 1..N }`, lock confirmation |
| **View Thrift Cycles** | `GET /api/v1/work/thrift/{id}/cycles` | `ThriftCyclesTimeline` | `Manage Groups.png` (D267) | Cycle number, collection status, designated payout recipient |

---

### 2.10 Compliance, KYC & KYB Registration

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **Submit Individual KYC**| `POST /api/v1/individuals/{id}/kyc-documents` | `IndividualKycSubmissionModal`| `Individual verified.png` (D243) | `{ documentType: 'NIN'|'BVN'|'Passport', documentNumber, fileUpload }` |
| **Get KYC Status** | `GET /api/v1/compliance/status` | `KycStatusCard` | `Individual verified-1.png` (D234) | Compliance tier (Tier 0..3), daily transaction limit, pending docs |
| **KYB Step 1 Registration**| `POST /api/v1/org/kyb/register-step1` | `OrgRegistrationStep1Page`| `Create company.png` (D079) | `{ businessName, rcNumber, tin, companyEmail, industry }` |
| **KYB Step 2 Registration**| `POST /api/v1/org/kyb/register-step2` | `OrgRegistrationStep2Page`| `Create company.png` (D079) | `{ directorBvn, directorIdDoc, utilityBill, registeredAddress }` |

---

### 2.11 Platform Administration & SuperAdmin Oversight

| User Action / Flow | HTTP Verb & Route | UI View / Component | Relevant Design Reference | Required Client & Form State |
| :--- | :--- | :--- | :--- | :--- |
| **View Audit Logs** | `GET /api/v1/admin/audit-logs` | `AdminAuditLogsPage` | `Organisations (45).png` | Date range, actor email, action type, entity ID filter |
| **Review KYB Submissions**| `GET /api/v1/admin/compliance/kyb/pending` | `AdminKybReviewPage` | `Organization-1.png` (D279) | Pending company list, document viewer, approve/reject buttons |
| **Approve / Reject KYB** | `POST /api/v1/admin/compliance/kyb/{id}/decision`| `KybDecisionModal` | `done.png` (D408) | `{ decision: 'Approved'|'Rejected', reason: string }` |
| **Manage Fee Configs** | `GET /api/v1/admin/fees` | `AdminFeeSchedulesPage`| `Policy Type.png` (D305) | Transfer fees, VAS commission rates, gateway surcharge tiers |
| **Create Fee Rule** | `POST /api/v1/admin/fees` | `CreateFeeRuleModal` | `Create Level.png` (D077) | `{ transactionType, feeType: 'Fixed'|'Percentage', amount, cap }` |
| **Reconcile Settlements**| `GET /api/v1/admin/reconciliation/discrepancies`| `AdminReconciliationPage`| `Real Inventory.png` (D317) | Discrepancy list, external provider vs internal ledger delta |

---

## 3. Strict "NO MOCK DATA" Architectural Policy

1. **Backend as Sole Source of Truth**: The frontend shall never contain mock arrays, dummy user profiles, hardcoded financial balances, fake transactions, or simulated dashboard counters.
2. **Empty Collection States**: When the backend returns an empty array (`items: []`), the UI displays a clean empty state card with an explicit action to create the first record (e.g. "No staff members yet. Invite your first employee.").
3. **Loading Skeletons**: While network requests are active, the UI renders pulsing Tailwind skeleton placeholders matching the exact geometry of the expected card or table rows.

---

## 4. Centralized RFC 7807 Error Handling Strategy

The backend communicates failures using standard **RFC 7807 ProblemDetails** with domain-specific `code` extensions.

### Error Code Handling Matrix

| Domain Code | HTTP Status | User Message & UI Presentation | Form Action |
| :--- | :--- | :--- | :--- |
| `INSUFFICIENT_FUNDS` | `422 Unprocessable` | "Your wallet balance is insufficient for this transaction." | Highlights amount field in red, provides "Fund Wallet" shortcut |
| `INVALID_PIN` | `400 Bad Request` | "The transaction PIN entered is incorrect." | Clears PIN input, shows remaining attempts |
| `PIN_LOCKED` | `423 Locked` | "Transaction PIN has been locked due to multiple failed attempts. Please reset your PIN." | Blocks transfer form, offers PIN reset modal |
| `PIN_REQUIRED` | `422 Unprocessable` | "Transaction PIN is required to authorize this transfer." | Prompts for 4-digit PIN |
| `IDEMPOTENCY_CONFLICT`| `409 Conflict` | "A transaction with this reference is already processing or completed." | Prevents duplicate debit, opens transaction details |
| `WALLET_NOT_ACTIVE` | `422 Unprocessable` | "Your wallet is inactive or restricted by compliance policy." | Disables transfer CTA, links to KYC verification |
| `SELF_TRANSFER` | `422 Unprocessable` | "You cannot transfer funds to your own account." | Flags destination account field |
| `VAS_LIMIT_EXCEEDED` | `422 Unprocessable` | "Airtime/Data purchase exceeds daily compliance limit." | Shows current daily limit and upgrade tier link |
| `VALIDATION_ERROR` | `400 Bad Request` | "One or more form fields are invalid." | Binds `problemDetails.extensions.errors` to specific input fields |

---

## 5. Identified Gaps, Ambiguities & Inconsistencies

### 5.1 Backend Gaps (Resolved for Phase 2)
1. **Refresh Token Rotation (Implemented)**: The backend exposes `POST /api/v1/auth/refresh-token` (exchanges active refresh token for a new JWT access token and rotated refresh token) and `POST /api/v1/auth/revoke-token` (explicit revocation on logout). The frontend Axios client implements automated silent token refreshing with concurrent request queueing upon encountering 401 responses.
2. **VAS Operator Detection**: `GET /api/v1/vas/operators/detect` requires phone prefix lookup. The frontend combines backend detection with a fallback manual telco selector (MTN, Airtel, Glo, 9mobile).

### 5.2 Design Gaps (Inheriting Shared Design Language)
1. **Public Marketing Landing Page**: The design library focuses on authenticated dashboards. Public pages (Home, Features, Pricing, Public Job Board) will inherit the exact topbar, pill buttons, typography, and color palette of the application.
2. **Admin SuperAdmin Console**: Screens for Admin Fee Config and Ledger Reconciliation will inherit the standard table, filter, and modal components demonstrated in `Organisations Wallet.png` and `Invoice generator.png`.
