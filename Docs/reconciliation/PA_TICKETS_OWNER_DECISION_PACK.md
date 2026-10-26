# PA_TICKETS Owner Decision Pack

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Purpose:** owner completion of the decisions required before a PostgreSQL
schema-change request can be considered.

This pack records questions and evidence. It does not approve a baseline,
schema operation, migration, role, grant, or migration 026. Every decision is
`PENDING` because no verifiable owner approval or rejection is present in the
repository.

## Owner-selected intended V1 policy

The database owner selected the conservative recommendations in the current
working instructions as the intended V1 policy. This selection is recorded for
implementation guidance only; it is not a signed or attributable approval
record. Technical validation and the formal decision fields below remain
outstanding.

- **D01:** adopt the reviewed foundational baseline only after exact definition,
  hash, provenance, and expected-versus-observed validation; otherwise fail
  closed and keep history unknown.
- **D02:** use a dedicated non-superuser schema operator; no `postgres`
  exception without a separately scoped approval.
- **D03:** reconcile keys, defaults, ownership, and allocation metadata without
  resetting or advancing sequences; preserve existing values.
- **D04:** use canonical uppercase role/permission codes with explicit
  duplicate/alias remediation; ambiguous identities do not authorize.
- **D05:** preserve unknown historical grant facts and deny by default; do not
  invent effective dates, provenance, grants, or backfills.
- **D06:** defer client-scoped authorization unless the authoritative client
  relationship and FK semantics are validated; missing or ambiguous lineage
  fails closed.
- **D07:** use explicitly authored, lifecycle-managed scope/delegation rows
  with effective dates, revocation, auditability, and fail-closed absence;
  migration 031's `$scope_backfill$` conflict must be resolved before use.
- **D08:** provision the five role identities only with a separately approved
  permission-grant matrix; role creation never implies grants.
- **D09:** represent unknown migration history explicitly outside fabricated
  applied-migration rows; never infer history from object shape.
- **D10:** use an explicit actor type with a nullable user FK where appropriate,
  preserving user/system/legacy actors and existing audit hash-chain inputs;
  migration 038 remains blocked until compatibility is validated.
- **PRE:** perform a read-only shared and subtype-specific lifecycle preflight
  before mutation; denied or invalid transitions perform no persistence, with
  allowed and denied regression tests.
- **TGT/OP/BASE:** require independently authenticated target identity, a
  dedicated least-privilege operator, and exact baseline/provenance/hash
  comparison before any provider operation. No target metadata is supplied by
  this record.

## Decision records

### D01 — Foundational schema baseline *(prerequisite)*

**Question:** Adopt the existing foundational catalog as the named PostgreSQL
baseline, or reject it and require another baseline  
**Why it matters:** Adoption determines whether later additive changes can be
compared safely; object presence alone does not prove provenance.  
**Options/consequences:** Adopt only after exact definition equivalence and
hash review; reject and require a replacement baseline; defer and keep all
history unknown.  
**Evidence:** T62B.1 matrix; T62B.3 approval packet; hashes for foundational
scripts and migration artifacts; catalog definition export.  
**Owner role:** Database/release authority.  
**Decision:** `PENDING`  
**Rationale/evidence reference:** No approval record found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** All schema-change requests and migration 033 ledger adoption.

**Proposed decision (Slice 35 recommendation; not approval):** Adopt Strategy A: use the historically verified foundational artifact tests/scripts/postgresql-foundational-ddl.sql at Git commit 1bcc493d267e23e3e126b9b698fa4344ebe8588a as the baseline, identified by SHA-256 (raw file bytes) 4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19. Keep current expanded worktree additions as separately versioned changes unless independently reviewed and approved.

**Rationale and trade-offs:** Slice 33 verified the historical bytes and Git path; Slice 34 found that the expanded artifact mixes source-required additions with DML, setval operations, reference data, and privileged audit-maintenance behavior. Strategy A preserves reproducible provenance and narrower approval scope while requiring an explicit additive sequence for current lifecycle compatibility. The untraceable 5CAA... value is not a candidate artifact.

**Explicit exclusions:** This proposal authorizes no schema execution, migration, seeding, sequence operation, grant, privilege change, cleanup, or provider test. It does not assert live catalog equivalence or resolve migration history.

