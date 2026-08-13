CEBIZPAY PLATFORM : PRODUCT REQUIREMENTS DOCUMENT
Product: Cebizpay (CEBIZ) Scope: Super Admin Portal · Organization (B2B) Portal, including ERP &
Invoicing · Consumer Mobile App Document Status: v1.0 Document Date: August 5, 2026
1. Executive Summary
Cebizpay is a multi-tenant fintech ecosystem delivered across three coordinated surfaces that share one
ledger, one identity model, and one compliance pipeline:
Surface
Primary Users
Core Purpose
Super Admin Portal
(web)
Cebizpay platform
operators
Platform-wide control
plane: KYC/KYB
approval, liquidity
oversight, admin
governance, referral
policy, announcements
Organization Portal
(web)
Mobile App
(iOS/Android)
1.1 Objectives
Corporate customers
(B2B)
Individual consumers,
employees, thrift
members
Corporate treasury,
payroll/HRIS,
employee loans &
savings, recruitment,
and a full
ERP/Invoicing suite
Personal wallet,
savings, thrift
(Ajo/Esusu), workplace
benefits, bills/VAS,
referrals, support
• One ledger and wallet model shared by Individuals, Organizations, and the platform operator.
• One identity & verification pipeline: Individuals complete KYC, Organizations complete KYB;
both terminate in Super Admin approval.
• One payroll-to-wallet pipeline: funds disbursed by an Organization land in an Individual’s wallet,
visible identically on mobile.
• One announcements/referral policy layer: configured by Super Admin (platform-wide) or
Organization (workplace-specific), consumed on mobile.
• A B2B-only ERP/Invoicing layer that extends the Organization Portal for inventory,
supplier/customer relationship management, and financial accounting.
1.2 System Architecture
                            ┌────────────────────────────┐
                         │      SUPER ADMIN PORTAL    │
                         │  (platform control plane)  │
                         └──────────────┬─────────────┘
                                        │ approves KYC/KYB, sets referral
                                        │ rate, publishes platform
                                        │ announcements, suspends entities
                    ┌───────────────────┼──────────────────────┐
                    │                                          │
        ┌───────────┴─────────────┐
┌────────────┴────────────┐
        │   ORGANIZATION PORTAL   │◄────────────► │       MOBILE APP        │
        │ (B2B: payroll, HRIS,    │  payroll,     │ (B2C: wallet, savings,  │
        │  treasury, ERP/invoicing│  loans,       │  thrift, Work domain,   │
        │  supplier/customer CRM) │  workplace    │  VAS, referrals)        │
        └─────────────────────────┘
announcements└─────────────────────────┘

2. Glossary & Terminology Standards
The following definitions are canonical and used consistently across every module of this document and
across all three product surfaces.

