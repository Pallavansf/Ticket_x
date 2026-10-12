# Architecture decisions

## ADR-001: A single ticket engine
All five ticket types use `PA_TICKETS`; type controls semantics. Work items are routed execution records, not a ticket type.

## ADR-002: Baseline schema preservation
The supplied schema is copied verbatim to `database/schema/001_PA_TICKETS_SCHEMA.sql`. Future changes are additive numbered migrations. The source under `Docs` is untouched.

## ADR-003: Oracle is infrastructure
Oracle EF Core provider configuration, SQL, sequences, and storage adapters live outside Domain.

## ADR-004: Configuration over hard-coding
Departments, catalogue, routing, workflow, approval, SLA, and escalation policies are configuration, not source code.

## ADR-005: Persistence deferred safely
No Oracle connection, provider, or credentials were supplied. No Oracle package is committed until those are selected; the solution stays buildable without them.

## ADR-006: Oracle 19c persistence target
Oracle Database 19c is the target. The Infrastructure project uses `Oracle.EntityFrameworkCore` 8.23.40 with EF Core 8 and configures `OracleSQLCompatibility.DatabaseVersion19`. The connection string is configuration/secret supplied; no credentials are committed. Oracle DDL execution remains pending an accessible 19c instance.

## ADR-007: Per-table numeric key allocation
The additive hardening migration creates a dedicated sequence for each baseline table that lacked one. Existing ticket, ticket-number, event, and audit sequences are preserved. The application never uses `MAX(id)+1`; ticket PKs and business numbers remain separate.

## ADR-008: Parallel workflow state
`PA_TICKETS_WORKFLOW_STEP_INSTANCE` is authoritative for active/pending/waiting/completed parallel steps and supports parent branches, work items, and approvals. `N_CURRENT_STEP_ID` remains an operational projection only.

## ADR-009: Attachment current version
Migration 002 adds a function-based unique Oracle index allowing exactly one `CURRENT` version per attachment, plus a status check. Uploads create rows; application transactions supersede the prior current version and never overwrite evidence.

## ADR-010: Lifecycle transaction boundary
Each application mutation loads the aggregate, authorizes the actor, applies domain rules, and calls one repository save. Infrastructure persists the aggregate projection, work-item changes, event, and audit rows in one Oracle transaction.

## ADR-011: Communication providers are adapters
Email, WhatsApp, Push, and InApp are channel contracts. Queue persistence is implemented, but provider delivery adapters are not registered without provider configuration. Domain has no SDK or credential dependency and no fake success path.

## ADR-012: Authentication/security separation
JWT/OIDC/SAML provider configuration and TOTP/MFA belong to security Infrastructure/Application contracts. Ticket authorization (RBAC, scope, assignment, and SoD) remains a separate server-side application concern. Provider secrets use references/protected storage, not ordinary plaintext configuration.

## ADR-013: Versioned SLA configuration
SLA policy versions, effective dates, targeting dimensions, calendars, waiting/pause, escalation, breach, and notification rules are configuration data. Existing ticket measurements retain their selected policy version; edits do not silently change history.

## ADR-014: Controlled workflow conditions
Workflow conditions use a typed field/operator/value DSL over known fields. Administrator-supplied C#, SQL, JavaScript, shell, or other executable expressions are never evaluated.

## ADR-015: Durable job boundary
SLA warnings, breach detection, escalation, notification scheduling, and workflow timeouts use an `IBackgroundWorkScheduler` contract. A durable queue/worker is required before these jobs are enabled; no unreliable in-memory timer is used.

## ADR-016: Workflow/approval/SLA concurrency
Migration 006 adds row-version projections to runtime workflow, approval, and SLA rows and a separate immutable approval-delegation table. Oracle-backed optimistic concurrency tests remain pending until a 19c target is available.

## ADR-017: Idempotency by business operation
Idempotency keys are required for workflow step completion, approval decisions/delegation, workflow publication, communication queue creation, SLA operations, and background-job deduplication. Keys are scoped by operation and protected by Oracle unique constraints; they are not indiscriminately added to read endpoints.

## ADR-018: Durable worker is opt-in
The background worker leases persistent job rows and is enabled only when `Jobs:Enabled` is explicitly configured. Without registered handlers it does not lease work. This prevents unconfigured environments from claiming or falsely completing SLA/escalation/notification jobs.

## ADR-019: Configuration publication is transactional
Workflow draft saves and publication run through application repositories, require administrator authorization and idempotency keys, and append hash-chain audit entries. Published versions cannot be edited in place.

## Phase 4 execution-layer continuation

WORK workflow step activation now creates a durable `PA_TICKETS_WORK_ITEM` row and links it to `PA_TICKETS_WORKFLOW_STEP_INSTANCE` within the same serializable execution transaction. Work-item identifiers are generated by the Oracle sequence and the linkage is protected by migration 009. Transition conditions consume a restricted persisted ticket context (type, priority, client, service); missing values evaluate deterministically as absent/false for equality-style predicates. Routing and assignment remain explicit configuration boundaries and do not fabricate an assignee.

## Durable work-item completion continuation