**Owner attestation (required to change Decision from PENDING):**
- Response (APPROVE, REJECT, or DEFER): ____________________
- Owner identity and role: ____________________
- UTC decision date: ____________________
- Decision scope/expiry: ____________________
- Approval reference or signature: ____________________
- Rationale and evidence references: ____________________

**Technical validation status:** Raw-byte hash and Git provenance are source-verified. Live catalog equivalence, sequence state/ownership, operator privileges, and provider behavior remain unverified and require separate TGT/OP/PRE authorization.

**User-confirmed decision input (Slice 58; not formal approval):** The user explicitly confirmed the recommended `APPROVE` selection for review, subject to verification of the exact foundational baseline and its compatibility with the existing database. The formal `Decision` remains `PENDING`; owner identity/role, UTC decision date, scope/expiry, approval reference or signature, and technical validation evidence are still required.

**Explicit owner-supplied decision (Slice 67; formal evidence incomplete):**
Owner identity: `Mr. H`; role: `SE`. The owner explicitly approves the
recommended D01/BASE decision in principle, subject to exact baseline and
compatibility verification. No approval reference/signature, UTC date, expiry,
or compatibility evidence was supplied. Formal `Decision` remains `PENDING`.
### D02 — Least-privilege schema operator *(prerequisite)*

**Question:** Approve a dedicated non-superuser operator, or document a
strictly scoped exception for `postgres`  
**Why:** T62A.2 found `postgres` is superuser; this exceeds minimum authority.  
**Options:** Dedicated role with CONNECT, target-schema USAGE/CREATE, and
required object/sequence rights; time-bounded exception with explicit scope;
defer all operations.  
**Evidence:** T62A.2 privilege metadata and DDL privilege inventory.  
**Owner role:** Security/database authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Any schema operation.

### D03 — Sequence allocation safety *(prerequisite)*

**Question:** How will existing keys, defaults, ownership, and future sequence
allocation be reconciled without unsafe reset/advance  
**Why:** T62B.1 observed start/increment/cache `1/1/1`, no ownership relation,
and unset `last_value`; EF/provider allocation is provider-specific.  
**Options:** Approved metadata-only reconciliation followed by controlled
allocation plan; explicitly assigned keys for a bounded fixture; defer writes.
No option may reset or advance a sequence in this task.  
**Evidence:** EF `SequenceDefault` mappings, provider generators, exact
sequence catalog report, primary-key/sequence reconciliation method.  
**Owner role:** Database/provider authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Inserts, backfills, and migrations 028â€“037.

### D04 — Canonical RBAC codes

**Question:** Are role and permission codes canonical uppercase, and how are
aliases/case duplicates resolved  
**Why:** Migration 031 adds canonical indexes and rejects ambiguous identities;
037 depends on protected built-in codes.  
**Options:** Uppercase-only with explicit duplicate remediation; preserve legacy
aliases with a mapping; defer RBAC upgrades.  
**Evidence:** Migrations 031/037, role/permission mappings, duplicate report.  
**Owner role:** Security/RBAC authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migrations 031, 034, 037 and authorization acceptance.

### D05 — Historical grant validity *(prerequisite)*

**Question:** What effective-date/backfill policy applies to existing grants,
and do unknown historical grants deny by default  
**Why:** Migrations 031/034 add dates/status/provenance and require unknown
history to remain non-authorizing.  
**Options:** Evidence-backed backfill; preserve unknown and fail closed; reject
historical rows pending manual reconciliation.  
**Evidence:** Existing source contract, migrations 031/034, data lineage and
backfill/rollback plan.  
**Owner role:** Security/compliance/data authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migrations 031/034 and persisted authorization validation.

### D06 — Client and scope relationships

**Question:** What is the authoritative client identity and intended FK behavior
for scope rows  
**Why:** Migration 035 intentionally defers client FKs; authorization readers
depend on parent relationships.  
**Options:** Approved existing client key/FK; nullable/deferred relationship
with explicit fail-closed behavior; defer client-scoped authorization.  
**Evidence:** Client source contract, migration 035, scope reader mappings.  
**Owner role:** Authorization/data authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 035 client/scope work and client-scoped tests.

### D07 — Scope and delegation derivation

