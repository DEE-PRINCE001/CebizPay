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
| Currency                 | NGN transactional currency in V1                         |
| Other currencies         | Reporting/FX architecture only in V1                     |
| Ledger                   | Central ledger for every monetary movement               |
| Ledger entries           | Immutable                                                |
| Payment provider         | Flutterwave primary                                      |
| Payment fallback         | Paystack                                                 |
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
                 /             \
          Flutterwave         Paystack
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

* Provider abstraction
* Payment attempts
* Provider references
* Provider webhooks
* Reconciliation
* Provider failover

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

# 11. KYC/KYB

## Individual KYC

Supports:

* Government ID
* NIMC Card
* Driver's License
* International Passport
* Liveness selfie
* Automated quality checks
* Manual Admin review after failed automated attempts

## Organization KYB

Two-step registration:

1. Company name/email/phone
2. Year established + CAC certificate + company logo

Documents are reviewed by Super Admin.

Statuses:

```text
PENDING
VERIFIED
REJECTED
SUSPENDED
```

The PRD specifies different transactional restrictions based on these statuses.

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

# 19. Payment Provider Architecture

Flutterwave is primary.

Paystack is fallback.

Application layer:

```text
IPaymentProvider
```

Infrastructure:

```text
FlutterwavePaymentProvider
PaystackPaymentProvider
```

Flow:

```text
Application
    ↓
IPaymentProvider
    ↓
Flutterwave
    ↓
Failure handling / fallback policy
    ↓
Paystack where appropriate
```

Provider-specific models must not leak into Domain/Application.

---

# 20. Payment Failover

PRD specifies automatic switching to a secondary gateway within 3 seconds when the primary is unavailable.

However:

**Fallback does not mean blindly retrying every failed transaction.**

We distinguish:

```text
Business rejection
→ do not automatically retry

Technical failure
→ fallback may be appropriate

Timeout / unknown provider state
→ reconcile before retry

Known success
→ never retry
```

This prevents duplicate financial operations.

---

# 21. Payment Provider Data

Separate:

```text
CEBIZPAY transaction
```

from:

```text
Provider transaction
```

Example:

```text
CEBIZPAY Transaction
reference = CP-123

Payment Attempt
provider = FLUTTERWAVE

Provider reference
FLW-999

Webhook
FLW-999 → SUCCESS
```

Provider references must be unique.

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
Central immutable ledger

Payments:
Flutterwave primary
Paystack fallback

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