Term Definition
Individual User Any natural person with a Cebizpay wallet, verified
via KYC. May or may not be affiliated with an
Organization.
Staff An Individual User who has accepted a workplace
binding to a specific Organization (Work domain).
Organization A corporate tenant verified via KYB.
Term Definition
Wallet The single ledger account owned by an Individual
User or an Organization.
KYC Identity verification process for Individual Users
(NIMC Card, Driver’s License, or International
Passport plus a liveness check).
KYB Business verification process for Organizations
(CAC Certificate plus company logo).
Announcement — Platform Published by Super Admin; visible to all
Organizations and/or all Mobile users.
Announcement — Workplace Published by an Organization; visible only to that
Organization’s Staff on the mobile Work dashboard.
Payroll Loan The Staff-facing feature (Mobile, Work domain) for
requesting a salary-deducted advance.
Corporate Loan Plan The Organization-facing configuration of loan
products, eligibility rules, and the approval
workflow that underpins Payroll Loans.
Corporate Savings Plan An Organization-sponsored group savings scheme
that Staff subscribe to.
Individual Savings Plan A personal fixed-lock or goal-based plan created by
an Individual User on mobile.
Thrift A peer-to-peer rotational contribution group
(Ajo/Esusu), available on the Mobile App.
Transaction PIN The 4-digit numeric PIN (or biometric equivalent)
required for every outbound financial action, on
every surface.
Status: Verified / Pending /
Suspended / Rejected
Canonical lifecycle states for both Individual Users
and Organizations.
Company Voucher A general-purpose Organization disbursement
record (cheques, multi-currency), issued through
the ERP module.
Term
Payment Voucher
Definition
A per-employee salary receipt generated
automatically by a payroll disbursement.
2.1 Copy & Localization Standards
All UI copy across the three surfaces must follow these standards:
• Action buttons that trigger a provider- or context-specific transaction (e.g., airtime purchase) must
render the selected option dynamically — e.g., “Buy MTN” or “Buy Airtel” based on the user’s
selection, never a static label.
• Standard phrasing for common actions and confirmations: – Funding prompt: “How much would you like to fund?” – Deposit confirmation: “[Amount] has been successfully deposited to your CEBIZ wallet.” – Card-based payment screens must reference card selection, not bank transfer instructions. – Workplace loan ledger labels: “Months Paid For,” “Interest rate (per month).” – KYC verification action: “Verify.” – Savings/thrift transaction labels: “Thrift Payment,” “Thrift Reimbursement” (label
containers must be sized to avoid text clipping).
• Module names are standardized platform-wide as: Wallets, Announcements, Suspended,
Credentials.
• All success/error confirmations across Super Admin, Organization, and Mobile use one reusable
feedback component (dynamic title, message, and icon).
3. User Roles & Access Control (RBAC)
Role
Surface
Access Scope
Super Admin
Super Admin
Portal
Full platform
Key Permissions
System setup; invite/delete
Admins; toggle Admin
permissions; set global referral
rate; approve/reject KYC & KYB;
suspend/reactivate any
Organization or Individual;
publish platform
announcements; grant/revoke
Organization edit permissions
Role Surface Access Scope Key Permissions
Admin Super Admin
Portal
Operational,
delegated
View dashboards; review
KYC/KYB documents; view
transaction/payroll logs; toggle
user/org status only if granted
edit permission by Super Admin
Read-Only
Admin
Super Admin
Portal
Auditor View-only across analytics,
organization profiles, individual
profiles, logs; no write actions
Org Super
Admin / CEO
Organization
Portal
Full corporate
tenant
KYB onboarding; wallet debits;
PIN management; staff
management; payroll execution;
loan approval/decline;
role/department creation; full
ERP access
Finance
Manager
Organization
Portal
Wallet & payroll View wallet balances; trigger
salary payments; view/edit
payment vouchers;
fund/transfer wallet; view ERP
financial reports
HR Manager Organization
Portal
HRIS &
recruitment
Manage staff profiles; create
departments/roles/levels;
publish job offers; review
applications; send staff invites;
publish workplace
announcements
Staff /
Individual User
Mobile App Self-service,
personal wallet
View payslips/earnings; apply
for payroll loans; contribute to
savings/thrift; view/apply to
internal job postings; manage
own KYC, cards, and PIN
Consumer
(unaffiliated)
Mobile App Self-service,
personal wallet
only
All Individual User mobile
features except the Work
domain (no Organization
binding)
3.1 Cross-Surface Permission Rules
1.
2.
3.
4.
5.
An Organization’s KYB status, set exclusively by Super Admin, gates whether any Org Portal role
(CEO, Finance Manager, HR Manager) can execute outbound payroll or wallet transfers (§6.2).
An Individual User’s KYC status, set exclusively by Super Admin, gates transaction volume limits on
the Mobile App (§6.1) and whether they may accept a Staff invitation from an Organization.
Super Admin suspension of an Organization immediately blocks that Organization’s payroll
execution, which cascades to every affiliated Staff member being unable to receive further
disbursements via the Mobile App.
Organization suspension of a Staff member restricts that individual’s Work-domain access on
Mobile but does not affect their personal wallet, savings, or thrift activity — employment status is
independent of Individual identity status.
Super Admin’s global referral commission rate is the single source of truth consumed by the
Mobile App’s Referral Dashboard; Organizations have no referral-rate authority.
4. Functional Requirements
4.1 Authentication & Security
• Email/password login (web) or phone+OTP login (mobile), both protected by rate-limiting: 5
failed attempts triggers a 5-minute lock (web); OTP requests are limited to 3 per device per 15
minutes.
• MFA/2FA prompt on successful web credential check when enabled for Admin profiles.
• Password rules — web (Admin/Org): minimum 8 characters, cannot reuse the last 3 passwords.
Password rules — mobile: minimum 7 characters, mixed case, numeric, and symbol required,
validated live against a 4-criteria checklist.
• Transaction PIN (4-digit, universal): required before any outbound financial mutation on every
surface — wallet transfer, payroll execution, card deletion, VAS purchase, loan request/approval,
thrift contribution. Biometric authentication (FaceID/TouchID/BiometricPrompt) is an accepted
mobile substitute via cryptographic challenge-response; raw biometric data is never transmitted
or stored server-side.
• PIN lockout: 3 incorrect entries locks wallet debits for 15 minutes, enforced platform-wide.
• Data-in-transit: TLS 1.3 with HTTP Public Key Pinning. Data-at-rest: AES-256-GCM; device-local
secrets stored in iOS Keychain / Android Keystore.
• Auth tokens: OAuth2, 15-minute access tokens, refresh tokens with a 30-day sliding window,
revoked on explicit logout.
4.2 Identity Verification: KYC and KYB
• KYC (mobile-originated, Super-Admin-approved): Individual submits a government ID (NIMC
Card, Driver’s License, or International Passport) plus a liveness selfie. Edge-detection rejects
blurry, low-contrast, or overly reflective captures; two failed automated attempts route to manual
Admin review.
• KYB (Organization Portal-originated, Super-Admin-approved): Two-step registration — (1)
company name, email, phone; (2) year established, CAC Certificate upload, company logo upload
(JPEG/PNG/PDF, max 5MB). Status is set to PENDING until Super Admin validates it.
• Review workflow: Admin navigates to the entity detail, opens View on the submitted document,
then selects Verify (confirms via modal, writes an audit entry, notifies the entity) or Reject
(requires a reason, writes an audit entry, notifies the entity).
• Lifecycle gating: – PENDING/REJECTED Individuals may transact but are capped below ₦50,000 per outbound
transaction. – PENDING/REJECTED Organizations may log in and configure HRIS/hierarchy but cannot execute
payroll or transfer wallet funds. – Only VERIFIED Individuals may accept a Staff invitation with full benefits, and only VERIFIED
Organizations may activate automated payroll or corporate savings plans.
4.3 Dashboards & Analytics

