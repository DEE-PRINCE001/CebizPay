# CebizPay Backend — Phase 0 Implementation

You are working on the backend of **CebizPay**, a production-oriented fintech application.

Your job in this task is to **complete the remaining Phase 0 foundation** of the backend.

Do NOT start implementing business-domain features yet.

---

## 1. NON-NEGOTIABLE PROJECT DECISIONS

These decisions have already been made and MUST NOT be changed without asking me.

### Technology

* .NET 10
* C#
* ASP.NET Core Web API
* PostgreSQL
* Entity Framework Core
* ASP.NET Core Identity
* JWT authentication
* MediatR
* CQRS
* FluentValidation
* Redis
* RabbitMQ
* Docker
* Docker Compose
* GitHub Actions
* Render initially
* Architecture must remain portable enough to move to AWS later

### Architecture

Use a **Clean Architecture Modular Monolith** for V1.

We deliberately are NOT using microservices yet.

Project structure:

```text
src/
├── CebizPay.Api
├── CebizPay.Application
├── CebizPay.Domain
├── CebizPay.Infrastructure
└── CebizPay.Workers

tests/
├── CebizPay.UnitTests
├── CebizPay.IntegrationTests
├── CebizPay.ApiTests
└── CebizPay.ArchitectureTests
```

Dependency direction:

```text
Domain
   ↑
Application
   ↑
Infrastructure
   ↑
API / Workers
```

More precisely:

```text
Domain
  └── no infrastructure dependencies

Application
  └── depends on Domain

Infrastructure
  ├── depends on Application
  └── depends on Domain

API
  ├── depends on Application
  └── depends on Infrastructure

Workers
  ├── depends on Application
  └── depends on Infrastructure
```

Do not introduce dependencies that violate this boundary.

---

## 2. EXISTING WORK — DO NOT RECREATE IT

Before modifying anything:

### FIRST perform a READ-ONLY repository audit.

Inspect:

* solution
* all `.csproj` files
* `Directory.Build.props`
* `Directory.Packages.props`
* `.editorconfig`
* `.gitignore`
* `global.json`
* Docker files
* Docker Compose
* `Program.cs`
* `appsettings*.json`
* existing EF Core configuration
* existing migrations
* existing Identity configuration
* existing Application interfaces
* existing Infrastructure implementations
* existing tests
* GitHub workflows if present

Determine what is already implemented.

**Do not recreate existing work.**

Do not overwrite working code merely to make it look different.

Do not downgrade packages.

Do not remove working functionality unless necessary to fix an architectural problem.

---

# 3. CURRENT FOUNDATION

The solution currently builds successfully.

The following have already been established:

* .NET 10
* `.slnx` solution format
* Clean Architecture project structure
* Application/Domain/Infrastructure/API/Workers projects
* Test projects
* Project references
* central build configuration
* PostgreSQL
* EF Core
* ASP.NET Identity foundation
* PostgreSQL Docker container
* Redis Docker container
* RabbitMQ Docker container
* EF migration foundation
* CQRS/MediatR
* FluentValidation
* Redis package
* RabbitMQ package
* Testcontainers foundation

There has also already been a Microsoft.OpenApi package vulnerability issue which has been resolved.

Do not reintroduce the vulnerable dependency.

---

# 4. PHASE 0 OBJECTIVE

Complete the engineering foundation required before implementing actual CebizPay business modules.

The completed foundation must include:

```text
Configuration
PostgreSQL
EF Core
Identity
JWT Authentication
Authorization foundation
CQRS/MediatR
FluentValidation
Redis
RabbitMQ
Messaging abstraction
Outbox foundation
Structured logging
Global exception handling
ProblemDetails
OpenTelemetry
Health checks
Readiness/liveness
OpenAPI
API versioning
CORS
Rate limiting foundation
Security headers
Docker
Docker Compose
Environment configuration
GitHub Actions CI
Automated tests
Architecture tests
Database migration workflow
Production-oriented defaults
```

Do this professionally, but do not over-engineer.

---

# 5. CONFIGURATION

Implement strongly typed configuration using Options pattern where appropriate.

Configuration must support:

* PostgreSQL
* Redis
* RabbitMQ
* JWT
* CORS
* application/environment settings

Production secrets MUST NOT be hardcoded.

Support environment variables cleanly.

Create/update:

```text
.env.example
```

It should contain variable names and safe examples/placeholders only.

Never commit real secrets.

Validate required configuration at startup where appropriate.

Fail fast for invalid critical configuration.

---

# 6. DATABASE / EF CORE

Use PostgreSQL through Npgsql.

Ensure:

* clean `ApplicationDbContext`
* EF Core dependency remains inside Infrastructure
* migrations remain inside Infrastructure
* proper connection configuration
* retry strategy where appropriate
* command timeout is sensible
* cancellation tokens are respected
* no business logic is placed inside DbContext
* no database credentials are hardcoded

