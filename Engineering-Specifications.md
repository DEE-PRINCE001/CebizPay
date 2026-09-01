# CEBIZPAY Backend — Master Architecture & Engineering Specification

**Project:** CEBIZPAY
**Backend:** C# / ASP.NET Core
**Database:** PostgreSQL
**Initial Hosting:** Render
**Target:** Production-ready V1, designed for 1M+ users and future cloud migration
**Document Purpose:** Master implementation context and handoff document

---

# 1. Project Scope

CEBIZPAY is a multi-tenant fintech ecosystem with three coordinated surfaces:

1. **Super Admin Portal**
2. **Organization (B2B) Portal**
3. **Consumer Mobile App**

The PRD defines a shared identity model, shared compliance pipeline and shared ledger across the three surfaces.

The backend must support:

* Individual users and wallets
* Organizations and corporate wallets
* KYC/KYB
* HRIS and workforce management
* Payroll
* Loans
* Savings
* Thrift/Ajo/Esusu
* Wallet transfers
* Funding and cards
* VAS
* ERP and invoicing
* Announcements
* Referrals
* Notifications
* Admin governance
* Auditability
* Financial reconciliation

---

# 2. Confirmed Architecture Decisions

These are now **locked project decisions**.

| Decision                 | Choice                                                   |
| ------------------------ | -------------------------------------------------------- |
| Architecture             | Modular Monolith                                         |
| Application architecture | Clean Architecture                                       |
| Background processing    | Dedicated Worker project                                 |
| Database                 | PostgreSQL                                               |
| ORM                      | EF Core / Npgsql                                         |
| Identity                 | ASP.NET Core Identity                                    |
| Identity model           | One `ApplicationUser` model                              |
| Organizations            | Multi-tenant                                             |
| Organization membership  | User can belong to multiple organizations simultaneously |
| Wallet model             | One primary corporate wallet per organization in V1      |
| External funding rails   | Multiple `ExternalFundingAccount` records per wallet     |
| Virtual Account provider | Monnify primary (provider-neutral abstraction)           |
| Card Funding provider    | Flutterwave primary, Paystack fallback                   |
| Bank Transfer provider   | Monnify primary, Flutterwave fallback, Paystack fallback |
| Funding fee model        | Configurable calculation + configurable fee bearer       |
| Future MFB portability   | Provider-neutral rails abstract future core banking/MFB  |
| Currency                 | NGN transactional currency in V1                         |
| Other currencies         | Reporting/FX architecture only in V1                     |
| Ledger                   | Central double-entry ledger for every monetary movement  |
| Ledger entries           | Immutable                                                |
| Messaging                | RabbitMQ                                                 |
| Cache/state              | Redis                                                    |
| Initial Redis hosting    | Render Redis                                             |
| Initial hosting          | Render                                                   |
| Containerization         | Docker from day one                                      |
| CI/CD                    | GitHub Actions                                           |
| Future infrastructure    | Must remain portable toward AWS/other cloud              |
| API style                | Versioned REST                                           |
| API version              | `/api/v1`                                                |

The PRD itself identifies NGN as the primary operational currency and describes other currencies for international payroll/reporting.

---

# 3. Architectural Philosophy

## V1

We will **not use microservices**.

We will build:

```text
                    CEBIZPAY
                       |
               ASP.NET Core API
                       |
             Modular Monolith
                       |
       +---------------+---------------+
       |               |               |
   PostgreSQL        Redis          RabbitMQ
       |               |               |
       |               |          Background
       |               |            Workers
       |               |
       +---------------+----------------
                       |
               External Providers
             /         |         \
      Monnify     Flutterwave    Paystack
    (VA & Payout) (Card & Payout)(Card & Payout)
```

The application will have strong internal module boundaries so that high-scale or high-risk domains can later be extracted into services without redesigning the entire system.

Likely future extraction candidates include:

* Payments
* Finance/Ledger
* Notifications
* VAS
* Reporting

Microservices are therefore a **future deployment option**, not a V1 requirement.

---

# 4. Bounded Contexts

The backend will be organized around these domain boundaries:

```text
Identity & Access
Organization & Workforce
Compliance
Finance
Payroll
Credit
Savings
Thrift
Payments
VAS
ERP
Communication
Governance
```

## Identity & Access

Owns:

* Application users
* Authentication
* Roles
* Permissions
* Sessions/tokens
* Password security
* MFA
* Transaction PIN security

## Organization & Workforce

Owns:

* Organizations
* Organization membership
* Departments
* Organization roles
* Salary levels
* Staff
* Invitations
* Recruitment/job postings

## Compliance

Owns:

* KYC
* KYB
* Documents
* Verification workflow
* Compliance status
* Compliance-related restrictions

## Finance

Owns:

* Wallets
* Ledger accounts
* Ledger transactions
* Ledger entries
* Internal transfers
* Balances
* Financial references
* Financial invariants

## Payroll

Owns:

* Payroll calculation
* Payroll execution
* Payroll deductions
* Payroll transactions
* Payment vouchers

## Credit

Owns:

* Loan products/plans
* Loan applications
* Loan approvals
* Loan contracts
* Repayments
* Payroll deductions

## Savings

Owns:

* Savings plans
* Contributions
* Accrual
* Withdrawals
* Penalties
* Corporate savings

## Thrift

Owns:

* Thrift groups
* Members
* Cycles
* Contributions
* Payouts
* Delinquency
* Reimbursement

## Payments

Owns:

* Capability-oriented provider abstractions (`IVirtualAccountProvider`, `ICardPaymentProvider`, `IBankTransferProvider`, `IBankAccountResolver`, `IProviderCustomerProfileProvider`, `IPaymentReconciliationProvider`)
* External funding account lifecycle (`ExternalFundingAccount` attached to `Wallet`)
* Dedicated virtual account provisioning (Monnify primary)
* Card funding lifecycle, saved cards, token management, and micro-charge verification (Flutterwave primary, Paystack fallback)
* Outbound bank transfer execution and provider routing (Monnify primary, Flutterwave fallback, Paystack secondary fallback)
* Sequential `PaymentAttempt` tracking and auditability
* Provider failover orchestration (TechnicalFailure-only failover, strict UNKNOWN reconciliation prerequisite)
* Provider webhook ingestion, signature authentication, deduplication, and asynchronous worker dispatch
* Provider reconciliation engine (scheduled, manual, mismatch resolution)
* Configurable fee engine and fee bearer calculation

## VAS

Owns:

* Airtime
* Data
* Utility purchases
* VAS limits
* Duplicate-purchase prevention

## ERP

Owns:

* Inventory
* Services
* Suppliers
* Customers
* Orders
* Expenses
* Invoices
* Company vouchers
* ERP reporting

## Communication

Owns:

* Announcements
* Notifications
* Referral system

## Governance

Owns:

* Admin management
* Audit logging
* Support
* Platform-wide settings
* Oversight/reporting

---

# 5. Project Structure

Recommended solution:

