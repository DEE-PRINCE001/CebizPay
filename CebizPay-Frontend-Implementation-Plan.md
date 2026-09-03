# CebizPay Frontend — Comprehensive Implementation Plan

**Date:** September 3, 2026  
**Based on:** CebizPay-PRD.md, Engineering-Specifications.md, and Audit Report  
**Purpose:** Stage-by-stage guide to remediate all existing frontend errors and implement all missing features, in full alignment with the PRD.

---

## How to Read This Plan

Each stage is **self-contained and ordered by dependency**. Stages that must come before others are noted. Within a stage, tasks are ordered by priority. Every task specifies:

- The **exact file(s)** to create or edit
- The **exact API endpoint(s)** to consume (from the backend controllers)
- The **PRD section** this satisfies
- The **acceptance criteria** to verify completion

> ⚠️ **Rule:** Do not begin a stage until the previous stage is complete. Many later stages depend on infrastructure built in earlier stages (e.g., role constants, PIN component, context changes).

---

## Stage 0 — Foundation Fixes (Pre-requisites for Everything Else)

> **Estimated scope:** ~4 small files modified  
> **No new pages. No new routes. Fix what is broken first.**

---

### Task 0.1 — Fix the Runtime Crash in AdminAuditLogsPage

**File:** `frontend/src/pages/admin/AdminAuditLogsPage.jsx`  
**PRD:** §4.13 (Admin Governance — Audit Trail)  
**Problem:** Line 87 calls `setSearch(e.target.value)` but the state variable is named `setAction`. This crashes the page whenever a user types in the search box.

**Fix:** In the `SearchInput` `onChange` handler, change `setSearch` → `setAction`.

**Acceptance Criteria:**
- Typing in the audit log filter input does not throw a ReferenceError
- The filter correctly narrows results by action string

---

### Task 0.2 — Establish Role Constants File

**New File:** `frontend/src/constants/roles.js`  
**PRD:** §3 (RBAC Matrix)

Create a single source-of-truth for all roles used across the frontend:

```js
export const ROLES = {
  // Platform-level (Super Admin portal)
  SUPER_ADMIN: 'SuperAdmin',
  ADMIN: 'Admin',
  AUDITOR: 'Auditor',
  COMPLIANCE_OFFICER: 'ComplianceOfficer',

  // Organisation-level (Org portal)
  ORG_SUPER_ADMIN: 'OrgSuperAdmin',
  ORG_CEO: 'CEO',
  FINANCE_MANAGER: 'FinanceManager',
  HR_MANAGER: 'HRManager',
  MEMBER: 'Member',
};

// Groups used for ProtectedRoute allowedRoles
export const PLATFORM_ADMIN_ROLES = [
  ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.AUDITOR, ROLES.COMPLIANCE_OFFICER
];

// Payroll execution: CEO/OrgSuperAdmin only
export const PAYROLL_EXEC_ROLES = [ROLES.ORG_SUPER_ADMIN, ROLES.ORG_CEO];

// Finance actions: CEO + Finance Manager
export const FINANCE_ROLES = [ROLES.ORG_SUPER_ADMIN, ROLES.ORG_CEO, ROLES.FINANCE_MANAGER];

// HR actions: CEO + HR Manager
export const HR_ROLES = [ROLES.ORG_SUPER_ADMIN, ROLES.ORG_CEO, ROLES.HR_MANAGER];
```

**Acceptance Criteria:**
- File exists; all role string values match exactly what the backend JWT emits in the `role` claim

---

### Task 0.3 — Add a `useRoleAccess` Hook

**New File:** `frontend/src/hooks/useRoleAccess.js`  
**PRD:** §3 (RBAC)

This hook centralises all role-checking logic so that every component can cleanly gate features:

```js
import { useAuth } from '../context/AuthContext';
import { ROLES, PLATFORM_ADMIN_ROLES, FINANCE_ROLES, HR_ROLES, PAYROLL_EXEC_ROLES } from '../constants/roles';

export function useRoleAccess() {
  const { user } = useAuth();
  const role = user?.role;

  return {
    role,
    isPlatformAdmin: PLATFORM_ADMIN_ROLES.includes(role),
    isSuperAdmin: role === ROLES.SUPER_ADMIN,
    isOrgAdmin: role === ROLES.ORG_SUPER_ADMIN || role === ROLES.ORG_CEO,
    canRunPayroll: PAYROLL_EXEC_ROLES.includes(role),
    canManageFinance: FINANCE_ROLES.includes(role),
    canManageHR: HR_ROLES.includes(role),
    hasRole: (allowedRoles) => allowedRoles.includes(role),
  };
}
```

**Acceptance Criteria:**
- Hook exported and importable from any page/component
- Boolean helpers return correct values based on JWT role claim

---

### Task 0.4 — Update `routes.js` with All Missing Route Constants

**File:** `frontend/src/constants/routes.js`  
**PRD:** Multiple sections

Add all missing route constants:

```js
// Org portal additions
ANNOUNCEMENTS: '/announcements',
VOUCHERS: '/vouchers',
RECRUITMENT: '/recruitment',

// ERP additions
ERP_REPORTS: '/erp/reports',

// Super Admin additions
ADMIN_ORGANIZATIONS: '/admin/organizations',
ADMIN_ORGANIZATION_DETAIL: '/admin/organizations/:id',
ADMIN_INDIVIDUALS: '/admin/individuals',
ADMIN_SAVINGS: '/admin/savings',
ADMIN_ANNOUNCEMENTS: '/admin/announcements',
ADMIN_SETTINGS: '/admin/settings',
```

**Acceptance Criteria:**
- All route constants are exported and available for use in nav components and `App.jsx`

---