Metric Family Super Admin View Organization View Mobile View
Liquidity/Balanc
e
Aggregated
platform-wide wallet
liquidity
Own corporate wallet
balance with
Fund/Transfer quick
actions
Own personal wallet
balance (eye-icon
toggle for privacy)
Entity counts Organizations,
Individuals,
Pending/Active/Reje
cted counts, Active
Savings Plans
Staff count,
Department/Role/Lev
el counts
N/A (personal only)
Earnings/financ
e trend
12-month platform
earnings line chart
12-month org
disbursement/earning
s chart
N/A
Announcements Publishes
platform-wide
announcements
Publishes workplace
announcements; sees
both
Sees platform
announcements
(Home) and workplace
announcements
(Work)
4.4 Organization Management
• Super Admin: Directory (name, category, email, address, status, actions), filter/search, server-side
pagination. Detailed profile with tabs: Transactions, Wallet Summary, Payroll Schedule, Payroll
History, Payroll Analytics, Corporate Savings Plans. Actions: Suspend/Re-activate, Grant/Stop
Edit Permission.
• Organization (self-service): KYB onboarding, corporate login, treasury and HRIS management as
described in §4.5–§4.7.
• Every metric shown in the Super Admin’s Organization Detail tabs is sourced directly from the
transaction, wallet, payroll, and savings tables the Organization Portal writes to.
4.5 Individual and Staff Management
• Super Admin: Directory (name, email, professional status “Staff”/“Not-a-Staff,” affiliated company,
KYC status), KYC verification workflow (§4.2), wallet/savings visibility.
• Organization (HRIS): Staff directory with Department/Role/Level, single or bulk (email-tag)
onboarding, suspend/reactivate with mandatory reason, organizational hierarchy setup
(Departments → Roles; Salary Levels).
• Mobile (self-service): Profile management, “Join Organisation” via invitation code, Work
dashboard.
• An Organization’s staff invitation (email token, PENDING status until accepted) and the Mobile App’s
“Join Organisation” flow (entering an invitation code) are two entry points into a single binding
process — see §5.2 for the end-to-end sequence.
4.6 Wallets, Ledger & Treasury
• Ledger immutability: entries are never deleted or edited; reversals are recorded as new
offsetting entries. Enforced platform-wide since Organization, Individual, and Super Admin views
all read the same ledger table.
• Funding: via dedicated virtual accounts (Wema Bank, Sterling Bank, Moniepoint) or via a
saved/new debit card (zero-auth or ₦50 micro-charge validation before saving a card). Identical
flow on Organization and Mobile surfaces; Super Admin has read-only visibility.
• Transfers: To Bank (external) or To Wallet (internal, resolves recipient name before send) —
identical pattern on Organization and Mobile.
• Withdrawals: Via Card or Via Merchant (mobile); the Organization equivalent is payroll
disbursement plus ad hoc bank/wallet transfer.
• Card management: list masked cards, add/delete — identical pattern on Organization and
Mobile.
• Fund validation: every outbound transfer is blocked if the amount plus fees exceeds available
balance.
• Failover: automatic switch to a secondary payment gateway within 3 seconds if the primary is
unavailable.
4.7 Payroll & Disbursement Engine
• Execution modes: Pay All, By Department, By Role, By Level, By Individual (Personal).
• Flow: select mode → calculate aggregate total → enter Transaction PIN/biometric → validate
wallet sufficiency (displays “Insufficient wallet balance. Kindly fund your wallet.” on failure) →
execute → auto-generate a Payment Voucher per affected Staff member.
• Result on Mobile: the paid Staff member’s wallet balance and “My Earning” figure on the Work
dashboard update in the same real-time ledger write.
• Result on Super Admin: the Payroll Analytics tab on the Organization’s detail profile aggregates
Local NGN, International NGN, and USDT spend, employee counts, and category breakdown
(Salaries, Pensions, Taxes, Benefits), sourced from the same transactions.
• Voucher: contains organization branding, recipient bank/account, transaction ID, amount (figures
and words), paying bank, description/remarks; printable to PDF; authorized users may edit
metadata (bank, remarks, description) post-generation for reconciliation, which writes an audit
entry (§7).
4.8 Loans
• Organization — Corporate Loan Plan configuration: name, description, amount, interest rate,
eligibility rules.
• Mobile — Staff request (Work domain, Payroll Loan): amount, periodical payment, repayable
duration, description; the system displays computed monthly payment and total repayment before
submission; underwriting caps total monthly debt at 33% of verified salary.
• Organization — approval: review request detail (amount, interest, duration, monthly deduction)
→ Approve (credits the Staff wallet, updates the payroll deduction schedule) or Decline (requires
a reason, notifies Staff).
• Super Admin visibility: aggregate “Total Loan Fund” is surfaced inside the Organization’s Wallet
Summary tab.
• Deduction priority: payroll loan repayments are deducted before net pay is credited to the Staff
wallet.
• Offboarding: if a Staff member with an outstanding loan is terminated, the loan converts
automatically to a standard individual loan contract with updated, non-payroll terms.
4.9 Savings
• Individual Savings Plan (Mobile): fixed-lock (30 days–2 years, 8–15% annual interest, daily
accrual) or goal-based recurring plans. Early withdrawal penalty: 100% of accrued interest plus
2.5% of principal.
• Corporate Savings Plan (Organization-sponsored, Staff-subscribed): name, description, target
amount, start/end date, frequency, color tag; Organization views total-saved-vs-target and
per-participant contribution schedules.
• Super Admin: a Savings Plans Overview lists both plan types by owner (Individual
vs. Organization) with target amount, deduction schedule, and timeline.
4.10 Thrift (Ajo/Esusu)
• Create thrift (name, frequency, target amount, dates, description); invite participants by email or
shareable invitation code; accept/decline invites; automated collection at 02:00 UTC with
wallet-then-card fallback; two consecutive missed cycles auto-locks the participant and flags the
group administrator.
• Departing members are refunded their net contributions within 24 hours of removal.
4.11 Announcements
• Platform Announcement: Super Admin creates (Title, Description, Publish); visible in a
dedicated Super Admin directory and surfaced on the Mobile Home dashboard.
• Workplace Announcement: Organization (HR Manager) publishes; visible only to that
Organization’s Staff on the Mobile Work dashboard.
• Both types share the same create/publish/list interaction pattern; only the audience scope differs.
4.12 Referral Program
• Super Admin’s Profile page holds the single global referral commission rate (default 5%), editable
only by Super Admin.
• The Mobile Referral Dashboard displays the user’s referral code, total earnings, and
referred-friend count and status, computed against the Super Admin-set rate.
• Bonus crediting requires the referred user to complete KYC Level 1 and deposit at least ₦1,000;
capped at ₦50,000 per user per month, with automatic escalation to manual risk review above
that threshold.
4.13 Admin Governance
• Add New Admin modal (Name, Email, Description/Role) → Send Invites dispatches a 24-hour
invitation token; the new admin appears with OFF (pending) status until activated.
• Admin list toggle (ON/OFF) and delete actions, both writing audit entries.
4.14 Value-Added Services (VAS): Airtime & Data
• Provider is auto-detected from the phone number (MTN, Glo, Airtel, 9mobile); a manual override
prompts for confirmation before proceeding.
• Airtime limits: ₦50–₦50,000 per transaction; data bundle limits follow operator pricing.
• Duplicate-purchase guard: identical amount and number combinations are blocked for 120
seconds.
• Pending-operator handling: transactions taking more than 15 seconds to confirm move to a PENDING
state, the wallet lock is released, and status is polled in the background; failed transactions are
auto-refunded.
4.15 Customer Support
• Chatbot (“Kola”) with numbered issue triage; escalates to a live agent on keyword match (“human
agent,” “representative”) or explicit link click.
• Offline ticket creation if no agent is available, with a 12-hour review SLA.
4.16 Enterprise Resource Planning (ERP) & Invoicing
This module family — Inventory & Stock, Services Catalog, Supplier/Vendor CRM, Customer CRM,
Purchase/Sales Orders, Operating Expenses, Invoicing, Company Payment Vouchers, and Financial
Accounting — is delivered within the Organization Portal.
• Inventory: item catalog with purchase/selling price, quantity, expiry, and automatic status (In-stock
above 10 units, Low stock for 1–10, Out of stock at 0).
• Services Catalog: Services Rendered vs. Services Bought, priced and categorized.
• Supplier/Vendor CRM: profile, return policy, order-on-the-way tracking.
• Customer CRM: profile, purchase history, acquisition channel (Online/Direct/Other).
• Purchasing & Sales: purchase orders, sales orders, operating expenses, each with delivery
notification options.
• Invoicing: contact plus due date plus line items, with an optional 7.5% VAT calculation,
Open/Closed status tracking, PDF/Image download, and email send. Invoice Settings manage
receiving bank accounts, billing contacts, and custom tags.
• Company Payment Vouchers: used for general company disbursements (cheques,
multi-currency), with a dedicated directory, creation form, and printable document — distinct
from the per-employee Payment Vouchers generated by payroll (§4.7).
• Financial Accounting: Daily/Weekly/Monthly/Yearly Sales and Purchase reports; Net Profit/Loss
calculated as Total Income minus Total Expenses; Cash/Transfer/Bank-Card settlement
statements.
5. Cross-Actor Interaction Flows
5.1 Organization Onboarding and Verification
Organization Portal                     --------------------
Register (Step 1: profile)
Super Admin Portal -------------------
Register (Step 2: CAC + logo) ---> status = PENDING
Admin/Super Admin reviews KYB docs
Verify ---------> status = VERIFIED
or
Reject (reason) -> status = REJECTED
Org may configure HRIS while PENDING/REJECTED,
but payroll & transfers stay BLOCKED until VERIFIED
5.2 Staff Invitation and Work-Domain Binding
Organization Portal (HR Manager)                 ---------------------------------
Bulk/Single Staff invite (email) ---> token sent
STAFF record: PENDING -> ACTIVE
Mobile App (Individual User) -----------------------------
User receives email OR enters
invitation code in "Join
Organisation" (Work tab) ---> Work dashboard unlocked
Super Admin sees the Individual's
"Professional Status" flip to Staff
with the affiliated company shown
5.3 Payroll Disbursement Across Surfaces
Organization Portal              Ledger (shared)              Mobile App        Super Admin -------------------              ---------------              ----------        -----------
Select Pay mode
Enter PIN
Validate balance
Execute ----------------------> Write PAYROLL_TRANSACTION
                                 Write PAYMENT_VOUCHER(s)
                                                        ---> Wallet balance,
                                                             "My Earning" update
                                                                                 Payroll Analytics
                                                                                 tab reflects the
                                                                                 same transaction
