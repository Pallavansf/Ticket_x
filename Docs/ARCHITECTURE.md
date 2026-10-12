# PA TICKETS architecture

## Purpose
PA TICKETS is the internal system of record for work. It records requests, incidents, problems, tasks, and changes with work, evidence, approvals, SLA, and audit history.

## Architecture
The .NET 8 backend follows Clean Architecture: `Domain` holds business concepts, `Application` use cases and ports, `Infrastructure` Oracle/identity/storage adapters, and `Api` the HTTP host. Dependencies point inward. Angular is a separate lazy-loaded TypeScript client.

## Core model
A ticket is the business request or issue. A work item is a routable executable unit within it. A ticket can contain sequential, parallel, or conditional work. Current ownership is only an operational projection; history remains in work item, assignment, and event records.

## Workflow and approvals
Workflow definitions, versions, steps, transitions, instances, and history are configuration data. Tickets retain their workflow version. Approval is independent from assignment and must enforce separation-of-duties rules.

## SLA and audit
SLA policy uses business calendars, targets, pauses, waiting reasons, and breaches. Events are the KPI foundation. Audit is append-only from the application perspective and uses hash/previous-hash fields for tamper evidence. Evidence is versioned, not overwritten or normally deleted.

## Security
The API enforces RBAC, client/department/team scope and SoD; the UI is not a security boundary. The design includes SSO/MFA integration points, correlation logging, secure files, and a read-only external auditor role.

## Database conventions
Oracle objects use `PA_TICKETS`; columns use `S_`, `N_`, and `D_`. Business/audit timestamps use `TIMESTAMP WITH TIME ZONE`; business timezone is `Asia/Kolkata`. This is a single organization: there is no tenant identifier.

## Phase 2 vertical slice
The first slice is exposed through authenticated `/api/v1/tickets` endpoints: create, classify, route, request/approve, execute, add evidence, complete work, resolve, and close. Controllers only translate HTTP requests to application commands. Aggregate invariants enforce approval/SOD, evidence-before-resolution, and resolution-before-closure. The repository emits event and audit records within the same transaction.

## Oracle 19c persistence
Oracle-specific EF Core configuration is isolated in Infrastructure. `Oracle.EntityFrameworkCore` 8.23.40 is configured for `OracleSQLCompatibility.DatabaseVersion19`; connection strings are environment/secret configuration. No Oracle target is available in this environment, so DDL/provider execution is pending and is not represented as passed.

## Phase 3 core engine
The single `Ticket` aggregate now controls classification, configurable routing, multiple work items, assignment/reassignment, waiting/resume, execution, evidence, resolution, reopening, cancellation, and closure. Current department/team/assignee values are projections; assignment rows, work items, events, and audit remain the history. Search is database-paginated through Infrastructure.

## Communication foundation
Communication is a separate capability associated with tickets/work items. `CommunicationChannel` supports Email, WhatsApp, Push, and InApp. Queueing persists a pending record; channel adapters are provider-specific Infrastructure contracts and no delivery is reported without a configured provider.

## Phase 6 Slice 5 communication and notification administration
Communication and notification administration controls the existing durable outbox and notification orchestrator; it does not introduce a parallel delivery engine. Template versions and notification-rule versions have draft, publish, and retire lifecycles, and queued messages retain their selected template-version identifier. Provider/channel administration exposes only the registered provider and the existing channel enum, with delivery enablement and retry bounds applied by the worker. Provider credentials are not persisted or returned because this deployment has no protected-secret/provider adapter boundary. Configuration operations use central authorization, optimistic row versions, idempotency, and audit history; auditors receive read-only access.

## Authentication and MFA foundation
Authentication is provider/configuration driven through JWT/OIDC extension points, with SAML representable as an additional provider type. Authorization remains a separate server-side concern. MFA contracts support disabled/optional/required policy, TOTP enrollment/verification/disable, delegated SSO MFA, and security events; secrets are represented as protected-secret/secret-reference fields and are never returned by normal APIs.

## SLA foundation
SLA policies are versioned and effective-dated configuration. Matching can target service, category, request type, ticket type, priority, department, team, and client. Calendars, holidays, waiting/pause, escalation, breach, and notification rules remain policy data. Resolution duration is explicitly active work plus waiting time.

## Phase 4 execution engine
Workflow definitions are represented as immutable versioned data. Validation rejects duplicate/orphan/unreachable steps, invalid transitions, unsupported condition fields, and invalid publication. Runtime instances maintain multiple authoritative step instances, including active/waiting/completed/failed states, controlled condition evaluation, and configurable join behavior (`ALL_REQUIRED`, `ANY_ONE`, or condition).

Approval remains independent from work assignment. Approval rules support any-one, all-required, sequential levels, requester/creator/executor separation-of-duties, immutable decision records, and controlled delegation. Rejection is a state outcome; its branch behavior is configuration-owned.

SLA execution uses version-selected policies and a deterministic business calendar. Response/resolution clocks support start, pause, resume, breach, completion, working hours, holidays, and active-plus-waiting duration. Timers/escalations require a durable background scheduler; no API-request timer is used.

## Durable Phase 4 execution
Workflow configuration drafts/publication, workflow step completion, approval decisions/delegation, SLA state changes, and communication queue creation are backed by Infrastructure repositories and Oracle transactions. Each retry-sensitive operation requires an idempotency key, and migration 007 stores dedupe state and durable jobs. Optimistic row-version columns and unique keys protect concurrent transitions; Oracle execution is still pending.

Background processing is an opt-in `PersistentBackgroundWorker` that leases durable job rows and dispatches only registered handlers. No API request or in-memory stopwatch is the source of truth for SLA, escalation, workflow timeout, or communication state.

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
