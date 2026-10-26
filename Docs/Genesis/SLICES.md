# PA_TICKETS Slice Plan (SLICES)

**Purpose:** convert `FRD.md` into small, independently verifiable slices.  
**Status authority:** `Docs/PA_TICKETS_MASTER_ROADMAP.md` remains authoritative for stage status. This file defines work units and acceptance checks, not claims of completion.

## How to use this file

- Execute **one slice at a time** unless dependencies are explicitly independent.
- Every slice names FR IDs, scope, dependencies, and evidence required.
- Before starting, compare the slice with the code, tests and previous result reports.
- Mark each criterion `PASS`, `PARTIAL`, `BLOCKED`, `NOT STARTED`, `NOT VERIFIED`, or `UNKNOWN` with an evidence reference.
- Do not repeat a slice whose blocker has not changed. Ask for the missing owner/operator action and continue safe independent source work.
- A slice can be source-only, test-only, documentation/governance, or provider-backed. Provider-backed slices require their own explicit authorization.
- Current statuses below reflect the available roadmap/traceability context; revalidate against the latest local repo before execution.

## Slice template

For any new slice, use:
- **ID / title**
- **FR IDs**
- **Stage and dependencies**
- **Objective**
- **Allowed changes / prohibited effects**
- **Acceptance criteria**
- **Test commands and expected evidence**
- **Actual result / status**
- **Next executable action**

---

## Stage 0 — Baseline and evidence integrity

### S0-01 — Establish repository baseline
- **FRs:** NFR-002, NFR-007
- **Objective:** capture branch, worktree state, existing roadmap, traceability and prior evidence without overwriting anything.
- **Acceptance:** branch/worktree evidence is retained; existing modified/untracked/ignored files are preserved; authoritative docs are identified.
- **Status:** historical stage exit `PASS` for documentation baseline only; re-check current state before changes.

### S0-02 — Evidence index and artifact provenance
- **FRs:** FR-016, NFR-002, NFR-007, NFR-010
- **Objective:** index prior sprint reports, schema artifacts, migration provenance and test evidence.
- **Acceptance:** each evidence item has a path/reference and scope limitation; no migration history is inferred from object shape.
- **Status:** partial by roadmap context; verify current evidence index.

## Stage 1 — Requirement traceability and implementation inventory

### S1-01 — Build/refresh FR-to-code matrix
- **FRs:** all FRs
- **Dependencies:** S0-01
- **Objective:** map each FR to Angular, API, application, domain, infrastructure, migration and tests.
- **Acceptance:** every FR has implementation paths, tests, evidence layer, status, gap and next acceptance criterion; duplicate or absent implementations are identified.
- **Status:** partial; existing `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md` is an input.

### S1-02 — Reconcile existing contracts and decisions
- **FRs:** FR-001–FR-018, NFR-005, NFR-010
- **Dependencies:** S1-01
- **Objective:** identify conflicts between FRD, API contracts, transition matrix, RBAC documents, migrations and owner decisions.
- **Acceptance:** conflicts are explicitly listed and resolved only by authoritative source/owner decision; no inferred policy.
- **Status:** partial / ongoing.

### S1-03 — Select highest-value unblocked source gap
- **FRs:** whichever FR is selected with evidence
- **Dependencies:** S1-01
- **Objective:** choose one actual source-level defect that can be safely fixed without blocked provider/schema work.
- **Acceptance:** selected gap has failing regression test or clear traceable acceptance criterion; implementation is minimal; tests pass.
- **Status:** next safe source-work pattern; select based on current repo rather than assume a particular gap.

## Stage 2 — Core ticket lifecycle and workflow

### S2-01 — Lifecycle invariant and transition matrix
- **FRs:** FR-001, FR-003
- **Objective:** validate allowed/denied transitions across REQUEST, INCIDENT, PROBLEM, TASK and CHANGE where supported.
- **Acceptance:** denied transitions leave state/events/save calls unchanged; allowed transitions follow the authoritative transition matrix.
- **Known evidence:** source-level cancellation and specialized Incident/Problem terminal-transition regressions exist; inspect current tests before adding duplicates.
- **Status:** PARTIAL.

### S2-02 — Workflow cancellation and failure semantics
- **FRs:** FR-004
- **Objective:** verify cancellation, in-flight cancellation, failure and retry semantics.
- **Acceptance:** cancellation is not mislabeled as failure; persisted execution status matches domain outcome.
- **Status:** source-level coverage exists; durable provider evidence remains partial.

### S2-03 — Transaction boundary and event durability
- **FRs:** FR-005, FR-006
- **Objective:** validate transaction ownership, nested persistence, audit/event durability and failure behavior.
- **Acceptance:** tests prove the intended transaction boundary; failed owner commit does not falsely clear durable events; no partial subtype/base-ticket result.
- **Known evidence:** Slice 46 introduced transaction-owner handling; Slice 50 added PostgreSQL failure-injection tests but provider execution was blocked by missing Incident schema.
- **Status:** source-level PARTIAL; provider BLOCKED.

