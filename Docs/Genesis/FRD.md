# PA_TICKETS Functional Requirements Document (FRD)

**Project:** PA_TICKETS — Enterprise Ticketing System  
**Repository:** `G:\WSS\aw\Tickets`  
**Branch baseline:** `workstream-i-lifecycle-authority`  
**Stack:** Angular standalone frontend, .NET 8 Clean Architecture backend, PostgreSQL validation path  
**Status:** Working baseline derived from the existing Master Roadmap and Requirement-to-Code Traceability. It is not a claim that every detailed product decision has been finalized.

## 1. Purpose

Define the functional scope and acceptance expectations for PA_TICKETS in one place so each implementation slice can be validated against explicit requirements. This FRD consolidates requirements currently supported by repository roadmap and traceability evidence. Where source records are incomplete or decisions remain pending, the requirement is marked **TBD / owner decision required** rather than guessed.

## 2. Product objective

Deliver a secure, usable, auditable enterprise ticketing application with real Angular-to-API-to-PostgreSQL behavior. The application supports five work-item types, lifecycle and workflow processing, configurable authorization, evidence/attachments, SLA and communications, reporting/export/scheduling, and operationally safe persistence and release.

Oracle is deferred unless explicitly reopened. PostgreSQL is the integration-validation path.

## 3. Scope

