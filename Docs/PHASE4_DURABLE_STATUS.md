# Phase 4 durable completion status

Workflow persistence: **PARTIAL** — configuration persistence now writes version, normalized step, and transition rows transactionally; runtime repositories/mappings are implemented; Oracle execution is pending.

Workflow execution: **PARTIAL** — step completion and persisted downstream step activation are transactional/idempotent. WORK step activation now creates and links one durable work item in the same execution transaction. Assignment/routing resolution and broader work-item completion orchestration remain pending.

Parallel execution: **PARTIAL** — domain joins and durable step state exist; concurrent Oracle integration tests are pending.

Conditional execution: **PASS** for the controlled DSL.

Dynamic routing: **PARTIAL** — configuration contracts exist; resolver implementations remain pending.

Approval persistence: **PARTIAL** — decision/history repositories are transactional and idempotent; Oracle validation is pending.

Approval delegation: **PARTIAL** — durable effective-dated delegation and decision-time expiry checks are implemented; production authorization integration remains pending.

SOD: **PASS** at domain and repository authorization boundaries.

SLA persistence: **PARTIAL** — durable pause/resume/breach/completion repository exists; Oracle validation is pending.

SLA execution: **PARTIAL** — deterministic domain clocks and durable state operations exist; policy selection and full orchestration remain pending.

SLA escalation: **PARTIAL** — durable job queue and worker boundary exist; no handler is registered without configured policy/provider.

Background worker: **PARTIAL** — restart-safe persistent leasing/retry worker exists and is opt-in.

Communication queue: **PARTIAL** — durable idempotent queueing/retry metadata exists; provider delivery remains pending.

Administration: **PARTIAL** — workflow draft/publish API is administrator-protected and audited; complete approval/SLA administration remains pending.

Angular: **PARTIAL** — ticket create/list exists; workflow administration and execution views remain pending.

Concurrency: **PARTIAL** — Oracle row versions, serializable transaction boundaries, unique join protection, and deployment preflight checks are defined; target validation is pending.

Idempotency: **PASS** for implemented mutation boundaries.

Audit: **PASS** for implemented repository mutations using the existing hash-chain model.

Tests: **11/11 domain tests passed**; Oracle-backed integration/concurrency tests are pending.

.NET build: **PASS — 0 errors**.

Angular build: **PASS**.

Oracle 19c: **PENDING ENVIRONMENT**.

Next recommended phase: connect Oracle 19c, run migrations 002–008 and preflight, then complete downstream workflow orchestration and authenticated integration/concurrency tests.

Recent implementation notes:
- Workflow draft save replaces only unpublished child definitions and records normalized steps/transitions in the same serializable transaction.
- Workflow publication requires persisted steps and remains immutable after publication.
- Migration 008 adds a unique (workflow instance, workflow step) join constraint.
- Development background processing is explicitly disabled by default (Jobs:Enabled=false).



Execution-layer continuation:
- Migration 009 links PA_TICKETS_WORK_ITEM to PA_TICKETS_WORKFLOW_STEP_INSTANCE and protects one work item per step instance.
- WORK step activation creates a durable PENDING work item, emits an event, and appends hash-chain audit data in the same transaction.
- Transition conditions now receive persisted ticket type, priority, client, and service values through the execution context.
- No fabricated assignee is created; routing/assignment remains partial until configured resolvers are available.


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

## PostgreSQL integration environment

The local PostgreSQL 18 service is installed and running. A credential-safe verification harness is available at `tests/scripts/verify-postgresql.ps1`; it requires `PA_TICKETS_TEST_CONNECTION_STRING` and never prints it. In this environment that variable was not configured, so PostgreSQL connection/database creation was **NOT EXECUTED** (passwordless probing correctly returned `fe_sendauth: no password supplied`). The current EF boundary remains Oracle-only; no Oracle migrations were run and no PostgreSQL results are treated as Oracle compatibility evidence. See `tests/POSTGRESQL_INTEGRATION.md`.

## PostgreSQL provider adapter (development/integration only)

Infrastructure now selects PostgreSQL only when `Database:Provider=PostgreSQL`; it reads `ConnectionStrings:TicketsPostgres` or the process environment variable `PA_TICKETS_TEST_CONNECTION_STRING`. The default path remains Oracle 19c with `UseOracle`. Npgsql EF Core 8.0.11 is referenced alongside the Oracle provider. Oracle migrations are not executed against PostgreSQL. A real PostgreSQL schema/persistence smoke test remains pending because the current model still contains Oracle-specific sequence/default and schema assumptions that require a dedicated PostgreSQL schema strategy before `EnsureCreated` or repository persistence can be claimed.

## PostgreSQL schema adapter

Added `tests/scripts/postgresql-schema.sql` and the scoped `provision-postgresql-schema.ps1` runner. It creates repeatable PostgreSQL-native sequences, core ticket/work-item/assignment/event/audit tables, foreign keys, public-ID uniqueness, work-item uniqueness, and active-assignment uniqueness only in `pa_tickets_test`; Oracle migrations are not used. The provisioner also runs a real PostgreSQL insert/commit and rollback smoke script. These are **POSTGRESQL INTEGRATION** checks. The current smoke is SQL-level; an EF/repository-level persistence test remains pending because lookup entities (department/user/client) are not represented by the existing EF ticket context.