### S2-04 — Provider persistence and Incident atomicity
- **FRs:** FR-002, FR-005, FR-006
- **Dependencies:** authorized target/catalog preflight, approved schema, OP-TEST authorization and migration 014 decision
- **Objective:** run the authorized PostgreSQL persistence and failure-injection cases.
- **Acceptance:** actual provider tests prove fresh-scope reload, audit/history durability, rollback/atomicity and test-owned cleanup; capture command and results.
- **Restrictions:** no provider operation before target/OP/PRE/OP-TEST and migration prerequisites are satisfied.
- **Status:** BLOCKED by operational authorization/schema readiness.

### S2-05 — API lifecycle acceptance
- **FRs:** FR-001–FR-006, FR-015
- **Dependencies:** provider acceptance and API test setup
- **Objective:** verify lifecycle outcomes through real API endpoints and persisted state.
- **Acceptance:** positive/negative HTTP matrix across applicable types; persisted lifecycle/audit state and concurrency conflicts are verified.
- **Status:** PARTIAL/BLOCKED pending provider evidence.

## Stage 3 — Identity, RBAC, scope and delegation

### S3-01 — Authentication contract
- **FRs:** FR-007, FR-015
- **Objective:** validate positive/negative authentication, claims and API errors.
- **Acceptance:** invalid credentials/tokens fail consistently; secrets are not logged; role/claims match persisted identity contract.
- **Status:** PARTIAL.

### S3-02 — Permission catalogue and role semantics
- **FRs:** FR-008
- **Dependencies:** owner decisions for canonical permission codes and role assignments
- **Objective:** reconcile configurable permission catalogue with source policies and migrations.
- **Acceptance:** duplicates/case aliases are handled per explicit owner decision; each built-in role's grants are approved and tested.
- **Status:** BLOCKED by owner decisions.

### S3-03 — Effective grants, dates and revocation
- **FRs:** FR-008, FR-009
- **Objective:** verify active/effective grants, expiration, revocation and fail-closed behavior.
- **Acceptance:** absent, expired, revoked or ambiguous facts deny; race tests prove revocation semantics.
- **Status:** PARTIAL/BLOCKED.

### S3-04 — Scope and delegation
- **FRs:** FR-009
- **Dependencies:** approved client/scope and delegation semantics
- **Objective:** validate organization/department/team/client/resource scope and explicit effective-dated delegation.
- **Acceptance:** in-scope access succeeds; out-of-scope access is denied; author, validity and revocation lineage are auditable.
- **Status:** PARTIAL/BLOCKED.

### S3-05 — Persisted authorization provider acceptance
- **FRs:** FR-007–FR-009
- **Dependencies:** approved schema baseline, migrations and provider authorization
- **Objective:** validate authorization facts and policy decisions using the real provider.
- **Acceptance:** live persisted positive/negative matrix passes without bypassing target guards.
- **Status:** BLOCKED pending schema/provider readiness.

## Stage 4 — Supporting capabilities

### S4-01 — Attachment authorization and integrity
- **FRs:** FR-010
- **Objective:** verify ownership/authorization order, hash validation, storage isolation and download behavior.
- **Acceptance:** unauthorized access fails before content disclosure; hash mismatch and expired/incomplete artifacts fail safely.
- **Status:** PARTIAL.

### S4-02 — SLA and escalation behavior
- **FRs:** FR-011
- **Objective:** verify due-time calculation, escalation policy and boundary conditions.
- **Acceptance:** time/zone assumptions are explicit; due/escalation transitions and failures are tested.
- **Status:** PARTIAL.

### S4-03 — Communications, notifications and retry
- **FRs:** FR-012
- **Objective:** verify queue persistence, transient/permanent failure classification, retry and duplicate control.
- **Acceptance:** terminal failures are observable/audited; retry does not create uncontrolled duplicate sends.
- **Status:** PARTIAL.

### S4-04 — Reports, exports and schedules
- **FRs:** FR-013
- **Objective:** validate report authorization, artifact creation/download/hash and scheduled execution.
- **Acceptance:** unauthorized requests cannot retrieve artifacts; hashes and expiry are checked; provider worker behavior is tested.
- **Known evidence:** source-level authorized export download/hash fix and regression test documented.
- **Status:** PARTIAL.

### S4-05 — Angular screen and API journey coverage
- **FRs:** FR-014, FR-015
- **Objective:** validate route, form, validation, loading/empty/error/success and backend integration behavior.
- **Acceptance:** focused Angular specs and browser-to-API journeys cover key roles and lifecycle paths.
- **Status:** PARTIAL.

## Stage 5 — PostgreSQL baseline, migration and provider readiness

### S5-01 — Owner decisions and target authorization
- **FRs:** FR-016, NFR-002, NFR-010
- **Objective:** reconcile D01/BASE, D09, TGT, OP/D02, PRE, migration-specific authorization and OP-TEST evidence.
- **Acceptance:** records are attributable, consistent, current, target-bound and unexpired; operator grants and evidence custody are verified.
- **Status:** BLOCKED in latest Slice 70 evidence. Do not repeat this slice without new approval/evidence.