**Question:** Are scope/delegation records derived from memberships or explicitly
authored  
**Why:** This controls whether migration 035 backfills rows and how revocation
is represented.  
**Options:** Deterministic derivation with approved provenance; explicit rows
with lifecycle ownership; defer delegated authorization.  
**Evidence:** Migration 035, `DatabaseDelegationFactsReader`, scope/delegation
contracts and data lineage.  
**Owner role:** Authorization authority.  
**Decision:** `PENDING`  
**Owner-proposed response:** `OWNER-PROPOSED — PENDING FORMAL APPROVAL`
Preferred option: explicitly authored, lifecycle-managed scope and delegation
records with an identifiable author, effective dates, explicit revocation,
auditable changes, and fail-closed behavior when valid authorization records
are absent.
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 035 and delegation/scope provider validation.

**Source compatibility note:** The `$scope_backfill$` block in
`database/migrations/postgresql/031_configurable_rbac.sql` currently derives
scope rows from active memberships and user department/team values. That draft
behavior conflicts with the preferred explicitly authored model and requires
an owner-approved migration-design resolution. `DatabaseDelegationFactsReader`
and `AdminHeadDelegationPolicy` read effective-dated delegation records and
fail closed when the required active delegation is absent; they do not prove
that the proposed authoring lifecycle exists.

### D08 — Built-in roles and grants

**Question:** Approve the five built-in role identities and any intended grants
separately from role-table creation  
**Why:** Migration 037 is explicitly session-gated and inserts roles only; it
must not invent grants.  
**Options:** Roles only; roles plus an separately approved grant matrix; defer
provisioning.  
**Evidence:** Migration 037, role matrix, permission policy, duplicate-role
report.  
**Owner role:** Security authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 037 and RBAC acceptance.

### D09 — Unknown migration history *(prerequisite)*

**Question:** How is unknown history represented without fabricating applied
migration records  
**Why:** Migration 033’s ledger is absent; object shape cannot prove execution.  
**Options:** Record an explicit unknown-history baseline outside the database;
adopt only after owner-approved fingerprint; reject ledger creation until
independent evidence exists.  
**Evidence:** T62B.1 catalog/matrix, artifact hashes, release records, 033
ledger contract.  
**Owner role:** Release authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 033 and all claims of applied history.

**User-confirmed decision input (Slice 58; not formal approval):** The user explicitly confirmed the recommended `APPROVE` selection to retain migration history as `UNKNOWN`, with fail-closed preflight and recovery handling. The formal `Decision` remains `PENDING`; owner identity/role, UTC decision date, scope/expiry, approval reference or signature, and attributable recovery evidence are still required.

**Explicit owner-supplied decision (Slice 67; formal evidence incomplete):**
Owner identity: `Mr. H`; role: `SE`. The owner explicitly approves retaining
migration history as `UNKNOWN` with fail-closed recovery. Recovery details,
evidence custody, approval reference/signature, UTC date, and expiry remain
unsupplied. Formal `Decision` remains `PENDING`.

### D10 — Audit actor contract *(prerequisite)*

**Question:** What actor types, nullability/FK behavior, writer/reader
compatibility, and hash-chain preservation rules apply to historical audit rows  
**Why:** Migration 038 intentionally aborts because the current non-null actor
contract cannot safely represent historical/system actors.  
**Options:** Typed legacy/system actor; nullable actor FK with explicit type;
separate actor relation; defer audit DDL.  
**Evidence:** Migration 038, audit EF mappings, chain coordinator, historical
preservation and verification plan.  
**Owner role:** Audit/compliance authority.  
**Decision:** `PENDING`  
**Owner-proposed response:** `OWNER-PROPOSED — PENDING FORMAL APPROVAL`
Preferred option: an explicit actor type with a nullable user foreign key where
appropriate, representing user, system, and historical/legacy actors without
fabricating user identities while preserving existing audit history and
hash-chain integrity.
**Rationale/reference:** No approval found; intentional abort remains.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 038 and audit schema changes.

**Source compatibility note:** `AuditRow` and `TicketsDbContext` currently map
`N_ACTOR_USER_ID` as a non-null `long`, while writers hash `actorUserId` and
`AuditChainVerifier` validates that value as part of the canonical chain. The
blocked migration 038 explicitly requires an approved actor-type, nullable/FK,
writer/reader-compatibility, and chain-integrity contract. The proposed actor
model is therefore a preference requiring further design; it is not established
by the current mappings and cannot authorize migration 038.

