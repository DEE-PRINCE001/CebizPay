Phase 7.0 Certification Gap Analysis

  ## Executive Verdict

  READY FOR PHASE 7 REMEDIATION

  │ Important
  │ The backend demonstrates outstanding engineering rigor: a clean modular monolith, an immutable central double-entry ledger,
  │ deterministic row-level database locking (FOR UPDATE), strict transactional outbox patterns, and robust audit trails. However,
  │ production readiness certification is blocked pending remediation of specific security controls (cross-tenant audit exposure via
  │ unvalidated header), unlinked provider failover dispatch, synchronous webhook ledger execution, worker containerization, and
  │ configuration/documentation drift.
  ──────
  ## Current Baseline

  • Solution Build State: Builds cleanly (net10.0) with 0 errors and 0 warnings.
  • Test Suite Execution:
      • Architecture Tests: 17 / 17 passing
      • Unit Tests: 955 / 955 passing
      • API Tests: 159 / 159 passing
      • Integration Tests: 109 / 109 passing (verified via Testcontainers suite)
      • Total Passing Tests: 1,240 / 1,240 (100%)
  • Solution Projects: 5 production projects (CebizPay.Api, CebizPay.Application, CebizPay.Domain, CebizPay.Infrastructure, CebizPay.
  Workers), 4 test projects (CebizPay.ArchitectureTests, CebizPay.UnitTests, CebizPay.ApiTests, CebizPay.IntegrationTests).
  • EF Core Migrations: 33 migrations applied in chronological sequence with full snapshot alignment (ApplicationDbContextModelSnapshot.
  cs).
  ──────
  ## Certification Scorecard

   Area                │ Status │ Risk     │ Evidence                     │ Gap                          │ Required Action
  ─────────────────────┼────────┼──────────┼──────────────────────────────┼──────────────────────────────┼──────────────────────────────
   Architecture        │ PASS   │ LOW      │ Clean Architecture verified  │ None. Pure domain &          │ Maintain dependency
                       │        │          │ by 17 NetArchTest rules in   │ application layers.          │ boundaries.
                       │        │          │ ArchitectureTests.cs:14-297. │                              │
   Authentication      │ GAP    │ HIGH     │ IdentityService.cs:353,      │ Hardcoded 30m token          │ Align token lifetime to 15m
                       │        │          │ JwtOptions.cs:38.            │ expiration in code, default  │ across code & configuration.
                       │        │          │                              │ 60m in config; PRD specifies │
                       │        │          │                              │ 15m.                         │
   Authorization       │ PASS   │ LOW      │ Role & permission policies   │ None. Granular               │ None.
                       │        │          │ in                           │ SuperAdmin/Admin/Auditor     │
                       │        │          │ AdminManageController.cs:35-96 │ policies enforced.           │
                       │        │          │    and controllers.          │                              │
   Tenant Isolation    │ GAP    │ CRITICAL │ CurrentOrganizationContext.cs:35-40 │ X-Organization-Id header is  │ Enforce
                       │        │          │        ,                     │ read without membership      │ HasAccessToOrganizationAsync
                       │        │          │ GetAuditLogsQueryHandler.cs:58-74 │ validation in                │ verification in audit