## Stage 1 — Fix Existing Data & Metrics Integrity

> **Estimated scope:** 3 files modified  
> **Fixes mock data and mismatched metrics across Org Dashboard and Admin Dashboard.**

---

### Task 1.1 — Fix the Org Dashboard MetricGrid (Remove Super Admin Metrics)

**Files to edit:**
- `frontend/src/components/dashboard/MetricGrid.jsx`
- `frontend/src/pages/customer/DashboardPage.jsx`

**PRD:** §4.3 (Organization Dashboard)  
**Problem:** `MetricGrid` shows "Organisations", "Individuals", "Pending Users", "Active Users", "Rejected Users", "Saving Plans" — all Super Admin–level KPIs. An org user has no context for these.

**In `DashboardPage.jsx`, add these API fetches:**

| Fetch | API | Data extracted |
|---|---|---|
| Staff count | `GET /org/staff?pageSize=1` | `totalCount` |
| Departments | `GET /org/departments?pageSize=100` | `items.length` |
| Roles | `GET /org/roles?pageSize=100` | `items.length` |
| Salary Levels | `GET /org/levels?pageSize=100` | `items.length` |
| Active Savings Plans | `GET /work/savings` | `array.length` |
| Pending Staff Invites | `GET /org/staff?status=Pending&pageSize=1` | `totalCount` |

**In `MetricGrid.jsx`, replace the 6 stats with:**

| Label | Icon | Source |
|---|---|---|
| Total Staff | `Users` | staff totalCount |
| Departments | `Building2` | departments length |
| Workforce Roles | `Briefcase` | roles length |
| Salary Levels | `BadgePercent` | levels length |
| Active Savings Plans | `PiggyBank` | savings length |
| Pending Invites | `Clock` | pending staff totalCount |

Remove the hardcoded `metrics` object in `DashboardPage.jsx` and pass real fetched data.

**Acceptance Criteria:**
- All 6 stats fetched live from the backend on every dashboard load
- No hardcoded numbers remain
- Stats reflect the authenticated org's own data

---

### Task 1.2 — Fix the Admin Dashboard (Replace All Hardcoded Mock KPIs)

**File:** `frontend/src/pages/admin/AdminDashboardPage.jsx`  
**PRD:** §4.3 (Super Admin Dashboard)  
**Problem:** "142 Corporate Tenants", "12,480 Platform Users", "₦482.5M 24h Volume" are static strings.

**Add API fetches for each KPI:**

```
GET /admin/manage/organizations?pageSize=1   → totalCount = Org count
GET /admin/manage/individuals?pageSize=1     → totalCount = Individual count
GET /admin/reconciliation/records            → records.length = Discrepancy count
GET /admin/compliance/edd/cases              → already fetched (re-use)
GET /admin/audit-logs?pageSize=1             → totalCount = Total audit entries
```

Replace every hardcoded stat with a live API value. Show `Skeleton` while loading and `—` on error.

**Acceptance Criteria:**
- Zero hardcoded stat values remain in `AdminDashboardPage.jsx`
- Loading and error states handled
- All KPIs update on Refresh click

---

### Task 1.3 — Fix Wallet Balance Source

**File:** `frontend/src/pages/customer/DashboardPage.jsx`  
**Problem:** Balance is derived from `externalAccounts[0].currentBalance` — fragile and not authoritative.

**Fix:** Add a dedicated wallet balance fetch:
```
GET /wallet/balance?currency=NGN
```
Use the returned `availableBalance` directly in `BalanceCard`. Keep the virtual account fetch for bank details only.

**Acceptance Criteria:**
- Balance displayed matches the authoritative wallet ledger balance
- Balance updates after every financial action (refetch on success)

---

## Stage 2 — Org Portal RBAC (Role-Driven UI Visibility)

> **Estimated scope:** 4 files modified  
> **Depends on:** Stage 0 (roles constants, `useRoleAccess` hook complete)

---

### Task 2.1 — Gate Payroll Actions by Role

**File:** `frontend/src/pages/customer/PayrollPage.jsx`  
**PRD:** §4.7, §3 RBAC  
**Rule:** Only CEO / Org Super Admin can initiate "Run Payroll".

```jsx
const { canRunPayroll } = useRoleAccess();
// Conditionally render:
{canRunPayroll && <Button icon={Zap} onClick={() => setIsWizardOpen(true)}>Run Payroll</Button>}
```

**Acceptance Criteria:**
- Finance Managers see payroll history but no "Run Payroll" button
- CEO/OrgSuperAdmin can see and use the Run Payroll button

---

### Task 2.2 — Gate Staff Management Actions by Role

**File:** `frontend/src/pages/customer/StaffPage.jsx`  
**PRD:** §4.5, §3 RBAC  
**Rule:** HR Manager and CEO can manage staff. Finance Manager is read-only.

Gate `Add Staff`, `Departments`, and `Roles & Levels` buttons behind `canManageHR`.

**Acceptance Criteria:**
- Finance Managers see the staff directory but no Add/Manage buttons
- HR Managers and CEOs have full access

---

### Task 2.3 — Gate Financial Actions in WalletPage and TransfersPage

**Files:** `frontend/src/pages/customer/WalletPage.jsx`, `TransfersPage.jsx`  
**PRD:** §4.6, §3 RBAC  
**Rule:** Only CEO / Finance Manager can initiate transfers, fund wallet, add external accounts.

Gate "Send Transfer", "Fund Wallet", "Add External Account" behind `canManageFinance`.

**Acceptance Criteria:**
- HR Managers and Members can view transaction history but cannot initiate transfers
- CEO and Finance Managers have full financial access

---

### Task 2.4 — Update CustomerNav with Role-Aware Items