## Candidate policy choices for owner review

The following conservative choices are proposed inputs for owner review. They
are not formal decisions, do not change the Decision fields above, and do not
authorize schema operations, migrations, grants, or provider tests.

| ID | Proposed choice | Rationale and dependencies | Acceptance evidence and prohibited operations |
|---|---|---|---|
| D01 | Adopt only one immutable foundational DDL artifact after exact-byte hash, provenance, and definition review | Prevents catalog shape from being mistaken for migration history; required before additive upgrades | Owner attribution, artifact/version, raw-byte SHA-256, provenance, expected/observed comparison; no schema change until accepted |
| D02 | Use a dedicated least-privilege non-superuser operator | Limits target and cleanup authority; T62A.2 found postgres is superuser | Role/grant evidence with scope and expiry; no privilege changes or operations until accepted |
| D03 | Preserve sequence state and use an approved metadata/reconciliation plan; prohibit blind reset or advance | Avoids collisions and fabricated key history | Sequence/default/ownership evidence and owner decision; no sequence operation or insert plan until accepted |
| D04 | Require canonical permission codes with explicit alias and duplicate handling | Migrations 031/037 and readers require stable identities | Canonical catalogue, duplicate report, mapping approval; no RBAC data changes |
| D05 | Preserve unknown grant dates/provenance and fail closed when validity cannot be established | Prevents historical uncertainty from authorizing access | Backfill/rollback policy and lineage evidence; no grant backfill or authorization acceptance |
| D06 | Derive client/tenant scope only from validated authenticated context and valid relationships | Keeps scope tied to authoritative identity and parent relationships | Client identity/FK contract and reader evidence; no scope migration or client-scoped tests |
| D07 | Prefer explicit lifecycle-managed scope/delegation records; preserve explicit rows and fail closed on conflicts | Matches the existing owner-proposed response; migration 031 derivation remains a conflict | Approved authored/derived model, provenance and revocation rules; no migration 031 execution |
| D08 | Define five built-in role identities separately from a separately approved grant matrix | Prevents role creation from inventing permissions | Role identity and grant matrix approval; no role/grant provisioning |
| D09 | Represent unknown migration history explicitly outside the ledger until provenance is approved | Avoids fabricated applied-migration records | Fingerprint, unknown-history record, and owner attribution; no ledger insertion or history claim |
| D10 | Use explicit user/service/system actor types with nullable user FK where justified and preserve hash-chain inputs | Supports historical actors without fabricated users while retaining audit integrity | Actor schema, writer/reader and chain evidence; migration 038 remains blocked |

For every row, the owner must supply the existing attribution fields
(identity, UTC date, scope, approval reference/signature, rationale, and
evidence references). Until then each formal Decision remains PENDING.
## Target declaration reconciliation (Slice 39)

The repository contains two incompatible PostgreSQL target declarations:

- Docs/PA_TICKETS_POSTGRESQL_READ_ONLY_VALIDATION_AUTHORIZATION.md names localhost:5432 / pa_tickets_e2e_verify / postgres for its read-only catalog packet. This is a historical authorization template with status PENDING.
- Docs/PA_TICKETS_AUTHORIZED_E2E_BRINGUP_RESULT.md names localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex and records a prior bring-up result for that target. It does not supply the required current attributable TGT approval fields.

No attributable canonical attestation currently selects one declaration as the target for the next operation. The database/release/security owner must make one explicit TGT decision identifying the host, port, database, role, authentication and hostname/peer/TLS expectations, scope, expiry, and approval reference. Until that decision and fresh independent identity evidence are recorded, neither target may be used and all provider operations remain blocked. This reconciliation does not change any formal decision status.
### TGT owner attestation - target selection (Slice 40)


**Selected database:** ____________________
**Selected role:** ____________________
**Approved hostname/peer/TLS expectations:** ____________________
**Authentication method (no secret):** ____________________
**Permitted operation scope:** ____________________
**UTC approval date:** ____________________
**Expiry:** ____________________
**Owner identity and role:** ____________________
**Approval reference or signature:** ____________________
**Rationale and evidence references:** ____________________