Authenticated `POST /api/v1/work-items/{id}/complete` now requires a persisted idempotency key and delegates to the durable workflow execution repository. The repository verifies the assigned actor and executable state, marks the work item completed, completes its linked workflow step, evaluates persisted ticket conditions, activates downstream steps, creates downstream WORK items, and writes events/audits in one serializable transaction. `POST /api/v1/workflow-instances/{id}/activate` provides idempotent initial-step activation and first work-item creation. Assignment/routing remains intentionally partial: unassigned work items cannot be completed by arbitrary authenticated users.

## Routing and projection continuation

WORK-step activation now resolves a configured routing rule by deterministic specificity (step, ticket type, priority, service, client), rejects equal-specificity ambiguity, and uses an explicit configured triage target when present. The selected department/team/user is persisted in the work item and assignment history, with a `WORK_ITEM_ASSIGNED` event and hash-chain audit. Ticket operational projection is updated transactionally to routed/in-progress or terminal status; workflow state remains authoritative. No routing target is fabricated when configuration is absent.

## Assignment lifecycle and public work-item IDs

Migration 010 adds S_PUBLIC_WORK_ITEM_ID with a unique index and a function-based unique index preventing more than one active assignment per work item. WORK-item creation allocates a cryptographically random 16-character identifier from ABCDEFGHJKLMNPQRSTUVWXYZ23456789 with bounded collision retry. Reassignment is exposed at POST /api/v1/work-items/{publicId}/assign, requires authentication and an idempotency key, closes the prior assignment with an effective end timestamp, creates a new assignment history row, updates ownership, and records event/audit entries transactionally. Oracle execution and authenticated integration/concurrency tests remain pending.

## Security and idempotency hardening

Migration 010 now backfills every legacy work item with a cryptographically generated 16-character identifier using the application alphabet, validates format/uniqueness, creates unique protection, and enforces NOT NULL. It also stores request fingerprints for idempotency records. Initial activation, work-item completion, and reassignment reject reuse of a key for a different logical request. Assignment authorization supports configured actor, department, and team scopes; empty development scope lists remain permissive for local setup. Oracle execution and race/integration tests remain NOT EXECUTED.

## Final hardening continuation

Migration 010 is now a single coherent Oracle deployment script: it adds the public-ID and fingerprint columns, backfills legacy work items with bounded cryptographic retry, validates nulls/duplicates/exact alphabet, creates uniqueness, enforces NOT NULL, and protects active assignments. Completion now requires the 16-character public work-item ID; numeric work-item PKs are not accepted by the public completion route. Reassignment fingerprints include target IDs and reason, so a reused key with changed ownership or reason conflicts. Actual Oracle execution and concurrent integration tests remain pending.

## Final verification status

Migration 010 was reviewed and corrected to a single coherent Oracle script; no Oracle client/database was available, so parsing/execution and backfill invariants remain unverified. The public completion API now accepts only the 16-character public work-item identifier. Duplicate EF PreviousHash mapping was removed, and deployment preflight now checks the fingerprint column. Available verification is limited to build, domain tests, and Angular build; API, persistence, race, rollback, and Oracle integration tests were not executed in this environment.

## Coding-completion verification

Stable API exception responses now distinguish forbidden (403), not found (404), invalid request (400), and conflict/idempotency/state errors (409) while returning a correlation trace ID. Available automated evidence remains 11 domain tests, .NET build, and Angular production build. No API, application-persistence, concurrency, rollback, or Oracle tests are present/executed in this environment; Angular test execution is unavailable because the generated CLI test target does not support the requested non-watch flags.

## Test-harness completion

Added application-level routing tests using the real ConfigurationWorkflowRouter and configuration binder: deterministic specificity precedence and equal-specificity ambiguity rejection. The .NET suite now reports 13 passing tests (11 existing domain/unit tests plus 2 application-level routing tests). No API host, persistence-provider, concurrency, rollback, E2E, or Oracle test harness exists in the repository; those remain unverified rather than being represented by mocks.

## Test harness boundary

The repository now has explicit test classification in tests/TESTING.md. Two application-level routing tests execute the real configuration router. No fake API/database/concurrency harness was added: Oracle-specific sequences, indexes, DBMS_CRYPTO, transaction isolation, and locking cannot be meaningfully reproduced by an unconfigured substitute provider. API, persistence, concurrency, rollback, and E2E tests remain pending infrastructure/Oracle availability. Angular production build passes; the Angular test target has no configured runner/spec suite.

## Phase 6 closeout policy boundaries

Role-to-permission mappings remain seeded deployment policy. Numbered migrations establish the system roles and grants; runtime administration manages user memberships, not privilege definitions or role grants. Changes to those grants therefore require a reviewed migration rather than a general-purpose role-permission API.

Workflow routing overrides remain deployment configuration under `WorkflowRouting:Rules` and `WorkflowRouting:Triage`. Persisted workflow steps and request-type defaults continue to provide routing targets, while deployment configuration supplies cross-cutting overrides. There is no second database-backed routing engine.

Approval expiry follows the existing decision-time contract: `ExpiresAt` is recorded for history and an approval is marked `EXPIRED` when a decision is submitted at or after its expiry. The current model does not promise autonomous expiry processing or enqueue approval-expiry jobs; no in-memory timer is used.