5.4 Individual KYC and Transaction Limit Lift
Mobile App                              Super Admin Portal -----------                             -------------------
Submit ID + liveness selfie ---> status = PENDING (capped < ₦50,000/txn)
                                         Admin/Super Admin reviews
                                         Verify ---------> status = VERIFIED (cap lifted)
                                              or
                                         Reject (reason) -> status = REJECTED (cap remains)
5.5 Payroll Loan Request, Approval, and Disbursement
Mobile App (Staff)              Organization Portal              Ledger -------------------              --------------------             ------
Request loan (amount,
duration, description)
System pre-checks 33% DTI --> Loan Requests queue
                                Review detail
                                Approve --------------------> Credit Staff wallet
                                                                Update payroll deduction
                                     or
                                Decline (reason) --> Staff notified, no ledger write
5.6 Suspension Cascades
Super Admin suspends an Organization
   -> Organization Portal: payroll execution and wallet transfers blocked
   -> Mobile App: affiliated Staff cannot receive further disbursements

Organization suspends a Staff member (reason required)
   -> Mobile App: Work-domain access restricted for that user
   -> Personal wallet, savings, and thrift activity are UNAFFECTED
5.7 Announcement Publication
Super Admin publishes PLATFORM announcement --> visible on Mobile Home dashboard
                                             --> visible to all Organizations

