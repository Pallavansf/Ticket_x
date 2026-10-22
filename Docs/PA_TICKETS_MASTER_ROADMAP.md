# PA_TICKETS Master Roadmap

**Repository:** `G:\\WSS\\aw\\Tickets`  
**Branch baseline:** `workstream-i-lifecycle-authority`  
**Authority:** This document is the single source of truth for future PA_TICKETS planning and status.

## Permanent objective

Deliver a secure, working enterprise ticketing application using the existing
Angular standalone frontend, .NET 8 Clean Architecture backend, and
PostgreSQL integration-validation path. Product scope includes REQUEST,
INCIDENT, PROBLEM, TASK, and CHANGE; identity and five-role configurable RBAC;
permissions, resource scope, delegated administration; lifecycle, workflow and
durable transitions; audit, attachments, SLA, escalation, communications;
reporting, exports and schedules; persistence, migrations and operational
safeguards; and real Angular-to-API-to-PostgreSQL end-to-end acceptance.
Oracle remains deferred unless explicitly reopened.

## Fixed stage order

Stages are sequential. Source-level work may proceed in parallel only where its
dependencies are independent. Database operations remain separately approved.

### Stage 0 — Roadmap and evidence baseline

**Objective:** establish this roadmap, preserve repository evidence, and keep
the migration and provider restrictions explicit.

**Reuse and completed evidence:** Sprint reports 1-7, T60/T62A/T62B reports,
RBAC design, schema scripts, migration files, and existing source projects.

**Status:** `PASS` for documentation baseline only; this status does not imply
provider or release readiness.

**Exit evidence:** roadmap, evidence index, branch/worktree capture, and
artifact hashes. Failure blocks all later status claims.

### Stage 1 — Implementation inventory and requirement-to-code traceability

**Objective:** map product requirements to Angular, API, application, domain,
persistence, migrations, and tests; identify the highest-value unblocked work.

**Reuse:** existing projects under `src/`, `client/`, `tests/`, schema scripts,
and the Sprint/T62 reports.

**Verified:** `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md` records the
source-backed inventory, hosted authorization evidence, and
database-independent tests.

**Partial/missing:** runtime/provider, browser, and release evidence remain
incomplete even where source implementation exists.

**Status:** `PASS` for the traceability-stage exit criterion only; product
requirements remain `PARTIAL` or `BLOCKED` in the matrix.

**Exit evidence:** reviewed matrix with `PASS`, `PARTIAL`, `BLOCKED`,
`NOT STARTED`, or `NOT VERIFIED`, each backed by source/test evidence.

**Downstream block:** without this inventory, later implementation risks
duplicating existing work.

### Stage 2 — Core ticket lifecycle and workflow

**Objective:** complete REQUEST/INCIDENT/PROBLEM/TASK/CHANGE lifecycle,
workflow, durable transitions, concurrency and audit behavior.

**Verified:** domain/application services, workflow execution code, and source
transaction/audit paths exist.

**Partial/missing:** provider translation, transaction atomicity, uncertain
commit/recovery, and persisted lifecycle evidence remain unverified.

**Source increment:** workflow cancellation now records in-flight steps as
`Cancelled` rather than `Failed`, with a database-independent regression test
(Stage 2 result). This does not change the `PARTIAL` status or provider-backed
exit requirements.

**Transition increment:** the specialized Incident/Problem status entry point
now rejects invalid closure, reopening, and cancelled-ticket mutations using
the same aggregate invariants as the shared lifecycle methods. The transition
matrix and regression evidence remain source-only; Stage 2 stays `PARTIAL`.

**Dependencies:** Stage 1 traceability, approved schema/baseline, provider
validation, and recovery/TOCTOU decisions.

**Exit evidence:** provider-backed persistence tests, failure-injection results,
audit-chain evidence, and API acceptance for each lifecycle.

**Downstream block:** Stage 6 persistence and full E2E acceptance.

### Stage 3 — Identity, configurable RBAC, scope and delegation

**Objective:** deliver identity, five roles, permissions, effective grants,
client/resource scope, and delegated administration.

**Verified:** authorization policies, hosted HTTP denial/positive tests, RBAC
source contracts, and configurable-RBAC design exist.

**Partial/missing:** persisted IDs, historical dates, client/scope/delegation
lineage, revocation races, and provider behavior remain unverified.

**Dependencies:** owner decisions D04-D08, schema migrations 030-037, and
provider-backed concurrency tests.

**Exit evidence:** positive/negative HTTP matrix, persisted authorization facts,
effective-date/revocation tests, and independent scope/delegation evidence.

**Downstream block:** secure cross-tenant API and browser acceptance.

### Stage 4 — Supporting product capabilities

**Objective:** complete attachments/evidence, SLA/escalation, communications,
notifications, reports, exports, schedules, and operational metadata.

**Verified:** source services and several hosted/service authorization seams
exist; attachment authorization ordering has provider-independent coverage.