**File:** `frontend/src/components/navigation/CustomerNav.jsx`  
**PRD:** §3 RBAC

Import `useRoleAccess` and filter `navItems` before render:
- Payroll: visible to `canManageFinance` or `canRunPayroll`
- Loans (added in Stage 4): visible to `canManageFinance`
- Announcements (added in Stage 4): visible to all authenticated org users
- Recruitment (added in Stage 7): visible to `canManageHR`

**Acceptance Criteria:**
- HR Managers do not see Payroll or Loans in the nav
- Finance Managers do not see Recruitment or Announcements creation
- Members see only: Dashboard, Wallet, VAS, Savings, Announcements, Settings

---

## Stage 3 — Super Admin Portal: Navigation & Core Directory Pages

> **Estimated scope:** 4 new pages, 3 files modified  
> **Depends on:** Stage 0

---

### Task 3.1 — Expand AdminNav from 1 Entity Tab to 3

**File:** `frontend/src/components/navigation/AdminNav.jsx`  
**PRD:** §4.4, §4.5, §4.13

Replace the single "Users & Orgs" item with 3 separate tabs, and add missing modules:

```
Overview          →  /admin
Organizations     →  /admin/organizations      (NEW)
Individuals       →  /admin/individuals         (NEW)
Platform Admins   →  /admin/users               (existing page, renamed tab)
Compliance & KYB  →  /admin/compliance          (existing)
Savings Overview  →  /admin/savings             (NEW)
Fee Matrix        →  /admin/fees                (existing)
Reconciliation    →  /admin/reconciliation      (existing)
Announcements     →  /admin/announcements       (NEW)
Thrift Oversight  →  /admin/thrift              (existing route, needs page)
Audit Logs        →  /admin/audit-logs          (existing)
Platform Settings →  /admin/settings            (NEW)
```

**Acceptance Criteria:**
- All tabs render; active tab highlights correctly
- All navigation routes match `routes.js` constants

---

### Task 3.2 — Create Super Admin Organizations Directory Page

**New File:** `frontend/src/pages/admin/AdminOrganizationsPage.jsx`  
**PRD:** §4.4  
**Backend Controller:** `AdminManageController`

**Page structure:**
- **Header:** "Organizations Directory", Refresh button
- **Stat Cards:** Total Orgs (live), Pending KYB (live), Active Orgs (live)
- **Search + Status filter:** All / Active / Suspended / Pending KYB
- **Organizations Table** columns: Organization Name, CAC Number, Registration Date, KYB Status (badge), Actions

**API endpoints:**
```
GET /admin/manage/organizations?search=&status=&pageNumber=&pageSize=20
```

**Actions per row:**
- **Suspend** → `PATCH /admin/manage/organizations/{id}/status` body `{ status: 'Suspended' }` — requires `ConfirmModal`
- **Reactivate** → same endpoint body `{ status: 'Active' }` — requires `ConfirmModal`
- **Grant Edit Permission** → `POST /admin/manage/organizations/{id}/permissions/grant`
- **Revoke Edit Permission** → `POST /admin/manage/organizations/{id}/permissions/revoke`
- **View Profile** → opens `AdminOrgProfileDrawer` (side panel) showing: org details, compliance tier, KYB history, wallet summary, staff count

**Register route in `App.jsx`:**
```jsx
<Route path="/admin/organizations" element={
  <ProtectedRoute allowedRoles={PLATFORM_ADMIN_ROLES}><AdminOrganizationsPage /></ProtectedRoute>
} />
```

**Acceptance Criteria:**
- List loads from API with pagination
- Search and status filter work
- Suspend/Reactivate require confirmation and update the row
- Empty state shown when no orgs match filter

---

### Task 3.3 — Create Super Admin Individuals Directory Page

**New File:** `frontend/src/pages/admin/AdminIndividualsPage.jsx`  
**PRD:** §4.5  
**Backend Controllers:** `AdminManageController`, `IndividualKycController`

**Page structure:**
- **Header:** "Individual Users Directory", Refresh button
- **Stat Cards:** Total Individuals, Pending KYC, Verified
- **Search + KYC Status filter**
- **Individuals Table** columns: Full Name, Phone, Email, KYC Tier, KYC Status (badge), Registration Date, Actions

**API:**
```
GET /admin/manage/individuals?search=&kycStatus=&pageNumber=&pageSize=20
```

**"Review KYC" action** → opens `AdminKycReviewModal`:
- Shows submitted BVN, NIN, document type, uploaded document
- **Approve** → `POST /admin/compliance/kyc/{userId}/approve`
- **Reject** → `POST /admin/compliance/kyc/{userId}/reject` with `{ reason }` — reason field required

**Register route in `App.jsx`:**
```jsx
<Route path="/admin/individuals" element={
  <ProtectedRoute allowedRoles={PLATFORM_ADMIN_ROLES}><AdminIndividualsPage /></ProtectedRoute>
} />
```

**Acceptance Criteria:**
- List loads with pagination
- KYC Approve and Reject flows work
- Rejection requires a reason text
- Toast on success/failure

---

### Task 3.4 — Rename AdminUsersPage Tab to "Platform Admins"

**File:** `frontend/src/pages/admin/AdminUsersPage.jsx`  
**Change:** Update the page `title` prop to "Platform Admins" (and subtitle accordingly). No logic changes needed — the page correctly manages admin accounts already.

**Acceptance Criteria:**
- Page title clearly reads "Platform Admins"
- Accessible from the "Platform Admins" nav tab

---

### Task 3.5 — Fix AdminCompliancePage: Add KYB Review Queue Tab

**File:** `frontend/src/pages/admin/AdminCompliancePage.jsx`  
**PRD:** §4.2