```text
CebizPay.sln

src/
├── CebizPay.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Filters/
│   ├── Extensions/
│   ├── Configuration/
│   └── Program.cs
│
├── CebizPay.Application/
│   ├── Identity/
│   ├── Organizations/
│   ├── Compliance/
│   ├── Finance/
│   ├── Payroll/
│   ├── Credit/
│   ├── Savings/
│   ├── Thrift/
│   ├── Payments/
│   ├── Vas/
│   ├── Erp/
│   ├── Communication/
│   └── Governance/
│
├── CebizPay.Domain/
│   ├── Identity/
│   ├── Organizations/
│   ├── Compliance/
│   ├── Finance/
│   ├── Payroll/
│   ├── Credit/
│   ├── Savings/
│   ├── Thrift/
│   ├── Payments/
│   ├── Vas/
│   ├── Erp/
│   ├── Communication/
│   └── Governance/
│
├── CebizPay.Infrastructure/
│   ├── Persistence/
│   ├── Identity/
│   ├── Payments/
│   ├── Messaging/
│   ├── Caching/
│   ├── Storage/
│   └── ExternalServices/
│
└── CebizPay.Workers/
    ├── Consumers/
    ├── Jobs/
    └── Services/

tests/
├── CebizPay.UnitTests/
├── CebizPay.IntegrationTests/
├── CebizPay.ApiTests/
└── CebizPay.ArchitectureTests/

deploy/
├── Dockerfile
├── docker-compose.yml
└── ...
```

---

# 6. Dependency Direction

The dependency rule is:

```text
Api
 ↓
Application
 ↓
Domain

Infrastructure
 ↓
Application / Domain

Workers
 ↓
Application / Infrastructure
```

The Domain layer must not depend on Infrastructure.

The Application layer must not know about:

* PostgreSQL
* Redis
* RabbitMQ
* Flutterwave
* Paystack
* HTTP implementation details

Those are infrastructure concerns.

---

# 7. Multi-Tenancy

V1 strategy:

**Shared PostgreSQL database + shared schema + organization ownership columns.**

Organization-owned entities will contain:

```text
organization_id
```

where applicable.

We will not create one database per organization in V1.

## Critical rule

Organization isolation is enforced at the application/domain authorization level and reinforced through database relationships and query design.

Every organization-scoped query must resolve the current organization context.

A user belonging to Organization A must never be able to access Organization B simply by changing an ID in a request.

This is a core security requirement.

---

# 8. Identity Model

We chose:

**One ASP.NET Identity `ApplicationUser`.**

Conceptually:

```text
ApplicationUser
├── IndividualProfile
└── AdminProfile
```

Organization membership is separate:

```text
ApplicationUser
        |
OrganizationMembership
        |
Organization
```

This allows:

```text
User A
 ├── Organization X
 └── Organization Y
```

simultaneously.

It also prevents organization employment status from becoming part of the user's global identity.

The PRD explicitly states that Staff status is independent from Individual identity status, and suspension of Staff work access does not affect personal wallet, savings or thrift activity.

---

# 9. Authentication

Required flows:

### Web

```text
Email + Password
        ↓
Rate limiting
        ↓
Credentials
        ↓
MFA if enabled
        ↓
Access Token + Refresh Token
```

### Mobile

```text
Phone
 ↓
OTP
 ↓
Authentication
 ↓
Access Token + Refresh Token
```

PRD requirements include:

* 5 failed web attempts → 5-minute lock
* maximum 3 OTP requests/device/15 minutes
* Admin MFA when enabled
* password history
* 15-minute access tokens
* refresh token with 30-day sliding window
* logout revocation.

---

# 10. Authorization

Authorization will combine:

```text
Authentication
+
Role
+
Permission
+
Organization membership
+
Resource ownership
+
KYC/KYB status
+
Entity status
+
Business policy
```

We will avoid scattering checks like:

```csharp
if (user.Role == "Admin")
```

through controllers.

Instead use ASP.NET Core authorization policies and application-level permission checks.

Example:

```text
CanExecutePayroll
=
Authenticated
AND
Payroll.Execute
AND
OrganizationMembership.Active
AND
Organization.KybStatus == VERIFIED
AND
Organization.Status != SUSPENDED
```

The PRD specifies that Organization KYB status gates outbound payroll and wallet transfers and that Individual KYC status gates transaction limits and staff invitation acceptance.

---

# 11. KYC, KYB, Risk Engine & Regulatory Compliance (CBN CDD 2023)

### 11.1 Regulatory Authority & Framework
Compliance architecture is governed strictly by the **Central Bank of Nigeria (Customer Due Diligence) Regulations, 2023**, the **Money Laundering (Prevention and Prohibition) Act, 2022**, and the **Terrorism (Prevention and Prohibition) Act, 2022**.

**Core Compliance Principles**:
1. **Sovereign Compliance Authority**: CebizPay internal compliance engines, risk ratings, and compliance officer reviews remain authoritative for customer approvals and transaction eligibility. External verification partners provide raw verification evidence (`VerificationEvidence`), not unconstrained authorization.
2. **Distinct Regulatory Models**:
   - **Individuals (Natural Persons)**: Governed by the **CBN Three-Tiered KYC Framework**.
   - **Organizations (Legal Persons & Arrangements)**: Governed by separate **Corporate CDD Requirements** (CAC incorporation, MemArt, UBOs >= 5%, Directors, Signatories, TIN). Tiered KYC must NEVER be applied to legal persons.
3. **Provider Outage != Verification Failure**: External provider downtime or timeouts must never automatically reject a customer; transactions queue for retry or administrative review.

---

### 11.2 Layered & Versioned Transaction Limit Architecture

CebizPay separates transaction limit enforcement into four decoupled layers to ensure strict regulatory compliance, operational flexibility, and fail-closed safety:

1. **Statutory Regulatory Ceilings (CBN Three-Tiered KYC Framework)**:
   - Hard, non-overridable ceilings established by Central Bank of Nigeria regulations.
   - Applies strictly to **natural persons (individuals)** and never to legal persons.
   - **Tier 1 (Basic)**: Single transaction cap $\le$ ₦50,000; Cumulative daily cap $\le$ ₦300,000.
   - **Tier 2 (Standard)**: Single transaction cap $\le$ ₦200,000; Cumulative daily cap $\le$ ₦1,000,000.
   - **Tier 3 (Full)**: Unrestricted by statutory ceiling (bounded by product policy and provider rail constraints).

2. **CebizPay Versioned Product Policies (`TransactionLimitPolicy`)**:
   - Versioned, configurable business limits per product, operation channel, and customer tier.
   - Must strictly remain $\le$ Statutory Regulatory Ceilings (`ProductCap = Min(ConfiguredCap, RegulatoryCeiling)`).
   - Allows tighter business thresholds (e.g., Tier 1 single cap configured to ₦30,000) while guaranteeing regulatory compliance.

3. **Payment Provider Rail Constraints**:
   - Physical constraints imposed by upstream payment infrastructure providers (e.g. Flutterwave Card Funding ₦2,000,000 cap; Monnify Payout ₦10,000,000 cap).

4. **Customer-Specific Risk Restrictions (`ComplianceRestriction`)**:
   - Account-level caps placed directly on an individual customer or organization by compliance officers or risk rules.

**Effective Limit Rule**:
$$\text{Effective Single Cap} = \min(\text{Regulatory Ceiling}, \text{Configured Product Policy}, \text{Provider Rail Cap}, \text{Customer Risk Cap})$$

| Tier Level | Identification Requirements | Verification Method | Statutory CBN Ceiling | Policy Defaults |
| :--- | :--- | :--- | :--- | :--- |
| **Tier 1 (Basic)** | Phone number, Legal Full Name, Initial OTP verification | Internal OTP + Database deduplication | $\le$ ₦50,000 / txn; ₦300,000 / day | Configurable $\le$ ₦50,000 |
| **Tier 2 (Standard)** | Tier 1 + BVN / NIN validation + Basic government ID | Automated BVN/NIN resolution via Dojah (Fallback: Smile ID) | $\le$ ₦200,000 / txn; ₦1,000,000 / day | Configurable $\le$ ₦200,000 |
| **Tier 3 (Full)** | Tier 2 + Proof of Address (utility bill) + Live Facial Biometric Match | Smile ID SmartSelfie™ + ID OCR + Address Geocoding | Unrestricted by statutory ceiling | Default ₦10,000,000 / txn |