Organization (HR Manager) publishes WORKPLACE announcement --> visible only on
Mobile Work dashboard, to that Org's Staff
6. Business Rules & Validations
1.
2.
3.
4.
5.
6.
7.
8.
9.
KYC/KYB Lifecycle: see §4.2.
PIN Security: 4-digit numeric, bcrypt/Argon2-hashed server-side; 3 failed attempts locks wallet
debits for 15 minutes; biometric is a mobile-only equivalent, never persisted server-side.
Ledger Immutability: no deletes or updates; reversals are new offsetting transactions, enforced
platform-wide.
Fund Sufficiency Check: every outbound transfer (personal, corporate, payroll) is pre-validated
against available balance plus fees before the PIN prompt.
Referral Bonus Rules: KYC Level 1 plus a minimum ₦1,000 initial deposit is required to trigger
crediting; capped at ₦50,000 per user per month, with overflow routed to manual risk review.
Payroll Loan Underwriting: total monthly debt obligations must not exceed 33% of verified
monthly salary; unverifiable salary routes to HR for manual review within 24 hours.
Savings Term Limits: Individual fixed plans run 30 days to 2 years; early exit penalty equals
100% of accrued interest plus 2.5% of principal.
Thrift Delinquency: two consecutive missed cycles auto-suspends payout eligibility and flags the
group administrator; departing members are refunded net contributions within 24 hours.
VAS Limits: airtime purchases range ₦50–₦50,000 per transaction; the duplicate-purchase block
window is 120 seconds.
10. Audit Logging: every status transition (VERIFIED, REJECTED, SUSPENDED), password change, PIN change,
edit-permission toggle, admin deletion, and voucher metadata edit writes an immutable Audit Log
entry, enforced platform-wide.
11. Currency Formatting: NGN displays with the ₦ symbol and thousands separators; non-NGN
currencies (USD, GHS, EUR, INR, USDT) are explicitly prefixed or suffixed with their code;
multi-currency figures normalize to the entity’s base currency for reporting via a shared FX
middleware.
12. Destructive Actions: all hard-delete UI actions (inventory item, customer, supplier, admin)
require a confirmation modal and resolve to a soft delete (is_deleted = true) to preserve historical
ledger, voucher, and invoice integrity.
7. Data Model
erDiagram
ADMIN_USER {
uuid id PK
string name
string email UK
string role
boolean is_active
}
    ORGANIZATION {
        uuid id PK
        string company_name
        string email UK
        string cac_number
        string logo_url
        string kyb_status
        boolean edit_permission_granted
        string transaction_pin_hash
    }

    INDIVIDUAL_USER {
        uuid id PK
        string full_name
        string email UK
        string phone
        string kyc_status
        string transaction_pin_hash
        uuid organization_id FK "nullable — set when bound as Staff"
        string professional_status "Staff | Not-a-Staff"
    }

    DEPARTMENT { uuid id PK
        uuid organization_id FK
        string name }

    ROLE { uuid id PK
        uuid department_id FK
        string role_name }

    SALARY_LEVEL { uuid id PK
        uuid organization_id FK
        string level_name
        decimal amount }

    KYC_DOCUMENT { uuid id PK
        uuid user_id FK
        string doc_type
        string status }

    WALLET { uuid id PK
        uuid owner_id
        string owner_type "INDIVIDUAL | ORGANIZATION"
        decimal balance
        decimal loan_repayable
        string currency }

    PAYROLL_TRANSACTION { uuid id PK
        uuid organization_id FK
        decimal total_amount
        string currency
        string payment_mode }

    PAYMENT_VOUCHER { uuid id PK
        uuid transaction_id FK
        uuid staff_id FK
        decimal amount
        text remarks }

    LOAN { uuid id PK
        uuid organization_id FK
        uuid individual_id FK
        decimal amount
        decimal interest_rate
        string status
        string source "PAYROLL | INDIVIDUAL_POST_OFFBOARD" }

    SAVINGS_PLAN { uuid id PK
        uuid owner_id
        string owner_type "INDIVIDUAL | ORGANIZATION"
        string plan_type "FIXED_LOCK | GOAL | CORPORATE"
        decimal target_amount
        string schedule }

    THRIFT { uuid id PK
        uuid organizer_id FK
        string name
        string frequency
        decimal contribution_amount
        string invitation_code }

    THRIFT_MEMBER { uuid id PK
        uuid thrift_id FK
        uuid individual_id FK
        string status }

    ANNOUNCEMENT { uuid id PK
        string scope "PLATFORM | WORKPLACE"
        uuid organization_id FK "null when scope = PLATFORM"
        string title
        uuid created_by FK }

    INVENTORY_ITEM { uuid id PK
        uuid organization_id FK
        decimal purchase_price
        decimal selling_price
        integer quantity }

    SUPPLIER { uuid id PK
        uuid organization_id FK
        string return_policy }

    CUSTOMER { uuid id PK
        uuid organization_id FK }

    INVOICE { uuid id PK
        uuid organization_id FK
        uuid customer_id FK
        decimal total_amount
        string status }

    COMPANY_VOUCHER { uuid id PK
        uuid organization_id FK
        decimal amount
        string currency }

    ORGANIZATION ||--o{ DEPARTMENT : has
    DEPARTMENT ||--o{ ROLE : contains
    ORGANIZATION ||--o{ SALARY_LEVEL : defines
    ORGANIZATION ||--o{ INDIVIDUAL_USER : employs
    INDIVIDUAL_USER ||--o{ KYC_DOCUMENT : provides
    INDIVIDUAL_USER ||--o{ WALLET : owns
    ORGANIZATION ||--o{ WALLET : owns
    ORGANIZATION ||--o{ PAYROLL_TRANSACTION : executes
    PAYROLL_TRANSACTION ||--o{ PAYMENT_VOUCHER : generates
    ORGANIZATION ||--o{ LOAN : approves
    INDIVIDUAL_USER ||--o{ LOAN : requests
    INDIVIDUAL_USER ||--o{ SAVINGS_PLAN : subscribes
    ORGANIZATION ||--o{ SAVINGS_PLAN : sponsors
    INDIVIDUAL_USER ||--o{ THRIFT : organizes
    THRIFT ||--o{ THRIFT_MEMBER : has
    INDIVIDUAL_USER ||--o{ THRIFT_MEMBER : joins
    ADMIN_USER ||--o{ ANNOUNCEMENT : publishes_platform
    ORGANIZATION ||--o{ ANNOUNCEMENT : publishes_workplace
    ORGANIZATION ||--o{ INVENTORY_ITEM : stocks
ORGANIZATION ||--o{ SUPPLIER : contracts
ORGANIZATION ||--o{ CUSTOMER : serves
ORGANIZATION ||--o{ INVOICE : issues
ORGANIZATION ||--o{ COMPANY_VOUCHER : disburses
8. API Architecture
8.1 Auth & Identity
• POST /api/v1/auth/login — web (Admin/Org)
• POST /api/v1/auth/register/phone — mobile (Individual)
• POST /api/v1/auth/register/otp/verify — mobile (Individual)
• POST /api/v1/auth/change-password — all surfaces
• POST /api/v1/org/kyb/register-step1 / register-step2 — Organization
• GET  /api/v1/individuals/{id}/kyc-documents · PATCH /api/v1/individuals/{id}/kyc-status — Super Admin
• PATCH /api/v1/organizations/{id}/status — Super Admin (VERIFY/REJECT/SUSPEND/RE_ACTIVATE)
8.2 Wallet & Ledger
• GET  /api/v1/wallet/virtual-accounts
• POST /api/v1/wallet/transfer/peer — wallet-to-wallet
• POST /api/v1/wallet/transfer/bank — external
• POST /api/v1/wallet/cards — link card
8.3 Payroll
• POST /api/v1/org/payroll/calculate
• POST /api/v1/org/payroll/execute
• GET  /api/v1/org/payroll/vouchers/{id} · PUT .../vouchers/{id}
• GET  /api/v1/admin/organizations/{id}/payroll-analytics — Super Admin read rollup
8.4 HRIS
• GET/POST /api/v1/org/staff, /staff/invite, /staff/create
• POST /api/v1/org/departments, /roles, /levels
• POST /api/v1/work/organisation/join — Mobile, binds via invitation code
8.5 Loans
• POST /api/v1/work/loans/apply — Mobile, Staff request
• PATCH /api/v1/org/loans/{id}/decision — Organization, approve/decline
8.6 Savings & Thrift
• POST /api/v1/savings/plan/create — Individual or Organization, owner_type distinguishes
• GET  /api/v1/savings/plan/{id}/history
• POST /api/v1/thrifts/create, /thrifts/invite — Mobile
8.7 Announcements & Referrals
• POST /api/v1/announcements — scope: PLATFORM requires Super Admin auth, WORKPLACE requires Org auth
• PUT  /api/v1/admin/settings/referral-rate — Super Admin, sole writer
• GET  /api/v1/profile/referrals — Mobile
8.8 VAS
• POST /api/v1/services/purchase/airtime
• GET  /api/v1/services/packages/data
8.9 ERP & Invoicing
• GET/POST /api/v1/erp/inventory/items, /erp/services, /erp/suppliers, /erp/customers
• GET/POST /api/v1/erp/invoices, PATCH .../status
• POST /api/v1/erp/vouchers
• GET /api/v1/erp/reports/sales, /purchase, /payment-mode
8.10 Admin Governance
• POST /api/v1/admin/manage/invite · PATCH .../toggle-status · DELETE /admin/manage/{id}
9. Non-Functional Requirements
• Performance: core write operations (wallet transfer, payroll execution, contribution updates,
utility purchases) resolve within 1,500ms under normal network conditions.
• Scalability: the database layer supports at least 5,000 concurrent connections and 250 write
operations per second without service degradation.
• Offline resilience (Mobile): cached transaction history, profile status, and drafted VAS requests
remain available offline; queued requests sync automatically on reconnect.
• PCI-DSS Level 1: card credentials are never stored on-device or on internal servers; card data is
handled via secure gateway iframes (Paystack/Flutterwave-class providers).
• NDPR Compliance: explicit consent prompts precede analytics tracking or PII processing;
personal data is hashed and encrypted at rest per §4.1.
10. Assumptions & Dependencies
1.
2.
3.
4.
5.
6.
All displayed figures and identifiers reflect production data sourced from live relational tables at
runtime.
The system integrates a multi-bank Banking-as-a-Service provider (e.g., Paystack, Monnify, or
Anchor) to provision virtual account numbers shown on Organization and Mobile funding screens.
NGN is the primary operational currency; International NGN and USDT apply specifically to
contractor/foreign-staff payroll, using real-time FX rates at execution time. The ERP module
additionally supports USD, GHS, EUR, and INR display via the same FX middleware.
Web application biometric prompts use WebAuthn (TouchID/FaceID), falling back to numeric PIN
entry on unsupported hardware.
Marking an ERP Invoice Closed (Paid) automatically generates a corresponding Receipt document
with matching line items and reference numbers.
Third-party payment gateways (e.g., Paystack, Flutterwave) provide card tokenization and
micro-charge validation services used during card onboarding.
11. Open Questions & Recommendations
The following items warrant product decisions ahead of general availability:
1.
2.
3.
4.
Thrift oversight: Thrift moves funds between Individual Users through rotational payouts but
currently has no dedicated reporting, delinquency, or dispute-resolution surface on the Super
Admin Portal. Recommendation: add a Thrift oversight module (directory, delinquency flags,
dispute queue) to Super Admin.
ERP compliance linkage: Organizations can issue invoices and record sales/purchases
independently of their KYB/suspension status. Recommendation: extend Organization
suspension to also freeze ERP write operations (invoice creation, voucher generation), and surface
read-only ERP summary metrics on the Super Admin Organization Detail profile.
Suspension notification: when an Organization is suspended and payroll is blocked, affected Staff
currently receive no dedicated in-app notice. Recommendation: trigger a push/in-app
notification tied to the Organization status-change event.
Support reporting: customer support ticket volume and resolution metrics have no Super
Admin-facing reporting surface. Recommendation: confirm whether this should be added for
platform-wide support-quality monitoring.
If any of the above should instead remain out of scope for v1.0, or if there are additional clarifications
needed on roles, data ownership, or module boundaries, please advise and this document will be updated
accordingly.