Add a second tab alongside the existing EDD queue:

| Tab | Label | API |
|---|---|---|
| `edd` | EDD Case Queue | `/admin/compliance/edd/cases` (existing) |
| `kyb` | Pending KYB Applications | `GET /admin/compliance/kyb/pending` |

The KYB tab shows organizations awaiting KYB review with:
- **Approve** → `POST /admin/compliance/kyb/{orgId}/approve`
- **Reject** → `POST /admin/compliance/kyb/{orgId}/reject` body `{ reason }` — `ConfirmModal` required

**Acceptance Criteria:**
- Tab navigation between EDD and KYB works
- Approve/Reject on KYB tab works with confirmation
- Existing EDD functionality untouched

---

## Stage 4 — New Org Portal Feature Pages

> **Estimated scope:** 3 new pages, several new components  
> **Depends on:** Stage 0, Stage 2

---

### Task 4.1 — Create Loans Management Page

**New File:** `frontend/src/pages/customer/LoansPage.jsx`  
**New Directory:** `frontend/src/components/loans/`  
- `CreateLoanPlanModal.jsx`
- `LoanRequestReviewModal.jsx`  
**PRD:** §4.8  
**Backend Controllers:** `CorporateLoanPlansController`, `OrgLoansController`

**Page: 2 sub-tabs**

**Tab 1 — Loan Plans** (Finance Manager + CEO only — gate with `canManageFinance`):
- Table of configured corporate loan plans
  ```
  GET /org/loans/plans
  ```
- "Create Loan Plan" → `CreateLoanPlanModal` → `POST /org/loans/plans`
  - Fields: Plan Name, Interest Rate (% p.a.), Max Tenure (months), Max Amount (₦), Eligibility Criteria
- Each row: Edit (`PUT /org/loans/plans/{id}`), Activate/Deactivate (`PATCH /org/loans/plans/{id}/status`)

**Tab 2 — Loan Requests** (CEO + Finance Manager; HR Manager view-only):
- Table of staff loan requests
  ```
  GET /org/loans/requests?status=&pageNumber=&pageSize=20
  ```
- Status filter: All / Pending / Approved / Declined
- Columns: Staff Name, Requested Amount, Tenure, Loan Plan, Request Date, Status, Actions
- **Approve** → `POST /org/loans/requests/{id}/approve`
- **Decline** → `POST /org/loans/requests/{id}/decline` body `{ reason }` — `ConfirmModal` required

**Stat Cards:** Total Requests, Pending Review, Total Disbursed.

**Add to CustomerNav and routes:**
```js
{ to: ROUTES.LOANS, label: 'Loans', icon: Landmark }  // visible to canManageFinance
```

**Acceptance Criteria:**
- Loan Plans can be created and listed
- Loan Requests can be approved/declined with confirmation
- Finance Manager cannot create loan plans but can review requests

---

### Task 4.2 — Create Announcements Page (Org Portal)

**New File:** `frontend/src/pages/customer/AnnouncementsPage.jsx`  
**New Components:**
- `frontend/src/components/announcements/CreateAnnouncementModal.jsx`
- `frontend/src/components/announcements/AnnouncementCard.jsx`  
**PRD:** §4.11  
**Backend:** `/org/announcements`

**Page structure:**
- **Header:** "Workplace Announcements", "Create Announcement" button (HR Manager + CEO only)
- **Announcements list** — card per announcement:
  ```
  GET /org/announcements?pageNumber=&pageSize=15
  ```
  Card shows: title, body (truncated), author name, created date, target audience badge

**CreateAnnouncementModal:**
- Fields: Title, Body (textarea), Target Audience (radio: All Staff / Specific Department), Department select (conditional), Publish Date (optional)
- `POST /org/announcements` with `{ title, body, targetAudience, departmentId?, scheduledAt? }`

**Gate "Create Announcement" button:** `canManageHR`.

**Add to CustomerNav:**
```js
{ to: ROUTES.ANNOUNCEMENTS, label: 'Announcements', icon: Megaphone }
// Visible to all authenticated org users
```

**Acceptance Criteria:**
- Announcements load from API
- HR Manager and CEO can create
- Members and Finance Manager can view but not create
- Empty state with CTA shown when no announcements exist

---

### Task 4.3 — Create Company Vouchers Page (Org Portal)

**New File:** `frontend/src/pages/customer/VouchersPage.jsx`  
**New Components:**
- `frontend/src/components/vouchers/CreateVoucherModal.jsx`
- `frontend/src/components/vouchers/VoucherDetailsDrawer.jsx`  
**PRD:** §4.16  
**Backend Controller:** `OrgCompanyVouchersController`

**Page structure:**
- **Header:** "Company Payment Vouchers", "Create Voucher" button (Finance Manager + CEO only)
- **Stat Cards:** Total Vouchers, Pending Approval, Total Disbursed Amount
- **Vouchers Table:**
  ```
  GET /org/vouchers?pageNumber=&pageSize=20
  ```
  Columns: Voucher #, Recipient, Amount, Purpose, Status badge, Created Date, Actions

**CreateVoucherModal:**
- Fields: Recipient name, Recipient bank details, Amount, Purpose/narration
- PIN step at end: `PinInput` required
- Submits via `apiClient.postFinancial('/org/vouchers', { ...data, transactionPin: pin })`

**VoucherDetailsDrawer:**
- Full voucher details
- Approve/Reject actions if status is `Pending`:
  - `POST /org/vouchers/{id}/approve`
  - `POST /org/vouchers/{id}/reject` body `{ reason }`

**Add to CustomerNav** (gated: `canManageFinance`).

**Acceptance Criteria:**
- Vouchers list loads with pagination
- Create voucher requires PIN; PIN validates before submission
- Approve/reject with confirmation modal