query
                       │        │          │      .                       │ GetAuditLogsQueryHandler.    │ handler.
   IDOR                │ PASS   │ LOW      │ Strict ownership checks in   │ Non-owned tickets return 404 │ Maintain IDOR guards across
                       │        │          │ CustomerSupportController.cs:107-117 │ Disguised.                   │ future endpoints.
                       │        │          │          and                 │                              │
                       │        │          │ GetSupportTicketByIdQuery.cs:48-60 │                              │
                       │        │          │       .                      │                              │
   Financial Integrity │ PASS   │ LOW      │ All monetary values use      │ None. Float/double strictly  │ Maintain invariant.
                       │        │          │ decimal / numeric(18,4).     │ prohibited.                  │
                       │        │          │ Database CHECK constraint    │                              │
                       │        │          │ CK_Wallets_AvailableBalance_ │                              │
                       │        │          │ NonNegative.                 │                              │
   Ledger Integrity    │ PASS   │ LOW      │ LedgerPostingService.cs:1-100 │ None. No direct balance      │ Maintain invariant.
                       │        │          │  . Immutable append-only     │ updates outside ledger       │
                       │        │          │ ledger entries; double-entry │ service.                     │
                       │        │          │ balance ∑Debits = ∑Credits.  │                              │
   Wallet Authority    │ PASS   │ LOW      │ Wallet.cs:95-111. Balance    │ None.                        │ Maintain invariant.
                       │        │          │ mutation private; called     │                              │
                       │        │          │ exclusively from             │                              │
                       │        │          │ LedgerPostingService.        │                              │
   Idempotency         │ PASS   │ LOW      │ Database-backed              │ None.                        │ Maintain invariant.
                       │        │          │ IdempotencyRecordConfiguration.cs:1-50 │                              │
                       │        │          │            with scoped       │                              │
                       │        │          │ uniqueness.                  │                              │
   Concurrency         │ PASS   │ LOW      │ Deterministic row-locking    │ None.                        │ Maintain invariant.
                       │        │          │ via SELECT ... FOR UPDATE    │                              │
                       │        │          │ with ordered Guid IDs in     │                              │
                       │        │          │ LedgerPostingService.cs:110-137 │                              │
                       │        │          │    .                         │                              │
   Webhooks            │ GAP    │ HIGH     │ PaymentsWebhookController.cs:70-85 │ Webhook endpoint             │ Decouple ingestion from
                       │        │          │       ,                      │ synchronously executes full  │ processing; process in
                       │        │          │ WebhookProcessor.cs:166-180. │ ledger posting before HTTP   │ background worker.
                       │        │          │                              │ 200 return.                  │
   Outbox              │ PASS   │ LOW      │ OutboxPublisherWorker.cs:70  │ None.                        │ Maintain invariant.
                       │        │          │ uses SELECT ... FOR UPDATE   │                              │
                       │        │          │ SKIP LOCKED.                 │                              │
   RabbitMQ            │ PASS   │ LOW      │ Durable exchanges and queues │ None. Horizontal scaling     │ Maintain invariant.
                       │        │          │ (exclusive: false,           │ safe.                        │
                       │        │          │ autoDelete: false) in        │                              │
                       │        │          │ NotificationDispatcherWorker.cs:75-81 │                              │
                       │        │          │          .                   │                              │
   Redis               │ PASS   │ LOW      │ Redis used strictly for rate │ None. Not financial          │ Maintain invariant.
                       │        │          │ limiting, OTP, token         │ authority.                   │
                       │        │          │ blacklisting, and duplicate  │                              │
                       │        │          │ VAS protection.              │                              │
   Provider Failure    │ GAP    │ CRITICAL │ PaymentProviderBankTransferExecutor.cs:208-220 │ IPaymentFailoverService is   │ Wire failover
service to
                       │        │          │                   ,          │ implemented and unit-tested  │ outbox event consumer or
                       │        │          │ PaymentFailoverService.cs:67 │ but NOT wired to background  │ automated dispatcher.
                       │        │          │ .                            │ consumers.                   │
   Notifications       │ GAP    │ MEDIUM   │ INotificationDeduplicator.cs:6 │ Doc comments claim "exactly- │ Update doc comments to
                       │        │          │   , FirebaseOptions.cs:6.    │ once delivery"; Firebase     │ reflect deduplication; add
                       │        │          │                              │ options missing from         │ FCM config template.
                       │        │          │                              │ .env.example.                │
   Referrals           │ PASS   │ LOW      │ DisabledReferralRewardActivationService.cs:11-23 │ None. Financial rewards      │ Maintain