The owner must select exactly one preserved candidate above. Selection alone does not authorize provider access and does not satisfy OP, BASE, PRE, or OP-TEST. Formal TGT status remains PENDING until these fields are completed with attributable evidence and a fresh independent identity record is collected.

**User-supplied TGT evidence (not independently executed by Codex):**
The user selected `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex` and supplied the owner labels `Mr.H & SE` and `Mr.H` as signature/reference labels. The supplied SQL result reports database `pa_tickets_codex_e2e`, role `pa_tickets_codex`, server address `::1`, port `5432`, PostgreSQL 18.6 on Windows 64-bit, recovery `false`, transaction read-only `off`, and SSL `false`. Non-TLS IPv6 loopback is the stated local read-only policy. This is supporting configuration/evidence input, not a live identity check performed by Codex.

**Explicit owner-supplied TGT decision (Slice 67; conditional):** Owner identity:
`Mr. H`; role: `SE`. The owner conditionally approves the proposed target
`localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, subject to
independent target and security verification. Host/peer/TLS policy,
authentication evidence, UTC date, fixed expiry, revocation, approval
reference/signature, and independent verification remain outstanding. TGT is
not operationally cleared.

The repository convention requires a UTC issue date and fixed expiry (`issued` and `expires`), rather than authorization that lasts until explicitly revoked. The supplied owner labels do not yet provide a verifiable accountable identity/organizational role, and no UTC date, fixed expiry, approval reference/custody location, or rationale/evidence references have been supplied. TGT therefore remains `PENDING`.

### D11 — Migration 026 separate authorization

**Question:** Is migration 026 separately authorized for its exact artifact,
target, lock/recovery, provider validation, and execution window  
**Why:** Reporting schema is absent and governed by an independent contract;
baseline adoption cannot authorize it.  
**Options:** Approve a separately bounded execution gate; reject/defer; require
further contract evidence.  
**Evidence:** Original SHA-256
`791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4`, review
candidate records, authorization/target/lock contracts, recovery evidence.  
**Owner role:** Migration authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 026 only; it must not be merged into baseline approval.

## Prerequisite grouping and independent decisions

Before any schema-change request is considered, D01, D02, D03, D05, D09, and
D10 require resolution. D04, D06, D07, and D08 may be decided independently,
but their downstream migrations remain blocked until their own evidence and
approvals are complete. D11 remains an independent approval chain.

## Evidence references

Prepared evidence is in T62B.2â€“T62B.6 documents, especially the T62B.1 exact
definition matrix, T62B.3 baseline packet, T62B.4 register, and T62B.5 action
sheet. Those documents contain proposals and evidence requirements; none is an
owner approval.

## Current source-to-schema mapping for RBAC review

The current authorization reader requires these persisted facts before an API permission decision can be made:

| Source requirement | Current mapped object/columns | Approval or evidence still required |
|---|---|---|
| Active actor and organizational context | `PA_TICKETS_USER` (`N_USER_ID`, `N_DEPARTMENT_ID`, `N_TEAM_ID`, `N_ACTIVE`) | Confirm authoritative user/client relationship and historical validity. |
| Permission metadata | `PA_TICKETS_PERMISSION` (`S_PERMISSION_CODE`, `S_MODULE_CODE`, `S_ACTION_CODE`, `S_SCOPE_CLASS`, `S_SECURITY_CLASS`, `N_READ_ONLY`, `N_ACTIVE`) | Approve the configurable catalogue and its canonical codes; the approved target currently lacks `S_MODULE_CODE`. |
| Active memberships | `PA_TICKETS_USER_ROLE` and `PA_TICKETS_ROLE` with active/effective dates | Approve role identities, date backfill, revocation, and unknown-history behavior. |
| Effective grants | `PA_TICKETS_ROLE_PERMISSION` joined to `PA_TICKETS_PERMISSION` | Approve grant status, effective interval, and initial-grant semantics. |
| Resource scope | `PA_TICKETS_ROLE_SCOPE` (`S_SCOPE_TYPE`, organization/department/team/client/resource fields, validity) | Approve client FK/ownership and explicit-versus-derived scope records. |
| Delegation | `PA_TICKETS_ADMIN_HEAD_DELEGATION` | Approve explicit effective-dated Admin Head-to-Admin delegation and revocation lineage. |
| Audit actor | `PA_TICKETS_AUDIT` actor fields and migration 038 contract | Approve typed legacy/system actor handling, FK/nullability, and hash-chain preservation before migration 038. |
| Migration history | Migration 033 ledger contract | Approve an explicit unknown-history representation; never infer applied migrations from object shape. |

These mappings are source-derived requirements, not evidence that the target database contains a compatible schema or data set.

## Five-role policy semantics for approval

The source policy and reviewed RBAC documents identify `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and `SPECTATOR` as distinct built-in identities. Their intended semantics are:

- `USER`: ordinary ticket operations allowed by explicitly granted, scope-checked permissions.
- `ADMIN`: department-scoped administration and ticket operations; grants are constrained by the approved permission and separation-of-duties rules.
- `ADMIN_HEAD`: the Admin Head role may manage an Admin only through an explicit, effective-dated delegation record.
- `SUPER_ADMIN`: organization-wide authority remains permission-driven and subject to protected-role and approval controls; it is not a generic bypass.
- `SPECTATOR`: read-only access; any effective active Spectator membership denies mutation requests even if another role grants a write permission.

The security/RBAC owner must approve the exact permission assignments for each role separately from role-table creation. The superseded fixed 34-code catalog cannot be treated as the current source of truth. The reviewed configurable catalogue draft in `database/migrations/postgresql/031_configurable_rbac.sql` contains 36 permission definitions, including `ROLE_PERMISSION_ADMIN` and `ROLE_SCOPE_ADMIN`. `AUDIT_ACTIVITY` is a report identifier in `AuditorReportingService`; the report service authorizes the report with `AUDITOR_VIEW`, so `AUDIT_ACTIVITY` must not be represented as a separate permission without an owner-approved contract.

## Recommended migration order and recovery boundary

After D01-D03, D05, D09, and D10 are approved, the proposed order is:

1. Add/validate metadata and constraints without inventing historical facts.
2. Apply the owner-approved configurable RBAC schema/catalogue change.
3. Reconcile role, permission, membership, grant, scope, and delegation rows using approved effective-date and provenance rules.
4. Record the approved migration history representation without claiming unknown migrations were applied.
5. Validate persisted authorization decisions and revocation/concurrency.
6. Consider migrations 031-037 individually under their own preconditions.

Any failure must preserve the prior schema and authorization facts, or produce an explicitly recorded uncertain outcome requiring reconciliation. Migration 026 and migration 038 remain separate approval chains; neither is authorized by this pack. No option permits weakening fail-closed authorization or making the existing target appear compliant by adding ad hoc columns or rows.

## Source-aligned RBAC reconciliation and migration readiness

This section records source evidence for the current blocker and a proposed
execution shape. It is not an approval, executable migration, or claim that the
approved PostgreSQL target has been changed.

### Canonical permission catalogue and module mapping

The only reviewed source-controlled catalogue with the metadata required by
`DatabaseAuthorizationFactsReader` is the `VALUES` block in
`database/migrations/postgresql/031_configurable_rbac.sql` (the permission
definitions begin at the `DO $permissions$` block). It defines 36 codes. The
following matrix is transcribed from that block; it is a draft requiring D04,
D05, and D08 approval before any data operation:

| Module | Permission codes | Action/scope/security/read-only shape |
|---|---|---|
| APPROVALS | `APPROVAL_ADMIN`, `APPROVAL_PUBLISH`, `APPROVAL_READ` | MANAGE/DEPARTMENT/DEPARTMENT_ADMIN/false; PUBLISH/ORGANIZATION/DEPARTMENT_ADMIN/false; VIEW/NONE/READ_ONLY/true |
| AUDIT | `AUDIT_EXPORT`, `AUDITOR_VIEW` | EXPORT/RECORD/READ_ONLY/true; VIEW/RECORD/READ_ONLY/true |
| CATALOGUE | `CATALOGUE_PUBLISH`, `CATEGORY_ADMIN`, `CATEGORY_READ`, `REQUEST_TYPE_ADMIN`, `REQUEST_TYPE_READ`, `SERVICE_ADMIN`, `SERVICE_READ` | Publish/manage/view actions; organization or none scope as declared in 031; read entries are READ_ONLY/true |
| CLIENTS | `CLIENT_ADMIN` | MANAGE/DEPARTMENT/DEPARTMENT_ADMIN/false |
| COMMUNICATIONS | `COMMUNICATION_ADMIN`, `COMMUNICATION_PUBLISH`, `COMMUNICATION_READ`, `COMMUNICATION_RETIRE` | Manage/publish/view/retire; organization or none scope; read is READ_ONLY/true |
| CONFIGURATION | `CONFIG_ADMIN` | MANAGE/ORGANIZATION/SECURITY_GOVERNANCE/false |
| NOTIFICATIONS | `NOTIFICATION_ADMIN`, `NOTIFICATION_PUBLISH`, `NOTIFICATION_READ`, `NOTIFICATION_RETIRE` | Manage/publish/view/retire; organization or none scope; read is READ_ONLY/true |
| SECURITY | `ROLE_ADMIN`, `ROLE_PERMISSION_ADMIN`, `ROLE_SCOPE_ADMIN` | ASSIGN/DEPARTMENT/DEPARTMENT_ADMIN/false; CONFIGURE and SCOPE/ORGANIZATION/SECURITY_GOVERNANCE/false |
| SLA | `SLA_ADMIN`, `SLA_PUBLISH`, `SLA_READ`, `SLA_RETIRE` | Manage/publish/view/retire; organization or none scope; read is READ_ONLY/true |
| TICKETS | `TICKET_ADMIN`, `TICKET_READ`, `TICKET_UPDATE` | CONFIGURE/DEPARTMENT/DEPARTMENT_ADMIN/false; VIEW/RECORD/READ_ONLY/true; EDIT/RECORD/BUSINESS/false |
| USERS | `USER_ADMIN` | MANAGE/DEPARTMENT/DEPARTMENT_ADMIN/false |
| WORKFLOWS | `WORKFLOW_ADMIN`, `WORKFLOW_PUBLISH`, `WORKFLOW_READ` | Manage/publish/view; organization or none scope; read is READ_ONLY/true |

The application has an additional reporting catalogue in
`src/PA.Tickets.Infrastructure/Reporting/AuditorReportingService.cs`. Its
report identifiers include `AUDIT_ACTIVITY`, but every report definition uses
the permission `AUDITOR_VIEW`; this distinction is required to avoid inventing
an RBAC permission. The existing target's missing `S_MODULE_CODE` therefore
remains a real schema/data compatibility blocker, while the exact permission
row set and any report-specific additions remain owner decisions.

Although 031 declares the metadata columns nullable during its additive phase,
`DatabaseAuthorizationService` rejects null or blank module, action, scope, or
security metadata. Adding columns alone therefore cannot unblock authorization;
the approved catalogue must populate and validate every required field.

### Source-to-schema contract matrix

| Requirement | Source evidence | Required object/fields | Current conclusion | Blocking decision |
|---|---|---|---|---|
| Actor | `AdministrationRows.cs`; `DatabaseAuthorizationFactsReader.cs` | `PA_TICKETS_USER` active row, department/team IDs | Reader requires an active actor; no fallback actor is permitted | D05/D06 owner confirmation of identity lineage |
| Permission metadata | `AdministrationPermissionRow`; `DatabaseAuthorizationFactsReader` | Code, module, action, scope, security, read-only, active | `S_MODULE_CODE` and the other metadata are required; target lacks module metadata | D04/D05; approved 36-code catalogue and historical treatment |
| Membership | `AdministrationUserRoleRow`; reader join | User/role IDs, active, valid-from/to | Effective membership is date-bounded and fail-closed | D05 and D08 |
| Grant | `AdministrationRolePermissionRow`; reader filters | Active, `S_EFFECTIVE_STATUS='ACTIVE'`, non-null valid-from, interval | `UNKNOWN` or missing dates do not authorize | D05; no guessed backfill |
| Scope | `AdministrationUserRoleScopeRow`; `ScopeAuthorizationPolicy` | Membership, type, IDs/resources, validity | Scope rows are explicit in the reader; 031 has a draft backfill, 035 inserts none | D06/D07 |
| Delegation | `AdminHeadDelegationRow`; `AdminHeadDelegationPolicy` | Admin Head/Admin IDs, active interval | Delegation is a separate effective-dated relation | D07 |
| Audit actor | `AuditRow`; 038 blocked script | Actor ID/type/FK and chain semantics | Existing non-null actor contract cannot safely represent historical/system actors | D10; 038 remains intentionally blocked |
| Migration history | 033 ledger draft | Artifact hash, baseline, target fingerprint, status | Object shape cannot establish history; no applied rows may be fabricated | D09 |