---

## Stage 5 — ERP Enhancements

> **Estimated scope:** 1 new page, 2 files modified  
> **Depends on:** Stage 0

---

### Task 5.1 — Add Financial Reports Tab to ERP

**New File:** `frontend/src/pages/erp/ReportsPage.jsx`  
**PRD:** §4.16  
**Backend Controller:** `OrgReportsController`

**Page structure:**
- **Header:** "ERP Financial Accounting Reports"
- **Date Range Picker** (From / To — defaults to current month)
- **3 sub-tabs:**

| Tab | API Endpoint | Description |
|---|---|---|
| Sales | `GET /org/reports/sales?from=&to=` | Revenue summary by period |
| Purchases | `GET /org/reports/purchases?from=&to=` | Cost of goods by period |
| Payment Mode | `GET /org/reports/payment-mode?from=&to=` | Breakdown by payment method |

Each tab shows: Summary stat row (Total Amount, Count) + detailed data table + Export to CSV button.

**Update `ErpNav.jsx`:** Add:
```jsx
{ to: ROUTES.ERP_REPORTS, label: 'Financial Reports', icon: BarChart3 }
```

**Acceptance Criteria:**
- Reports page accessible via ERP nav
- Date range change triggers new API fetch
- All 3 report tabs load and display data
- CSV export works per report

---

### Task 5.2 — Add Invoice Receipt Viewer

**New Component:** `frontend/src/components/erp/InvoiceReceiptDrawer.jsx`  
**PRD:** §4.16  
**Backend Controller:** `OrgReceiptsController`

Add "View Receipt" button inside `InvoiceDetailsDrawer` — visible only when `invoice.status === 'Paid'`:
```
GET /org/invoices/{invoiceId}/receipt
```
Drawer shows: Receipt number, Date, Items, Amount Paid, Payment Method, PDF download link.

**Acceptance Criteria:**
- "View Receipt" appears only on Paid invoices
- Receipt data loads from API
- PDF download link works

---

### Task 5.3 — Add Payroll Voucher Viewer Page

**New File:** `frontend/src/pages/customer/PayrollVouchersPage.jsx`  
**PRD:** §4.7  
**Backend Controller:** `OrgCompanyVouchersController` (filtered by `type=Payroll`)

- Accessible at `/payroll/vouchers`
- Add a "Vouchers" sub-link/tab in `PayrollPage` header
- Lists payroll-generated vouchers:
  ```
  GET /org/vouchers?type=Payroll&pageNumber=&pageSize=20
  ```
- Columns: Voucher #, Period, Total Amount, Staff Count, Status, View
- View opens side drawer with per-staff line items

**Acceptance Criteria:**
- Payroll vouchers load
- Drawer shows line items per staff member

---

## Stage 6 — Super Admin Missing Modules

> **Estimated scope:** 3 new pages  
> **Depends on:** Stage 0, Stage 3

---

### Task 6.1 — Create Super Admin Savings Overview Page

**New File:** `frontend/src/pages/admin/AdminSavingsPage.jsx`  
**PRD:** §4.9  
**Backend Controllers:** `AdminSavingsInterestPoliciesController`

**Page: 2 sub-tabs**

**Tab 1 — Interest Rate Policies:**
```
GET /admin/savings/interest-policies
```
Table: Savings Type, Rate (% p.a.), Effective Date, Status (Active/Archived), Actions (Activate)
- "Create Policy" button → modal:
  - Fields: Savings Type (Fixed-Lock / Goal-Based), Rate, Effective Date
  - `POST /admin/savings/interest-policies`
- Activate: `PATCH /admin/savings/interest-policies/{id}/activate`

**Tab 2 — Active Savings Plans Overview:**
```
GET /admin/savings/overview   (platform-wide aggregation)
```
- Stat Cards: Total Active Plans, Total Savings Balance, Total Accrued Interest
- Table: Org Name, Plan Count, Total Balance, Interest Accrued

**Register route.**

**Acceptance Criteria:**
- Interest policies can be created and activated
- Platform savings overview shows live aggregated data

---

### Task 6.2 — Create Super Admin Announcements Page

**New File:** `frontend/src/pages/admin/AdminAnnouncementsPage.jsx`  
**New Component:** `frontend/src/components/admin/CreatePlatformAnnouncementModal.jsx`  
**PRD:** §4.11  
**Backend:** `/admin/announcements`

**Page structure:**
- **Header:** "Platform Announcements", "Create" button (SuperAdmin only)
- **Table:** Title, Target (All Users / All Orgs / Specific Org), Published Date, Expiry, Status, Actions
  ```
  GET /admin/announcements
  ```
- **CreatePlatformAnnouncementModal:**
  - Fields: Title, Body, Target Audience, Schedule Date, Expiry Date
  - `POST /admin/announcements`

**Acceptance Criteria:**
- Platform announcements list loads
- SuperAdmin can create announcements with audience targeting
- Edit and delete work on existing announcements

---

### Task 6.3 — Create Super Admin Platform Settings Page (with Referral Rate)

**New File:** `frontend/src/pages/admin/AdminSettingsPage.jsx`  
**PRD:** §4.12  
**Backend:** `/admin/settings/referral`

**Page structure:**
- **Referral Commission Rate Section:**
  - Displays current rate
  - "Edit" button → inline form with `Input` (0.00–100.00%) + Save
  - `GET /admin/settings/referral` → display
  - `PUT /admin/settings/referral` body `{ commissionRate }` → update
- Additional platform config sections can be added here over time

**Add to AdminNav:**
```jsx
{ to: ROUTES.ADMIN_SETTINGS, label: 'Platform Settings', icon: SlidersHorizontal }
```