Do not introduce premature database sharding, read replicas, or multiple databases.

Those are not Phase 0 requirements.

---

# 7. ASP.NET IDENTITY

Use ASP.NET Core Identity.

The system must provide a foundation for:

* users
* roles
* password hashing
* security stamps
* token generation
* password reset
* email/phone verification foundation
* account security

Do not prematurely invent business-specific user properties.

If a custom `ApplicationUser` is required by the existing architecture, implement it cleanly.

Otherwise keep the current Identity foundation intact until the Identity domain is designed.

---

# 8. JWT AUTHENTICATION

Implement JWT authentication infrastructure.

Include:

* token validation
* issuer validation
* audience validation
* signing key validation
* expiration validation
* clock skew kept appropriately small
* secure configuration through environment variables
* authentication middleware
* authorization middleware

Do NOT hardcode JWT secrets.

Do NOT implement insecure development bypasses.

Do not build refresh-token business flows unless the existing project already requires them.

If refresh-token design requires a major architectural decision, STOP and ask.

---

# 9. AUTHORIZATION FOUNDATION

Prepare authorization for:

* roles
* policies
* claims

Keep authorization extensible for future:

* customer
* organization
* admin
* staff
* organization-level permissions

Do not invent the final permission matrix yet.

---

# 10. CQRS / MEDIATR

Use lightweight CQRS.

Commands:

```text
change state
```

Queries:

```text
read data
```

MediatR handlers should orchestrate use cases.

Do NOT put the entire business domain inside handlers.

Do NOT create unnecessary abstractions for trivial operations.

Do not introduce a separate read database.

Do not introduce event sourcing.

Do not introduce microservices.

---

# 11. VALIDATION

Use FluentValidation.

Validation should be separated from domain invariants.

Use:

```text
FluentValidation
    ↓
request/input validation

Domain
    ↓
business invariants
```

Don't duplicate validation unnecessarily.

---

# 12. REDIS

Implement Redis infrastructure behind an Application abstraction.

Application must NOT directly reference StackExchange.Redis.

Use an interface such as:

```text
ICacheService
```

Infrastructure implements it.

Redis will eventually support:

* caching
* OTP/session-related temporary data
* rate limiting support where appropriate
* distributed coordination where justified

Do NOT use Redis as the source of truth for financial data.

Do NOT store the central ledger in Redis.

Implement sensible serialization and expiration behavior.

---

# 13. RABBITMQ

Implement RabbitMQ infrastructure behind an Application abstraction.

Application should not depend directly on RabbitMQ APIs.

Create an abstraction such as:

```text
IEventPublisher
```

Infrastructure owns RabbitMQ implementation.

Prepare for:

* domain/integration events
* asynchronous processing
* retries
* dead-letter handling
* durable queues
* durable exchanges
* publisher confirms where appropriate
* consumer acknowledgements
* graceful shutdown

Do not create dozens of queues/exchanges without actual business requirements.

Keep the foundation extensible.

---

# 14. OUTBOX FOUNDATION

Because this is a fintech application, asynchronous events must not be published in a way that creates a database/message consistency gap.

Implement an **Outbox Pattern foundation**.

The design should support:

```text
Database transaction
       ↓
Business state + Outbox message
       ↓
commit
       ↓
background publisher
       ↓
RabbitMQ
```

The Outbox must be persisted in PostgreSQL.

Include fields sufficient for:

* message ID
* event type
* payload
* created timestamp
* processed timestamp
* retry count
* error information
* processing status

Implement this as a reusable foundation.

Do NOT implement specific wallet/payment events yet.

---

# 15. BACKGROUND WORKERS

The Workers project should be prepared to run background processing.

At minimum prepare the architecture for:

```text
Outbox publisher
RabbitMQ consumers
retry processing
scheduled/background tasks
```

Workers must:

* shut down gracefully
* respect cancellation tokens
* avoid duplicate processing where possible
* log failures
* not crash the entire service because of one bad message

Do not create fake business consumers.

---

# 16. GLOBAL ERROR HANDLING

Implement centralized exception handling.

Use ASP.NET Core `ProblemDetails`.

The API should return consistent error responses.

Do not expose:

* stack traces
* database errors
* internal implementation details
* secrets

in production responses.

Support appropriate mappings for:

* validation errors
* unauthorized
* forbidden
* not found
* conflict
* business/domain errors
* unexpected errors

Use a correlation/trace identifier.

---

# 17. STRUCTURED LOGGING

Implement structured logging.

Prefer Serilog if compatible with the current project.

Logs should support:

* timestamp
* log level
* correlation/trace ID
* request information
* exception information
* environment
* service name

Do NOT log:

* passwords
* JWTs
* refresh tokens
* OTPs
* API keys
* payment credentials
* full sensitive financial information
* sensitive personal information unnecessarily

Use structured properties instead of concatenated log strings.

---

# 18. OBSERVABILITY

Implement a reasonable OpenTelemetry foundation.

Prepare:

```text
Logs
Metrics
Traces
```

At minimum support:

* HTTP request tracing
* database instrumentation
* outgoing HTTP instrumentation
* useful runtime metrics

Do not build a complicated observability platform in Phase 0.

The implementation must remain compatible with future production exporters.

---

# 19. HEALTH CHECKS

Implement health endpoints.

At minimum distinguish:

```text
Liveness
Readiness
```

Readiness should verify critical dependencies such as:

```text
PostgreSQL
Redis
RabbitMQ
```

Liveness should not fail merely because a downstream dependency is temporarily unavailable.

The health endpoints must be suitable for:

* Docker
* Render
* Kubernetes/future infrastructure

---

# 20. API DOCUMENTATION

Configure OpenAPI professionally.

Requirements:

* no vulnerable OpenAPI dependency
* XML documentation support if already configured
* endpoint documentation
* authentication scheme documentation
* useful API metadata

Do not introduce an unnecessary Swagger dependency if the current .NET 10 OpenAPI approach is sufficient.

---

# 21. API VERSIONING

Establish API versioning.

Use a consistent strategy.

Prefer a URL-based strategy such as:

```text
/api/v1/...
```

unless the current project already has a deliberate alternative.

Do not create multiple versions of every endpoint yet.

---

# 22. CORS

Configure CORS through environment/configuration.

Do NOT use:

```text
AllowAnyOrigin
```

as a production default.

Development may use controlled origins.

Production origins must come from configuration.

---

# 23. RATE LIMITING

Establish ASP.NET Core rate limiting foundation.

Use it for protection of endpoints where appropriate.

Do not implement arbitrary limits without understanding the eventual business requirements.

Prepare the architecture for distributed deployment.

If Redis-backed distributed rate limiting is required beyond the built-in capabilities, document it rather than inventing an unsafe implementation.

---

# 24. SECURITY HEADERS

Implement sensible HTTP security headers where applicable.

Consider:

* content type sniffing protection
* frame protection
* referrer policy
* HTTPS-related headers

Do not blindly copy a browser security policy that breaks APIs.

Do not implement HSTS in a way that causes local development problems.

Production behavior may differ from development.

---

# 25. DOCKER

Create production-quality Docker support.

We need:

```text
Dockerfile
.dockerignore
docker-compose.yml
```

Use a multi-stage Docker build.

Requirements:

* non-root runtime where practical
* small runtime image
* deterministic build
* no secrets in image
* proper environment configuration
* healthcheck support
* graceful shutdown

The Docker image should be suitable for eventual deployment to Render and portable to AWS.

---

# 26. DOCKER COMPOSE

The development Compose environment must support:

```text
PostgreSQL
Redis
RabbitMQ
```

Keep persistent volumes.

Use health checks.

Use service dependencies appropriately.

Do not unnecessarily containerize the API during active development if running it directly is more convenient.

---

# 27. GITHUB CODESPACES

The current development environment is:

```text
Local VS Code
      ↓
GitHub Codespace
      ↓
.NET + Docker + PostgreSQL + Redis + RabbitMQ
```

Do NOT assume the developer's local Windows machine hosts these services.

Ensure the project works correctly inside the Codespace.

Do not hardcode machine-specific paths.

---

# 28. GITHUB ACTIONS

Create a CI workflow.

At minimum:

```text
restore
↓
build
↓
unit tests
↓
architecture tests
↓
integration tests
```

Use PostgreSQL/Redis/RabbitMQ through Testcontainers or appropriate CI services.

CI must fail on:

* compilation errors
* failed tests
* relevant analyzer failures
* package vulnerabilities where configured

Do not put production secrets into the repository.

Do not deploy automatically yet unless an existing deployment workflow explicitly requires it.

---

# 29. TESTING

Implement the foundation for:

### Unit tests

Domain/Application logic.

### Integration tests

Real PostgreSQL/Redis/RabbitMQ using Testcontainers where appropriate.

### API tests

HTTP-level behavior.

### Architecture tests

Verify dependency rules.

For example:

```text
Domain must not depend on Infrastructure
Application must not depend on Infrastructure
Domain must not reference EF Core
Domain must not reference Redis
Domain must not reference RabbitMQ
```

Do not write meaningless tests purely to increase coverage.

---

# 30. DATABASE MIGRATION WORKFLOW

Make migrations predictable.

Developers should be able to:

```text
create migration
apply migration
rollback where appropriate
```

Do not automatically run destructive migrations on application startup.

Production migration strategy must be safe for deployment.

---

# 31. FINTECH-SPECIFIC FOUNDATION