### S5-02 — Immutable migration artifact verification
- **FRs:** FR-016, NFR-007, NFR-010
- **Objective:** verify exact reviewed migration bytes and provenance.
- **Acceptance:** Git blob and expected SHA-256 match; working-copy byte mismatch is not silently accepted.
- **Known evidence:** migration 014 historical blob SHA-256 `08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3` was reproduced.
- **Status:** PASS for historical blob hash only; this is not execution authorization.

### S5-03 — Fresh read-only target/catalog preflight
- **FRs:** FR-016, NFR-001, NFR-007, NFR-010
- **Dependencies:** S5-01 and S5-02 plus explicit read-only authorization
- **Objective:** verify actual server/peer, database, role, security state, schema/search path, ticket dependency, Incident objects, indexes, sequences and migration-history state.
- **Acceptance:** target identity and expected/observed catalog are recorded; unknown/partial/incompatible state stops the operation; no writes occur.
- **Status:** BLOCKED; not yet executed under cleared authorization.

### S5-04 — One-time migration 014 execution
- **FRs:** FR-016, NFR-003, NFR-007, NFR-010
- **Dependencies:** S5-03 PASS, operation-specific approval, fixed UTC window, operator/grants, recovery and evidence custody
- **Objective:** apply only the exact approved migration artifact in a controlled transaction.
- **Acceptance:** confirmed transaction outcome, no automatic retry, post-apply catalog matches artifact, evidence retained; uncertain outcome is reconciled read-only.
- **Restrictions:** migration 026 excluded; no fabricated ledger row or speculative cleanup.
- **Status:** BLOCKED.

### S5-05 — Provider test-write authorization and cleanup boundary
- **FRs:** FR-002, FR-005, FR-006, NFR-003, NFR-010
- **Dependencies:** explicit OP-TEST authorization and approved schema
- **Objective:** approve and verify the precise provider test scope, unique generated records, cleanup/recovery and retention.
- **Acceptance:** test scope is bounded; cleanup targets only test-owned unique records; evidence is retained.
- **Status:** BLOCKED.

## Stage 6 — Full persistence and E2E acceptance

### S6-01 — Real-provider persistence matrix
- **FRs:** FR-001–FR-013, FR-016
- **Dependencies:** Stage 5 exit evidence
- **Objective:** run authorized provider tests for ticket types, lifecycle, RBAC, scope, attachments, SLA, communication, reports and audit as applicable.
- **Acceptance:** real database persistence and reload, positive/negative authorization, failure recovery and audit evidence pass.
- **Status:** BLOCKED.

### S6-02 — Browser-to-API-to-PostgreSQL journeys
- **FRs:** FR-014, FR-015, FR-017
- **Dependencies:** S6-01 and authorized E2E environment
- **Objective:** verify end-user flows through Angular, API and PostgreSQL.
- **Acceptance:** create/update/transition/view flows work under applicable roles; validation/denial/error paths work; persisted results match UI/API responses.
- **Status:** BLOCKED.

### S6-03 — Concurrency, revocation and recovery
- **FRs:** FR-005, FR-006, FR-008, FR-009, FR-017
- **Dependencies:** S6-01
- **Objective:** validate row-version/concurrent updates, authorization revocation races, audit failures and uncertain commits.
- **Acceptance:** no silent partial success; conflicts/recovery outcomes are deterministic and evidenced.
- **Status:** BLOCKED.

### S6-04 — E2E cleanup and evidence audit
- **FRs:** FR-017, NFR-002, NFR-007, NFR-010
- **Objective:** verify test-owned cleanup, retained evidence and no access to protected targets.
- **Acceptance:** cleanup is scoped to generated fixtures; no prohibited target was touched; test evidence is reproducible.
- **Status:** BLOCKED.

## Stage 7 — Release readiness

### S7-01 — Operational/security release checklist
- **FRs:** FR-018, NFR-001–NFR-010
- **Objective:** review deployment, monitoring, support, secrets, permissions, backups, restore, migration/recovery and rollback.
- **Acceptance:** evidence-backed release checklist approved by accountable owners.
- **Status:** NOT VERIFIED.

### S7-02 — Release and post-release verification
- **FRs:** FR-018
- **Dependencies:** S7-01 PASS and separate release approval
- **Objective:** execute approved release and validate health, core journeys, telemetry and rollback readiness.
- **Acceptance:** release and post-release checks are evidenced; rollback path is viable.
- **Status:** NOT VERIFIED; no release is implied by this slice plan.

## Cross-slice gates

A slice cannot cross these gates without the listed evidence:
- **Source → provider:** real-provider authorization, target guard and schema preconditions.
- **Provider → browser E2E:** provider acceptance and API contract evidence.
- **Schema change:** approved baseline, exact artifact, target/operator/scope/window/recovery and evidence custody.
- **Migration history:** never inferred from object shape; unknown remains unknown until reconciled.
- **Release:** approved Stage 7 package and operational evidence.

## Slice completion ledger

Maintain a compact ledger with columns: `Slice ID | FR IDs | Status | Evidence | Blocker | Next action | Updated UTC`.
Do not invent dates. Update only after running or reviewing actual evidence.