**Acceptance Criteria:**
- Current referral rate displayed on load
- Rate editable with validation (number, 0–100 range)
- Toast shown on successful save

---

## Stage 7 — Recruitment Module

> **Estimated scope:** 2 new pages, 4 components  
> **Depends on:** Stage 0, Stage 2

---

### Task 7.1 — Create Org Recruitment Management Page

**New File:** `frontend/src/pages/customer/RecruitmentPage.jsx`  
**New Directory:** `frontend/src/components/recruitment/`  
- `CreateJobModal.jsx`
- `ApplicationReviewModal.jsx`  
**PRD:** §4.5 (Staff & HRIS)  
**Backend Controllers:** `OrgRecruitmentJobsController`, `OrgRecruitmentApplicationsController`

**Page: 2 sub-tabs**

**Tab 1 — Job Postings:**
```
GET /org/recruitment/jobs?pageNumber=&pageSize=20
```
- Job cards: title, department, type, location, deadline, applicant count
- "Post New Job" (HR Manager + CEO only) → `CreateJobModal`
  - `POST /org/recruitment/jobs` with: title, description, departmentId, employmentType, location, salaryRangeMin, salaryRangeMax, applicationDeadline
- Actions per card: View Applications (filter Tab 2 to this job), Close Job (`PATCH /org/recruitment/jobs/{id}/status`)

**Tab 2 — Applications:**
```
GET /org/recruitment/applications?status=&jobId=&pageNumber=&pageSize=20
```
- Status filter: All / Applied / Shortlisted / Interview / Offer / Rejected
- Columns: Applicant Name, Job Title, Applied Date, Status badge, Actions
- **Review** → `ApplicationReviewModal`:
  - Shows applicant details, resume link, cover letter
  - Status progression buttons: Shortlist → `PATCH {id}/status { status: 'Shortlisted' }`, Interview, Offer, Reject (requires reason)

**Add to CustomerNav** (gated: `canManageHR`):
```js
{ to: ROUTES.RECRUITMENT, label: 'Recruitment', icon: UserSearch }
```

**Acceptance Criteria:**
- Jobs can be posted and closed
- Applications progress through status stages
- Finance Managers do not see the Recruitment nav item

---

### Task 7.2 — Create Public Careers Page

**New File:** `frontend/src/pages/public/CareersPage.jsx`  
**PRD:** §4.5 (Public Job Board)  
**Backend Controller:** `PublicRecruitmentController`

**Page structure (unauthenticated, uses `MarketingLayout`):**
- All publicly-listed jobs across the platform:
  ```
  GET /public/recruitment/jobs
  ```
- Job cards: company name, title, location, type, deadline
- "Apply Now" → `PublicApplicationModal`:
  - Fields: Full Name, Email, Phone, Cover Letter, Resume URL
  - `POST /public/recruitment/jobs/{jobId}/apply`

Route already defined: `CAREERS: '/careers'`.

**Acceptance Criteria:**
- Page loads without authentication
- All published jobs visible
- Application submits and shows success confirmation

---

## Stage 8 — Technical Debt & Code Quality

> **Estimated scope:** 6 files modified  
> **Can be developed in parallel with Stages 4–7**

---

### Task 8.1 — Fix Savings and Thrift Endpoint Routing

**File:** `frontend/src/pages/customer/SavingsPage.jsx`  
**Problem:** Savings fetched from `/work/savings` (staff Work-domain endpoint). Corporate savings plans created by the org (via `OrgSavingsController`) use a different endpoint.

**Fix:**
- Keep `GET /work/savings` for staff's own individual savings plans
- Add `GET /org/savings/plans` for org-created corporate savings plans
- Merge both arrays; badge individual plans as "Personal", corporate as "Corporate"
- Same for thrift: keep `GET /work/thrift`, add `GET /org/thrift` if available

**Acceptance Criteria:**
- Both personal and corporate savings plans appear in the list
- Plans clearly labelled by source type

---

### Task 8.2 — Fix AuthContext: Access Token Storage Security

**File:** `frontend/src/context/AuthContext.jsx`  
**PRD:** §4.1 (Security)  
**Problem:** Access tokens are written to `localStorage` — vulnerable to XSS.

**Fix:**
1. Remove `localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, ...)` from `handleAuthSuccess` and the token update listener
2. Access token lives only in React state (memory)
3. On mount, if no in-memory token but a refresh token exists in localStorage, call:
   ```
   POST /auth/token/refresh { refreshToken }
   ```
   to silently re-authenticate and restore session
4. If silent refresh fails (token expired), call `logout()` and redirect to `/login`

**Acceptance Criteria:**
- Access token never persisted to `localStorage`
- Page refresh triggers silent re-authentication
- Expired sessions correctly redirect to login

---

### Task 8.3 — Add `PinInput` to Savings Withdrawal Flow

**File:** `frontend/src/components/savings/WithdrawSavingsModal.jsx`  
**PRD:** §4.1 (Transaction PIN required)

If `PinInput` is not already in this modal, add:
1. `const [pin, setPin] = useState('')` state
2. `<PinInput label="Authorize Withdrawal with 4-Digit PIN" value={pin} onChange={setPin} />` before submit button
3. Pass `transactionPin: pin` in the withdrawal API request body:
   ```
   POST /work/savings/{planId}/withdraw { amount, transactionPin: pin }
   ```

**Acceptance Criteria:**
- Withdrawal modal always shows PIN entry
- Cannot submit without a 4-digit PIN

---

### Task 8.4 — Add `PinInput` to All VAS Purchase Forms

**Files:** `AirtimeForm.jsx`, `DataBundleForm.jsx`, `ElectricityForm.jsx`, `CableTvForm.jsx`  
**PRD:** §4.1, §4.14

