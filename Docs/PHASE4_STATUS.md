# PostgreSQL Phase 4 product-readiness audit (2026-09-15)

Oracle 19c was not run. Phase 5 was not started. The required PostgreSQL build/test commands executed against the configured PostgreSQL 18 test database.

| Capability | Result | Evidence / remaining boundary |
|---|---|---|
| Waiting/pending reason catalogue | COMPLETE | PostgreSQL persists active/effective-dated reasons, allowed transitions, required metadata, waiting history, and automatically couples configured wait/resume transitions to SLA pause/resume for multiple periods. |
| SLA orchestration | COMPLETE | PostgreSQL persists and administers targeted effective policy versions and calendars/holidays, calculates due times, preserves selected versions, enforces pause rules, and runs configured escalation jobs. |
| Approval orchestration | COMPLETE | PostgreSQL API E2E covers ANY_ONE, ALL_REQUIRED, SEQUENTIAL, rejection, delegation, SoD, idempotent retry, and concurrent decisions. |
| Workflow configuration | COMPLETE | Draft/publish immutability, controlled conditions, join rules, routing precedence, effective published-version selection, idempotent creation, and historical version retention are PostgreSQL-tested. |
| Ticket/work-item lifecycle | COMPLETE | PostgreSQL persistence E2E covers create, classify, route, execute, wait/resume, complete, resolve, close, reopen, cancel; durable workflow E2E covers assignment and reassignment. |
| Audit/event/evidence integrity | COMPLETE | Transactional hash-chain writes, concurrent same-entity continuity, and PostgreSQL deny-by-default UPDATE/DELETE protection are tested. Controlled maintenance disposal is separately logged; cryptographic immutability is not claimed. |
| Durable background jobs | COMPLETE | PostgreSQL E2E covers enqueue, dedupe, concurrent leasing, retry/backoff, stale-lease recovery, completion, and completed-job non-reclamation. |
| Authenticated API | COMPLETE | PostgreSQL API E2E covers authentication, authorization, SoD, invalid identifiers, idempotency, and concurrent completion/approval decisions. |

The full suite is **33/33 passed** after adding automatic waiting/SLA coupling, SLA administration/escalation, workflow-instance creation, and audit protection PostgreSQL tests. All four requested remaining gaps are implemented and tested on PostgreSQL 18.

Exact next step: perform a separate production deployment review of PostgreSQL roles/privileges and operational escalation handlers. Oracle remains deferred and Phase 5 must not start until explicitly authorized.

# Phase 4 status

Workflow engine: **PARTIAL**
Immutable workflow-version model, validation, controlled conditions, runtime step state, and join rules exist. Durable Oracle workflow-instance persistence and full transition orchestration remain pending.

Sequential workflow: **PASS** at domain runtime level.

Parallel workflow: **PASS** at domain runtime level using authoritative step instances and configurable join rules.

Conditional workflow: **PASS** for the controlled field/operator/value DSL. Arbitrary executable conditions are rejected by design.

Dynamic routing: **PARTIAL**. Core ticket routing and configurable relationship/routing contracts exist; full role/manager/resolver-backed workflow routing remains pending.

Approval engine: **PASS** at domain level for any-one, all-required, sequential levels, rejection state, and immutable records. Durable approval persistence/API orchestration remains pending.

Approval SOD: **PASS** for requester, creator, and production-executor separation rules in the approval domain.

Approval delegation: **PARTIAL**. Delegation model and additive table exist; authorization, effective-date persistence, and API orchestration remain pending.

SLA calculation: **PASS** at deterministic domain level for business calendars, working hours, holidays, pause/resume, breach, and active-plus-waiting time.

SLA versioning: **PASS** at schema/contract level with effective dating and selected policy versions.

SLA calendar: **PASS** at domain level; Oracle administration persistence remains pending.

SLA escalation: **PARTIAL**. Configuration fields and background-job contract exist; durable worker execution is deferred.

Administration: **PARTIAL**. Administrator-protected workflow validation endpoint exists. Full workflow/approval/SLA authoring UI and persistence are deferred.

Communication integration: **PARTIAL**. Workflow/application contracts can queue communications; external delivery adapters remain provider-dependent and pending.

Security: **PARTIAL**. Server-side JWT boundary, administrator route protection, and domain SOD exist; production IdP/MFA and scoped authorization integration remain pending.

Tests: **PASS — 11/11 domain tests**.

.NET build: **PASS — 0 errors**.

Angular build: **PASS**.

Oracle 19c validation: **PENDING ENVIRONMENT**.

## Known limitations

- No Oracle 19c instance is available, so migrations, EF runtime mappings, optimistic concurrency, and transactional integration tests are not claimed as passed.
- Workflow, approval, and SLA configuration repositories are not yet wired to admin persistence.
- A durable background worker/queue is required before timers, escalation, and scheduled notifications are enabled.
- Full Angular administration and workflow runtime screens remain deferred.

## Next recommended phase
Connect Oracle 19c and implement durable workflow/approval/SLA repositories plus worker-backed timers, then add authenticated API integration and concurrency tests.

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