Even though business modules are not being implemented yet, the foundation must respect the nature of the application.

Never use:

```text
float
double
```

for monetary amounts.

Prefer appropriate decimal/value-object approaches when financial models are introduced.

Do not implement wallet/ledger/payment logic yet.

The future architecture must support:

```text
central ledger
organization wallets
payment providers
Flutterwave primary
Paystack fallback
idempotency
concurrency control
auditability
```

without requiring a major rewrite of the infrastructure.

---

# 32. PERFORMANCE / SCALE

The eventual target is approximately:

```text
1M+ users
```

The Phase 0 architecture should therefore avoid obvious scalability problems.

Use:

* async APIs
* cancellation tokens
* connection pooling
* Redis for appropriate caching
* RabbitMQ for asynchronous work
* database indexes where known
* no unnecessary synchronous blocking
* no in-memory-only critical state
* stateless API design
* horizontally scalable workers

Do NOT prematurely optimize every component.

Do NOT introduce microservices.

---

# 33. CODE QUALITY RULES

Follow these strictly:

* nullable reference types enabled
* async/await properly used
* cancellation tokens propagated
* dependency injection
* interfaces only where they provide architectural value
* no service locator
* no static global state
* no magic strings where configuration/constants are appropriate
* no hardcoded secrets
* no dead code
* no commented-out old implementations
* no unnecessary abstractions
* no giant classes
* no giant methods
* meaningful names
* XML documentation where public APIs require it
* consistent namespaces
* consistent formatting

---

# 34. DO NOT DO THESE THINGS

Do NOT:

* implement wallet functionality
* implement ledger functionality
* implement payment functionality
* implement Flutterwave integration
* implement Paystack integration
* implement organization workflows
* implement KYC workflows
* implement VAS
* implement HRIS
* implement ERP
* implement business-specific APIs

Those belong to later phases.

Do NOT:

* introduce microservices
* introduce Kubernetes
* introduce Kafka
* introduce event sourcing
* introduce a second database
* introduce a distributed transaction coordinator
* introduce unnecessary cloud-specific dependencies

---

# 35. CRITICAL DECISION RULE

If you encounter a decision that materially affects:

* financial data integrity
* security
* authentication architecture
* authorization architecture
* database architecture
* message delivery semantics
* concurrency
* scalability
* deployment architecture
* API contract
* domain architecture

and the decision is NOT specified above:

**STOP. Do not guess. Ask me before proceeding.**

For minor implementation choices, use sound .NET engineering judgment.

---

# 36. IMPLEMENTATION PROCESS

Follow this exact process:

### STEP 1 — AUDIT

Perform a read-only audit.

Report:

```text
Already implemented
Missing
Incorrect
Potential issues
```

Do not modify code during the audit.

### STEP 2 — PLAN

Create a concise implementation plan grouped by:

```text
Configuration
Infrastructure
Security
Messaging
Observability
Testing
Docker
CI
```

### STEP 3 — IMPLEMENT

Implement incrementally.

After each major area:

```text
restore
build
test
```

Fix issues before proceeding.

### STEP 4 — REVIEW

Review the entire implementation for:

* architecture violations
* security problems
* secrets
* incorrect dependencies
* unnecessary packages
* broken cancellation
* improper logging
* incorrect health semantics
* Docker problems
* CI problems

### STEP 5 — FINAL VERIFICATION

Run:

```text
dotnet restore
dotnet build
dotnet test
```

Also verify:

```text
docker compose config
docker compose up
health endpoints
database migration
Redis connectivity
RabbitMQ connectivity
API startup
```

If available, run static/analyzer checks.

---

# 37. REQUIRED FINAL REPORT

At the end, DO NOT dump thousands of lines of code into the chat.

Give me a concise report:

```text
PHASE 0 STATUS

Completed:
- ...
- ...

Files created:
- ...

Files modified:
- ...

Packages added:
- ...

Architecture decisions made:
- ...

Tests:
- Build: PASS/FAIL
- Unit: PASS/FAIL
- Integration: PASS/FAIL
- Architecture: PASS/FAIL

Infrastructure:
- PostgreSQL: PASS/FAIL
- Redis: PASS/FAIL
- RabbitMQ: PASS/FAIL

Docker:
- PASS/FAIL

GitHub Actions:
- PASS/FAIL

Known issues:
- ...

Decisions requiring my approval:
- ...
```

Do not claim something is complete if it was not actually verified.

---

# FINAL INSTRUCTION

**Inspect first. Preserve existing work. Implement only missing Phase 0 foundation.**

The goal is not to produce the largest amount of code.

The goal is to leave the repository with a **clean, secure, testable, scalable, production-oriented .NET 10 foundation** that we can confidently build the CebizPay business domains on top of.

Do not proceed into business-domain implementation.

Start with the repository audit now.