For each VAS form:
1. Add `pin` state
2. Add `<PinInput label="Authorize Purchase with PIN" ... />` before submit
3. Include `transactionPin: pin` in POST request body

**Acceptance Criteria:**
- All 4 VAS forms require PIN before purchase
- Wrong PIN shows inline error from API

---

### Task 8.5 — Fix Payroll History API Endpoint

**File:** `frontend/src/pages/customer/PayrollPage.jsx`  
**Problem:** Currently fetches from `/org/reports/settlements?settlementMethod=Payroll` — a reporting endpoint.

**Fix:** Change to the dedicated payroll endpoint:
```
GET /org/payroll/batches?pageNumber=&pageSize=15
```
(Confirm exact endpoint path from `PayrollController` implementation.)

Update `PayrollBatchList` props mapping to match the actual batch response shape.

**Acceptance Criteria:**
- Payroll history shows actual payroll batch records (Batch ID, Period, Staff Count, Amount, Status)

---

## Stage 9 — Topbar Global Search Functional Implementation

> **Estimated scope:** 1 file modified

---

### Task 9.1 — Implement Global Search Navigation Handler

**File:** `frontend/src/components/navigation/Topbar.jsx`  
**Problem:** `handleSearch` does nothing (`// Search handler` comment).

**Fix:** When user submits the search:
- In the **Org portal** (when `isAdmin === false`): navigate to `/staff?search={query}` — the staff directory supports search and is a commonly used entity
- In the **Admin portal** (when `isAdmin === true`): navigate to `/admin/organizations?search={query}` — the org directory supports search

Import `useNavigate` (already imported) and implement:
```js
const handleSearch = (e) => {
  e.preventDefault();
  if (!searchQuery.trim()) return;
  if (isAdmin) {
    navigate(`/admin/organizations?search=${encodeURIComponent(searchQuery.trim())}`);
  } else {
    navigate(`/staff?search=${encodeURIComponent(searchQuery.trim())}`);
  }
  setSearchQuery('');
};
```

Ensure `/staff` and `/admin/organizations` pages read the `search` query param from `useSearchParams()` on mount and pre-populate their search inputs.

**Acceptance Criteria:**
- Searching from the topbar navigates to the correct directory page
- Target page's search input is pre-filled with the query

---

## Stage 10 — Final Polish & Remaining PRD Items

> **Estimated scope:** 5 files modified  
> **Depends on:** All prior stages

---

### Task 10.1 — Compliance Tier Transaction Limit Feedback

**File:** `frontend/src/pages/customer/SettingsPage.jsx` (and `ComplianceStatusBadge.jsx`)  
**PRD:** §4.2 (Tiered Limits)

Add a limits info block below the compliance badge in the Settings compliance tab:

| Tier | Daily Limit | Single Transaction Limit |
|---|---|---|
| Tier 1 (Unverified) | ₦50,000 | ₦20,000 |
| Tier 2 (BVN/NIN verified) | ₦500,000 | ₦200,000 |
| Tier 3 (Full KYC) | Unlimited | ₦5,000,000 |

Read the current tier from `complianceData.currentTier` and display the corresponding limits.

**Acceptance Criteria:**
- Correct limits shown for current user tier
- Updates if tier changes after re-fetch

---

### Task 10.2 — VAS Duplicate Purchase Countdown UI

**Files:** All 4 VAS form components  
**PRD:** §4.14 (120-second duplicate guard)

When a VAS purchase returns a `409 Conflict` error with duplicate detected:
1. Parse `retryAfterSeconds` from the error response
2. Show countdown: "Duplicate detected. Retry available in 00:45"
3. Disable the submit button during countdown; re-enable when it reaches 0

**Acceptance Criteria:**
- Duplicate error shows countdown timer
- Submit re-enables when countdown expires

---

### Task 10.3 — Early Withdrawal Penalty Preview

**File:** `frontend/src/components/savings/WithdrawSavingsModal.jsx`  
**PRD:** §4.9 (Early withdrawal penalty = 10%)

Before PIN entry step, after amount input:
1. Call preview endpoint (confirm existence with backend):
   ```
   GET /work/savings/{planId}/withdraw/preview?amount={amount}
   ```
2. Display: "Early withdrawal penalty: ₦{penaltyAmount}" and "You will receive: ₦{netAmount}"
3. Show only if `penaltyAmount > 0`

**Acceptance Criteria:**
- Penalty preview shown when applicable
- Net receive amount clearly displayed before confirmation

---

### Task 10.4 — PIN Lockout Error Handling (Cross-cutting)

**PRD:** §4.1 (3 wrong PIN = 15-minute lock)

Enhance `parseProblemDetails` in `frontend/src/utils/problemDetails.js` to detect lockout status:
- HTTP 423 (Locked) or a specific error code in the response body
- Return `{ isLocked: true, lockoutMinutes: 15, message: '...' }` in the parsed result

In each financial modal that uses PIN (`QuickTransferModal`, `RunPayrollWizardModal`, `WithdrawSavingsModal`, `CreateVoucherModal`, all VAS forms):
- When `isLocked === true`, show a persistent alert: "Transaction PIN locked for 15 minutes. Too many failed attempts."
- Disable the PIN input and submit button

**Acceptance Criteria:**
- Lockout error shown clearly in all financial modals
- User cannot retry during lockout period

---

### Task 10.5 — Thrift Delinquency Indicators

**File:** `frontend/src/components/savings/ThriftGroupDetailModal.jsx`  
**PRD:** §4.10 (2 missed cycles = auto-lock)