### 3.1 In scope
- Work-item types: `REQUEST`, `INCIDENT`, `PROBLEM`, `TASK`, `CHANGE`.
- Ticket creation, reading, updating, assignment, lifecycle transitions and workflow execution.
- Durable state transitions, concurrency handling, audit events and transaction/recovery behavior.
- Authentication and the distinct roles `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, `SPECTATOR`.
- Configurable permissions, effective grants, client/resource scope and delegated administration.
- Attachments/evidence, ownership, secure download and storage isolation.
- SLA evaluation, escalation, notifications, communications and retry/persistence behavior.
- Reporting, exports, artifact download, schedules and background jobs.
- Angular UI, API contracts, validation/error behavior and operational logging.
- PostgreSQL schema reconciliation, migrations, persistence tests and full E2E acceptance, subject to separate operational authorization.
- Evidence-backed stage status and release readiness.

### 3.2 Out of scope or explicitly constrained
- Oracle implementation/validation unless explicitly reopened.
- Migration 026: separate approval chain; do not execute or modify it under the current plan.
- Any schema/data/permission operation against a database not explicitly approved for that operation.
- Inferring migration history from existing object shape.
- Declaring provider, browser, or release readiness from source code or fake/in-memory tests alone.

## 4. Actors and authorization model

### 4.1 Actors
- Authenticated end user.
- Ticket administrator.
- Super administrator.
- Admin Head with explicitly controlled delegation.
- Read-only spectator.
- Background worker/service actor where supported by the existing source contract.
- Database/release authority and least-privilege database operator for operational work.

### 4.2 Role semantics currently documented
- `USER`: ordinary ticket operations allowed by explicitly granted, scope-checked permissions.
- `ADMIN`: department-scoped administration and ticket operations constrained by approved permissions and separation-of-duties rules.
- `ADMIN_HEAD`: may manage an Admin only through an explicit, effective-dated delegation record.
- `SUPER_ADMIN`: organization-wide authority remains permission-driven and subject to protected-role and approval controls; it is not a generic bypass.
- `SPECTATOR`: read-only access; effective active Spectator membership denies mutation requests even if another role grants write permission.

**Owner decisions required:** exact role-to-permission assignments, canonical permission catalogue, historical grant validity/backfill, client/scope relationship semantics, delegation lifecycle, and protected-role grant policy. Do not infer these from role names.

## 5. Functional requirements

IDs are stable references for the slice plan. Statuses describe the current evidence, not a promise that implementation is complete.

### FR-001 — Work-item types
The system shall support `REQUEST`, `INCIDENT`, `PROBLEM`, `TASK`, and `CHANGE` as distinct work-item types using the existing domain/API contracts.
- Acceptance: each type can be represented and retrieved through the appropriate contract; subtype-specific fields/records are validated; tests cover supported and invalid type combinations.
- Current evidence: source-level domain/application services exist.
- Status: **PARTIAL** — provider and full API/browser acceptance remain incomplete.

### FR-002 — Ticket identity and persistence
The system shall persist tickets and related records using the established identifier and mapping conventions without changing externally observable behavior without approval.
- Acceptance: create, commit, dispose/reload in a fresh scope, and verify the same persisted record and required related evidence.
- Status: **PARTIAL** — an isolated repository persistence proof has passed previously; full lifecycle/provider acceptance remains incomplete.

### FR-003 — Lifecycle transition rules
The system shall enforce valid lifecycle transitions and reject invalid terminal-state changes without changing status, emitting invalid domain events, or saving invalid mutations.
- Acceptance: transition matrix covers allowed and denied transitions for all applicable types; rejected operations leave aggregate state/events/persistence unchanged.
- Status: **PARTIAL** — source-level regressions exist; provider-backed lifecycle coverage remains incomplete.

### FR-004 — Workflow execution
The system shall execute configured workflow steps durably, preserve execution state, and correctly handle cancellation, failure and retry behavior.
- Acceptance: workflow success, failure, cancellation and in-flight cancellation are tested; cancelled steps are not misclassified as failures; persisted state matches the result.
- Status: **PARTIAL** — source-level tests exist; durable provider evidence is incomplete.

### FR-005 — Transactional atomicity and recovery
Ticket, subtype, lifecycle, audit and workflow changes that are defined as one operation shall commit atomically. Uncertain commit outcomes shall be reconciled rather than blindly retried.
- Acceptance: provider failure injection proves rollback/atomicity; audit and subtype rows do not become inconsistent; uncertain outcomes are classified and reconciled without automatic retry.
- Status: **BLOCKED** for provider acceptance pending operational authorization and schema readiness.

### FR-006 — Auditability
The system shall record relevant actor, action, timestamps, lifecycle changes and audit evidence according to the existing audit contract. Audit failures must not silently produce a success claim.
- Acceptance: persisted audit event/chain evidence matches the operation; failures and rollback behavior are tested.
- Status: **PARTIAL/BLOCKED** — provider durability and full audit-chain evidence remain incomplete.

### FR-007 — Authentication
The system shall expose authenticated API behavior through the existing authentication contracts and handle invalid, missing or expired credentials consistently.
- Acceptance: positive and negative authentication tests, claim validation, and API error contract tests.
- Status: **PARTIAL** — persisted identity/session and end-to-end behavior remain to be verified.

### FR-008 — Configurable RBAC and permissions
Authorization shall be based on the approved configurable permission catalogue, effective grants, scope and role semantics; missing or ambiguous authorization data shall fail closed.
- Acceptance: positive/negative HTTP matrix; persisted authorization facts; no implicit privilege from missing records; role/permission canonicalization and duplicate handling follow owner decisions.
- Status: **PARTIAL/BLOCKED** — owner decisions and provider-backed authorization evidence remain incomplete.

### FR-009 — Scope and delegated administration
The system shall enforce approved organization/department/team/client/resource scope and effective-dated delegation. Revocation and expiry must be respected.
- Acceptance: in-scope access succeeds; out-of-scope access is denied; expiry/revocation races are tested; delegation records have traceable authorship and lifecycle.
- Status: **PARTIAL** — provider-backed lineage and race tests remain incomplete.

### FR-010 — Attachments and evidence
The system shall enforce attachment ownership and authorization before access, provide secure downloads, and preserve storage confidentiality/integrity.
- Acceptance: unauthorized callers cannot read metadata or content; persisted relationships and object hashes are verified; storage ACL and TOCTOU cases are tested.
- Status: **PARTIAL** — source safeguards exist; provider/storage isolation evidence remains incomplete.

### FR-011 — SLA and escalation
The system shall evaluate configured SLA policies and escalate work items according to approved rules.
- Acceptance: policy boundary, due-time, escalation and persistence/worker behavior tests; time zone and retry semantics are explicit.
- Status: **PARTIAL** — source behavior exists; provider/worker acceptance remains incomplete.

### FR-012 — Communications and notifications
The system shall support the documented communication/notification channels and durable dispatch/retry behavior.
- Acceptance: queued, sent, transient failure, permanent failure, retry and terminal failure behaviors are observable and auditable; duplicate delivery is controlled according to the approved contract.
- Status: **PARTIAL** — source seams exist; provider/worker persistence evidence remains incomplete.

### FR-013 — Reporting, exports and schedules
The system shall authorize reports, generate export artifacts, verify artifact integrity, provide authorized download, and support scheduled/background execution where configured.
- Acceptance: authorization precedes data/artifact access; completed artifact hash matches persisted metadata; expired/incomplete artifacts are rejected; schedule/worker behavior is tested.
- Status: **PARTIAL** — a source-level export download fix and regression test are documented; provider worker/storage behavior remains incomplete.

### FR-014 — Angular user experience
The Angular standalone UI shall expose routes, screens, forms, validation, ticket actions and API integration consistent with backend contracts.
- Acceptance: focused component/service specs plus browser-to-API journeys for key roles and lifecycles; loading, empty, success, validation and error states are verified.
- Status: **PARTIAL** — Angular source/specs exist; full browser E2E remains unverified.

### FR-015 — API contract and error handling
The API shall validate inputs, enforce authorization, return consistent error responses, and avoid leaking secrets or sensitive data through errors/logs.
- Acceptance: request validation, authorization denial, conflict/concurrency, not-found, unexpected error and secret-redaction tests.
- Status: **PARTIAL** — source tests exist; complete operational telemetry and API/browser acceptance remain incomplete.

### FR-016 — PostgreSQL schema and migrations
PostgreSQL schema changes shall be based on an approved baseline, exact artifact provenance, target identity verification, and an operation-specific execution/recovery plan.
- Acceptance: exact migration bytes/hash are verified; current target and catalog are read-only inspected first; expected/observed definitions are reconciled; transaction outcome and post-apply catalog evidence are retained.
- Status: **BLOCKED** — authorization and migration history remain unresolved. Migration 026 is excluded.

### FR-017 — End-to-end acceptance
The product shall be validated through real Angular → API → PostgreSQL flows across roles, work-item types, scopes, attachments, reports, audit and failure paths.
- Acceptance: persisted fixtures, HTTP/browser matrices, concurrency/revocation tests, recovery/cleanup evidence and no protected-target access.
- Status: **BLOCKED** by provider readiness and unresolved authorization gates.

### FR-018 — Release readiness
Release shall require operational runbooks, backup/restore evidence, migration/recovery plan, security review, monitoring/support plan, approvals and post-release verification.
- Acceptance: completed release checklist backed by real evidence.
- Status: **NOT VERIFIED**.

## 6. Non-functional requirements

### NFR-001 Security by default
Fail closed on missing/ambiguous authorization facts. Enforce least privilege, scope, ownership, secure attachment access, secret redaction and protected-target restrictions.

### NFR-002 Auditability
Important changes and operational decisions must have attributable evidence. Do not fabricate audit entries, approvals, migration ledger rows or historical facts.

### NFR-003 Data integrity
Use transactions and concurrency controls where required. Do not silently retry uncertain commits. Preserve consistency among ticket, subtype, workflow and audit records.

### NFR-004 Maintainability
Retain .NET 8 Clean Architecture boundaries and Angular standalone structure. Prefer narrow, tested changes; avoid unnecessary rewrites and duplicate implementations.

### NFR-005 Compatibility
Preserve existing business logic and observable output unless a requirement explicitly authorizes behavior change. Make API/DB contract changes explicit and traceable.

### NFR-006 Observability and privacy
Provide useful diagnostics without logging passwords, tokens, secrets or unnecessary sensitive payloads.

### NFR-007 Reproducibility
Tests, migrations and acceptance evidence must identify exact commands, target class, artifact/hash and outcome, without exposing credentials.

### NFR-008 Performance
Avoid unnecessary database round trips, repeated materialization and avoidable work, but only optimize after establishing output/behavior equivalence with regression tests. No performance target has been formally supplied in the source baseline; quantify targets before claiming performance acceptance.

### NFR-009 Time and localization
Time-zone, timestamp, date-effective and expiry behavior must be explicit and consistently tested. Exact business rules remain owner-approved contract items where not specified.

### NFR-010 Recovery
Unknown migration history or uncertain transaction outcomes remain `UNKNOWN` until reconciled with trustworthy evidence. Fail closed; do not infer history from object shape.

## 7. Data, database and migration constraints

- PostgreSQL is the integration-validation provider; Oracle remains deferred unless explicitly reopened.
- Never access `pa_tickets_test` or `pa_tickets_h_verify`; never use `pa_tickets_test_user`.
- Never run `tests/scripts/provision-postgresql-schema.ps1`.
- Do not modify `.env.test.local` or `.vscode/mcp.json`.
- Do not execute or modify migration 026; it has a separate approval chain.
- No database connection, SQL, migration, schema/data change, seed, cleanup, sequence or privilege change without explicit authorization for the exact target and operation.
- Never infer applied migration history from schema shape. Never fabricate a migration ledger row.
- Migration 014's reviewed historical Git-blob bytes have recorded SHA-256 `08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3`; the working-copy bytes previously differed. Use only the exact approved artifact bytes for any separately authorized operation.

## 8. Status vocabulary

- `PASS`: required behavior and appropriate evidence exist at the stated layer.
- `PARTIAL`: some behavior/evidence exists, but acceptance is incomplete.
- `BLOCKED`: required dependency or authorization prevents the next validation.
- `NOT STARTED`: work has not begun.
- `NOT VERIFIED`: no sufficient evidence supports a conclusion.
- `UNKNOWN`: the underlying state cannot safely be inferred.

A source-level PASS is not a provider/browser/release PASS. State the layer for every claim.

## 9. Requirements traceability

Every FR must map to:
1. source path(s);
2. test(s);
3. slice ID(s);
4. current status;
5. evidence reference;
6. known gap and next acceptance criterion.

Use `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md` as an existing evidence input and update it only from verified source/test results.

## 10. Open decisions

The following remain owner decisions and must not be invented by implementation agents:
- canonical permission catalogue and role grants;
- historical/effective grant validity and backfill policy;
- client/scope relationships and delegation derivation;
- protected role and separation-of-duties policy;
- audit actor and legacy provenance semantics;
- approved baseline adoption and migration-history treatment;
- target identity/security policy, operator grants, operation windows, recovery, custody and retention;
- explicit performance targets and any unspecified SLA/time-zone rules.

## 11. Source basis and limitations

This FRD consolidates the repository's Master Roadmap and Requirement-to-Code Traceability findings available during drafting. It is a **working baseline**, not proof that every requirement is implemented or that every owner decision is approved. Existing detailed design documents, transition matrices, API contracts and owner decisions remain authoritative for their specific subjects. Reconcile this FRD against those documents before treating it as a formally approved replacement.