---

### 11.3 Legal Persons & Corporate KYB Architecture

Corporate onboarding enforces full corporate due diligence:
1. **Corporate Identity**: CAC Certificate of Incorporation, Memorandum and Articles of Association (MemArt), Tax Identification Number (TIN), Registered Business Address.
2. **Ultimate Beneficial Ownership (UBO)**: Mandatory capture and identity verification (BVN/NIN + Government ID) of all natural persons holding **5% or more equity** or controlling interest.
3. **Governance & Signatories**: Identity verification for all registered Directors and authorized banking/wallet signatories.
4. **Automated CAC Resolution**: Primary lookup via Dojah CAC API (Fallback: Smile ID Business Verification).

---

### 11.4 Strategic Multi-Provider KYC/KYB Routing Matrix

| Capability | Primary Provider | Fallback Provider | Rationale & SLA |
| :--- | :--- | :--- | :--- |
| **Individual ID / BVN / NIN** | **Dojah** | **Smile ID** | Direct NIBSS/NIMC integration with high uptime; Smile ID provides robust failover |
| **Liveness & 1:1 Biometrics** | **Smile ID** | **Dojah** | ISO/IEC 30107-3 Level 2 certified SmartSelfie™ optimized for African demographics |
| **Document OCR & Verification** | **Smile ID** | **Dojah** | High-precision MRZ and visual inspection for NIMC, Passports, Driver's Licenses |
| **AML, PEP & Sanctions** | **Dojah** | **Smile ID** | Real-time screening against UN, OFAC, EU, PEP, and domestic adverse media databases |
| **Bank Account Name Resolution**| **Flutterwave** | **Paystack** / **Monnify**| Rapid interbank NUBAN inquiry via NIP switch |
| **CAC / Business Verification** | **Dojah** | **Smile ID** | Direct Corporate Affairs Commission registry integration |
| **Beneficial Owner Verification**| **Dojah** | **Smile ID** | Cross-references CAC shareholding filings with NIBSS/NIMC identity records |

---

### 11.5 Provider Result Normalization Model

All external verification responses must normalize into a standard domain result model:

```text
ProviderVerificationResult
├── Match           → Identity parameters match external database with high confidence (>= 90%).
├── Mismatch        → Explicit discrepancy in name, DOB, or photo.
├── NotFound        → Identifier (BVN/NIN/CAC) does not exist in registry.
├── Pending         → Asynchronous verification in progress (e.g. manual document check).
├── Unavailable     → Provider infrastructure timeout / network partition (triggers fallback).
├── Error           → Provider rejected request format / authentication failure.
└── ReviewRequired  → Fuzzy match score (70–89%) or ambiguous document capture.
```

---

### 11.6 Risk Engine & CDD / EDD Workflows

```text
Customer Registration
        ↓
Risk Assessment Engine (Computes Risk Score: 0 - 100)
  ├── Customer Category (Individual, SME, Large Corporate)
  ├── PEP / Sanctions Match Result (Dojah/Smile ID)
  ├── Geographic Risk & Industry Sector
  └── Initial Velocity Profile
        ↓
Risk Level Classification
  ├── Low Risk (Score < 30)     → Standard CDD (Tier 1/2 automated approval)
  ├── Medium Risk (Score 30-69)  → Standard CDD (Tier 2/3 automated approval + periodic review)
  └── High Risk (Score >= 70)   → Enhanced Due Diligence (EDD Required)
                                      ↓
                                Mandatory EDD Workflow:
                                ├── Source of Funds Documentation
                                ├── Source of Wealth Documentation
                                ├── Purpose & Nature of Relationship
                                ├── Senior Management / Compliance Officer Manual Sign-off
                                └── Ongoing Continuous Transaction Monitoring
```

---

### 11.7 KYC/KYB PII Data Security & NDPR / PCI-DSS Invariants

1. **Zero Plaintext Storage in Logs/Telemetry**: BVN, NIN, Passport numbers, and biometric vectors must never be written to application logs, audit logs, or error traces.
2. **Audit Sanitization**: `AuditSanitizer` automatically masks BVN/NIN (e.g. `222*****123`) before persisting `AuditLog.AfterJson`.
3. **Encryption at Rest**: Sensitive identity fields are encrypted using database-level AES-256-GCM encryption.
4. **Encrypted Blob Storage**: ID document scans and selfie captures reside in private, encrypted cloud storage accessible only via short-lived signed URLs.

---

# 12. Database Architecture

PostgreSQL is the authoritative transactional store.

Use:

* EF Core
* Npgsql
* UUID identifiers
* `timestamptz`
* `numeric` for monetary amounts
* explicit currency fields
* foreign keys
* unique constraints
* check constraints
* carefully designed indexes
* transactions
* concurrency controls

Never use floating-point types for financial amounts.

---

# 13. Financial Data Model

The financial core is:

```text
Wallet
   |
LedgerAccount
   |
LedgerTransaction
   |
LedgerEntry
```

## Wallet

Represents the user's or organization's current financial account.

```text
Wallet
- id
- owner
- currency
- available_balance
- status
- timestamps
```

V1 wallet transaction currency:

```text
NGN
```

## Ledger Account

Represents the accounting account associated with a wallet.

## Ledger Transaction

Represents an atomic financial event.

Examples:

```text
PEER_TRANSFER
BANK_TRANSFER
PAYROLL
LOAN_DISBURSEMENT
LOAN_REPAYMENT
SAVINGS_CONTRIBUTION
SAVINGS_WITHDRAWAL
THRIFT_CONTRIBUTION
THRIFT_PAYOUT
VAS_PURCHASE
FEE
REFUND
REVERSAL
```

## Ledger Entry

Represents an individual debit/credit movement.

---

# 14. Ledger Rules

The ledger is **immutable**.

Never:

```text
UPDATE ledger entry
DELETE ledger entry
```

Corrections occur through new offsetting transactions.

Example:

```text
Original:
Debit  Wallet A   ₦10,000
Credit Wallet B   ₦10,000

Reversal:
Credit Wallet A   ₦10,000
Debit  Wallet B   ₦10,000
```

The PRD explicitly requires immutable ledger entries and offsetting reversals.

---

# 15. Wallet Balance

Wallet balance is a **materialized current state**, not the financial history.

The ledger remains authoritative for transaction history and auditability.

Balance changes and ledger entries occur inside the same database transaction.

```text
BEGIN

Lock relevant wallet rows
Validate balance
Create ledger transaction
Create ledger entries
Update wallet balance

COMMIT
```

Failure:

```text
ROLLBACK
```

---

# 16. Central Ledger Rule

We decided:

**Every monetary movement goes through the central ledger.**

This includes:

```text
Wallet transfers
Payroll
Loans
Savings
Thrift
VAS
Fees
Refunds
Reversals
```

This is consistent with the PRD's requirement for one shared ledger used across the platform.

---

# 17. Financial Concurrency

Financial operations use:

```text
Database transaction
+
Row-level locking
+
Idempotency
+
Appropriate optimistic concurrency
```

Example:

```text
BEGIN

SELECT wallet FOR UPDATE

Check available balance + fees

Create financial transaction
Create ledger entries
Update balance

COMMIT
```

This prevents concurrent operations from spending the same balance.

The PRD requires outbound transfers to be blocked when amount + fees exceed available balance.

---

# 18. Transaction PIN

PIN:

* 4 digits
* required before outbound financial mutations
* server-side hashed
* 3 incorrect attempts → wallet debits locked for 15 minutes
* biometric authentication may substitute on mobile
* raw biometric information never reaches the server