In the member list inside the thrift group detail modal, add a warning badge next to any member where `member.missedCycles >= 2`:
- Badge: `<Badge variant="danger">Delinquent</Badge>`
- Tooltip/title: "Locked — 2 consecutive missed contribution cycles"

**Acceptance Criteria:**
- Members with 2+ missed cycles show a delinquency badge
- Badge tooltip explains the lock status

---

## Stage 11 — Final Route Registration in App.jsx

> Complete after all new pages are created and ready.

**File:** `frontend/src/App.jsx`

Register all new routes. Complete additions:

```jsx
// ── Super Admin New Routes ───────────────────────────────────────────
<Route path="/admin/organizations" element={
  <ProtectedRoute allowedRoles={PLATFORM_ADMIN_ROLES}><AdminOrganizationsPage /></ProtectedRoute>
} />
<Route path="/admin/organizations/:id" element={
  <ProtectedRoute allowedRoles={PLATFORM_ADMIN_ROLES}><AdminOrganizationsPage /></ProtectedRoute>
} />
<Route path="/admin/individuals" element={
  <ProtectedRoute allowedRoles={PLATFORM_ADMIN_ROLES}><AdminIndividualsPage /></ProtectedRoute>
} />
<Route path="/admin/savings" element={
  <ProtectedRoute allowedRoles={PLATFORM_ADMIN_ROLES}><AdminSavingsPage /></ProtectedRoute>
} />
<Route path="/admin/announcements" element={
  <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN]}><AdminAnnouncementsPage /></ProtectedRoute>
} />
<Route path="/admin/settings" element={
  <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN]}><AdminSettingsPage /></ProtectedRoute>
} />

// ── Org Portal New Routes ─────────────────────────────────────────────
<Route path="/loans" element={<ProtectedRoute><LoansPage /></ProtectedRoute>} />
<Route path="/announcements" element={<ProtectedRoute><AnnouncementsPage /></ProtectedRoute>} />
<Route path="/vouchers" element={<ProtectedRoute><VouchersPage /></ProtectedRoute>} />
<Route path="/recruitment" element={<ProtectedRoute><RecruitmentPage /></ProtectedRoute>} />
<Route path="/payroll/vouchers" element={<ProtectedRoute><PayrollVouchersPage /></ProtectedRoute>} />

// ── ERP New Route ─────────────────────────────────────────────────────
<Route path="/erp/reports" element={<ProtectedRoute><ReportsPage /></ProtectedRoute>} />

// ── Public Route (no ProtectedRoute) ─────────────────────────────────
<Route path="/careers" element={<CareersPage />} />
<Route path="/careers/:jobId" element={<CareersPage />} />
```

Import all new pages at the top of `App.jsx`.

**Acceptance Criteria:**
- All routes are reachable via URL
- All protected routes redirect to `/login` if unauthenticated
- Role-restricted routes redirect correctly for unauthorized roles

---

## Stage Summary

| Stage | Focus | New Pages | Files Modified | Priority |
|---|---|---|---|---|
| 0 | Foundation: Crash fix, role constants, route constants | 0 | 4 | 🔴 Critical |
| 1 | Data integrity: Kill all mock data, fix metrics | 0 | 3 | 🔴 Critical |
| 2 | Org RBAC: Role-gated UI for all Org portal actions | 0 | 4 | 🟠 High |
| 3 | Super Admin nav split + 3 new directory pages + KYB fix | 3 | 3 | 🟠 High |
| 4 | Org new pages: Loans, Announcements, Vouchers | 3 | 2 | 🟠 High |
| 5 | ERP: Financial Reports, Receipts, Payroll Vouchers | 3 | 3 | 🟡 Medium |
| 6 | Admin missing: Savings, Platform Announcements, Referral Settings | 3 | 2 | 🟡 Medium |
| 7 | Recruitment: Org module + Public Careers page | 2 | 2 | 🟡 Medium |
| 8 | Tech debt: Auth security, PIN gaps in VAS/Savings, endpoint fixes | 0 | 6 | 🟠 High (parallel) |
| 9 | Global search functional | 0 | 1 | 🟢 Low |
| 10 | Polish: Tier limits, VAS cooldown, penalties, lockout UX | 0 | 5 | 🟢 Low |
| 11 | Route registration: Wire all new pages into App.jsx | 0 | 1 | 🔴 Required (last) |

**Total new pages:** ~11 pages  
**Total files modified:** ~36 files  
**Backend changes required:** None — all backend controllers are confirmed implemented.

---

## Final Verification Checklist

- [ ] Zero hardcoded mock data in any page
- [ ] All financial mutation flows require 4-digit Transaction PIN
- [ ] Super Admin portal has 3 separate entity tabs: Organizations, Individuals, Platform Admins
- [ ] Super Admin can KYB-approve/reject organizations
- [ ] Super Admin can KYC-approve/reject individual users
- [ ] Org Dashboard MetricGrid shows org-relevant live data (staff, departments, roles, etc.)
- [ ] Admin Dashboard shows zero hardcoded KPIs — all live
- [ ] Loans page exists with plan config and request review
- [ ] Announcements page exists for both Org and Admin portals
- [ ] Company Vouchers page exists
- [ ] Recruitment module exists with Job Postings and Application Review
- [ ] Public Careers page accessible unauthenticated
- [ ] ERP Financial Reports tab exists and calls OrgReportsController
- [ ] `AdminAuditLogsPage` search input does not crash
- [ ] Access tokens not stored in `localStorage`
- [ ] Role-gated UI correctly hides/shows actions based on `user.role`
- [ ] All new routes registered in `App.jsx`
- [ ] All new route constants in `routes.js`
- [ ] CustomerNav items filtered correctly by role
- [ ] AdminNav has all required tabs including Savings, Announcements, Platform Settings