### Role and grant decisions

The five role identities are `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and
`SPECTATOR` in 031 and 037. The 031 draft grant block proposes: all active
permissions for `SUPER_ADMIN`; `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`,
and `ROLE_ADMIN` for `ADMIN` and `ADMIN_HEAD`; `TICKET_READ` and `TICKET_UPDATE`
for `USER`; and every read-only permission for `SPECTATOR`. This is source
evidence of a proposal, not an approved grant matrix. D08 must approve or
reject those assignments, including whether Admin Head delegation is required
for each administration action. `RoleAuthorizationPolicy` and
`AdministrationService` remain the enforcement boundary and must continue to
deny incomplete metadata, unknown statuses, protected-role changes, and
unapproved sensitive grants.

### Proposed replacement migration sequence (non-executable)

No existing migration is changed or enabled by this packet. After the owner
decisions and a separately authorized target preflight, a replacement sequence
should be prepared in this order:

1. **Read-only preflight:** verify target identity, baseline artifact hashes,
   schema definition snapshot, unknown history, and operator scope. Abort on
   ambiguity, prohibited target, duplicate/case-variant identities, or missing
   evidence.
2. **Additive metadata:** add only the permission/grant metadata and indexes
   supported by the approved contract. Preserve IDs and rows; do not invent
   historical dates or active grants. This corresponds to the DDL intent in
   031/034, subject to a reviewed combined script and D05 rules.
3. **Catalogue reconciliation:** load the owner-approved catalogue from a
   source-controlled artifact, preserving existing IDs where codes match;
   reject unknown, duplicate, case-variant, or conflicting definitions. The
   36-row 031 draft is not automatically authoritative.
4. **Role/grant reconciliation:** apply only D08-approved role identities and
   grants. `UNKNOWN` effective history remains non-authorizing. No fixed 34-code
   replacement or fabricated historical grant is allowed.
5. **Scope/delegation:** create the 035 relations only after D06/D07. Do not
   backfill scope or delegation rows until derivation/provenance and client-key
   semantics are approved. 035's deliberate lack of client FKs remains a
   decision point.
6. **Ledger representation:** only after D09, record the explicitly approved
   baseline/history representation. Never mark 026, 028, or 029 applied from
   object shape alone; preserve the unknown state when evidence is absent.
7. **Proposal and built-in roles:** consider 036 and 037 separately. 036
   requires the approved proposal workflow; 037 requires its explicit session
   approval and inserts roles only. Neither grants permissions implicitly.
8. **Audit actor:** do not run 038. Prepare a future actor-compatible change
   only after D10 approves actor types, nullability/FKs, writer/reader
   compatibility, and hash-chain preservation.
9. **Post-validation:** read back definitions and authorization facts, verify
   fail-closed behavior for unknown history and revocation, capture evidence,
   and stop for reconciliation on any uncertain transaction outcome.

Each step needs an owner-approved artifact, transaction/recovery plan, and
explicit authorization. A rollback must not delete historical facts or
fabricate ledger entries; an uncertain outcome is held for independent
reconciliation. Migration 026 remains a separate authorization chain with its
own exact hash, target identity, lock/recovery, and execution approval.

### Readiness outcome and next action

Ready source work: the canonical source-to-schema matrix, 031 catalogue
transcription, role/grant proposal, and ordered non-executable sequence are now
available for owner review. Clarification remains required for D01-D10; no
owner approval is present. The next action is for database/release,
security/RBAC, authorization/data, and audit/compliance owners to record
explicit decisions in the existing packet. Only then may a separately scoped
schema-change request be drafted and reviewed. The API/Angular acceptance gate
remains blocked by missing permission metadata and unresolved persisted RBAC
facts.