The PRD explicitly specifies bcrypt/Argon2 hashing and these lockout rules.

---

# 19. Capability-Oriented Payment Provider Architecture

Payment integrations follow **capability-oriented provider abstractions**. The core Domain and Application layers remain strictly provider-neutral and are never coupled to external provider SDKs, proprietary DTOs, endpoint URLs, or credentials.

### Provider Capability Routing Matrix

| Capability | Primary Provider | Fallback Provider | Secondary Fallback | Core Abstraction |
| :--- | :--- | :--- | :--- | :--- |
| **Virtual Accounts (DVA)** | **Monnify** | BaaS Rails | Future CebizPay MFB | `IVirtualAccountProvider` |
| **Card Funding** | **Flutterwave** | **Paystack** | — | `ICardPaymentProvider` |
| **Bank Transfers (Payouts)** | **Monnify** | **Flutterwave** | **Paystack** | `IBankTransferProvider` / `IBankTransferExecutor` |
| **Account Resolution** | **Flutterwave** | **Paystack** | **Monnify** | `IBankAccountResolver` |
| **Provider KYC & Limits** | **Monnify** | — | — | `IProviderCustomerProfileProvider` |
| **Reconciliation** | Provider-specific | Provider-specific | Provider-specific | `IPaymentReconciliationProvider` |
| **Individual KYC & ID (BVN/NIN)**| **Dojah** | **Smile ID** | — | `IKycVerificationProvider` |
| **Liveness & Biometrics** | **Smile ID** | **Dojah** | — | `IBiometricVerificationProvider` |
| **Document Verification** | **Smile ID** | **Dojah** | — | `IDocumentVerificationProvider` |
| **AML, PEP & Sanctions** | **Dojah** | **Smile ID** | — | `IAmlScreeningProvider` |
| **Corporate KYB & CAC** | **Dojah** | **Smile ID** | — | `IKybVerificationProvider` |

### Application Layer Boundaries

```text
Application Layer
├── IVirtualAccountProvider
├── ICardPaymentProvider
├── IBankTransferProvider / IBankTransferExecutor
├── IBankAccountResolver
├── IProviderCustomerProfileProvider
├── IPaymentReconciliationProvider
├── IKycVerificationProvider
├── IBiometricVerificationProvider
├── IDocumentVerificationProvider
├── IAmlScreeningProvider
├── IKybVerificationProvider
└── IRiskEngineService
        ↓
Infrastructure Adapters
├── MonnifyPaymentProvider (VA, Payouts, KYC Sync, Reconciliation)
├── FlutterwavePaymentProvider (Cards, Payouts, Account Resolution, Reconciliation)
├── PaystackPaymentProvider (Cards Fallback, Payouts Fallback, Account Resolution, Reconciliation)
├── DojahVerificationAdapter (BVN, NIN, CAC, AML/PEP, Signatories)
└── SmileIdVerificationAdapter (SmartSelfie™ Liveness, 1:1 Biometrics, Document OCR, Fallback ID)
```

No provider-specific models, recipient codes, or request tokens leak into the Domain or Application layers.

---

# 20. Payment Failover & Financial Safety Invariants

Provider failover is state-aware, attempt-tracked, idempotent, and concurrency-safe.

### 20.1 Result Classification Invariants

Every provider operation must map to the authoritative `PaymentProviderResult` classification:

```text
PaymentProviderResultStatus
├── Success           → External operation definitively succeeded.
├── BusinessFailure   → Terminal rejection (e.g. invalid account, blocked recipient). NEVER fail over.
├── TechnicalFailure  → Gateway 5xx / connection failure. Fallback provider dispatch is permitted.
└── Unknown           → Timeout / network partition / ambiguous state. NEVER fail over immediately.
```

### 20.2 Strict Failover Rules

1. **Business Failure**: If a provider rejects an operation with a business rule violation (e.g., account frozen, insufficient destination bank liquidity, invalid NUBAN), **DO NOT fail over automatically**. Fail the transaction cleanly to prevent invalid retry loops.
2. **Technical Failure**: When the primary provider suffers a verified infrastructure outage (HTTP 502/503/504 or network drop before processing):
   - **Bank Transfers**: Monnify (Primary) → Flutterwave (Fallback 1) → Paystack (Fallback 2).
   - **Card Funding**: Flutterwave (Primary) → Paystack (Fallback).
3. **UNKNOWN / Timeout State — Absolute Reconciliation Invariant**:
   - If a provider request times out, returns HTTP 504, or produces an ambiguous response:
   - **DO NOT immediately fail over to the fallback provider.**
   - **DO NOT immediately retry on another rail.**
   - The system MUST query the status endpoint or wait for a webhook to definitively reconcile the in-flight attempt before any secondary dispatch.
   - *Rationale*: Charging Paystack while Flutterwave's charge actually succeeded causes double card charges. Dispatching a Paystack payout while Monnify's transfer is processing causes duplicate disbursements.

---

# 21. External Funding Account & Card Token Architecture

### 21.1 External Funding Account Model

A CebizPay `Wallet` may have **multiple external funding accounts** across multiple partner institutions and providers:

```text
Wallet (Financial Aggregate)
  ├── ExternalFundingAccount #1 (Monnify - Wema Bank)
  ├── ExternalFundingAccount #2 (Monnify - Sterling Bank)
  ├── ExternalFundingAccount #3 (Future BaaS / Partner)
  └── ExternalFundingAccount #4 (Future CebizPay MFB Account)
```

**Key Invariants**:
- `ExternalFundingAccount` belongs directly to a `Wallet` (not directly to a `User`).
- The `Wallet` remains the authoritative CebizPay financial object; external accounts serve as funding and access rails.
- Supported fields: `Id`, `WalletId`, `Provider`, `ProviderCustomerReference`, `ProviderAccountReference`, `AccountNumber`, `BankName`, `BankCode`, `AccountName`, `Currency`, `Status` (Active/Suspended/Closed), `IsPrimary`, `CreatedAtUtc`, `UpdatedAtUtc`.
- Guarantees **Future MFB Portability**: When CebizPay acquires an MFB license, core banking accounts are attached as new `ExternalFundingAccount` records without altering Wallet, Ledger, or Application use cases.

### 21.2 Card Token & Saved Card Security (PCI-DSS Level 1)

CebizPay enforces strict PCI-DSS zero raw credential storage:
- **Never store, log, or transmit**: PAN, CVV/CVC, card PIN, or raw magnetic stripe/chip data.
- Card entry occurs through provider-hosted secure fields / iframes (Flutterwave Standard / Inline, Paystack Popup).
- `SavedCard` entity stores only: `Id`, `WalletId`, `UserId`, `Provider`, `ProviderToken` (reusable authorization token), `MaskedPan` (e.g. `**** **** **** 4123`), `CardBrand` (Visa, Mastercard, Verve), `ExpiryMonth`, `ExpiryYear`, `Status` (Active, Expired, Revoked), `CreatedAtUtc`.
- **Card Lifecycle Support**: (1) Save card, (2) Charge saved card, (3) One-time card funding, (4) Delete/Revoke saved card, (5) Micro-charge verification (zero-auth or ₦50 refundable charge), and (6) Refunds.

### 21.3 Refund Handling & Non-Negative Wallet Invariant

When external providers issue refunds or reversals:
- The provider event is reconciled and matched to the original `FundingTransaction` and `LedgerTransaction`.
- **Invariant**: A reversal/refund MUST NOT make a wallet balance negative (`AvailableBalance >= 0`).
- If insufficient funds remain in the wallet at reversal time:
  - **DO NOT silently debit the wallet below zero.**
  - Debit up to available balance and record the remainder in a **Recovery Outstanding** tracking state for automatic deduction from subsequent deposits.