**Partial/missing:** storage ACL/TOCTOU, persisted report/schedule workers,
attachment relationships, retries, and provider evidence remain incomplete.

**Dependencies:** Stage 1 traceability, Stage 3 authorization, schema/provider
validation, and approved storage/evidence policies.

**Exit evidence:** API and persistence tests for ownership/isolation, storage
confidentiality, worker retry behavior, and audit outcomes.

**Downstream block:** complete API and Angular acceptance.

### Stage 5 — PostgreSQL schema/provider readiness and authorized reconciliation

**Objective:** reconcile the selected source baseline with an independently
verified PostgreSQL catalog using the hardened read-only adapter.

**Verified:** source-level adapter, deterministic snapshots, target guard,
preflight hash verifier, and authorization packet are implemented/tested.

**Blocked:** no owner-approved target identity, least-privilege role, baseline
adoption, provenance, evidence custody, or live provider authorization exists.
Migration history is unknown; migration 026 remains separate.

**Dependencies:** D01-D03, D09-D11 and independent hostname/peer/TLS policy.

**Exit evidence:** signed authorization, exact artifact hash, read-only catalog
result, expected/observed diff, target identity evidence, and retention record.

**Downstream block:** Stage 6 cannot claim persistence readiness.

### Stage 6 — Persistence integration and full end-to-end acceptance

**Objective:** validate real Angular-to-API-to-PostgreSQL behavior across
roles, lifecycles, scopes, attachments, reports, audit, and failure paths.

**Status:** `BLOCKED` by Stage 5 and unresolved provider/owner decisions.

**Exit evidence:** isolated persisted fixtures, HTTP and browser matrices,
concurrency/revocation evidence, cleanup/retention record, and no protected
target access.

### Stage 7 — Release readiness

**Objective:** approve deployment, migration/recovery, security, operational
monitoring, support, and rollback readiness.

**Status:** `NOT VERIFIED`; no release approval or migration execution is
claimed.

**Exit evidence:** signed release checklist, backups/restore evidence,
provider-specific runbooks, security review, owner approvals, and post-release
verification plan.

## Requirement-to-code status matrix

| Requirement | Current evidence | Status | Next evidence |
|---|---|---|---|
| Angular standalone shell and routes | `client/src/app` and existing Angular specs | `PARTIAL` | Browser/API acceptance matrix |
| .NET Clean Architecture projects | `src/PA.Tickets.*` projects and DI | `PASS` source structure | Integration behavior evidence |
| Core ticket lifecycle/workflow | Domain/application services and workflow tests | `PARTIAL` | Provider transaction and lifecycle tests |
| Identity and five-role RBAC | RBAC design, policies, hosted HTTP tests | `PARTIAL` | Persisted facts, revocation, scope evidence |
| Scope and delegated administration | Authorization readers and T45-T47 coverage | `PARTIAL` | Provider-backed lineage and race tests |
| Attachments/evidence | Attachment service and T57/T58 seam tests | `PARTIAL` | Persisted metadata/storage isolation |
| SLA/escalation/communications | Source services and focused tests | `PARTIAL` | Provider and worker evidence |
| Reporting/exports/schedules | Reporting services and authorization tests | `PARTIAL` | Persisted artifact/worker acceptance |
| Schema/provider reconciliation | Sprints 3-7 adapter, hash preflight, packet | `BLOCKED` | Owner authorization and read-only provider result |
| Migration 026 reporting | Separate contracts and fixed hashes | `BLOCKED` | Independent approval chain; never infer execution |
| Persistence E2E | Provider tests exist but guarded/unrun | `BLOCKED` | Stage 5 exit evidence and explicit test approval |
| Release readiness | No complete release evidence | `NOT VERIFIED` | Stage 7 approval package |

`PASS` means direct implementation and appropriate evidence exist; it never
means database or release approval. `PARTIAL`, `BLOCKED`, `NOT STARTED`, and
`NOT VERIFIED` must remain until their exit evidence is recorded.

## Permanent restrictions

Never access `pa_tickets_test` or `pa_tickets_h_verify`; never use
`pa_tickets_test_user`; never run
`tests/scripts/provision-postgresql-schema.ps1`; and perform no database
connections, SQL, migrations, schema changes, seeding, cleanup, sequence or
privilege changes without a separate explicit authorization. Do not modify
`.env.test.local` or `.vscode/mcp.json`, do not execute or alter migration 026,
and never infer migration history from object shape. Keep Oracle deferred and
owner decisions pending until verifiable evidence exists.

## Anti-loop rules

Future work must read this roadmap, inspect existing code, name the exact item
and exit criterion it advances, reuse existing tests, make one narrow change,
and update status only from evidence. A new sprint is justified only by new
evidence or a real dependency. A blocked stage must not cause the roadmap to be
replaced; independent source work should continue where safe.

## Immediate next action

Advance Stage 2 with a focused core lifecycle/workflow implementation inventory
and the highest-value unblocked source-level gap. Do not start another
provider-preflight sprint or live database operation.