invariant.
                       │        │          │                     .        │ strictly disabled.           │
   Support             │ PASS   │ LOW      │ CustomerSupportController.cs:1-151 │ None. 12h SLA worker         │ Maintain invariant.
                       │        │          │       . No SupportAgent      │ verified.                    │
                       │        │          │ role. Zero financial         │                              │
                       │        │          │ mutation paths.              │                              │
   Performance         │ GAP    │ MEDIUM   │ Lack of automated            │ Missing empirical benchmarks │ Implement Stage 7.3 load
                       │        │          │ load/stress test suite in    │ for 1,500ms / 250 rps        │ testing suite.
                       │        │          │ codebase.                    │ targets.                     │
   Load Testing        │ GAP    │ MEDIUM   │ No k6 / Locust / NBomber     │ No automated load test       │ Create load test suite in
                       │        │          │ scripts committed.           │ harnesses.                   │ Stage 7.3.
   Observability       │ PASS   │ LOW      │ Structured Serilog logging   │ None.                        │ Maintain invariant.
                       │        │          │ with CorrelationId, Activity │                              │
                       │        │          │ trace propagation, and       │                              │
                       │        │          │ Prometheus metrics.          │                              │
   Backup              │ GAP    │ HIGH     │ Absence of automated         │ Operational procedure        │ Provide backup automation in
                       │        │          │ PostgreSQL pg_dump/WAL       │ undocumented.                │ Stage 7.4.
                       │        │          │ archiving scripts in repo.   │                              │
   Restore             │ GAP    │ HIGH     │ No documented or rehearsed   │ Operational procedure        │ Provide restore runbook in
                       │        │          │ restore runbook.             │ undocumented.                │ Stage 7.4.
   Disaster Recovery   │ GAP    │ HIGH     │ RTO/RPO targets undefined in │ DR plan undocumented.        │ Define DR runbook in Stage
                       │        │          │ repo runbooks.               │                              │ 7.4.
   Deployment          │ GAP    │ HIGH     │ Dockerfile:1-47 packages     │ Workers cannot be deployed   │ Create multi-service
                       │        │          │ only Api; no Docker          │ via existing Docker          │ Docker/compose
                       │        │          │ packaging for                │ configuration.               │ configuration.
                       │        │          │ CebizPay.Workers.            │                              │
   Rollback            │ GAP    │ MEDIUM   │ No automated database        │ Rollback operational script  │ Provide rollback guidelines
                       │        │          │ rollback or canary           │ missing.                     │ in Stage 7.4.
                       │        │          │ deployment scripts.          │                              │
   Documentation       │ GAP    │ LOW      │ Token lifetime, exactly-once │ Documentation                │ Synchronize documentation in
                       │        │          │ claims, and config section   │ inconsistencies.             │ Stage 7.1.
                       │        │          │ naming discrepancies.        │                              │
   Test Coverage       │ PASS   │ LOW      │ 1,240 passing tests across   │ CI workflow excludes         │ Include integration suite in
                       │        │          │ Unit, Architecture, API, and │ IntegrationTests.            │ CI pipeline.
                       │        │          │ Integration suites.          │                              │
  ──────
  ## Critical Findings

  ### 1. Cross-Tenant Audit Log Leakage via Unvalidated X-Organization-Id Request Header

  • Evidence:
      • In CurrentOrganizationContext.cs:35-40, CurrentOrganizationId blindly parses and returns the GUID from the client-supplied X-
      Organization-Id header without validating whether the authenticated user has an active membership in that organization.
      • In GetAuditLogsQueryHandler.cs:58-74, when the caller is not a platform admin, the handler sets effectiveOrgId =
      currentTenantOrgId directly from _currentOrgContext.CurrentOrganizationId.Value and executes _dbContext.AuditLogs.Where(a => a.
      OrganizationId == effectiveOrgId.Value). It does NOT call HasAccessToOrganizationAsync(currentTenantOrgId).
  • Affected Components: CurrentOrganizationContext.cs:13, GetAuditLogsQueryHandler.cs:15, AdminAuditLogsController.cs:18.
  • Attack/Failure Scenario: A malicious authenticated user (such as a standard consumer or an employee of Organization B) calls GET
  /api/v1/admin/audit-logs supplying X-Organization-Id: <Victim-Org-A-Guid>. Because the user is authenticated, the endpoint bypasses
  the platform admin check, resolves the victim organization ID directly from the header, and leaks Organization A’s entire audit
  history (including employee salary adjustments, voucher modifications, and executive actions).
  • Impact: Cross-tenant privilege escalation and violation of tenant isolation.
  • Recommended Remediation: In GetAuditLogsQueryHandler.cs:60, invoke await _currentOrgContext.
  HasAccessToOrganizationAsync(currentTenantOrgId, cancellationToken) and verify the user holds an administrative role (e.g. OrgOwner,
  HRManager) in that organization. Additionally, harden CurrentOrganizationContext.cs:35 so that header resolution requires membership
  verification before returning the context ID.
  • Certification Impact: BLOCKING. Must be remediated in Stage 7.1.

  ### 2. Multi-Rail Provider Automatic Failover is Unwired in Production Execution

  • Evidence:
      • IPaymentFailoverService.cs:7 and PaymentFailoverService.cs:25 implement sequential failover (Monnify → Flutterwave → Paystack)
      with strict business failure and UNKNOWN state protection.
      • However, FailoverAsync is only invoked in test files (PaymentFailoverServiceTests.cs:100 and
      PaymentWebhookAndFailoverIntegrationTests.cs:278).
      • In production execution (PaymentProviderBankTransferExecutor.cs:208-220), when a provider call encounters a TechnicalFailure, it
      writes PaymentAttemptFailedEvent to the Outbox but never dispatches a failover attempt.
      • In PaymentReconciliationService.cs:148-154, when reconciliation discovers a TechnicalFailure, it treats it identically to a
      BusinessFailure and triggers a full ledger reversal (PostBankTransferReversalCoreAsync) instead of attempting fallback.
      • No worker in CebizPay.Workers consumes PaymentAttemptFailedEvent or coordinates secondary provider dispatch.
  • Affected Components: PaymentProviderBankTransferExecutor.cs:26, PaymentFailoverService.cs:25, PaymentReconciliationService.cs:23.
  • Attack/Failure Scenario: During a Monnify technical outage or gateway 502/503 error, all outbound bank transfers immediately fail or
  reverse. The documented automatic fallback to Flutterwave and Paystack is dead code in production runtime.
  • Impact: Inability to survive primary payment provider infrastructure failures; false claim of resilient multi-rail failover.
  • Recommended Remediation: Wire an asynchronous RabbitMQ consumer/outbox handler or automated workflow to trigger
  IPaymentFailoverService.FailoverAsync upon verified technical failure events, ensuring fallback attempts are dispatched automatically
  while preserving the UNKNOWN reconciliation invariant.
  • Certification Impact: BLOCKING. Must be remediated in Stage 7.2.
  ──────
  ## High Findings

  ### 3. Synchronous Financial Mutation and Ledger Locking During External Webhook Ingestion

  • Evidence: PaymentsWebhookController.cs:70 calls _webhookProcessor.ProcessWebhookAsync(...) inline during HTTP handling. In
  WebhookProcessor.cs:166-180, this synchronously performs database row locks (SELECT ... FOR UPDATE), ledger transaction writes, and
  wallet balance arithmetic before returning HTTP 200. Conversely, the background WebhookProcessingWorker.cs:10 and
  WebhookProcessingService.cs:93 only mark existing records as processed.
  • Impact: Violates the architectural rule requiring thin webhook boundaries (ingest → verify signature → persist → 200 OK → async
  worker processing). External providers with 5–10s webhook timeouts will time out during database contention and spam duplicate
  deliveries.
  • Recommended Remediation: Move financial ledger posting and wallet mutation out of the HTTP request thread and into
  WebhookProcessingWorker.cs:35.

  ### 4. Missing Docker Packaging for Background Workers & Absence of Production Database Migration Runner

  • Evidence:
      • Dockerfile:1-47 packages and runs only CebizPay.Api.dll. There is no Dockerfile or target for Program.cs:1.
      • Program.cs:248-259 gates Database.MigrateAsync() strictly inside if (app.Environment.IsDevelopment()).
  • Impact: Deploying the Docker image to staging or production starts only the HTTP API. Background workers (outbox dispatcher,
  reconciliation, webhook processor, SLA monitor) never start. Production databases will not be migrated automatically on startup.
  • Recommended Remediation: Create a multi-stage Docker build producing both API and Worker container images; create production
  deployment manifests and an automated EF Core migration bundle/script.

  ### 5. JWT Access Token Lifetime Discrepancy & Ineffective Configuration

  • Evidence:
      • PRD §4.1 and Spec §9 mandate 15-minute access tokens.
      • JwtOptions.cs:38, appsettings.json:16, and appsettings.Development.json:15 configure ExpirationInMinutes: 60.
      • In IdentityService.cs:353, token generation hardcodes Expires = DateTime.UtcNow.AddMinutes(30), completely ignoring the
      configured option.
  • Impact: Security configuration drift; access tokens remain valid for 30 minutes instead of the mandatory 15 minutes.
  • Recommended Remediation: Refactor IdentityService.cs to read _jwtOptions.ExpirationInMinutes and set default and configuration
  values across all appsettings files to 15.

  ### 6. Undocumented Disaster Recovery Procedures and Lack of Backup/Restore Runbooks

  • Evidence: No backup automation, WAL archiving configuration, point-in-time recovery scripts, or documented RPO/RTO metrics exist in
  the repository.
  • Impact: Inability to guarantee recovery objectives under infrastructure loss or database corruption.
  • Recommended Remediation: Author formal backup, restore, and disaster recovery procedures in Stage 7.4.
  ──────
  ## Medium Findings

  • Inaccurate "Exactly-Once" Terminology in Documentation/Comments: INotificationDeduplicator.cs:6, NotificationDeliveryRecord.cs:7,
  and NotificationDeduplicator.cs:12 claim "exactly-once delivery" for push/SMS/email channels. External network transports cannot
  guarantee exactly-once delivery.
      • Action: Correct terminology to "application-level deduplication / at-least-once delivery with channel deduplication".
  • Integration Tests Bypassed in CI Pipeline: ci.yml:28-36 executes Unit, Architecture, and API tests, but excludes CebizPay.
  IntegrationTests.
      • Action: Enable containerized integration testing in GitHub Actions using Docker service containers.
  • Missing Firebase Configuration Template: FirebaseOptions.cs:6 defines section "Firebase", but .env.example and appsettings.json omit
  it entirely.
      • Action: Add "Firebase" configuration block to .env.example and appsettings.json.
  • Missing Automated Load Testing Suite: No load-testing scripts (k6/NBomber) exist in the repository to validate the 1,500ms core-
  write SLA and 250 writes/sec requirement under concurrent load.
      • Action: Implement load test scenarios in Stage 7.3.

  ──────
  ## Low / Informational

  • Boilerplate Worker Remaining in Project: Worker.cs:6 is registered as a hosted service alongside the 12 specialized workers, logging
  heartbeats every 10 seconds. Recommend pruning prior to production.
  • Default CORS Development Origins: Program.cs:74-86 falls back to hardcoded localhost ports when Cors:AllowedOrigins is empty. In
  production, missing CORS configuration should fail closed.
  ──────
  ## Stage 7.1 Work Required

  ### Architecture + Security + Financial Integrity

  1. Tenant Isolation Hardening:
      • Fix CurrentOrganizationContext.cs:35 to validate user membership when resolving CurrentOrganizationId from headers.
      • Update GetAuditLogsQueryHandler.cs:60 to mandate organizational admin permissions and verify tenant access via
      HasAccessToOrganizationAsync.
      • Audit CreateSupportTicketCommand.cs:102 to verify organizational membership when associating tickets with an organization.
  2. Token Security Alignment:
      • Modify IdentityService.cs:353 to use _jwtOptions.ExpirationInMinutes.
      • Update JwtOptions.cs:38, appsettings.json, and .env.example to enforce 15-minute token expiration.
  3. CI/CD Pipeline Hardening:
      • Update ci.yml:28 to run containerized integration tests on GitHub Actions runners.
  4. Automated Security & IDOR Regression Suite:
      • Add dedicated tests for cross-tenant audit log query prevention, token expiration compliance, and tenant spoofing rejection.

  ──────
  ## Stage 7.2 Work Required

  ### Concurrency + Idempotency + Failure/Resilience

  1. Asynchronous Multi-Rail Provider Failover Wiring:
      • Create a background worker / consumer subscribed to PaymentAttemptFailedEvent that invokes PaymentFailoverService.cs:67.
      • Ensure TechnicalFailure initiates fallback attempts while strictly preserving the UNKNOWN reconciliation invariant and business
      failure terminality.
  2. Asynchronous Webhook Ledger Processing:
      • Refactor PaymentsWebhookController.cs:70 to perform signature check, SHA-256 deduplication, durable persistence, and fast HTTP
      200 return.
      • Shift heavy financial ledger updates into WebhookProcessingWorker.cs:35.
  3. Chaos & Resilience Verification:
      • Add automated tests simulating provider timeouts, transient PostgreSQL connection drops, Redis disconnects, and RabbitMQ message
      re-deliveries.

  ──────
  ## Stage 7.3 Work Required

  ### Performance + Load + Observability

  1. Automated Load Test Suite Development:
      • Develop k6 or NBomber load test scripts targeting:
          • Authentication & token refresh (100 req/s).
          • Peer transfers and bank transfer debits under concurrent row contention (250 writes/s).
          • Webhook ingestion bursts (500 webhooks/s).
          • Paginated audit and report queries under 100k+ rows.

  2. Database Query & Index Profiling:
      • Run EXPLAIN ANALYZE on all high-frequency queries (wallet balance lock, outbox claiming, audit logs).
      • Validate connection pool configuration (MaxPoolSize, idle timeout) against expected concurrent container counts.
  3. Alerting & Metric Thresholds:
      • Define Prometheus / OpenTelemetry alerting rules for outbox lag, webhook processing failure rates, and database lock wait times.

  ──────
  ## Stage 7.4 Work Required

  ### Backup + Restore + DR + Deployment

  1. Container & Worker Deployment Packaging:
      • Create a production Dockerfile for CebizPay.Workers (or a multi-target build).
      • Update docker-compose.yml to define containerized API and Worker services.
  2. Database Migration Strategy:
      • Package EF Core migration bundles (dotnet ef migrations bundle) for production execution.
      • Ensure migrations execute as an isolated pre-deployment job rather than inside development startup.
  3. Disaster Recovery & Operational Runbooks:
      • Document automated PostgreSQL backup procedures (daily snapshots + WAL archiving).
      • Document and rehearse the cold-restore procedure.
      • Define explicit RPO (< 5 minutes) and RTO (< 60 minutes) metrics.

  ──────
  ## Missing Evidence

  • Real High-Concurrency Performance Evidence: While database row-locking logic is sound, empirical benchmark evidence proving ≤ 1,
  500ms core writes under 250 writes/sec does not yet exist in the repository.
  • Provider Live Sandbox Execution: External provider integrations are verified via mock/simulation fixtures; live end-to-end execution
  against real upstream sandboxes requires operational environment credentials.
  • Disaster Recovery Drill Evidence: Database backup restoration has not been exercised in a clean target environment.
  ──────
  ## Documentation Conflicts

  1. Token Lifetime: PRD §4.1 and Engineering Spec §9 mandate 15-minute access tokens; code default in JwtOptions.cs is 60 minutes, and
  IdentityService.cs hardcodes 30 minutes.
  2. Delivery Guarantees: Comments in INotificationDeduplicator.cs claim "exactly-once delivery"; architectural invariants forbid
  claiming exactly-once delivery over third-party channels (FCM/Twilio/SendGrid).
  3. Webhook Processing Model: Engineering Spec §21.4 mandates asynchronous worker ledger processing; current implementation executes
  financial mutations inline in the HTTP controller.
  ──────
  ## Phase 6 Regression Findings

  • Phase 6A (Admin Governance): Soft-deletion verified. Admin safeguards intact.
  • Phase 6B (Announcements): Platform and workplace scoping verified. Tenant isolation intact.
  • Phase 6C (Notifications): Queue configuration verified durable and non-exclusive (exclusive: false). Documentation comments need
  adjustment regarding exactly-once semantics; Firebase configuration template missing from .env.example.
  • Phase 6D (Referrals): Verified fixed reward model, qualification commutative logic, and strict disability of financial rewards
  (DisabledReferralRewardActivationService). Zero wallet/ledger leakage.
  • Phase 6E (Customer Support): Verified deterministic Kola chatbot, strict ticket ownership checks, disguised 404 on IDOR attempts,
  12-hour SLA background worker, absence of SupportAgent role, and zero financial mutation authority.
  ──────
  ## Recommended Execution Order

    graph TD
        A["Stage 7.1: Tenant Isolation & Token Hardening"] --> B["Stage 7.2: Failover Wiring & Async Webhook Processing"]
        B --> C["Stage 7.3: Load Testing & Performance Benchmarking"]
        B --> D["Stage 7.4: Worker Docker Packaging & DR Runbooks"]
        C --> E["Stage 7.5: Final Independent Adversarial Audit"]
        D --> E

  1. Task 1: Stage 7.1 Security & Invariant Remediation (Code modification required; no production access needed):
      • Remediate X-Organization-Id tenant isolation in GetAuditLogsQueryHandler and CurrentOrganizationContext.
      • Fix JWT token lifetime to 15 minutes.
      • Add containerized integration tests to CI workflow.
  2. Task 2: Stage 7.2 Concurrency & Failure Resilience (Code modification required; no production access needed):
      • Wire IPaymentFailoverService.FailoverAsync to an asynchronous consumer.
      • Decouple webhook HTTP ingestion from ledger posting.
  3. Task 3: Stage 7.3 Performance & Observability (Code addition of test scripts; no production access needed):
      • Implement k6/NBomber load test scenarios.
      • Validate connection pool and query execution plans.
  4. Task 4: Stage 7.4 Deployment & DR Readiness (Configuration/Docker changes; no production access needed):
      • Create CebizPay.Workers Docker packaging.
      • Establish migration bundle deployment step.
      • Author DR runbooks and RPO/RTO procedures.
  5. Task 5: Stage 7.5 Independent Final Adversarial Audit:
      • Execute full verification before production deployment.

  ──────
  ## Final Certification Readiness

    PHASE 7.0 STATUS:
    READY FOR PHASE 7 REMEDIATION

    CURRENT PRODUCTION CERTIFICATION:
    NOT CERTIFIED