---

# 21.4 Webhook Architecture & Security

Webhook ingestion is hardened against forgery, replay attacks, and duplicate delivery:

```text
Provider Webhook
      ↓
PaymentsWebhookController (Anonymous endpoint, TLS 1.3)
      ↓
IWebhookSignatureVerifier (Constant-time HMAC / Secret Hash validation)
      ↓
Webhook Event Deduplication (Unique constraint on [Provider, ProviderEventId])
      ↓
Durable Persistence (WebhookEvents table in PostgreSQL)
      ↓
Immediate 200 OK Acknowledgment
      ↓
Transactional Outbox / RabbitMQ Queue
      ↓
Background Worker (PaymentWebhookConsumer / PaymentReconciliationWorker)
      ↓
Central Double-Entry Ledger Posting & Wallet Mutation
```

**Security Rules**:
- **Signature Verification**: Paystack HMAC-SHA512 (`x-paystack-signature`), Flutterwave secret hash (`verif-hash`), Monnify SHA-512 signature (`monnify-signature`). All comparisons use `CryptographicOperations.FixedTimeEquals`.
- **Deduplication**: Webhooks with identical `ProviderEventId` or payload hash are acknowledged idempotently with HTTP 200 without re-executing ledger writes.
- **Asynchronous Execution**: Heavy financial processing, ledger row locking, and external calls occur asynchronously in background workers, never blocking provider webhook timeout SLAs.

---

# 21.5 Configurable Fee Engine & Accounting Models

Funding and payment fees are completely decoupled from hardcoded logic and managed by Super Admin.

### 21.5.1 Fee Calculation Models
- **FREE**: ₦0 fee.
- **FIXED**: Flat amount (e.g. ₦100 per transaction).
- **PERCENTAGE**: Percentage rate (e.g. 1.5%).
- **PERCENTAGE + CAP**: Percentage rate subject to configurable minimum and maximum caps (e.g. 1.5% capped at ₦2,000).

### 21.5.2 Fee Bearer Models
- **CUSTOMER_PAYS**: Fee is added to requested funding amount. Payer is billed `RequestedAmount + Fee`; wallet receives `RequestedAmount`.
- **DEDUCT_FROM_FUNDS**: Fee is deducted from gross inbound funds. Payer sends `GrossAmount`; platform deducts `Fee`; wallet receives `GrossAmount - Fee`.
- **PLATFORM_ABSORBS**: Payer sends `GrossAmount`; wallet receives `GrossAmount`; CebizPay absorbs the provider cost and processing fee as a platform expense.

### 21.5.3 Ledger Double-Entry Representation

Total fee economics separates **Provider Cost** from **CebizPay Platform Fee Revenue**:

```text
Scenario: DEDUCT_FROM_FUNDS (₦100,000 deposit, ₦700 platform fee, ₦150 provider cost)
  DEBIT   Inbound Clearing Account         ₦100,000
  CREDIT  Customer Wallet Account           ₦99,300
  CREDIT  Platform Fee Revenue Account         ₦700

Provider Settlement / Cost:
  DEBIT   Provider Expense Account             ₦150
  CREDIT  Provider Settlement Clearing         ₦150
```

---

# 21.6 Customer KYC vs Provider KYC vs Provider Limits

The system maintains strict architectural separation between three distinct identity/limit domains:

```text
[CebizPay Customer & KYC Policy]
       ↓
  Authoritative for internal permissions (Pending/Rejected cap < ₦50k; Verified uncapped)
       ↓
[Internal Limit / Eligibility Profile]
       ↓
  Calculates single-transaction, daily volume, and velocity rules
       ↓
[Provider Adapter Layer]
       ↓
  Maps internal identity to external compliance requirements
       ↓
[Provider KYC Sync & Provider Limit Profile]
       ↓
  External enforcement constraints (Monnify Tier limits, BVN/NIN validation status)
```

**Guiding Rule**: Provider limit profiles NEVER become the source of truth for CebizPay authorization. However, external provider limits act as physical constraints on transaction dispatch. If an internal user is verified but exceeds Monnify's daily aggregate limit, the system gracefully handles the external provider rejection without corrupting internal KYC status.

---

# 22. RabbitMQ

RabbitMQ is our V1 messaging platform.

Use it for asynchronous communication such as:

```text
Payment events
Notifications
Webhook processing
Reconciliation jobs
Payroll async processing where appropriate
Savings scheduled processing
Thrift cycle processing
Audit-related asynchronous work
Reporting jobs
```

RabbitMQ is **not** the source of truth for financial state.

PostgreSQL remains authoritative.

---

# 23. Transactional Outbox

For important domain events:

```text
Business transaction
        ↓
PostgreSQL transaction
        ↓
Domain state + Outbox record
        ↓
COMMIT
        ↓
Outbox publisher
        ↓
RabbitMQ
        ↓
Consumer
```

This avoids:

```text
Database commit succeeds
RabbitMQ publish fails
Event permanently disappears
```

Consumers must be idempotent because message delivery may occur more than once.

---

# 24. Redis

Redis is **not our primary message broker**.

Use Redis for its strengths:

```text
Caching
OTP state
Rate limiting
Temporary session/security state
Short-lived locks
Distributed coordination where justified
Frequently-read reference data
```

Never use Redis as the authoritative wallet balance store.

---

# 25. API Architecture

API version:

```text
/api/v1
```

Examples from the PRD include:

```text
POST /api/v1/auth/login
POST /api/v1/auth/register/phone

POST /api/v1/wallet/transfer/peer
POST /api/v1/wallet/transfer/bank

POST /api/v1/org/payroll/calculate
POST /api/v1/org/payroll/execute

POST /api/v1/work/loans/apply
PATCH /api/v1/org/loans/{id}/decision

POST /api/v1/savings/plan/create

POST /api/v1/services/purchase/airtime

GET /api/v1/erp/invoices
POST /api/v1/erp/invoices
```

These align with the PRD API architecture.

---

# 26. API Principles

Controllers remain thin.

```text
Controller
    ↓
Application Use Case
    ↓
Domain
    ↓
Infrastructure
```

Do not return EF entities directly.

Use:

```text
Request DTO
Command/Query
Handler/Application Service
Response DTO
```

All API responses must use stable contracts.

---

# 27. Error Handling

Use ASP.NET Core `ProblemDetails`.

Standard structure:

```json
{
  "type": "https://api.cebizpay.com/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "code": "WALLET_INSUFFICIENT_FUNDS",
  "message": "Insufficient available balance.",
  "traceId": "..."
}
```

Rules:

* Never expose stack traces
* Never expose database errors
* Never expose provider secrets
* Never leak sensitive information
* Use stable machine-readable error codes
* Include trace/correlation ID

---

# 28. Idempotency

Idempotency is mandatory for money-moving operations and important external-provider operations.

Request:

```http
Idempotency-Key: <unique-key>
```

Store:

```text
IdempotencyKey
UserId
Endpoint
RequestHash
Status
Response
CreatedAt
```

Rules:

```text
Same key + same request
→ return original result

Same key + different request
→ 409 Conflict
```

This is especially important for:

* wallet transfers
* bank transfers
* payroll execution
* VAS purchases
* payment initialization
* financial contributions

---

# 29. VAS Duplicate Protection

PRD requirement:

```text
Airtime:
₦50 – ₦50,000

Duplicate-purchase block:
120 seconds
```

This will be enforced using a combination of:

```text
Idempotency
+
Redis short-lived duplicate detection
+
Database/provider transaction state
```

The PRD specifies the VAS limit and 120-second duplicate window.

---

# 30. Audit Logging

Audit logging is separate from financial ledger logging.

### Ledger

Answers:

> What happened financially?

### Audit log

Answers:

> Who performed what action, when and from where?

Audit record should capture:

```text
ActorId
Action
ResourceType
ResourceId
Before
After
IP
UserAgent
CorrelationId
Timestamp
```

The PRD requires immutable audit entries for:

* status transitions
* password changes
* PIN changes
* permission toggles
* admin deletion
* voucher metadata edits.

---

# 31. Soft Delete

Hard-delete UI actions must resolve to soft deletion for applicable entities.

```text
is_deleted = true
```

This protects historical:

* ledger references
* vouchers
* invoices
* business records

The PRD specifically calls this out for inventory items, customers, suppliers and admins.

---

# 32. Background Workers

The Worker project handles long-running and scheduled operations.

Examples:

```text
OTP/email/SMS processing
Notifications
Payment reconciliation
Provider webhook processing
Savings accrual
Loan repayment processing
Thrift cycle processing
Report generation
Expired token cleanup
Outbox publishing
```

HTTP requests should not remain open waiting for long-running work unless the PRD explicitly requires synchronous completion.

---

# 33. Caching Strategy

Cache only data where stale values are acceptable.

Good candidates:

```text
Reference/configuration data
Public/static data
Frequently-read non-critical metadata
Rate-limit state
OTP state
Short-lived session/security state
```

Avoid caching authoritative:

```text
Wallet balance
Ledger state
Payment state
Loan balance
```

unless the cache is explicitly treated as a performance optimization and never the source of truth.

---

# 34. Observability

Every request should carry:

```text
TraceId
CorrelationId
```

Structured logs should capture:

```text
Timestamp
Level
Service
Environment
TraceId
CorrelationId
UserId where appropriate
OrganizationId where appropriate
Endpoint
Duration
Result
Error code
```

Metrics:

```text
HTTP latency
HTTP error rate
Database latency
Database pool utilization
RabbitMQ queue depth
Worker failures
Payment success rate
Payment provider latency
Ledger failures
Redis hit rate
Cache latency
```

Health checks:

```text
API
PostgreSQL
Redis
RabbitMQ
Critical external dependencies
```

---

# 35. Testing Strategy

## Unit Tests

Test:

* Domain rules
* Validators
* Financial calculations
* DTI calculation
* Savings penalties
* Thrift rules
* Permission policies

## Integration Tests

Test against real infrastructure where practical:

```text
PostgreSQL
Redis
RabbitMQ
```

Use containers for reproducibility.

## API Tests

Validate:

* authorization
* tenant isolation
* status restrictions
* request/response contracts
* error contracts

## Financial Tests

Mandatory:

```text
Insufficient funds
Concurrent transfer
Duplicate request
Rollback
Reversal
Provider timeout
Provider duplicate webhook
Provider failure
Reconciliation
```

## Architecture Tests

Ensure:

```text
Domain → no Infrastructure dependency
Application → no direct provider dependency
Controllers → no business logic
```

---

# 36. Performance Requirements

The PRD specifies:

* Core write operations ≤ 1,500ms under normal network conditions
* Database layer supports at least 5,000 concurrent connections
* At least 250 write operations/sec without degradation.

These are acceptance targets, not reasons to blindly configure 5,000 active PostgreSQL connections per API instance.

The actual implementation will use:

```text
Connection pooling
+
bounded DB concurrency
+
efficient queries
+
proper indexes
+
horizontal API scaling
+
async processing
```

---

# 37. Load Testing

We will test:

### Baseline

Normal expected traffic.

### Peak

Expected launch traffic.

### Stress

Beyond expected capacity.

### Spike

Sudden traffic increase.

### Soak

Long-running sustained traffic.

### Financial concurrency

Simultaneous:

```text
Wallet transfers
Payroll execution
VAS purchases
Savings contributions
Loan operations
```

### Failure testing

Simulate:

```text
PostgreSQL slowdown
Redis failure
RabbitMQ backlog
Flutterwave failure
Paystack failure
Network timeout
Worker crash
Duplicate webhook
```

---

# 38. Deployment Architecture

V1:

```text
                         Internet
                            |
                         Render
                            |
                    ASP.NET Core API
                       /         \
                      /           \
               PostgreSQL        Redis
                      |
                  RabbitMQ
                      |
               Worker Process
                      |
             External Providers
                /           \
         Flutterwave       Paystack
```

Everything runs in Docker.

---

# 39. Docker

Docker is required from day one.

We should have separate containerized components where appropriate:

```text
API
Worker
```

Infrastructure dependencies in development can be run using Docker Compose:

```text
PostgreSQL
Redis
RabbitMQ
```

Production deployment configuration will remain environment-driven.

---

# 40. GitHub Actions CI/CD

Pipeline:

```text
Push / Pull Request
        ↓
Restore
        ↓
Build
        ↓
Unit Tests
        ↓
Integration Tests
        ↓
Architecture Tests
        ↓
Docker Build
        ↓
Security/quality checks
        ↓
Deploy
```

Deployment should only happen after required checks pass.

---

# 41. Render V1

Render is the initial deployment platform.

Keep the application cloud-portable by avoiding unnecessary coupling to Render-specific services.

Future migration target:

```text
Render
   ↓
AWS
```

Potential future AWS equivalents:

```text
Render Web Service → ECS/EKS
Render PostgreSQL → RDS PostgreSQL
Render Redis → ElastiCache
RabbitMQ → Amazon MQ / managed RabbitMQ
```

This is an engineering portability strategy, not a current infrastructure requirement.

---

# 42. Security Threat Model

Priority threats:

```text
Account takeover
Credential stuffing
OTP abuse
Broken authorization
IDOR
Tenant isolation failure
Privilege escalation
Payment replay
Double spending
Webhook forgery
Webhook replay
API abuse
SQL injection
Sensitive-data leakage
Secret leakage
Admin abuse
```

Controls include:

```text
ASP.NET Identity
RBAC/permissions
Authorization policies
Rate limiting
OTP throttling
PIN protection
MFA
Idempotency
Database transactions
Row locking
Webhook verification
Immutable ledger
Audit logging
Encryption
TLS
Secrets management
```

## The PRD explicitly requires TLS, encryption at rest, PIN protection, MFA, OAuth-style tokens, PCI-DSS Level 1 and NDPR compliance.

# 43. Important PRD Business Rules

These must remain visible during implementation.

### KYC/KYB

Pending/rejected Individuals can transact with the specified outbound cap.

Pending/rejected Organizations cannot execute payroll or transfer wallet funds.

Only verified Individuals can accept Staff invitations with full benefits.

Only verified Organizations can activate automated payroll/corporate savings.

### Payroll

Supports:

```text
Pay All
By Department
By Role
By Level
By Individual
```

Payroll must validate wallet sufficiency and generate payment vouchers.

### Loans

Total monthly debt obligations must not exceed:

```text
33% of verified monthly salary
```

Loan repayment takes priority before net payroll credit.

### Savings

Fixed individual plans:

```text
30 days – 2 years
8–15% annual interest
Daily accrual
```

Early withdrawal:

```text
100% accrued interest
+
2.5% principal
```

### Thrift

Two consecutive missed cycles:

```text
Suspend payout eligibility
Flag group administrator
```

Departing members:

```text
Net contribution refund within 24 hours
```

---

# 44. V1 Open Questions — Now In Scope

We decided to treat all PRD recommendations as V1 scope.

Therefore V1 includes:

### Thrift Oversight

Super Admin:

* thrift directory
* delinquency flags
* dispute queue

### ERP Compliance Linkage

Organization suspension:

* freezes ERP writes
* allows read-only summary visibility where specified

### Suspension Notification

Organization suspension produces an in-app/push notification to affected Staff.

### Support Reporting

Support ticket volume/resolution reporting is included for platform-wide support monitoring.

These four items are explicitly identified by the PRD as recommendations/open decisions, and we chose to include them in V1.

---

# 45. Development Roadmap

## Phase 0 — Foundation

```text
Solution
Clean Architecture
Docker
PostgreSQL
EF Core
ASP.NET Identity
Authentication
JWT/token system
Redis
RabbitMQ
Exception handling
ProblemDetails
Logging
Health checks
API versioning
GitHub Actions
```

## Phase 1 — Identity & Compliance

```text
Individual registration
Organization registration
Login
OTP
MFA
Password management
Roles
Permissions
KYC
KYB
Admin review
Organization membership
```

## Phase 2 — Financial Core

```text
Wallet
Ledger
Balance
Transaction PIN
Peer transfer
Bank transfer
Fees
Idempotency
Concurrency
Audit
```

**This is the most critical foundation.**

## Phase 3 — Payments

```text
Flutterwave
Paystack
Virtual accounts
Cards
Webhooks
Reconciliation
Provider failover
```

## Phase 4 — Financial Products

```text
Payroll
Loans
Savings
Thrift
VAS
```

## Phase 5 — HRIS + ERP

```text
Staff
Departments
Roles
Salary levels
Recruitment
Inventory
Suppliers
Customers
Orders
Invoices
Expenses
Vouchers
Reports
```

## Phase 6 — Communication + Governance

```text
Announcements
Notifications
Referrals
Support
Thrift oversight
Admin governance
```

## Phase 7 — Production Hardening

```text
Integration testing
Load testing
Security testing
Concurrency testing
Failure testing
Observability
Performance optimization
Backup/restore
Disaster recovery
Production deployment
```

---

# 46. AI-Assisted Development Rules

AI tools will be used as **engineering assistants, not architectural authorities**.

Available tools:

```text
Antigravity CLI
GitHub Copilot
ChatGPT
Claude
```

## ChatGPT

Use for:

* architecture reasoning
* reviewing designs
* explaining unfamiliar .NET concepts
* challenging implementation decisions
* code review
* test strategy
* debugging
* threat modeling
* documentation
* analyzing trade-offs

Do not blindly accept generated architecture.

## Copilot

Use primarily for:

* repetitive implementation
* DTOs
* mappings
* straightforward CRUD
* test scaffolding
* boilerplate
* refactoring suggestions

The developer remains responsible for the design.

## Antigravity CLI

Use for:

* repository-wide exploration
* implementation assistance
* repetitive multi-file changes
* navigating larger codebases
* test execution/debugging assistance

Every significant generated change must still be reviewed.

## Claude

Use as a secondary reviewer when useful, especially for:

* alternative architectural opinions
* code review
* reasoning comparison
* identifying overlooked edge cases

---

# 47. AI Safety Rule for Financial Code

AI-generated code must receive extra scrutiny for:

```text
Money calculations
Ledger operations
Concurrency
Transactions
Authorization
Authentication
Password/PIN handling
Payment integrations
Webhook processing
Idempotency
KYC/KYB
Tenant isolation
```

For these areas:

```text
AI suggestion
      ↓
Developer reasoning
      ↓
Tests
      ↓
Code review
      ↓
Integration validation
```

Never paste AI-generated financial code directly into production without understanding it.

---

# 48. Definition of Done

A feature is not considered complete merely because the endpoint works.

For important backend functionality:

```text
Requirement understood
        ↓
Domain rule identified
        ↓
Data model designed
        ↓
Application use case implemented
        ↓
Authorization implemented
        ↓
Validation implemented
        ↓
Error handling implemented
        ↓
Transaction/concurrency reviewed
        ↓
Idempotency considered
        ↓
Audit requirements considered
        ↓
Events considered
        ↓
Unit tests
        ↓
Integration tests
        ↓
API tests
        ↓
Logging/observability
        ↓
Documentation
        ↓
Code review
```

---

# 49. Current Project State

## Locked

```text
Architecture:
Modular Monolith + Clean Architecture

Database:
PostgreSQL

Identity:
ASP.NET Core Identity
Single ApplicationUser

Tenancy:
Shared database/schema
Organization-scoped data

Membership:
User can belong to multiple organizations

Organization wallet:
One primary wallet in V1

Currency:
NGN transactional currency in V1

Ledger:
Central double-entry immutable ledger

External Funding Accounts:
Multiple ExternalFundingAccount records per Wallet

Virtual Accounts (DVA):
Monnify primary

Card Funding:
Flutterwave primary, Paystack fallback

Bank Transfers (Payouts):
Monnify primary, Flutterwave fallback, Paystack fallback

Funding Fee Model:
Configurable calculation (Free, Fixed, Percentage, Percentage+Cap)
Configurable fee bearer (CustomerPays, DeductFromFunds, PlatformAbsorbs)

Future MFB Portability:
Core banking accounts attach as ExternalFundingAccount without mutating domain core

Messaging:
RabbitMQ

Cache/state:
Redis

Hosting:
Render V1

Containers:
Docker

CI/CD:
GitHub Actions
```

## PRD requirements explicitly preserved

```text
KYC/KYB
RBAC
MFA
OTP
PIN
Wallet
Ledger
Payroll
Loans
Savings
Thrift
VAS
ERP
Invoicing
Announcements
Referrals
Audit
Admin Governance
PCI-DSS
NDPR
1,500ms core-write target
250 writes/sec
5,000 concurrent DB connections
```

## V1 recommendations adopted

```text
Thrift oversight
ERP suspension linkage
Suspension notifications
Support reporting
```

---

# 50. Implementation Starting Point

We are now ready to begin implementation.

The first implementation milestone should be:

```text
PHASE 0 — FOUNDATION

1. Create Git repository
2. Create .NET solution
3. Create projects
4. Establish project references
5. Configure analyzers
6. Configure PostgreSQL
7. Configure EF Core
8. Configure ASP.NET Identity
9. Configure authentication/token infrastructure
10. Configure Redis
11. Configure RabbitMQ
12. Configure Docker
13. Configure structured logging
14. Configure global exception handling
15. Configure ProblemDetails
16. Configure health checks
17. Configure API versioning
18. Create initial GitHub Actions pipeline
19. Create development docker-compose
20. Create initial database migration
21. Verify entire stack locally
```

**We should implement this foundation before building any business module.**

The first business module after the foundation should be **Identity + Organization + Compliance**, followed by the **Financial Core/Ledger**.

---

# 51. Context Transfer Protocol

If this conversation becomes too large and we need a new chat, the following document should be transferred as the project's master context.

The new conversation should be instructed:

> **Treat the following CEBIZPAY Master Architecture & Engineering Specification as the authoritative project context. Do not redesign previously locked decisions unless a contradiction with the PRD or an implementation issue requires it. Continue from the current implementation phase. Ask me only when a major decision is genuinely unresolved. Keep responses concise and implementation-focused.**

The PRD remains the **source of truth for product requirements**.

This specification is the **source of truth for our engineering decisions** unless we explicitly revise one.

---

# 52. Change History / Superseded Decisions

The following decisions have been updated and synchronized across all authoritative engineering documentation:

### 1. Payment Provider Architecture & Hierarchy
* **OLD DECISION**: Flutterwave primary, Paystack fallback across all payment and payout operations.
* **UPDATED DECISION**: Capability-specific routing:
  - Virtual Account Provisioning: Monnify primary (`IVirtualAccountProvider`)
  - Card Funding: Flutterwave primary, Paystack fallback (`ICardPaymentProvider`)
  - Bank Transfers / Payouts: Monnify primary, Flutterwave fallback, Paystack secondary fallback (`IBankTransferProvider`)
  - Account Resolution: Flutterwave, Paystack, Monnify (`IBankAccountResolver`)
* **REASON**: Optimizes transaction routing, fee economics, and provider capabilities while maintaining loose coupling through provider-neutral domain abstractions.

### 2. External Funding Account Architecture
* **OLD DECISION**: Dedicated virtual accounts were represented as a single `VirtualAccount` entity tied to an `IndividualId` or `OrganizationId`.
* **UPDATED DECISION**: Multiple `ExternalFundingAccount` records belong directly to a `Wallet`. Supports simultaneous accounts across providers (Monnify Wema, Sterling, Moniepoint) and future CebizPay MFB core-banking accounts.
* **REASON**: The Wallet is the core CebizPay financial ledger object. Decoupling external accounts onto the Wallet allows multi-provider funding and guarantees zero-downtime portability to a future MFB license.

### 3. Card Funding Lifecycle & Tokenization
* **OLD DECISION**: Basic checkout initialization without token persistence or micro-charge support.
* **UPDATED DECISION**: Full V1 card funding lifecycle: (1) Save card, (2) Charge saved card, (3) One-time card funding, (4) Delete/Revoke card, (5) Micro-charge / zero-auth verification, and (6) Refunds. Zero raw PAN/CVV/PIN storage (safe provider tokens and masked display metadata only).
* **REASON**: Enforces PCI-DSS compliance while unlocking frictionless repeat card funding and thrift automated collections.

### 4. Fee Engine & Accounting Models
* **OLD DECISION**: Hardcoded/fixed transfer fees.
* **UPDATED DECISION**: Configurable fee calculation models (`FREE`, `FIXED`, `PERCENTAGE`, `PERCENTAGE_WITH_CAP`) and configurable fee bearer models (`CUSTOMER_PAYS`, `DEDUCT_FROM_FUNDS`, `PLATFORM_ABSORBS`) managed by Super Admin. Provider costs and CebizPay platform fees are recorded in separate ledger accounts.
* **REASON**: Enables dynamic commercial models and full double-entry ledger transparency.

### 5. Failover Invariants & UNKNOWN State Safety
* **OLD DECISION**: Blind 3-second failover to secondary provider.
* **UPDATED DECISION**: Strict classification:
  - `BusinessFailure`: NEVER fail over.
  - `TechnicalFailure`: Fallback allowed.
  - `Unknown` / Timeout: Strict prerequisite to reconcile in-flight attempt before any retry or failover.
* **REASON**: Prevents double card charges, duplicate bank disbursements, and financial ledger corruption.

### 6. KYC/KYB Strategic Multi-Provider Routing & Provider Evidence Model
* **OLD DECISION**: Single generic manual KYC review without automated provider integration or capability routing.
* **UPDATED DECISION**: Multi-provider capability-based routing:
  - Individual ID / BVN / NIN: Dojah primary, Smile ID fallback (`IKycVerificationProvider`)
  - Liveness & 1:1 Biometrics: Smile ID (SmartSelfie™) primary, Dojah fallback (`IBiometricVerificationProvider`)
  - Document OCR & Verification: Smile ID primary, Dojah fallback (`IDocumentVerificationProvider`)
  - AML, PEP & Sanctions: Dojah primary, Smile ID fallback (`IAmlScreeningProvider`)
  - Bank Account Name Resolution: Flutterwave primary, Paystack fallback 1, Monnify fallback 2 (`IBankAccountResolver`)
  - CAC / Business Verification: Dojah primary, Smile ID fallback (`IKybVerificationProvider`)
  - Beneficial Owners / Directors: Dojah primary, Smile ID fallback (`IKybVerificationProvider`)
* **REASON**: Maximizes verification accuracy, leverages specialized vendor strengths, and prevents single-provider vendor lock-in without redundantly routing low-risk customers through multiple providers.

### 7. CBN Customer Due Diligence 2023 & Sovereign Compliance Authority
* **OLD DECISION**: External verification results directly drove user status; Individual Tiered KYC applied generically.
* **UPDATED DECISION**:
  - Full alignment with Central Bank of Nigeria (Customer Due Diligence) Regulations, 2023.
  - Clear architectural separation between Individual Tiered KYC (Tier 1, 2, 3) and Corporate CDD (CAC, MemArt, UBO >= 5%, Directors, Signatories, TIN).
  - Sovereign Compliance Authority: CebizPay internal compliance engine and compliance officer reviews remain authoritative. External provider results provide verification evidence (`VerificationEvidence`), not unconstrained authorization.
  - Provider outage or timeout is never treated as customer verification failure.
  - Downstream provider KYC synchronization pushes internal verified identity data to external banking rails (Monnify) to unlock provider transaction limit profiles.
* **REASON**: Enforces strict compliance with CBN CDD Regulations 2023 and AML/CFT/CPF legislation while retaining sovereign platform control over customer risk.

### 8. Future MFB Portability
* **OLD DECISION**: Virtual accounts modeled as static single-provider records tied to users.
* **UPDATED DECISION**: External accounts attach to `Wallet` as `ExternalFundingAccount` records. When CebizPay secures an MFB license, internal core-banking accounts attach as `ExternalFundingAccount` records with zero modifications to Wallet, Ledger, or Application use cases.
* **REASON**: Guarantees seamless, zero-downtime evolution into a licensed Microfinance Bank without technical debt or domain rewrites.

### 9. Unified Webhook Ingestion & Multi-Rail Reconciliation Hardening (Batch 7)
* **OLD DECISION**: Provider webhooks were processed synchronously and could directly mutate ledger balances or retry blindly.
* **UPDATED DECISION**:
  - **Thin HTTP Boundary**: Webhook Ingestion $\to$ Signature Verification (HMAC-SHA512/SHA256) $\to$ SHA-256 Payload Hash $\to$ Durable `WebhookEvent` / `ComplianceWebhookEvent` Persistence $\to$ DB Deduplication $\to$ Immediate HTTP 200 Fast Acknowledgement.
  - **Asynchronous Worker Claiming**: Distributed, concurrency-safe worker claiming via PostgreSQL row locking (`FOR UPDATE SKIP LOCKED`), bounded exponential backoff with jitter, and dead-letter isolation (`DeadLetter`).
  - **Strict Financial Ledger Rule**: Webhooks are strictly external signals, NEVER the financial ledger. Canonical flow: `Webhook → validated event → internal operation → verified provider state → central ledger → wallet`. PostgreSQL + central ledger remain authoritative.
  - **Authoritative Cross-Rail Reconciliation**: `IReconciliationEngine` coordinates status requeries across Payment Attempts, Bank Transfers, Card Funding, Card Refunds, and Compliance Operations.
  - **UNKNOWN State Safety**: Ambient or requery UNKNOWN statuses trigger exponential polling backoff and NEVER trigger premature failover or ledger reversal.
  - **Zero Negative Wallet Balance Guarantee**: Refund reversals exceeding available balance transition to `CardRefundStatus.RecoveryOutstanding` and persist a durable `RecoveryOutstandingRecord`.
  - **Administrative Super Admin Governance**: Endpoints for status requery (`/api/v1/admin/reconciliation/requery`), event retries (`/api/v1/admin/reconciliation/events/{id}/retry`), and manual review dispositions (`ConfirmSuccess`, `ConfirmFailure`, `ConfirmReversal`, `Dismiss`).
* **REASON**: Guarantees zero double credits, zero duplicate ledger postings, resilient recovery from provider outages, and complete regulatory auditability.



