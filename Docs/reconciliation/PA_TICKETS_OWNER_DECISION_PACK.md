# PA_TICKETS Owner Decision Pack

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Purpose:** owner completion of the decisions required before a PostgreSQL
schema-change request can be considered.

This pack records decisions and evidence. On 28 October 2026, an owner-reviewed
copy was supplied at
`Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK_owner_reviewed.md`. It
records owner approval in principle of the policy directions D01-D10, attributed
to Mr. H (Genesis, Developer & DBA). This is owner-supplied direction, not
independently authenticated identity or a signed operation authorization.
Formal decisions remain `PENDING` wherever their required attribution,
technical evidence, exact grant matrix, or independent review is outstanding.
No database operation, migration, role/grant provisioning, provider test,
cleanup, or migration 026 is authorized by the owner-reviewed copy.

**Reconciliation note:** the owner-reviewed copy marks the D01 and D02 formal
`Decision` fields `APPROVED`, while also stating that the fields are not an
independently authenticated signature and that their technical/operational
gates remain. The canonical formal decisions remain `PENDING` until the
required attributable evidence and prerequisites are satisfied. The owner
policy selections are recorded as approved in principle below; they do not
establish D01 baseline compatibility or D02 operator grants.

## Owner-selected intended V1 policy

The owner-approved copy confirms the conservative recommendations below as
intended V1 policy direction. This supports policy/design work in principle;
technical validation and formal decision fields remain outstanding. D08's
exact role-to-permission grant matrix is not approved by a general policy
endorsement, and migration 031 remains a proposal.

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

**Owner-delegated policy selection (implementation guidance; not formal approval):**
canonical uppercase role and permission codes, explicit remediation of duplicate
or alias identities, and fail-closed handling of ambiguous authorization facts.
This direction does not authorize database operations, identity mutation, or
automatic adoption of a catalogue draft. The formal `Decision` remains
`PENDING` until the attribution fields above are completed by the authorized
owner.

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

**Owner-delegated policy selection (implementation guidance; not formal approval):**
preserve the five built-in system roles and use the FRD v0.7 §4 capability
matrix and permission ceilings as the intended role policy. Reconcile each
concrete permission-code grant against the canonical catalogue before
implementation. This direction does not approve every grant in migration 031,
authorize database operations, or permit role/grant provisioning. The formal
`Decision` remains `PENDING` until the attribution fields above are completed
by the authorized owner.

**Selected policy clarification for D08 (implementation guidance; not formal
approval):** preserve `USER`, `ADMIN`, `ADMIN_HEAD`, `SUPER_ADMIN`, and
`SPECTATOR`. USER may create and respond to own/assigned work, with no default
assignment authority. ADMIN operates within authorized team/department scope;
ADMIN_HEAD operates within effective delegation and approved group scope.
SUPER_ADMIN uses explicit, audited grants from the approved permission
catalogue. SPECTATOR remains read-only and receives no default export.
`TICKET_UPDATE` alone is insufficient to authorize assignment or approval;
assignment requires the FR-021 authority rules and approval requires designated
approver and separation-of-duties checks. Fixed-role core protections apply
only after the exact code mapping is approved. Missing or ambiguous
authorization facts fail closed. This clarification records selected policy
direction only: it does not approve migration 031's proposed grants, create
grants, or change the formal D08 status.

**Decision text prepared for authorized approval (formal status remains
`PENDING`):**

> Approve the five canonical built-in role identities `USER`, `ADMIN`,
> `ADMIN_HEAD`, `SUPER_ADMIN`, and `SPECTATOR`, and approve the role-to-permission
> mapping in the attached D08 grant matrix as the built-in default policy.
> USER's `TICKET_READ` and `TICKET_UPDATE` grants are limited to own/assigned
> work and creation/response actions; they confer no assignment authority, and
> `TICKET_UPDATE` alone does not authorize assignment or approval. ADMIN grants
> apply only within authorized team/department scope. ADMIN_HEAD grants apply
> only within effective delegation and approved group scope. SUPER_ADMIN
> receives explicit, audited grants for each approved catalogue permission.
> SPECTATOR is read-only and has no default export grant. Assignment requires
> the approved FR-021 rule and scope checks; approval requires designated
> approver and separation-of-duties checks. Fixed-role core grants may not be
> removed only after their exact permission-code mapping is approved. Unknown,
> missing, or ambiguous authorization facts fail closed. This approval does
> not authorize database changes, seed execution, or adoption of migration
> 031's proposed grants.

**Formal attribution required to record approval:** authorized Security/RBAC
approver identity; UTC decision date; decision scope and exact approved grant
matrix/version; approval reference or signature; rationale; and evidence
references. Until those fields are supplied, the formal D08 `Decision` remains
`PENDING`.

**S3-02 source reconciliation (2026-10-27; evidence, not formal D08 approval):**
The intended permission-code mapping used for source work is:

| Role | Proposed codes | Core/configuration treatment | Scope and source finding | D08 status |
|---|---|---|---|---|
| `USER` | Proposed `TICKET_READ`, `TICKET_UPDATE` | Intended fixed core, but not protected as mandatory yet | S3-04 source now enforces requester-or-assignee ticket reads/updates and pre-pagination search scope. Selected D08 guidance denies default assignment authority; `TICKET_UPDATE` alone does not authorize assignment or approval. Exact action scope and fixed-core mapping remain unapproved. | Pending |
| `ADMIN` | Proposed `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`, `ROLE_ADMIN` | Fixed core; source revocation guard implemented for these four codes | Ticket resources and administration targets use effective department/team scopes; assignment remains subject to FR-021 rules, and approval requires designation and separation-of-duties checks. | Proposed from FRD §4 and selected guidance; formal decision pending |
| `ADMIN_HEAD` | Proposed `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`, `ROLE_ADMIN`; further grants configurable within ceilings | Configurable, not fixed core | Effective delegated/team scope exists and has source tests. Assignment remains subject to FR-021 rules; approval requires designation and separation-of-duties checks. Exact administration ceilings remain unapproved. | Pending |
| `SUPER_ADMIN` | All 36 codes in the canonical catalogue table above: `APPROVAL_ADMIN`, `APPROVAL_PUBLISH`, `APPROVAL_READ`, `AUDIT_EXPORT`, `AUDITOR_VIEW`, `CATALOGUE_PUBLISH`, `CATEGORY_ADMIN`, `CATEGORY_READ`, `CLIENT_ADMIN`, `COMMUNICATION_ADMIN`, `COMMUNICATION_PUBLISH`, `COMMUNICATION_READ`, `COMMUNICATION_RETIRE`, `CONFIG_ADMIN`, `NOTIFICATION_ADMIN`, `NOTIFICATION_PUBLISH`, `NOTIFICATION_READ`, `NOTIFICATION_RETIRE`, `REQUEST_TYPE_ADMIN`, `REQUEST_TYPE_READ`, `ROLE_ADMIN`, `ROLE_PERMISSION_ADMIN`, `ROLE_SCOPE_ADMIN`, `SERVICE_ADMIN`, `SERVICE_READ`, `SLA_ADMIN`, `SLA_PUBLISH`, `SLA_READ`, `SLA_RETIRE`, `TICKET_ADMIN`, `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`, `WORKFLOW_ADMIN`, `WORKFLOW_PUBLISH`, `WORKFLOW_READ` | Intended fixed core only for explicitly approved catalogue codes; not encoded as “all active” | Source uses explicit permission and organization-scope checks; canonical catalogue is not formally approved. No dynamic revocation guard can infer approval from active rows. | Pending catalogue and grant approval |
| `SPECTATOR` | `APPROVAL_READ`, `AUDITOR_VIEW`, `CATEGORY_READ`, `COMMUNICATION_READ`, `NOTIFICATION_READ`, `REQUEST_TYPE_READ`, `SERVICE_READ`, `SLA_READ`, `TICKET_READ`, `WORKFLOW_READ`; excludes `AUDIT_EXPORT` | Visibility configurable; mutation deny invariant; no default export | Runtime denies mutations and rejects `AUDIT_EXPORT` at grant validation and authorization. Migration 031's all-read-only proposal includes export and conflicts with FRD §4. | Pending; export excluded from proposed default |

The 031 catalogue has 36 proposed codes; this is not an approved seed matrix.
Existing mutation call sites reuse `TICKET_UPDATE` for ticket response,
lifecycle, assignment, approval-related and workflow operations; ticket
creation has no direct `TICKET_UPDATE` check in `TicketLifecycleService`.
Approval configuration uses `APPROVAL_READ`, `APPROVAL_ADMIN`, and
`APPROVAL_PUBLISH`. The selected guidance requires FR-021 assignment authority
and designated-approver/separation-of-duties checks in addition to
`TICKET_UPDATE`; their complete role mapping is not approved here. USER
requester/assignee scope is now source-enforced under S3-04. No chat permission
code or permission check exists. `AUDITOR_VIEW` protects auditor reports;
`AUDIT_EXPORT` has no source authorization call site. The precise
role-to-code/action mapping, fixed-role core set, complete approved catalogue,
and SUPER_ADMIN catalogue-retention semantics remain open. The formal D08
`Decision` remains `PENDING` pending owner attribution and approval.

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

The owner-reviewed copy selects `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex` as the intended target and prefers bounded localhost connectivity. This is owner direction, not fresh independent target or peer verification and not an operation authorization. The operation-specific TGT record still needs authenticated target identity, host/peer/TLS expectations, scope, fixed expiry, revocation, approval reference, and independent review. Until those are recorded, provider operations remain blocked. This reconciliation does not change any formal decision status.
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
sheet. Those documents contain proposals and evidence requirements. The
owner-reviewed copy referenced above separately records policy approval in
principle; it does not supply technical acceptance or operation authorization.

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
transcription, role/grant proposal, and ordered non-executable sequence are
available. The owner-reviewed copy now records D01-D10 policy direction as
approved in principle. Formal decision evidence, exact D08 grant mapping,
technical prerequisites, and operation authorization remain outstanding. No
schema-change request is authorized. The API/Angular acceptance gate remains
blocked by missing permission metadata and unresolved persisted RBAC facts.

### S5-01 E2E operation authorization packet — owner completion required

**Status:** `PREPARED / UNSIGNED`; this section is a request for bounded owner
decisions, not authorization to connect, inspect, execute SQL, apply migrations,
run provider tests, write data, or clean up data. D01 and S5-01 remain `PENDING`
and `BLOCKED`, respectively. Migration history remains `UNKNOWN`.

#### Owner-declared E2E development environment (intent only)

The owner declares a dedicated local PostgreSQL development/E2E target at
`localhost:5432`, database `pa_tickets_codex_e2e`, role `pa_tickets_codex`, with
the password supplied through `PA_TICKETS_E2E_DB_PASSWORD`. The owner states no
business data is intentionally stored there and requests PostgreSQL-backed
development/testing. This records environment classification and intent only;
it does not independently establish target identity, database emptiness,
operator identity, least-privilege grants, signed approval, or operation
authorization. The credential value was not read or recorded.

| Operation | Authorization status | Exact remaining gate |
|---|---|---|
| S5-03 read-only fingerprint | **NOT AUTHORIZED** | Separate owner-signed S5-01 packet naming the exact read-only operation/artifact/hash; owner-provisioned CNG signing/revocation certificates and pinned ACL-protected policy; fresh signed peer/authorization/revocation evidence; owner-account binding; least-privilege evidence; validity, recovery and custody. SLICES v4 §S5-01 now permits owner-controlled evidence only for this exact local target; no provider operation is authorized. |
| S5-10 schema application | **NOT AUTHORIZED** | Separate signed schema-application scope naming each final artifact/hash, expected effects and order; D01 adoption and prerequisite decisions; operator/target/grant evidence; validity/revocation; recovery and evidence custody. |
| Provider tests and cleanup | **NOT AUTHORIZED** | Separate signed test-write scope naming exact tests, generated identifiers, allowed writes and cleanup boundaries, plus operator/target/grant evidence, validity/revocation, recovery and evidence custody. |

#### Proposed reusable E2E authorization profile (PENDING governance review)

**Purpose:** enable bounded development on the owner-declared dedicated E2E
instance without converting environment intent into standing database access.
This earlier reusable-profile proposal remains pending and does not authorize
operations. For the local S5-03 path only, its independent-verification model
is superseded by the owner-adopted SOP v3.1 amendment recorded below. The
profile's broader schema-write, provider-test and cleanup scopes remain
unapproved and are not covered by this amendment.

**Exact target constraint:** `localhost:5432`, database
`pa_tickets_codex_e2e`, role `pa_tickets_codex`; credential source is
`PA_TICKETS_E2E_DB_PASSWORD`. No hostname, port, database, role, credential
source, wildcard, or fallback may be substituted. The environment declaration
does not prove emptiness, schema provenance, identity, or least privilege.

| Separate scope | Proposed allowlist boundary | Required evidence for each issued authorization |
|---|---|---|
| A. Catalog inspection | Read-only catalog/fingerprint operation only; exact query artifact and SHA-256; no DDL/DML. | Exact target and operator identity; read-only role grants; peer/TLS verification; signed approval; validity/expiry and revocation state; expected catalog comparison; sanitized evidence custodian. |
| B. Schema/migration | Only individually listed final artifacts in exact order with hashes and expected effects; no implicit data seeding, grant changes, sequence changes, or cleanup. | D01 and prerequisite decisions; exact target/operator/grants; approved artifact manifest; signed operation scope; preconditions; recovery/recreation plan; validity/expiry and revocation; evidence retention. |
| C. Provider-test writes | Only named test suites and operations against generated, uniquely identifiable test records. | Exact test names/build identity, write boundary, target/operator/grants, signed scope, validity/expiry/revocation, failure handling, evidence custody. |
| D. Test-data cleanup | Only enumerated identifiers created by the authorized test run; no broad truncate, drop, reset, or cleanup outside that set. | Exact created identifiers, cleanup statements/artifact and hash, scope, operator, independent review, signed approval, recovery and evidence retention. |

**Common controls proposed for every scope:** default deny; one operation per
authorization; no `allowAll`, indefinite validity, wildcard targets, ambient
connection-string/environment fallback, or credentials in arguments/logs. Bind
execution to the exact host, port, database, role, operation and artifact bytes.
Require independently verified operator and approver identity, least-privilege
grants, a fixed UTC validity window, a current revocation check, target/peer
verification, and sanitized append-only evidence. On any mismatch or uncertain
outcome, stop, do not retry, classify the result `UNKNOWN`, and obtain separate
authorization for read-only reconciliation. Database recreation/restore is a
separate destructive operation and is excluded unless independently approved.

**Explicit exclusions:** production/shared databases; `pa_tickets_test`,
`pa_tickets_h_verify`, `pa_tickets_test_user`; migration 026; superuser access;
`DROP DATABASE`, broad schema drops/truncates, arbitrary SQL, unlisted artifacts,
provisioning, role/grant changes, and operations outside the four scopes.
Anything excluded requires a separate review and operation-specific approval.

**Trust implementation status after SOP v3.1 adoption:** the owner-controlled
trust model and local verification controls are now approved for this exact
environment. The existing S5-03 runner still lacks cryptographic signature
verification, Windows listener/process collection, pinned-key validation,
revocation-key verification, local operator-account binding, least-privilege
evidence checks, and the adopted audit-custody controls. Until those controls
are implemented and verified offline, supplied profile claims remain
untrusted, the runner remains fail-closed, and Execute remains disabled.

**Offline implementation evidence (2026-10-27):**
`tests/scripts/invoke-postgresql-foundational-fingerprint.ps1` now validates
explicit target/profile structure and the pinned S5-03 artifact. It marks
repository/user-supplied identity, signature, and revocation references as
unverified and still has no database-client execution path. The focused command
`dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~PostgreSqlFoundationalPreflightScriptTests`
passed 22 tests. This is source-level/offline evidence only; no provider result
or authorization was established.
**Prior proposed change path:** SOP v3 does not specify a complete amendment
workflow. This pack previously proposed an attributable project-owner
decision, database/security review and independent reviewer. The owner has
since adopted the exact local-E2E amendment text; its adoption record and
consistency/security review are recorded below. The independent-review
requirement is replaced only for this exact local environment. No general
review waiver is implied.

#### Sole-developer local E2E amendment — owner direction and status

**Owner direction received 2026-10-28:** Mr. H, Genesis, Developer & DBA,
states that he is the sole PA_TICKETS developer and DBA, approves the direction
to use proportionate owner-controlled verification for the dedicated local
PostgreSQL E2E environment, and authorizes preparation of a narrowly scoped
Genesis SOP amendment replacing independent review for that environment only.
The direction excludes production, shared databases and unrestricted
operations. It does not authorize a PostgreSQL connection or any schema/data
operation. Identity is owner-supplied and has not been independently
authenticated; no signature, approval reference, or effective timestamp is
inferred from the conversational direction.

**Draft:** `Docs/Genesis/SOP_v3.1_LOCAL_E2E_AMENDMENT_PROPOSED.md` proposes
controls for the exact target `localhost:5432 /
pa_tickets_codex_e2e / pa_tickets_codex`, Windows OS listener/process/service
verification, a locally pinned owner key, signed one-operation authorization,
time limits, revocation, least privilege, fail-closed behavior and sanitized
evidence. Its current raw-byte SHA-256 is
`B78980670B1E0E8E79C333A36D7B93F46633C02B2172088C8B33469BED28A18E`. It
explicitly records that owner-controlled evidence is not independent
attestation and cannot resist a malicious local administrator. The hash
identifies this proposal revision; it does not signify adoption.

**Final owner adoption decision:** On 2026-10-28, Mr. H, Genesis, Developer &
DBA and sole PA_TICKETS administrator, approved adoption of the exact reviewed
file bytes identified above and the narrowly scoped owner-controlled
verification exception for the dedicated local PostgreSQL E2E environment.
The approval reference is the owner message headed **“Genesis — SOP v3.1 Final
Adoption Decision”** in the active conversation. No cryptographic signature
was supplied or verified; the owner identity remains conversationally
attributed, not independently authenticated.

**Adoption record/effective timestamp:** `2026-10-28T07:19:32Z` (UTC), the
time this exact-text adoption was entered in this canonical pack. The approved
amendment file is kept byte-for-byte at the approved hash. Its `PROPOSED — not
adopted or effective` header and intentionally incomplete checklist describe
its state before this separate adoption record; this canonical record records
the owner's subsequent adoption and effective time.

**Status:** `OWNER-ADOPTED — EFFECTIVE WITHIN THE DEFINED LOCAL E2E SCOPE`.
The exception applies only to `localhost:5432 / pa_tickets_codex_e2e /
pa_tickets_codex`. It does not authorize S5-03 or any database operation.

**Consistency/security review recorded with adoption:**

- SOP v3 §§7.1 and 7.3 protections remain intact: protected databases and
  credentials stay prohibited; migrations 026 and 038 remain outside this
  amendment; no migration history is inferred. SOP §7.2 still requires a
  separate operation-specific authorization. SOP §8's evidence integrity and
  truthful-status rules remain applicable.
- FRD DD-07's fixed local target is preserved. SLICES v4 S5-01 has now been
  narrowly reconciled: the owner-controlled Windows account and OS peer
  evidence model applies only to `localhost:5432 / pa_tickets_codex_e2e /
  pa_tickets_codex`; all other environments retain independent identity
  verification. This wording change does not satisfy the signed packet or
  technical controls by itself.
- Security review of the adopted design: exact IPv6 loopback and process
  binding, artifact/scope binding, owner-controlled Windows signing and
  separate recovery/revocation keys, short validity, least-privilege ceilings,
  fail-closed checks, and sanitized evidence are specified. The design
  explicitly does not resist a malicious local administrator or establish
  independent host integrity.
- The proposal's own adoption checklist is now completed for exact-text owner
  adoption and effective time by this record. Consistency review identifies
  the S5-01 reconciliation above as an outstanding implementation/governance
  condition. No independent reviewer or signature is invented.

Adoption does not authorize S5-03. A separate S5-01 operation authorization
and verified technical controls are still required. Execute remains disabled;
no PostgreSQL operation is authorized by this amendment.

**Adoption record supersedes the earlier approval-required checklist for this
amendment only:** the project owner has adopted the exact version/hash above
with the approval reference and effective timestamp recorded above. No
independent reviewer is required by the adopted exception within its exact
local E2E scope. Owner identity remains conversationally attributed and is not
cryptographically authenticated. Database privileges, local key/verifier and
revocation controls, operator-account evidence, and operation-specific S5-01
authorization remain outstanding. This exception does not approve other
operation scopes or remove their separate authorization requirements.

**Offline implementation status (2026-10-28):** SLICES v4 S5-01 wording now
allows the adopted owner-controlled evidence path for the exact local target
and preserves independent identity requirements elsewhere. The offline runner
has a separate `VerifyLocal` mode; `Execute` remains hard-disabled. The new
`tests/scripts/LocalE2eTrust.psm1` verifies pinned RSA-PSS signed envelopes
against public certificates in the Windows LocalMachine certificate store,
ACL-protected local trust and evidence paths, current Windows owner SID, exact operation/target/artifact and
30-minute authorization bounds, separate signed revocation with version/hash
binding and five-minute freshness, two-minute peer freshness, Windows IPv6
listener/process ancestry, service account/state, executable SHA-256 and
Authenticode thumbprint, data/config paths, and the fingerprint artifact's
exact relation allowlist. It records sanitized allow/deny attempts when the
owner trust policy and custody directory are available. Denials before the
policy is trusted cannot be safely written to that custody location.

The offline tests use ephemeral test-only RSA certificates, never installed
or treated as production trust. They exercise signature success/tampering,
wrong pins, expired/revoked/mismatched authorization claims, stale or revoked
revocation claims, and synthetic process/service/endpoint/artifact mismatches.
These tests do not inspect the actual Windows listener, verify a live
Authenticode signer, prove PostgreSQL grants, establish owner identity, or
prove that the pinned signing certificates were generated by CNG with
non-exportable owner-restricted private keys. The verifier uses public keys
only; it does not open the separately controlled recovery key's private key.
That key-provider, non-exportability, private-key ACL, and recovery-key custody
evidence must be established during explicit owner provisioning.
Least-privilege facts are currently represented as owner-signed evidence whose
hash and shape are checked; database privileges have not been measured live.
No local CNG keys, policy, revocation source, peer evidence, privilege evidence
or custody directory have been provisioned. The operation-specific S5-01
authorization remains absent. No PostgreSQL connection, SQL, migration,
provisioning, seeding or cleanup occurred. S5-01 and S5-03 remain BLOCKED.

**Simplified S5-03 implementation update (2026-10-28; offline only):** The
adopted one-operation policy body was rehashed as
`17DCC7EBF56F696E11A4C86298B0A431BB46AB1D577A7ED4A35CFC5030448866`; the
pinned SQL bytes were rehashed as
`2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8`.
`tests/PA.Tickets.Domain.Tests/LocalE2eS503ReadOnlyInspection.cs` now contains
a bounded Npgsql inspection routine that validates the fixed target, policy
and artifact hashes, a maximum 30-minute operation window, a fresh active
revocation assertion, current Windows SID, and fresh least-privilege evidence
before loading the test-only local configuration or opening a connection. It
sets the transaction read-only, checks database/role/address/port/read-only
state on that same session before issuing the pinned fingerprint, then rolls
back and writes sanitized attempt/result records when an existing custody
directory is configured. There is no arbitrary-SQL argument. The routine has
not been wired to the existing PowerShell runner; its Execute mode remains
disabled.

This is offline guard implementation, not evidence that the inputs are
authentic or that the live role is least-privileged. Authorization and
privilege records are typed evidence inputs; the code validates their fields
and freshness but does not independently establish the truth of owner-entered
assertions. The separate operation authorization, owner-collected effective
privilege report, current revocation status, recovery instruction, and
approved evidence-custody location/retention are still absent. The file sink
requires an existing non-reparse directory but does not verify its ACL. No
connection or SQL was executed. S5-01 and S5-03 remain BLOCKED; do not enable
or invoke Execute mode until those operation-specific evidence gaps are
resolved and the guarded path receives a reviewed invocation interface.

**Authorization provenance and privilege bootstrap (required before guard
enablement):** Caller-created C# records are not authorization. `ExecuteAsync`
now returns `EVIDENCE_PROVENANCE_NOT_CONFIGURED` unconditionally before
configuration loading or connection creation; synthetic, correctly shaped
authorization or privilege objects cannot enable it. The reviewable source
must be an owner-completed operation-specific entry in this canonical pack,
with an exact local evidence-custody manifest binding the authorization and
privilege-report byte hashes. The entry must state operation and expected
read-only catalog effect; `::1:5432`, database, role, SQL artifact/hash and
policy hash; owner/operator identity and Windows SID; UTC start and expiry no
longer than 30 minutes; revocation authority/current status; custody location
and retention; and stop/no-retry recovery handling. The local manifest and
custody directory must be ACL-protected for the owner account, with actual
Windows SID and ACL evidence retained. This is owner-controlled attribution,
not a cryptographic signature or independent identity proof. Until a concrete
loader validates this exact source and custody, execution remains disabled.

Effective PostgreSQL privileges cannot be established from configuration,
password authentication, or an owner-authored assertion. They require a
read-only privilege-only probe whose pinned query reports role attributes
(superuser, role/database creation, replication and RLS bypass), inherited
membership closure, database/schema privileges, and effective table/sequence
write privileges for the target role. Its raw sanitized output and query hash
must be retained and owner-reviewed. The existing approved exception covers
only the fingerprint artifact; it does not authorize this separate probe.
The non-circular bootstrap is therefore a separately authorized, bounded
read-only privilege-verification operation before S5-03, under the existing
SOP v3.1 assurance path or a separately owner-adopted local exception naming
the privilege-probe artifact/hash. If neither authorization path is available,
no first connection is permitted and the guard stays disabled. The privilege
report then becomes a prerequisite for the separately authorized fingerprint;
it is not inferred from the fingerprint or from signed/typed evidence alone.

### Separate local privilege-preflight operation

**Policy status: OWNER-ADOPTED. Execution status: NOT AUTHORIZED / NOT EXECUTED.** This is a
separate read-only operation under the same narrowly scoped connection-based
assurance exception. It does not expand the S5-03 fingerprint authorization,
authorize that fingerprint, or permit writes. The initially adopted SQL bytes
and hash below are retained as historical approval evidence; the approved
alias-only revision is recorded immediately after the initial adoption entry.

- **Artifact:**
  `tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql`
- **SHA-256:** `C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`
- **Target:** IPv6 `::1:5432`, database `pa_tickets_codex_e2e`, role
  `pa_tickets_codex`; credential source remains `.env.test.local` key
  `PA_TICKETS_E2E_DB_PASSWORD`.
- **Expected effect:** report session identity, role flags, membership paths
  and effective database/schema/relation/column/sequence privileges. No data
  rows are selected. The artifact starts a read-only transaction, checks the
  exact target before privilege catalog queries, and ends with `ROLLBACK`.
  Its SQL is static and contains no dynamic execution, DDL/DML, grant changes,
  or sequence calls.
- **Owner evidence:** retain the complete sanitized output and exact artifact
  hash in the designated custody location. Review the returned role flags,
  every effective membership, database/schema CREATE, table and column write,
  truncate, and sequence UPDATE results. Any elevated or ambiguous capability
  is a stop condition, not a basis for privilege changes.
- **Operation authorization:** before running, record owner/operator identity
  and Windows SID, operation name and expected read-only effect, exact target
  and artifact hash, UTC start and fixed expiry (maximum 30 minutes), current
  revocation status, evidence-custody location/retention, and stop/no-retry
  recovery rule. This is a separate one-operation authorization. No signature
  or independent identity check is inferred; the owner-controlled local
  exception and its residual Windows-process identity risk must be explicitly
  adopted for this added operation.
- **Boundary:** no S5-03 fingerprint, migration, write, seed, role/grant
  change, provider test, cleanup, or follow-on connection is authorized by
  this proposal. The resulting report is evidence only and does not grant
  execution permission.

**Exact owner decision text proposed for adoption:**

> I approve the separate, one-time, read-only PostgreSQL privilege-preflight
> operation using
> `tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql`,
> SHA-256 `C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`,
> only against IPv6 `::1:5432`, database `pa_tickets_codex_e2e`, role
> `pa_tickets_codex`, using `.env.test.local` / `PA_TICKETS_E2E_DB_PASSWORD`.
> The operation may execute only this artifact in a read-only transaction,
> with same-connection target assertions before privilege catalog inspection,
> and must roll back. It may report role attributes, membership paths, and
> effective database, schema, relation, column, and sequence privileges. It
> authorizes no other SQL or database operation. I accept for this local
> development operation the same connection-based assurance limitation
> adopted for the single S5-03 fingerprint; the PostgreSQL Windows process
> and executable are not independently authenticated. Execution requires a
> separate owner-attributable authorization naming the operator/SID, UTC
> validity window of at most 30 minutes, current revocation state, evidence
> custody and retention, and stop/no-retry recovery handling. Missing or
> ambiguous evidence means do not connect. This approval does not authorize
> S5-03, writes, migrations, seeding, provisioning, role/grant changes,
> provider tests, or cleanup.

Initial policy adoption is recorded below. The remaining blank fields are solely for
the separate operation-specific authorization; until completed and verified,
the privilege query must not be run.

**Policy adoption record (2026-10-28):** Mr. H, Genesis Developer & DBA and
sole PA_TICKETS administrator, explicitly approved adoption of this exact
privilege-preflight policy and artifact in the current conversation. Approval
reference: the owner's message beginning “Genesis — Local E2E
Privilege-Preflight Policy Adoption” and the explicit confirmation “i
approve” in the same conversation. The SQL artifact hash was independently
recomputed from repository bytes as
`C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`, matching
the approved value. The approval message's precise timestamp is unavailable.
This repository record was entered at `2026-10-28T10:55:54Z`; that is the
record-entry time only and is not represented as the owner's approval or
effective timestamp. The policy permits only the named read-only preflight at
the exact endpoint/database/role above. It does not satisfy operation-specific
authorization, provide actual privilege results, or authorize S5-03.

**Owner-approved SQL revision (2026-10-28T12:13:11Z; exact artifact verified):**
Mr. H explicitly approved replacing the privilege-preflight artifact pin with
the alias-only revision in the current conversation under the heading
“PA_TICKETS — Privilege-Preflight SQL Revision Approval.” The raw-byte SHA-256
of `tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql`
was recomputed as
`0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034`. A
byte-level Git diff against the previously adopted artifact confirms the only
SQL change is renaming the membership-query alias `current_role` to
`member_candidate` in its JOIN and corresponding WHERE reference. The former
hash `C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`
remains the historical artifact hash for earlier attempts and approvals; it is
not the current execution pin. This revision changes no query scope or
read-only controls and grants no additional operation authority. Its owner
message timestamp is not independently available; `2026-10-28T12:13:11Z` is
the repository record-entry time only.

**Bounded invocation preparation (not executed):** The eventual invocation
must use the existing Development `ConnectionStrings:TicketsPostgres`
configuration and the existing explicit test-only `.env.test.local` loader
for `PA_TICKETS_E2E_DB_PASSWORD`; it must not read, echo, or persist the
completed connection string. Before opening a connection, the operation
authorization, fixed target/artifact hash, validity window, current revocation
status, and existing sanitized evidence-custody destination must validate.
On that one Npgsql connection it must begin a transaction, set it READ ONLY,
verify `current_database()`, `current_user`, `inet_server_addr() = '::1'`,
`inet_server_port() = 5432`, and `transaction_read_only = 'on'`, then execute
only the pinned privilege-preflight SQL and roll back. Any mismatch stops
before catalog inspection. No implementation currently binds a genuine
operation-authorization record to this distinct preflight, and custody is not
configured; therefore this is an invocation contract only, not an executable
or authorized command. Do not reuse the S5-03 fingerprint authorization.

**Operation-specific authorization — owner completion required:**

- Operation: `LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT` (single invocation).
- Target: `[::1]:5432`, database `pa_tickets_codex_e2e`, role
  `pa_tickets_codex`.
- Artifact and SHA-256: the path and hash stated above.
- Credential source: `.env.test.local` / `PA_TICKETS_E2E_DB_PASSWORD` (secret
  value excluded from the record).
- Expected effect: read-only catalog privilege evidence; transaction rollback;
  no application rows, writes, DDL, grants, migrations, seeds, tests, or cleanup.
- Owner identity/role and attributable approval reference: ______.
- Operator identity and Windows SID: ______.
- Valid-from UTC / fixed expiry UTC (maximum 30 minutes): ______ / ______.
- Current revocation check, authority, time, and evidence reference: ______.
- Evidence custody path, ACL verification, custodian, and retention: ______.
- Recovery/uncertain outcome: stop, do not retry, classify `UNKNOWN`; separately
  authorize read-only reconciliation: ______.

Until these fields are completed with verifiable evidence and the bounded
invocation has a provenance-checked authorization/custody source, no database
connection or SQL is permitted. Adoption is not operational authorization.

**Execution-readiness check (2026-10-28; offline only):** The raw-byte
SHA-256 of the approved privilege-preflight SQL was recomputed as
`C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`; it
matches both the adopted value and this record. A read-only filesystem check
found the expected `%ProgramData%\Genesis\PA_TICKETS\LocalE2E\trust-policy.json`,
`current-revocation.json`, `Evidence` directory, and proposed
`privilege-preflight-authorization.json` absent. Therefore ACLs, owner SID,
revocation freshness, operation authorization, and custody cannot be verified.
No provenance-checked privilege-preflight loader or database invocation is
enabled; caller-created objects and repository text alone remain insufficient
to open a connection. Focused offline tests passed 34/34, covering the SQL
artifact, existing fail-closed S5-03 guard, and local configuration. No real
`.env.test.local` was read, and no database connection or SQL execution
occurred. S5-01/S5-03 remain blocked.

**Owner-session identity and custody precheck (2026-10-28; read-only):** The
Windows session running this preparation reported account
`FIRSTWATER\Hakim.Basha`, SID `S-1-5-21-398012740-3661577184-344811926-4198`.
The owner has explicitly confirmed in the current conversation that this
account/SID belongs to Mr.H. Record this as **OWNER-ATTESTED / CONFIRMED BY
OWNER**; it is not independent identity verification or a cryptographic
signature. The fixed
`%ProgramData%\Genesis\PA_TICKETS\LocalE2E` root and its `trust-policy.json`,
`current-revocation.json`, `Evidence` directory, and
`privilege-preflight-authorization.json` were absent. Consequently no directory
ownership, inherited ACL, effective access, or unexpected-writer review could
be made. No filesystem or ACL changes were performed. The existing owner
provisioning runbook remains the explicit owner-executed creation procedure;
the authorization record must be created only after that custody setup and
ACL review, and must be separately approved before any invocation.

The preparation process was not elevated. The fixed ProgramData directory
creation and ACL commands therefore remain an owner-run elevated step. No
trust, authorization, revocation, or evidence file was fabricated. The
existing fingerprint authorization remains separate from the privilege
preflight authorization.

**Follow-up custody verification (2026-10-28; read-only):** For owner Mr.H,
the current Windows SID again matched the owner-attested value
`S-1-5-21-398012740-3661577184-344811926-4198`; the session was not elevated.
The fixed `%ProgramData%\Genesis` parent and all descendants for this setup
were absent: `trust-policy.json`, `current-revocation.json`, `Evidence`,
`privilege-preflight-authorization.json`, and `fingerprint-authorization.json`.
No ACLs exist to inspect at these paths, so inheritance, ownership, effective
write access, and unexpected writers remain unverified. No setup results were
present to review, and no filesystem changes were made. Phase 1 remains
**BLOCKED** pending owner execution of the elevated setup in the existing
provisioning runbook, followed by a fresh ACL inspection and placement of the
approved trust/revocation and operation-specific authorization records.

Denied or uncertain attempts can be retained only after the owner-approved
custody directory and ACL are verified. At present, that custody source is not
configured, so the guard does not claim audit recording for an attempted
execution; it fails before opening a connection.

The owner approval reference for policy adoption is the current-conversation
message “I APPROVE!” dated 2026-10-28. The exact message timestamp is not
available in the conversation metadata; no precise effective UTC time is
claimed. The record's edit/recording time is not substituted for the owner's
approval time.

Focused offline validation: `LocalE2eS503ReadOnlyInspectionTests` passed 17/17;
`LocalE2EPostgresConfigurationTests` passed 12/12; `git diff --check` passed.
No `.env.test.local` was read by these tests.
#### Target and identity

| Field | Prepared value | Owner evidence still required |
|---|---|---|
| Host / port | `localhost:5432` (owner-identified target) | Resolve and approve the actual endpoint/peer identity, address family, and hostname/peer/TLS policy for this operation. A prior owner-supplied report states `::1` and SSL off; it is not independently verified here. |
| Database / role | `pa_tickets_codex_e2e` / `pa_tickets_codex` | For this exact local exception, verify the live Windows listener/process/service against the owner-pinned policy and bind a fresh signed authorization to this database and role. Separately provide owner-signed least-privilege evidence; the verifier does not measure database grants offline. |
| Accountable approver / operator | Owner-supplied: `Mr. H`, Genesis, Developer & DBA; identity is not independently verified. | Verified identity and evidence reference; UTC approval timestamp; signed/attributable approval reference; operation validity and expiry; revocation authority and method. This declaration does not authorize S5-03. |
| Independent verifier / approvers | Not applicable to this exact local E2E scope under the owner-adopted SOP v3.1 exception; no individual is represented as an independent reviewer. | Owner-controlled verification must be implemented and evidenced; separate operation-specific authorization remains required. Other environments/scopes are not covered by this exception. |

#### Separated operation scopes

No scope below is authorized by this packet until its own exact artifact,
identity, privilege, time, recovery, and evidence fields are completed and
signed. Approval of one scope does not authorize another.

| Scope | Proposed boundary | Current exact-artifact / readiness evidence |
|---|---|---|
| **A — D01 baseline adoption** | Decide whether the immutable historical artifact or a separately reviewed replacement is the authoritative baseline. No database operation is included. | Historical `tests/scripts/postgresql-foundational-ddl.sql` at commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a`, SHA-256 `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19`, is the current proposal. The current expanded source file hashes to `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`; it is not byte-identical and has not been shown semantically equivalent. D01 remains `PENDING`; T62B.1 expected-versus-observed definitions, normalized differences, source dependencies, sequence/ownership and recovery evidence are prerequisites to adoption. |
| **B — S5-03 read-only catalog fingerprint** | One explicitly named, read-only catalog operation against the approved target; no writes. | The historical proposal `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_e2e_verify.sql` remains unchanged at SHA-256 `CEE95FC8BC178D79DC6E1A36020CC42D9E5DA31E1FA5FF649EF7B1E744B7E3B9`; it asserts `pa_tickets_e2e_verify` / `postgres` and is not usable for the owner-identified target. A static successor candidate now exists at `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql`, SHA-256 `2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8`; it asserts `pa_tickets_codex_e2e` / `pa_tickets_codex`, port 5432, and loopback server address, and remains unexecuted and unauthorized. The SQL server cannot prove the client used the literal hostname `localhost`; independent host/peer/TLS policy and invocation evidence are still required. `Docs/PA_TICKETS_POSTGRESQL_READ_ONLY_VALIDATION_AUTHORIZATION.md` remains `TARGET CONFLICT` and records baseline hash `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`, neither the historical D01 candidate (`4F4EC…`) nor current source hash (`21D7F…`). The exact successor bytes, expected comparison artifact, current baseline decision, identity evidence, and evidence custodian must be approved before S5-03. No fingerprint is authorized here. |
| **C - S5-10 schema application** | Only exact reviewed DDL/migration artifacts in an owner-approved order, with pre/post checks; no seed or grant data unless separately approved. | See the proposed source-level manifest below. Every candidate remains unapproved; review drafts with unconditional stop guards are not executable. Owners must approve the final artifact set, order, expected effects, and operation separately. |
| **D — provider-test writes and cleanup** | No provider tests, generated writes, or cleanup are included in A–C. | S5-05 requires a separate list of exact test names, unique generated identifiers, write scope, and cleanup procedure. None is authorized or named by this packet. |

#### Proposed S5-10 source-level artifact inventory (not execution authorization)

Owner-directed technical selection for this runner is **Option A**: preserve
the immutable historical baseline as the reconciliation reference and treat
the expanded baseline and S5-08 extracts as comparison evidence, not automatic
executable additions. This technical selection does not formally adopt the
baseline; D01 remains pending. The
order below is a proposed review order, not an approved migration plan. Hashes
identify bytes and do not prove deployment, compatibility, or migration history.

| Proposed order | Artifact and SHA-256 | Purpose / dependency | Classification and status |
|---|---|---|---|
| 0 | Historical `tests/scripts/postgresql-foundational-ddl.sql` at commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a`; blob `aaa63d138d845472d405d4411bf36b9e8284201e`; SHA-256 `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19` (33,519 bytes) | Candidate initial foundational schema; no earlier artifact dependency. | Historical immutable candidate, preferred by Option A; adoption pending D01 and compatibility/recovery evidence. Not authorized to execute. |
| Alternative; excluded from proposed sequence | Current `tests/scripts/postgresql-foundational-ddl.sql`; SHA-256 `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64` (42,379 bytes) | Expanded candidate includes two nullable ticket columns, three operational tables and sequences, reference rows, sequence `setval`, and audit-maintenance token/guard functions. It differs from the historical candidate by 106 added lines and zero removed lines. | Source comparison only; not selected as baseline. Do not layer its full-baseline DDL over the historical baseline. Its additions need separate reviewed/approved treatment. The conflicting hash `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7` has no verified artifact provenance and is rejected as an authority. |
| 1 | `database/migrations/postgresql/028_local_password_authentication.sql` — `0D5291A9B5EC06F0146E947A7B988A87013EE68B69DE1983AB75761FAEE0B45B` | Adds local-password authentication fields; depends on baseline `PA_TICKETS_USER`. | Review candidate; no execution approval. |
| 2 | `database/migrations/postgresql/029_user_profile_hierarchy.sql` — `9F40FB5326CB2F5FC48CD68D73636BC8B3EED5366D6EC58320207FF0F5C7FB3D` | Adds user-profile hierarchy; depends on baseline user table and its profile columns. | Review candidate; no execution approval. |
| 3 | `database/migrations/postgresql/033_rbac_migration_ledger.sql` — `8ACA3EC0431A91DEE9276B7924C8F8D0FA1E4FF11647B807CE33BA41393C9ABC` | Defines migration ledger; requires D09/history policy and reviewed fingerprint. | Review only; migration history remains `UNKNOWN`. Never fabricate ledger rows or infer applied history. |
| 4 | `database/migrations/postgresql/034_rbac_permission_metadata.sql` — `3C428BB4FC11D1F38C1BB2400FE008360E06C8131504D0F730DC328D0DB9ECB2` | Adds permission metadata; depends on baseline RBAC relations and 033 ledger. | Review candidate; no backfill or execution approval. |
| 5 | `database/migrations/postgresql/035_rbac_scopes_and_delegation.sql` — `C0CCC9DA3C3A441CEA6928ADBB8E50030F0D0716A59B9EADAFE2AACA97E15D9D` | Adds scope/delegation structures; depends on baseline user, role and membership relations plus 033. | Review candidate; D06/D07 and execution approval remain unresolved. |
| 6 | `database/migrations/postgresql/036_rbac_proposals.sql` — `FC65DC8A3E7B86EBCDE3FF31BE89F9E51FEFA8971921490EC21353E33B356562` | Adds proposal structures; depends on 033, 034 and an approved workflow contract. | Review candidate; no proposed permission grants or execution approval. |
| 7 | `database/migrations/postgresql/037_builtin_role_provisioning.sql` — `6853071A8DF0784E2046133B650A87BC84F47C3F5CEBA6276FE8051968A0FD71` | Creates five built-in role identities; depends on baseline role structures and explicit session setting. | Review candidate; creates identities only, no grants. D08 and execution approval remain unresolved. |
| 8 (proposed placement) | `database/migrations/postgresql/039_ticket_type_foundations_REVIEW.sql` — `0B6B82CA634A00997FA99B0D76C7FE805B9FBECEBED94243A95C08F77F7C4681` | Creates Incident, Problem, Change, Change Implementation and Relation tables/sequences. Static source references show direct dependency on baseline `PA_TICKETS`; no RBAC table/column references or dependency on 028–037 were found. | Review-only draft with unconditional abort guard; not executable. Place after 037 to preserve the existing 033–037 order and numeric progression, not because 037 is a technical prerequisite. Migration 038 is excluded. |
| Evidence only; no sequence position | `Docs/reconciliation/proposed/postgresql/S5-08_foundational_ddl_only_REVIEW.sql` — `0807A915E7E501F64EBA7F689F533F67965EF586F651AC1AF4D4D2A687FCC313` | Full DDL extraction from the expanded current foundational candidate; repeats the 40 current table definitions. | Review-only and unconditionally abort-guarded; not additive and must not be applied after row 0. |
| Evidence only; no sequence position | `Docs/reconciliation/proposed/postgresql/S5-08_foundational_data_only_REVIEW.sql` — `121397D455CEBEBA124BCE8D9E15B1C1C0D85CD1276D9E751141FA662B7289DA` | Four reference-data inserts and audit-maintenance token data extracted from the expanded candidate. | Review-only and unconditionally abort-guarded; no seed/data approval. |
| Evidence only; no sequence position | `Docs/reconciliation/proposed/postgresql/S5-08_sequence_operations_DEFERRED_REVIEW.sql` — `721CCB7546D028C223AE803489F271314B921EE1C761704B90676162C67BC9A3` | Three sequence `setval` operations extracted from the expanded candidate. | Deferred review-only; requires separate D03 decision and approval. |

**Excluded from this proposed sequence:** migrations 030–032 are superseded;
026 is protected and excluded; 038 (audit actor) remains blocked; 040 is
separate feature-switch work; 027 is Oracle. `tests/scripts/postgresql-schema.sql`
and `tests/scripts/postgresql-slice1-schema.sql` are test/overlap profiles, not
baseline build inputs. S5-08's separated artifacts are evidence for review, not
an execution-ready replacement set. Migration 039's proposed placement does
not resolve D01, D09, S5-10 approval, or migration-history uncertainty.

#### Dependency-validated review ordering (source-only; supersedes the earlier linear proposal)

This is a topological review order, not an execution order. It corrects the
earlier sequence, which omitted communication DDL/root 013 and placed 039 after
037 without a dependency. Artifacts marked REVIEW or BLOCKED still require
finalization and separate authorization.

| Review stage | Artifact(s) | Verified prerequisites and conflicts |
|---|---|---|
| 0 | Historical Option A baseline `tests/scripts/postgresql-foundational-ddl.sql` at `1bcc493d267e23e3e126b9b698fa4344ebe8588a` | Supplies `PA_TICKETS`, `PA_TICKETS_WORK_ITEM`, `PA_TICKETS_EVENT`, RBAC base tables and `PA_TICKETS_ROLE_SEQ`; migration history remains UNKNOWN. D01 adoption/compatibility is not established. |
| 1A | `028_local_password_authentication.sql` and `029_user_profile_hierarchy.sql` — **overlap review only** | Option A already defines the password hash, rotation flag, phone, manager ID, email-verification flag, and upper-email index these files add. The 029 name-based FK guard would add a second equivalent self-FK: the baseline declares the manager reference inline while 029 checks only for its custom constraint name. Do not include either unchanged in an executable set until exact normalized definitions and constraints are reconciled. |
| 1B | `039_ticket_type_foundations_REVIEW.sql` | Depends on baseline `PA_TICKETS` only; no dependency on 028–037 or 013 found. It overlaps test/generic schema artifacts, which must not also be treated as baseline inputs. Its abort guard makes it review-only. |
| 1C | `S5-09_communication_schema_REVIEW.sql` | Depends on baseline `PA_TICKETS`, `PA_TICKETS_WORK_ITEM`, `PA_TICKETS_EVENT`, and its five named communication sequences. It includes a direct Event FK. The phase-5 test adapter defines overlapping communication objects; do not apply that adapter as a second schema owner. Sequence ownership/current state and schema/search_path remain unknown; candidate abort guard remains mandatory. |
| 2A | Root `database/migrations/013_notification_orchestration.sql` | Requires the baseline ticket/event relations and communication table. Creates three notification sequences/tables and uses `ADD COLUMN IF NOT EXISTS` plus `CREATE INDEX IF NOT EXISTS` for communication lineage fields already present in the candidate. It does not add the communication→notification rule/execution FKs. The phase-5 adapter also declares these three notification tables/sequences with `IF NOT EXISTS`; those are overlapping test definitions, not a second migration, and must not be used to infer complete schema equivalence. Root 013 is outside the PostgreSQL migration folder and needs explicit review/admission. |
| 2B | `033_rbac_migration_ledger.sql` | Depends on baseline only. Must not record any migration as applied from artifact/object presence; D09/history decision and fingerprint remain prerequisites. |
| 3 | `034_rbac_permission_metadata.sql` | Requires baseline permission/grant relations and 033. Adds metadata/effective-grant columns; it performs an UPDATE, so it is not a read-only or schema-only fingerprint artifact. Its effects need separate schema-operation scope. |
| 4A | `035_rbac_scopes_and_delegation.sql` | Requires baseline user/role/membership relations and 033. Adds two sequences and scope/delegation tables; D06/D07 remain formally unresolved. |
| 4B | `037_builtin_role_provisioning.sql` | Requires baseline role table and role sequence only; no technical dependency on 034–036 found. It writes role identities and requires its explicit session setting plus separate D08/operation approval. |
| 5 | `036_rbac_proposals.sql` | Requires baseline role/permission/user relations, 033, 034 and the stated approved workflow contract. It creates the proposal table and indexes; it does not grant permissions. |
| 6 | Communication→notification FK block from `tests/scripts/postgresql-slice5-schema.sql` | Depends on communication candidate and root 013 tables. The source block guards four constraints by name; the candidate already creates the Event FK, so that one is redundant and should be retained only with its existing guard. The three notification FKs are not in root 013 and require review as a separately finalized post-013 artifact. Do not apply the test adapter as a whole. |

Stages 1B, 1C, 2A and the RBAC branch (2B onward) are independent after the
baseline and may be reviewed in parallel; the table gives one valid linearized
topological presentation only. The historical baseline plus numbered files
is not a complete deployment manifest: 028/029 overlap, 013 admission is
unresolved, and several entries are review drafts or perform data writes.
Do not execute this ordering or infer migration history from it.

Sequence review: the communication candidate defines five standalone
communication sequences; root 013 defines three notification sequences; 035
defines two scope/delegation sequences; 039 defines five ticket-type
sequences. These source artifacts do not establish ownership by their ID
columns or live allocation state. The candidate SQL has no `OWNED BY` or
`setval`; 035 explicitly starts its two new sequences at 1/increment 1; 039
creates its sequences without assigning ownership. Before any schema approval,
the final artifacts must state whether ownership is intentionally absent or
assign it consistently, verify `nextval` defaults/USAGE privileges, and
preserve unknown current values. No sequence reset/advance is authorized.

#### S5-03 read-only fingerprint authorization completion record (prepared; unsigned)

This record narrows the existing S5-01 packet to one operation. It records
owner-selected target intent and an offline artifact identity only. It is not
an approval, signature, independent verification, or execution authorization.

| Field | Prepared scope / owner completion |
|---|---|
| Operation | One read-only S5-03 catalog fingerprint; no DDL/DML, migration, provisioning, test writes, seed, or cleanup. |
| Target | `localhost:5432` / database `pa_tickets_codex_e2e` / role `pa_tickets_codex`; credential source `PA_TICKETS_E2E_DB_PASSWORD` (secret value not read or recorded). |
| Exact artifact | `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql`; offline SHA-256 verified as `6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342`. Prior artifact `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql` / `2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8` is historical and superseded for new S5-03 invocations. |
| Expected effect | Read-only fingerprint/catalog evidence for comparison with the approved historical Option A baseline; actual catalog equivalence and migration history are not presumed. |
| Owner approval | Owner must supply attributable identity/role, UTC issue time, fixed expiry, exact scope/artifact hash, signature or approval reference, rationale/evidence references. **Outstanding.** |
| Operator identity | Named operator and independently verified identity/evidence reference; exact database role and current privileges. **Outstanding.** |
| Independent review | Named independent reviewer and dated attestation covering the authorization, exact bytes, target, operator, and boundaries. **Outstanding.** |
| Peer/target verification | Independently verified host/address family/server identity and approved local peer/TLS method; demonstrate the client connects to `localhost:5432` and server-side target checks agree. **Outstanding.** The SQL candidate cannot prove the literal client hostname. The runner now performs offline structural checks for a Windows IPv6-loopback listener/process evidence envelope, but does not collect live OS evidence or cryptographically verify its trust, signature, provenance, or revocation. |
| Least privilege | Independent evidence that `pa_tickets_codex` is the intended operator and can perform only the required read-only catalog inspection on this target. **Outstanding.** |
| Validity and revocation | UTC `validFrom`/fixed `expiresAt`; named revocation authority, current revocation source/check time and evidence reference. **Outstanding.** |
| Recovery/uncertain outcome | State that the operation is read-only; define stop/no-retry and classify uncertain result `UNKNOWN`; identify separately authorized read-only reconciliation path. **Outstanding.** |
| Evidence custody | Sanitized command/invocation and output locations, custodian, access/retention, and integrity/hash procedure. **Outstanding.** |
| Execution mechanism | The current runner has no database-client path and `-Mode Execute` always fails closed. A trusted peer collector/verifier and independently reviewed executor or named authorized operator procedure must be approved before the first fingerprint. **Outstanding.** |

#### Proposed Windows localhost peer-assurance contract (engineering proposal; not approved)

For the owner-selected non-TLS target, the proposed supported endpoint is
strictly IPv6 loopback `::1:5432` reached through the approved host name
`localhost`; IPv4, wildcard, non-loopback, and other address families fail
closed. This proposal does not treat DNS resolution, a PostgreSQL password
login, or a server-reported address by itself as process identity proof.

A trusted Windows evidence collector must capture, immediately before the
authorized connection, the listener address/family/port and owning PID from
the OS socket table; the matching process ID, creation time, executable path,
command line and image SHA-256; Authenticode signer identity; service name,
service account and service-to-process relationship; and PostgreSQL data and
configuration paths. The collector must verify the executable and service
against an independently approved process policy. It must produce a
time-bounded signed evidence envelope binding those facts to `localhost:5432`,
`pa_tickets_codex_e2e`, `pa_tickets_codex`, the exact SQL artifact path/hash,
and the raw SHA-256 of the exact authorization-profile bytes. The later
authorized connection evidence must also bind the actual remote endpoint and
server-returned database, role, address, port and read-only transaction state.

Trust requires an independently controlled signing key, its public trust
anchor fingerprint distributed out-of-band, an independently maintained
revocation source, a named reviewer/custodian, and a fresh signed attestation.
The signing key must not be available to the database host or ordinary
development session. A local administrator can falsify local OS observations;
if that threat is in scope, reviewer-controlled collection or managed-device
attestation is required. Disk hash/signature evidence alone does not prove the
integrity of the running process or host kernel.

The runner's offline validator now checks a strict evidence shape, exact
`::1` endpoint, listener-PID/process-PID equality, process identity claims
against the supplied approved-process claims, a valid process-start/capture
timeline, capture age no greater than two minutes, expiry no more than five
minutes after capture, active revocation state, required trust references,
and binding to the exact target/artifact/authorization-profile bytes. These
checks are structural only: the process claims, signer thumbprint, trust
anchor, signature reference and `ACTIVE` revocation claim remain
**UNVERIFIED**. No live listener/process query, signature verification,
revocation lookup or database-client path is implemented. The validator's
success is not server identity verification or execution eligibility.

Before implementation of collection or Execute mode, Security/DBA and an
independent reviewer must approve the evidence schema, collector identity,
process allowlist, signature format, trust-anchor custody, revocation
freshness, host-integrity threat model, expiry policy and actual endpoint
binding method. Execute remains disabled until that contract and a separate
operation-specific S5-01 authorization are formally accepted.

Genesis SLICES v4 S5-01 acceptance requires the accountable owner to complete and sign
the packet; Codex leaves all unknown attribution and verification fields
blank. S5-03 remains BLOCKED until the signed operation packet and independent
evidence exist. Merely populating structural JSON profiles does not satisfy
these requirements.

Hashes identify current bytes only. The review reports document artifact roles,
source provenance, exclusions, and current differences; none establishes live
catalog compatibility, applied migration history, or authorization to execute.

#### Owner-completed authorization terms

The following fields remain intentionally blank for the accountable owners; no
identity, signature, time window, privilege grant, recovery evidence, or
approval reference is inferred:

- **Approved scope(s) and exact order:** ____________________
- **Authorized operator identity / role and independent identity evidence:** ____________________
- **Current least-privilege grants and target/schema boundary evidence:** ____________________
- **Permitted test names, generated-write identifiers, and cleanup boundary (or “none”):** ____________________
- **Validity start (UTC) / fixed expiry (UTC):** ____________________
- **Revocation authority and method:** ____________________
- **Backup or disposable-target recreation and recovery procedure:** ____________________
- **Evidence artifacts, custody location, retention, and responsible custodian:** ____________________
- **Uncertain outcome:** stop; do not retry; classify as `UNKNOWN`; identify the separately authorized read-only reconciliation action and evidence custodian: ____________________
- **Database owner approval / identity / UTC date / reference:** ____________________
- **Release authority approval / identity / UTC date / reference:** ____________________
- **Security reviewer approval / identity / UTC date / reference:** ____________________

Until these fields are completed against final immutable artifacts, D01 stays
`PENDING`; S5-01 stays `BLOCKED`; S5-03 and S5-10 stay `BLOCKED`; no provider
operation or migration-history claim is permitted.


## Owner-supplied connectivity evidence (2026-10-28; not independently collected)

The project owner reports that, from Windows CMD, service postgresql-x64-18
was RUNNING at PostgreSQL 18.6; TCP connections to IPv6 ::1:5432 and IPv4
127.0.0.1:5432 succeeded; and an authenticated connection reported database
pa_tickets_codex_e2e, role pa_tickets_codex, server address ::1, port 5432.
The reported read-only statement was:

    SELECT current_database(), current_user, inet_server_addr(), inet_server_port()

This is owner-supplied evidence of successful local network reachability and
database authentication for that invocation. Codex did not connect or execute
the query. No timestamped output artifact or independent collection was
provided with the report. The results do not identify the Windows process that
owned the listener, prove executable provenance or integrity, show the
configured bind address, enumerate role privileges, establish migration
history, or authorize S5-01/S5-03.

The successful IPv6 connection means a PostgreSQL configuration change is not
needed to resolve a connectivity failure. The owner also reports successful
IPv4 loopback connectivity. The previously observed wildcard listeners (:: and
0.0.0.0) can accept loopback connections; changing to loopback-only would
remove non-loopback access but would also be an access-policy change requiring
impact review and a restart. Keep the proposed value listen_addresses =
'::1,127.0.0.1' as an optional restriction proposal only. It is not required
for the connectivity result. Whether wildcard binding violates the adopted
local process-verification policy remains a separate verification question:
the OS listener address must be freshly collected and compared with the
approved policy. Do not edit configuration or restart solely because of the
earlier connectivity concern.

### Current S5-01/S5-03 evidence interpretation

| Control | Current evidence/status |
|---|---|
| Local network and database authentication | Owner-reported success to ::1 and 127.0.0.1; demonstrates connectivity for that invocation only. |
| Windows process/service/listener identity | Outstanding; fresh elevated listener PID, process ancestry, image path, service association and ACL evidence are still required. |
| PostgreSQL executable authentication | Outstanding; previous disk candidate was NotSigned. The owner report does not resolve the adopted Authenticode requirement. |
| Actual least-privilege grants for pa_tickets_codex | Unknown; current_database/current_user and endpoint output do not inspect grants or effective privileges. |
| Owner CNG signing and separate recovery/revocation trust | Outstanding; keys, pinned public certificates, protected policy/custody paths, signed fresh peer/revocation evidence are not established. |
| Operation-specific S5-01 authorization | Outstanding; no signed, time-bounded one-operation authorization and complete evidence custody/recovery record is supplied. |
| S5-03 execution authorization | Not authorized; the connectivity report is not operation authorization. Execute remains disabled and the runner has no database-client execution path. |

The smallest remaining local S5-01 work is to complete the owner-run elevated
OS identity inspection; resolve the unsigned executable under the adopted
Authenticode rule (or obtain adoption of a separate narrowly scoped amendment
and implement its reviewed verifier support); provision and verify the
owner-controlled CNG/recovery trust, ACL-protected policy and evidence custody;
obtain trustworthy evidence of the role's actual least-privilege grants; and
complete the signed, expiring operation-specific S5-01 packet with revocation,
uncertain-outcome and evidence-custody terms. None of these controls is
satisfied by a successful password-authenticated query alone. S5-01 and S5-03
remain BLOCKED; this owner report changes neither status.

### Adopted one-operation S5-03 local catalog-inspection exception

**Status: OWNER-APPROVED / ADOPTED FOR THIS NARROW POLICY SCOPE / NOT AUTHORIZATION TO CONNECT.** The owner explicitly adopted the following policy body by approving its exact SHA-256. It amends adopted Genesis SOP v3.1 and supplies the narrow SLICES v4 S5-01 clarification for this single local inspection. Policy adoption does not authorize a database connection or SQL execution.

**Proposed policy text for owner adoption:** This provision is a narrowly scoped amendment to the adopted Genesis SOP v3.1 local E2E controls and a corresponding clarification to SLICES v4 S5-01. For one S5-03 catalog fingerprint only, it permits an ordinary Npgsql connection without the custom Windows CNG signing, peer-evidence, authorization-envelope, and revocation-certificate infrastructure otherwise required by SOP v3.1. It applies only to IPv6 host `::1`, port `5432`, database `pa_tickets_codex_e2e`, role `pa_tickets_codex`, with the password read at runtime from `.env.test.local` key `PA_TICKETS_E2E_DB_PASSWORD`. It does not apply to `localhost` name resolution, IPv4, another address, database, role, credential source, machine, or environment. SSL is disabled only for this IPv6 loopback connection. No fallback target, ambient `PG*` setting, or secret in arguments or logs is permitted.

Before catalog queries, the client must begin a transaction, make its first transaction command set that transaction read-only, and confirm on that same connection that `current_database()` is exactly `pa_tickets_codex_e2e`, `current_user` is exactly `pa_tickets_codex`, `inet_server_addr()` is exactly `::1`, `inet_server_port()` is `5432`, and `current_setting('transaction_read_only')` is `on`. Any missing, unexpected, or ambiguous value aborts before fingerprint queries. The client may then execute only the single pinned S5-03 catalog fingerprint artifact named below and must roll back/end the read-only transaction. Authorization is for one operation only, valid for no more than 30 minutes from its recorded UTC start, and revoked on expiry, explicit owner revocation, or any target, role, endpoint, artifact, or runtime-check mismatch. On uncertainty, stop and do not retry; reconciliation requires separate authorization.

The client must retain sanitized audit evidence of the owner/operator, operation, UTC start and end, target assertion results, exact artifact hash, authorization validity/revocation check, completion status, and evidence-custody location. Evidence must exclude credentials and the completed connection string. This exception accepts the residual risk that loopback password authentication and runtime assertions establish the responding PostgreSQL session's database, role, address, port, and transaction mode, but do not independently authenticate the Windows service process, executable provenance, host kernel, or local administrator. This risk acceptance is limited to the named local development target. Independent identity verification remains required in every other environment. The exception authorizes no migration, DDL/schema change, data write, seeding, sequence change, role/grant/privilege change, provider-test write, fixture, provisioning, or cleanup; read-only catalog inspection is not provider acceptance and does not establish migration history or schema compatibility.

**Exact adoption text SHA-256:** `17DCC7EBF56F696E11A4C86298B0A431BB46AB1D577A7ED4A35CFC5030448866`. Hash input is the exact body following **Proposed policy text for owner adoption:**, excluding that label and trailing blank lines, encoded as UTF-8 without BOM with CRLF normalized to LF. Do not approve a changed body under this hash.

**Bound artifact:** `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql` � SHA-256 `2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8`. This is the expected previously recorded value; recheck current bytes immediately before owner adoption and again before execution. Any byte change invalidates the binding.

**Explicitly excluded:** all migrations; DDL/schema changes; data changes or seeding; sequence changes; role/grant/privilege changes; provider-test writes; fixtures; provisioning; and cleanup. Read-only catalog inspection is not provider acceptance and does not establish migration history or schema compatibility.

**Required before enabling this mode:**

1. **Completed:** owner adoption of this exact policy body is recorded below with its hash, owner identity, approval source, and canonical record effective timestamp.
2. Separate operation-specific S5-01 authorization naming the single fingerprint operation, exact target and artifact hash, UTC start and fixed expiry (at most 30 minutes), owner/operator identity, exclusions, stop/recovery handling, and evidence-custody location and retention.
3. Sanitized owner-collected read-only evidence of effective PostgreSQL role privileges and memberships demonstrating the role is not superuser and has no write, role-management, database-creation, replication, or other elevated capability. A signed assertion alone does not prove actual grants. Unknown or excess privilege blocks the operation.
4. A reviewed execution path that pins endpoint and artifact bytes, has no ambient target fallback, performs the runtime checks before catalog queries, enforces the read-only transaction, captures sanitized evidence, and rolls back on every exit. The current S5-03 runner remains fail-closed with Execute mode disabled; this proposal does not enable it.
5. Invocation-time confirmation that authorization is within its fixed validity window and has not been revoked in the canonical record. Missing or stale approval, revocation status, evidence, or target identity fails closed.

This adopted policy is reflected narrowly in SLICES v4 S5-01 for this exact target and single operation; independent verification remains required elsewhere. S5-01 remains BLOCKED and S5-03 remains unauthorized until the separate operation-specific authorization and technical evidence are complete.
**Owner adoption record — APPROVAL RECORDED; OPERATION NOT AUTHORIZED:**

- Decision: **APPROVE adoption of the exact policy body**
- Exact policy text SHA-256: `17DCC7EBF56F696E11A4C86298B0A431BB46AB1D577A7ED4A35CFC5030448866`
- Accountable owner: Mr. H, Genesis, Developer & DBA (as identified in the explicit owner approval recorded below; no independent identity check is claimed)
- Approval reference: **Mr. H's explicit current-conversation message: “I APPROVE!”**, approving the finalized exception and naming this exact policy-body SHA-256; no external reference or cryptographic signature was supplied.
- Approval date: **2026-10-28**, as stated by the owner. A precise effective UTC time is not present in the message metadata available to this record and is therefore **not asserted**. The canonical record timestamp is not substituted for the owner's approval time.
- SLICES v4 S5-01 reconciliation: applied narrowly below; independent verification remains required outside this exception.
- Scope/expiry: one S5-03 fingerprint only; operation authorization remains separately required and may not exceed 30 minutes.

### Owner-approved simplified local read-only policy direction (2026-10-28)

**Status:** `OWNER-APPROVED POLICY DIRECTION / SOURCE RECONCILED / NO DATABASE OPERATION PERFORMED`.

The owner supplied the policy direction in the current conversation attachment
`Pasted text.txt` (attachment SHA-256
`99B195886BF4F0A4ABB391A1F349BF784DB74748C1A3E5815B8FE18AE15E5F3E`). The
attachment identifies the decision as owner-approved simplification direction
for Mr.H. This digest identifies the supplied attachment bytes, not a detached
signature or cryptographically authenticated approval. The canonical record
entry was made at `2026-10-28T11:32:28.5530502Z`; this is the record-entry time,
not an asserted owner-message timestamp.

For the exact local E2E target only (`::1:5432`, database
`pa_tickets_codex_e2e`, role `pa_tickets_codex`, password from
`PA_TICKETS_E2E_DB_PASSWORD` via the existing explicit local loader), the
standing owner-approved policy direction permits only these two read-only
operations and exact raw-byte SQL artifacts:

| Operation | Artifact | SHA-256 |
|---|---|---|
| `LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT` | `tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql` | `0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034` |
| `S5-03_READ_ONLY_FINGERPRINT` | `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql` | `6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342` |

The simplified policy replaces CNG signing certificates/keys, signed
authorization and revocation envelopes, ProgramData trust/custody provisioning,
and repeated time-limited owner forms for these two operations only. It retains
exact target and artifact allowlists, credential secrecy, a same-connection
read-only transaction and identity assertion before catalog SQL, bounded
timeouts, sanitized attempt/result evidence, and stop/no-retry handling for
mismatch or uncertainty. The local result log belongs under the existing
`test-results/evidence` project path. The policy does not attest the Windows
PostgreSQL process or executable. It does not apply to production, staging,
shared targets, any other database/role/endpoint, arbitrary SQL, writes,
migrations, role/grant changes, provisioning, seeds, provider-test writes, or
cleanup. Such operations remain separately controlled.

The previously adopted SOP v3.1 amendment and its approved original SHA-256
`B78980670B1E0E8E79C333A36D7B93F46633C02B2172088C8B33469BED28A18E` remain
historical records; this simplification is an addendum/direction and does not
rewrite those bytes or claim the old cryptographic controls were performed.
SLICES v4 and the existing local provisioning runbook are reconciled to state
that those provisioning steps are not prerequisites to the two local read-only
operations.

**Implementation evidence (offline only):** the test project contains a
two-entry operation/hash allowlist, explicit operation plus confirmation
opt-in, exact Development target validation, same-connection database/role/
server-address/port/read-only checks before artifact SQL, transaction rollback,
bounded command timeouts, and sanitized evidence output. The PowerShell wrapper
accepts only the two named operations and verifies the selected raw artifact
hash before invoking the guarded test path. Focused tests passed 15/15 on
2026-10-28. This code was not invoked; no credential file was read, no
connection was opened, and no SQL was executed. Policy direction and offline
implementation readiness are not S5-01 or S5-03 completion and are not evidence
of live effective privileges or catalog state.

**Privilege-preflight invocation attempt (2026-10-28; stopped before runner):**
The fixed privilege-preflight artifact was re-hashed as
`C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`. The
owner-selected wrapper parameters were
`-Operation LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT -ExecuteReadOnly`. Windows
PowerShell rejected the unsigned `.ps1` under the current execution policy
before loading the script body (`PSSecurityException`; signature status
`NotSigned`). No wrapper code, test, credential loader, database connection,
or SQL ran; no runner evidence file was created. Environment execution flags
were unset. This is `NOT STARTED / BLOCKED BY LOCAL SCRIPT EXECUTION POLICY`,
not a database denial or privilege finding. The observation was recorded at
`2026-10-28T11:36:28.9295330Z`; this is the Codex record time, not an owner
approval timestamp. No policy or machine setting was changed. Next action:
owner runs the exact wrapper command in a PowerShell session where execution
of this reviewed local script is permitted under the machine's existing policy;
do not use an execution-policy bypass. Then review the sanitized result before
deciding whether to proceed to the separate S5-03 fingerprint invocation.

**Native .NET launcher reconciliation (2026-10-28T11:43:39.4824787Z; implementation approval, offline only):** The owner authorized a native .NET CLI entry point without changing PowerShell policy. `tools/PA.Tickets.LocalE2eReadOnlyRunner` is a narrow launcher for the existing Npgsql inspection engine. It accepts exactly `LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT` or `S5-03_READ_ONLY_FINGERPRINT`, requires `--execute-readonly`, verifies the pinned artifact hash, and then invokes only the existing guarded test-host path. It accepts no target, SQL, or credential arguments. This launcher remains subject to applicable organizational controls and does not itself expand the two-operation policy or grant database authorization. Offline parser/artifact tests and builds are the only validation in this task; no database connection or SQL ran.

**Native launcher validation update (2026-10-28T11:45:12.5392926Z; offline only):** 38 focused tests passed (runner guard, CLI argument/artifact checks, explicit test-host dormancy, and local PostgreSQL configuration tests); the CLI project and API built with 0 warnings/errors, and `git diff --check` passed. The operation opt-in variables were cleared for tests. The CLI does not call PowerShell and is not a mechanism for overriding organizational restrictions. No connection, SQL, or real `.env.test.local` read occurred. The invocation path is offline-validated and available for explicit owner selection, but no live result exists.

### LOCAL_GUARD_REJECTED diagnosis (2026-10-28T11:52:15.8593301Z)

Owner-supplied attempts produced:

- `test-results/evidence/local-e2e-readonly-48338f4e607d416792927571d3abf0b4.json` — privilege preflight, started `2026-10-28T11:47:56.8216011Z`, finished `11:47:57.4206791Z`, expected artifact hash, `DENIED_NO_CATALOG_QUERY`, zero result sets/rows, reason `LOCAL_GUARD_REJECTED`.
- `test-results/evidence/local-e2e-readonly-87fdf7e23ba34da0ba5ceaa75c4fe6b9.json` — S5-03 fingerprint, started `2026-10-28T11:49:04.943741Z`, finished `11:49:05.3043676Z`, expected artifact hash, `DENIED_NO_CATALOG_QUERY`, zero result sets/rows, reason `LOCAL_GUARD_REJECTED`.

These records establish that neither reached the pinned catalog SQL. They do
**not** record the failing stage or whether `OpenAsync` completed, so it is
unknown whether either attempt contacted PostgreSQL. The identical reason code
does not prove an identical underlying cause. Source tracing found the exact
diagnostic defect: the catch block mapped every `InvalidOperationException`,
including configuration failures and post-connect session-identity guards, to
the same `LOCAL_GUARD_REJECTED` code and discarded its specific type/state.
The existing evidence cannot identify which specific guard failed, so no
underlying configuration or target defect is asserted.

The runner now records sanitized `Stage`, `ConnectionOpened`,
`CatalogQueryStarted`, `ReadOnlyTransactionSet`, and
`SessionIdentityVerified` fields. Failure codes distinguish local
configuration, fixed-target configuration, connection/authentication,
read-only enforcement, session identity mismatch/check, pinned SQL, rollback,
cancellation, and evidence-output failures. Exception text and connection
details remain suppressed. The first new invocation should be treated as a
single diagnostic retry; stop on any failure and inspect that record before
considering another attempt.

Offline validation after this diagnostic change passed 45/45 focused tests;
the native CLI and API built with 0 warnings/errors; `git diff --check` passed.
The test invocation opt-ins were cleared. No PostgreSQL connection, SQL, or
`.env.test.local` read occurred in this diagnosis task. The privilege preflight
is ready for one new explicit owner invocation under the existing standing
local read-only policy; the S5-03 fingerprint was not invoked.

### SESSION_IDENTITY_MISMATCH follow-up (2026-10-28T11:56:25.9447868Z; offline diagnosis)

Latest owner-supplied evidence is
`test-results/evidence/local-e2e-readonly-708866e76196486484bba7e7595b8eb0.json`:
operation `LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT`; stage `SESSION_IDENTITY`;
`ConnectionOpened=true`; `ReadOnlyTransactionSet=true`;
`SessionIdentityVerified=false`; `CatalogQueryStarted=false`; outcome
`DENIED_NO_CATALOG_QUERY`; reason `SESSION_IDENTITY_MISMATCH`. Thus PostgreSQL
was contacted and the read-only transaction command succeeded, but no pinned
privilege catalog query ran. The evidence does not record the session values
or identify which of database, role, address, port, or read-only state failed.
No individual mismatch can safely be asserted from this record.

Source review found the query values were originally read in positional order
matching the SELECT list, so no proven ordinal swap or `current_user` versus
`session_user` substitution explains the failure. However, comparing
`inet_server_addr()::text` directly to the literal `::1` was unnecessarily
representation-sensitive. The validator now parses the value as an IPv6
address, accepts equivalent exact loopback spellings and an explicit `/128`,
and still rejects IPv4, other IPv6 addresses, zones, or non-host prefixes.
The query now aliases each identity field and maps by column name. Each
identity mismatch emits its own safe reason (`SESSION_DATABASE_MISMATCH`,
`SESSION_ROLE_MISMATCH`, `SESSION_ADDRESS_MISMATCH`, `SESSION_PORT_MISMATCH`,
or `SESSION_READ_ONLY_MISMATCH`). This corrects the representation risk and
diagnostic granularity, but does not prove which condition caused the observed
attempt.

Focused identity and runner tests passed 51/51; CLI/API builds had 0
warnings/errors; `git diff --check` passed. No database connection was made in
this follow-up. A single new explicit privilege-preflight invocation is
appropriate to establish the specific mismatch or collect the report; if it
fails, stop and review the new field-specific record. Do not run S5-03 until
the preflight succeeds and its privilege report is reviewed.

Owner-invoked commands (run one operation per invocation):

```powershell
dotnet run --project tools/PA.Tickets.LocalE2eReadOnlyRunner -- --operation LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT --execute-readonly
dotnet run --project tools/PA.Tickets.LocalE2eReadOnlyRunner -- --operation S5-03_READ_ONLY_FINGERPRINT --execute-readonly
```

**Verified privilege-preflight result (2026-10-28):** The owner-invoked
`LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT` completed at
`2026-10-28T12:14:25.1152866Z` with outcome
`COMPLETED_READ_ONLY_ROLLED_BACK`. Evidence:
`test-results/evidence/local-e2e-readonly-32cc021b1f154caeadbf8cf96ceac527.json`,
SHA-256 `2406FFF391CE8FDB87F17105C5E30B0AADAB3B35FC8310425F7DF6F3DD36535B`.
The report records connection open, read-only transaction, same-session identity
verification, catalog completion, successful rollback, and connection disposal.
It used the approved artifact hash
`0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034`; nine
result sets and 546 rows were captured, with result digest
`94C66EA0B797C227EBC6D72285678625B64172173BC82963A455474E40BFF876`.

Verified target: database `pa_tickets_codex_e2e`, role `pa_tickets_codex`,
server `::1/128`, port `5432`, transaction read-only `on`. The role is not a
superuser and has `rolcreatedb=false`, `rolcreaterole=false`,
`rolreplication=false`, and `rolbypassrls=false`; `rolinherit=true` and
`rolcanlogin=true`. The reported role-membership path set is empty.

The effective privilege results are write-capable: database CONNECT, CREATE,
and TEMP are true; `public` schema USAGE and CREATE are true; the role owns all
43 reported table-like relations and has SELECT, INSERT, UPDATE, DELETE, and
TRUNCATE on all 43; all 458 reported columns allow SELECT, INSERT, and UPDATE;
all 40 reported sequences allow USAGE and UPDATE. The `public` schema owner is
`pg_database_owner`. Thus the role is non-superuser but has broad owner-level
write authority. The read-only transaction protected this preflight operation;
it does not erase these standing privileges.

**S5-03 fingerprint readiness: BLOCKED pending privilege-policy resolution.**
The preflight authorization terms designate elevated or ambiguous effective
capabilities as a stop condition. These results do not establish a
least-privilege read-only operator, so the role is not accepted here as meeting
that requirement. No grants or roles were changed. Before S5-03, the owner and
database/security authority must either approve a bounded policy disposition
for this exact local role and read-only fingerprint, or separately authorize
least-privilege remediation and update the fixed-role operation configuration
under an owner-approved revision. The fingerprint command remains uninvoked;
this result does not complete S5-01 or authorize S5-03.

Record entry time: `2026-10-28T12:15:36Z` (Codex UTC record time, not an owner
approval timestamp).

### S5-03 local read/write role policy clarification (owner-selected; 2026-10-28)

**Owner direction:** Mr. H, Genesis Developer & DBA and sole PA_TICKETS administrator, selected use of the existing `pa_tickets_codex` role for both local E2E development writes and the two approved fixed read-only inspection operations. The owner explicitly accepts the role's already documented standing object-write capabilities for those two inspection invocations, because each invocation is constrained to one pinned artifact inside a verified PostgreSQL `READ ONLY` transaction. This direction is limited to `::1:5432`, database `pa_tickets_codex_e2e`, role `pa_tickets_codex`, and the two operation names/artifacts already listed above. It grants no database operation by itself.

The privilege-preflight evidence remains the source for the role's actual current privilege facts; no grants or ownership were changed. The existing development/schema-owner role remains intact. The previously drafted separate `pa_tickets_codex_inspector` role and successor SQL were withdrawn from the implementation proposal and were not executed or adopted.

For these two local inspections only, standing table/column writes, schema/database CREATE, sequence UPDATE, and relation ownership are not automatic rejection conditions. The following remain rejection conditions: wrong endpoint/database/role; superuser, CREATEDB, CREATEROLE, REPLICATION, or BYPASSRLS role attributes; any reported effective role membership; missing/stale privilege evidence; altered artifact; failed same-connection identity check; failure to set read-only mode; prohibited SQL; timeout or uncertain completion. The transaction must be set `READ ONLY` before the identity query and all catalog SQL, and rolled back. The .NET runner's two-operation artifact allowlist and fixed target remain unchanged. This exception does not waive stricter least-privilege requirements for shared, staging, production, schema-write, migration, provider-test-write, seed, provisioning, or cleanup workflows.

**Timestamp/attribution:** Owner direction is present in the explicit conversation message dated `2026-10-28`; that message supplies no exact UTC timestamp or separate approval reference. Codex recorded this reconciliation at `2026-10-28T12:26:08Z`; that is a record-entry time, not the owner's approval timestamp or a cryptographic signature.

**Current status:** The privilege preflight was previously owner-invoked and reviewed; its successful rolled-back evidence is recorded above. Offline source tests accept the documented write-capable role only for the two fixed operations, exact target, fresh evidence, no-membership status, and permitted role attributes. The fingerprint runner retains catalog result rows in sanitized evidence for comparison without another connection; bounded data-integrity checks return no application rows. No database connection was made for this policy/source update. Active S5-03 artifact: `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql`, SHA-256 `6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342`. The former artifact/hash `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql` / `2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8` and its failed execution evidence remain historical. S5-01/S5-03 are not marked PASS until the successor fingerprint result is collected, reviewed, and compared to the accepted baseline.

### S5-03 approved artifact activation (2026-10-28)

Mr. H's explicit current-conversation instruction approves the unchanged
existing-database successor bytes and directs activation. No detached signature
or exact owner-message UTC timestamp was supplied; this record does not invent
either. Canonical record-entry time is not treated as the owner-approval time.

- Operation: `S5-03_READ_ONLY_FINGERPRINT` only.
- Active artifact: `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql`.
- Approved SHA-256: `6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342` (raw file bytes).
- Target: `::1:5432`, database `pa_tickets_codex_e2e`, role `pa_tickets_codex`.
- The old artifact remains preserved at SHA-256
  `2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8` and
  is rejected by active mappings for new S5-03 invocations. Its prior failure
  evidence is unchanged.
- The privilege-preflight operation and its approved artifact/hash remain
  unchanged.
- Activation does not execute the operation or mark S5-01/S5-03 complete.

### S5-03 successful fingerprint review and M1 schema reconciliation (2026-10-28)

**Evidence:** `test-results/evidence/local-e2e-readonly-335da6214778405f932672cd41a21244.json`, raw SHA-256 `A01F241ADF25DB76BE856B29C01E82C63F594DD4F9F2B03FDB6DAA47ABB2F8BB`. Started `2026-10-28T12:48:30.9181352Z`; finished `2026-10-28T12:48:31.3776817Z`. Operation `S5-03_READ_ONLY_FINGERPRINT`; artifact hash `6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342`; target `[::1]:5432/pa_tickets_codex_e2e` as `pa_tickets_codex`. Outcome `COMPLETED_READ_ONLY_ROLLED_BACK`; connection opened, read-only transaction set, same-session database/role/address/port verified, catalog query completed, rollback succeeded and connection disposed. Thirteen result sets / 1,450 rows were captured; result digest `CC8CD8AFC87BB2F49FBE455BAFE04712C118F92B8B62822F9B77EE959498C76F`. Set row counts, in order: `1, 171, 1, 458, 391, 88, 40, 125, 1, 1, 171, 1, 1`.

**Observed catalog:** PostgreSQL 18.6; `public` schema; 43 ordinary tables, 40 sequences and 88 indexes. All 37 tables, 33 sequences and 18 index names enumerated from the historical baseline at commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a` / SHA-256 `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19` are present by exact name. The candidate's comparison reports 83 additional names: six tables (`PA_TICKETS_PRIORITY`/`pa_tickets_priority`, `PA_TICKETS_STATUS`/`pa_tickets_status`, `PA_TICKETS_STATUS_TRANSITION`/`pa_tickets_status_transition`), seven sequences (the same three upper/lower pairs plus `PA_TICKETS_AUDIT_MAINTENANCE_N_MAINTENANCE_ID_seq`), and 70 indexes. The same three case-variant table/sequence/index families are a collision risk for quoted versus unquoted identifiers; no cleanup or consolidation is authorized. Extra index names include PostgreSQL-generated constraint indexes and additional application indexes; “unexpected” is inventory-relative, not a finding that each index is invalid.

**Definition and mapping findings:** the fingerprint captured all columns, formatted/internal types, nullability, defaults, constraints, indexes and sequence metadata. Exact full-definition equivalence to the historical DDL and every EF mapping has not yet been computed; name presence alone is insufficient. In particular, `PA_TICKETS_PERMISSION` contains only `N_PERMISSION_ID bigint NOT NULL`, `S_PERMISSION_CODE varchar(150) NOT NULL`, `S_PERMISSION_NAME varchar(250) NOT NULL`, nullable `S_DESCRIPTION text`, and `N_ACTIVE boolean NOT NULL DEFAULT true`. The five permission metadata fields required by migration 034 (`S_MODULE_CODE`, `S_ACTION_CODE`, `S_SCOPE_CLASS`, `S_SECURITY_CLASS`, `N_READ_ONLY`) are absent. `PA_TICKETS_ROLE_PERMISSION` contains only its key and role/permission IDs; migration 034's effective dates, status/provenance and audit columns are absent. This is the concrete blocker for the database authorization reader and lifecycle persistence test (previous SQLSTATE `42703` for `p.S_MODULE_CODE`).

Catalog inventory also finds no `PA_TICKETS_INCIDENT`, `PA_TICKETS_PROBLEM`, `PA_TICKETS_CHANGE`, `PA_TICKETS_CHANGE_IMPLEMENTATION`, or `PA_TICKETS_RELATION`; no five communication relations; no root-013 notification rule/version/execution relations; no `PA_TICKETS_SCHEMA_MIGRATION` ledger; and no migration-035 scope/delegation relations. Every one of the 40 sequences reports owner `pa_tickets_codex` but no `OWNED BY` relation/column. The fingerprint records sequence definitions/default expressions, not current sequence values or safe next allocations. The schema owner is `pg_database_owner`; the recorded public ACL grants USAGE to PUBLIC and USAGE/CREATE to `pg_database_owner`.

**Migration-history result:** `UNKNOWN`, ledger object absent. Neither baseline-object presence nor the catalog result proves that any migration ran. Preserve the historical baseline bytes and do not infer applied history.

**Minimal source-to-schema plan (review order only; no artifact is authorized to execute):**

1. Reconcile full catalog definitions against the immutable historical baseline and current EF mappings, preserving the six case-variant table names and existing sequence state until exact ownership/allocation facts and an owner disposition are established. Do not replay the baseline over this populated schema.
2. Resolve the observed permission-metadata gap with an owner-reviewed additive replacement for the 034 intent after the explicit unknown-history/033 contract is settled. Preserve existing IDs and grant facts; missing dates/status remain non-authorizing. Do not use the current 034 script unchanged without reviewing its data update and transaction effects against the live rows.
3. Add 035 scope/delegation structures only after D06/D07 and approved lifecycle/provenance behavior. No inferred scope rows.
4. Add the five ticket-type relations from the 039 review candidate only after exact baseline and FK/sequence compatibility review; the catalog confirms they are absent. Root 014–016 are source references, not execution history.
5. For notification functionality, reconcile the five communication tables first, then root PostgreSQL 013's three notification relations/sequences and communication columns; only then consider notification lineage FKs. Root 013 is outside the current numbered PostgreSQL order and requires its own reviewed artifact/dependency admission. No communication objects are presently observed.
6. Before any insert/provider test, separately reconcile sequence defaults, ownership and current allocation against existing keys without `setval`, reset, or blind advancement. An uncertain result stops for read-only reconciliation.

**Status:** S5-03 execution and sanitized evidence capture are verified, but the SLICES acceptance also requires comparison against an accepted baseline. Since D01 formal baseline adoption and exact definition comparison remain open, S5-03 is recorded `PARTIAL`, not PASS. S5-01 remains OPEN; no schema operation or write-test authorization follows from this fingerprint.

**Next M1 provider acceptance after separately authorized schema work:** rerun `Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`; `Ticket_lifecycle_service_persists_and_reloads_core_ticket_flow_in_postgresql`; `Incident_creation_rolls_back_base_ticket_after_injected_post_save_failure`; `Incident_creation_persists_ticket_incident_event_history_and_audit_together`; then `Authenticated_http_pipeline_executes_workflow_and_enforces_auth_idempotency_and_concurrency`. Persisted authorization follow-up includes `Database_authorization_enforces_permission_scope_effective_dates_and_auditor_read_only_access` and `Admin_head_delegation_is_identity_scoped_and_does_not_expand_ticket_scope`. These are write-capable provider tests and remain blocked until a separate exact test/write/cleanup authorization and isolated fixture/cleanup boundaries are approved. Do not run them under the fingerprint authorization.

### M1 additive RBAC metadata replacement proposal (review only; 2026-10-28)

**Candidate:** `Docs/reconciliation/proposed/postgresql/034_rbac_permission_metadata_M1_REVIEW.sql`, raw SHA-256 `08E29BD2ABA2F7F220A0099E6A2747769D2554BA707770B102D1BD68BDEA7B7C`. The artifact is outside the migration directory and has an unconditional exception inside a transaction before all proposed DDL. The failed transaction prevents later statements from executing in a script client that continues after the error. It has not been executed, approved for execution, or syntax/provider-tested.

The candidate preserves existing permission/grant rows and IDs. It adds the five nullable permission metadata fields required by `AdministrationPermissionRow`. It adds grant valid-from/to, provenance, updated-at/by fields; `S_EFFECTIVE_STATUS` defaults to `UNKNOWN`, `N_ACTIVE` defaults to `false`, and both are non-null so legacy rows remain non-authorizing. No `UPDATE`, permission-catalogue backfill, grant backfill, effective-date inference, provenance fabrication, grant, ledger row, sequence reset or sequence advance is present. Missing metadata stays NULL; authorization remains fail-closed until individually reconciled. It rejects partial/out-of-band metadata instead of silently accepting it. Its status/date constraints reject invalid values; transaction failure rolls back its DDL. The additive defaults are schema-level conservative classifications, not evidence of historical grant intent.

**Dependency sequence for the minimum core M1 write-test set (proposal, not an execution order approval):**

1. Reconcile exact baseline definitions and current catalog/EF differences; preserve object names and all sequence state. The S5-03 evidence is an inventory, not a pre-write sequence-state check.
2. Under D09, establish the 033 ledger structure only after agreeing the unknown-history representation outside fabricated applied rows. 033 creates an empty table/index and inserts no history. Its presence must not be treated as proof that earlier migrations ran.
3. Apply an owner-reviewed, finalized successor to 034, then migration 035's scope/delegation structures. `DatabaseAuthorizationFactsReader` queries permission metadata, effective grants and `PA_TICKETS_USER_ROLE_SCOPE`; therefore both 034 and 035 schema are prerequisites for persisted authorization tests. No scope/grant rows are inferred or seeded.
4. Apply a finalized 039 ticket-type foundation after its exact `PA_TICKETS` FK/key and sequence prerequisites are verified. It is needed for the core lifecycle provider test that iterates ticket types and for Incident atomicity tests. The current 039 file is an unconditional-stop review draft. Root 014–016 are not evidence that these objects were applied.
5. Before any test writes, separately verify current sequence state versus existing keys for every sequence the test can consume. S5-03 captured start/increment/cache and defaults but not current allocation values. All 40 observed sequences lack `OWNED BY` relation/column. Do not use `setval`, restart, reset, blind advance, or broad owner reassignment. Require a separate narrowly bounded read-only sequence-state evidence artifact because the adopted local exception currently allowlists only the privilege preflight and fingerprint artifacts.

Root PostgreSQL migration 013 and the five communication tables are not prerequisites for the selected core ticket lifecycle/Incident tests. Defer them unless a chosen M1 API test is shown to persist notification/communication rows. If needed, order the communication-table successor before root 013, then add notification lineage FKs only after 013 creates the three referenced notification relations. This chain requires separate artifact/dependency review; root 013 is outside the numbered PostgreSQL migration directory. Migration 036/037 role-proposal/provisioning work, migration 028/029 authentication/profile work, migrations 026/038, and feature migration 040 are not in the minimum core lifecycle chain.

**Data/rollback controls:** take an owner-approved recovery snapshot of this dedicated target before schema work; confirm exact target and current catalog immediately beforehand; execute only exact reviewed hashes in one bounded operation with expected effects and transaction/recovery classification; stop on any precondition or uniqueness/constraint failure. PostgreSQL transactional DDL can roll back the candidate DDL, but an uncertain connection outcome remains UNKNOWN and must not be retried before separately authorized read-only reconciliation. Never drop/consolidate the case-variant priority/status objects as part of this change.

**Required owner decisions before execution:** formal D01 baseline/compatibility disposition; D03 sequence allocation/ownership plan; D04 canonical permission identity policy; D05 unknown historical grant handling; D09 unknown-history/ledger contract; D08 exact test role-to-permission matrix and any fixture grants; D06 before client-scoped authorization, and D07 for authored scope/delegation semantics. Separately authorize schema application and then exact provider test names, generated identifiers, permissible fixture writes, cleanup statements/boundaries, validity window, and recovery. The owner-selected policy directions do not themselves authorize these operations.

### M1 core-test dependency verification and executable candidates (2026-10-28)

This source-only follow-up traced the four named tests through their current
repository, service, EF mappings and authorization reader against the successful
S5-03 catalog evidence. No database connection or write was made. The evidence
has no sequence `last_value`/`is_called` state or application-row contents, so
sequence collision status and authorization/configuration fixture readiness
remain unknown. Migration history remains `UNKNOWN`.

The uppercase EF-mapped `PA_TICKETS`, `PA_TICKETS_EVENT`, `PA_TICKETS_AUDIT`,
`PA_TICKETS_AUDIT_CHAIN_LOCK`, `PA_TICKETS_WORK_ITEM`, `PA_TICKETS_ASSIGNMENT`,
audit-maintenance tables/token, status and priority tables are present in the
catalog export. The export shows duplicate lowercase `pa_tickets_status` and
`pa_tickets_priority` relations; quoted EF mappings use the uppercase tables,
and this work does not consolidate either pair. Permission metadata columns
and `PA_TICKETS_USER_ROLE_SCOPE` are absent. `PA_TICKETS_INCIDENT`,
`PA_TICKETS_PROBLEM`, and `PA_TICKETS_CHANGE` are absent.

The repository persistence test uses the existing ticket/event/audit relations;
its ticket ID and ticket number are supplied by the test, while event and audit
IDs use defaults. It does not need RBAC or migrations 033–035/039. The lifecycle
test uses ticket, ticket-number, work-item, event, audit and assignment
allocation, plus active `NEW` status and default/selected priority facts. Its
route/read checks invoke the database authorizer. The authorization reader
always queries `PA_TICKETS_USER_ROLE_SCOPE` and its permission projection
requires 034 metadata, so the 034 and 035 schemas are real dependencies for
that test; valid owner-approved authorization facts remain a distinct data
dependency. Both Incident tests require `PA_TICKETS_INCIDENT` and its generated
ID sequence. The success path also performs an authorized read. The generic
lifecycle test's enum loop stores the ticket type discriminator and does not
query Problem/Change subtype tables. None of these tests uses communication or
notification relations, root migration 013, 028/029, or 036/037.

Review-only allocation query:
`Docs/reconciliation/proposed/postgresql/M1_sequence_safety_inspection_REVIEW.sql`,
SHA-256 `526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A`.
It verifies `[::1]:5432`, database, role, and a read-only transaction, then
compares the existing test sequences' persisted state with current key maxima,
definitions, defaults and `OWNED BY` metadata. It contains no sequence
allocation or DDL. At the time this candidate was prepared, it was outside the
two-operation runner allowlist. Its fixed mapping and pin have since been added
as `M1_SEQUENCE_SAFETY_PREFLIGHT`; the distinct operation authorization remains
pending. Do not invoke it until that authorization is recorded. The prior
S5-03 evidence reports 40 sequences with
start/increment/cache `1/1/1` and no ownership relation but has no state values,
so it cannot establish whether the next value conflicts. Existing sequences
will not be reset or re-owned here. Newly proposed sequences are owned by their
generated-key columns.

Prepared schema-only candidates; all are **NOT APPROVED / NOT EXECUTED**:

| Artifact | Raw-byte SHA-256 | Bounded effect |
|---|---|---|
| `Docs/reconciliation/proposed/postgresql/034_rbac_permission_metadata_M1_EXECUTABLE_CANDIDATE.sql` | `DE4DAB8ECDA459D65FA71388875CEEC428A752A91966E290D392B700D077ADA2` | Replaces only 034 schema work. Preserves IDs/rows; adds nullable permission metadata and validity/provenance fields; legacy grants receive `UNKNOWN`/inactive defaults and an enforcement check. No rows, grants, dates, provenance or permission definitions are backfilled. It requires the 033 ledger structure to exist and be empty; no ledger rows are inserted. |
| `Docs/reconciliation/proposed/postgresql/035_rbac_scopes_and_delegation_M1_EXECUTABLE_CANDIDATE.sql` | `4C39AB673DD4B2A42838DBCA945809BC3D6E7BC8E3C7BD3C5655B5AA1306775C` | Creates scope and delegation structures and their sequences/indexes only. No scope/delegation data or client FK is added. |
| `Docs/reconciliation/proposed/postgresql/039_incident_foundation_M1_EXECUTABLE_CANDIDATE.sql` | `E40D2580100BEB1F90532FA546D671F4E2B5BC5E61F1BE12E42A843E25278C28` | Minimal Incident table/sequence needed by the two Incident tests. It is not the full migration 039 and must never be recorded as migration 039 applied. Problem/Change/implementation/relation objects are excluded from core-test scope. |

The prior 034 review-only file remains unchanged at SHA-256
`08E29BD2ABA2F7F220A0099E6A2747769D2554BA707770B102D1BD68BDEA7B7C`.
Migrations 033, 035 and the full 039 review draft are unchanged. The existing
033 hash is `8ACA3EC0431A91DEE9276B7924C8F8D0FA1E4FF11647B807CE33BA41393C9ABC`.
Candidate order is 033 empty-ledger structure (required by the 034 contract,
with no history rows) → 034 → 035 → Incident-only schema slice. This does not
assert prior migration execution or make migration history known.

Core lifecycle readiness is the four named provider tests. Full M1 additionally
requires approved role/permission grants and effective membership/scope facts,
specialized Problem/Change structures only for scenarios that use those
services, and authenticated API/frontend workflows. A schema-only change cannot
authorize those paths. Before schema application, approve exact artifact
hashes, effects/order, target, recovery, and the applicable D01–D09 decisions.
Before provider tests, separately authorize the exact tests, generated IDs,
fixture writes, validity/revocation, recovery and identifier-bounded cleanup.
The lifecycle and successful Incident tests require separately approved,
explicitly authored effective authorization facts; the 034 migration must not
turn unknown grants into active ones. The repository persistence test is the
shortest first provider check once sequence state and its exact test-write and
cleanup scope have been separately reviewed and authorized.

### M1 first repository test preparation — separate approval scopes (2026-10-29)

**Sequence inspection:** `M1_SEQUENCE_SAFETY_PREFLIGHT` maps only to
`Docs/reconciliation/proposed/postgresql/M1_sequence_safety_inspection_REVIEW.sql`,
raw-byte SHA-256
`526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A`. Static
review confirms a fixed `[::1]:5432 / pa_tickets_codex_e2e / pa_tickets_codex`
identity guard, `BEGIN` plus `SET TRANSACTION READ ONLY`, bounded timeouts,
catalog/key-max reads, and final rollback. It contains no DDL, DML, grants,
sequence allocation (`nextval`/`setval`), restart, or ownership change. This is
a prepared fixed-operation runner mapping, not an operation approval. The
existing privilege-preflight and S5-03 mappings and pins are unchanged. The
sequence operation remains inactive until the separate decision below is
recorded; after that decision it is enabled for only one invocation, then
disabled again.

**Approval A — read-only sequence-state inspection:** the pending approval
fields are resolved by Owner Decision A below for one invocation only. It
authorizes no test writes or cleanup.

**Owner Decision A — APPROVED / one invocation:** Mr.H explicitly approved the
fixed operation, target, artifact hash, single-invocation validity, revocation,
and stop/no-retry conditions in the current conversation under “PA_TICKETS —
Owner Decision: Approval A.” The raw-byte artifact hash was independently
recomputed offline as
`526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A` before
activation. Approval reference: the owner's explicit message in the current
conversation; no cryptographic signature is asserted. Activation/record-entry
time for this record: `2026-10-29T11:45:04Z` (Codex record time, not an
asserted owner-message timestamp). The owner-message timestamp is unavailable.
No revocation notice was received before execution in the current conversation;
no separate revocation service is asserted. The authorization was activated
immediately before the one invocation; that activation timestamp was not
separately captured. Any failure would have consumed the invocation. The
sequence operation's code gate has been removed after this invocation. No
additional execution is authorized by this record.

**Approval A execution result (2026-10-29):** One invocation was performed
under this owner decision using the fixed .NET runner. Evidence:
`test-results/evidence/local-e2e-readonly-2d4be3b5a5a74183aa3cbb2833b35a40.json`,
raw SHA-256
`6C08A4ED158DC72C25AE1939407127DDDF170EEF2AC2B77276E084864CF9EB57`.
Started `2026-10-29T11:43:46.0682875Z`; finished
`2026-10-29T11:43:46.7696502Z`. The runner reported
`COMPLETED_READ_ONLY_ROLLED_BACK`, connection opened, read-only transaction
set, session identity verified, catalog query completed, rollback succeeded,
and connection disposed. The pinned artifact hash matched. Two result sets
returned eight rows; result digest
`CDF1CB9B1C3E9C3A98629EE207D3567EA68D82222423DF0AB1F3E6980DF8C954`.

At inspection time, all reported sequences had start/increment/cache `1/1/1`.
`PA_TICKETS_EVENT_SEQ` and `PA_TICKETS_AUDIT_SEQ` were called through value 9,
with conservative next value 10 and no mapped event/audit keys (`maximum_key`
null). `PA_TICKETS_AUDIT_MAINTENANCE_N_MAINTENANCE_ID_seq` had last value 9,
next value 10, and maximum key 9. These three generated IDs therefore showed
no allocation collision at the captured snapshot. The test's explicit ticket
ID/number does not use the corresponding ticket sequences. The ticket, event,
audit, assignment, and work-item mapped tables returned no maximum key; the
assignment and work-item sequences were uncalled at 1. The ticket number
sequence has no column default or ownership mapping, but this test supplies
the number explicitly. The Incident sequence was reported missing and is not
used by this repository test.

Ownership metadata was absent for the ticket, ticket-number, work-item, event,
audit, and assignment sequences; the maintenance sequence alone was owned by
its mapped maintenance key column. No ownership or sequence value was changed.
The result establishes sequence safety only for this captured snapshot, not a
claim about future manual-key inserts or later database activity. Approval A
is consumed. The temporary execution allowance has been removed; no retry or
second sequence inspection is authorized by it.

**Approval B — repository persistence provider test and bounded cleanup
(PENDING, independent of A):** authorize only
`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`
and its exact fixture/cleanup. The test supplies a unique `N_TICKET_ID` and
`S_TICKET_NO` (`PERSIST-{ticketId}`), requested/creator user IDs `1001`, and
creates one `PA_TICKETS` row plus its `TICKET_CREATED` event and audit row, and
inserts/locks the matching `(TICKET, ticketId)` row in
`PA_TICKETS_AUDIT_CHAIN_LOCK`. The
ticket identity and number are explicit; they do not consume
`PA_TICKETS_SEQ` or `PA_TICKETS_TICKET_NO_SEQ`. Event and audit defaults
consume `PA_TICKETS_EVENT_SEQ` and `PA_TICKETS_AUDIT_SEQ`. The test creates no
work item or assignment. Cleanup is constrained to the exact ticket ID in
`PA_TICKETS_ASSIGNMENT`, `PA_TICKETS_WORK_ITEM`, `PA_TICKETS_EVENT`, the
maintenance audit function, the exact audit-chain lock row, and
`PA_TICKETS`; the function also removes audit rows scoped to that ticket and
writes one durable audit-maintenance record, advancing its maintenance
sequence. Sequence advances are not rolled back by
PostgreSQL, even when fixture rows roll back. No broad cleanup, truncate, or
sequence repair is included.

The test now checks the generated ID/number and ticket-scoped event, work-item,
assignment, audit, and audit-lock namespaces for collision before writes,
records the generated ID/number in test output, enables cleanup only after
`AddAsync` returns successfully, and performs the exact-ID cleanup in one
transaction. If persistence or cleanup has an uncertain outcome,
stop and do not retry; retain the generated ID/number in sanitized evidence and
obtain separate read-only reconciliation authorization. Any cleanup after a
failed/uncertain cleanup requires a new explicit authorization. This test
authorization does not authorize migrations or the sequence inspection.

The 034, 035, and Incident-only executable candidates remain unchanged and
pending separate schema approval; none is implicitly required or authorized by
these two scopes.

### Owner adoption — standing local E2E development policy (2026-10-29)

**Decision: OWNER-ADOPTED, prospective, exact-body scope.** Mr. H explicitly
approved `Docs/Genesis/LOCAL_E2E_POLICY_PROPOSED.md` at raw-byte SHA-256
`A171054FB8440B06B3A15EE5242A6DF6402C31647FC128F0B8007924A24798B8` and
confirmed that `pa_tickets_codex_e2e` contains only disposable development/test
data, with no valuable customer, personal, or production data requiring
preservation. Approval reference: Mr. H's explicit message in the current
conversation containing that approval and confirmation. The message's precise
timestamp is unavailable; canonical record-entry time is
`2026-10-29T12:02:23Z` and is not represented as the owner's message time.
No cryptographic signature is asserted.

The scope is limited to `[::1]:5432`, database `pa_tickets_codex_e2e`, role
`pa_tickets_codex`, and the existing explicit local credential configuration.
The adopted policy permits its fixed pinned read-only catalog/sequence
inspections and bounded named provider tests after current target, schema,
sequence, fixture-isolation and cleanup preconditions are checked. Provider
tests may write only their uniquely identified fixtures and clean only those
fixtures; durable audit-maintenance rows and nontransactional sequence
advances must be reported. Stop on uncertain persistence or cleanup and do not
retry or broaden cleanup. Application authentication/RBAC, audit-chain
integrity, and exact-target protections remain in force.

This adoption does not authorize schema migration execution: each migration
batch still needs one explicit owner approval naming exact artifacts/hashes,
order, expected effects, preconditions and recovery. Destructive operations
remain separately approved. Production, shared, staging, and other databases
are excluded. This entry does not alter the previous one-shot Approval A or
its successful sequence-inspection evidence; that authorization remains
consumed. The policy's adoption does not itself mark S5-01, S5-03 or M1
complete.

**First provider test:** the owner-authorized standing scope now covers
`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`
once its existing preconditions and guard variables are satisfied. The test
remains unrun as of this record. Its exact-ID fixture/collision checks and
bounded cleanup are documented above; no migration or other provider test is
included in this first invocation.

### Standing local E2E development policy proposal review (2026-10-29)

Reviewed `Docs/Genesis/LOCAL_E2E_POLICY_PROPOSED.md` against the current
SOP/SLICES/FRD, this decision pack, the local runner, and the prepared first
repository-test fixture. Its raw-byte SHA-256 is
`A171054FB8440B06B3A15EE5242A6DF6402C31647FC128F0B8007924A24798B8`.
Status remains **PROPOSED / NOT ADOPTED**. This record does not activate
provider-test writes, fixtures, cleanup, migrations, or destructive work.

The proposed boundary is the fixed local E2E endpoint `[::1]:5432`, database
`pa_tickets_codex_e2e`, role `pa_tickets_codex`, with the existing explicit
`PA_TICKETS_E2E_DB_PASSWORD` loader and no fallback. It preserves the two
already adopted read-only inspection operations, their pins and evidence; the
successful one-shot sequence inspection Approval A remains consumed and its
evidence/hash above remains historical. It proposes standing permission for
named bounded provider tests with unique test-owned fixture IDs, checked
collision namespaces, exact-ID cleanup, recovery/evidence and disclosure of
nontransactional sequence and durable audit-maintenance effects. It keeps
application authentication/RBAC, audit-chain integrity, exact-target checks,
and all nonlocal safeguards unchanged. Schema changes still require one
explicit owner approval per reviewed exact-hash migration batch. Destructive
operations remain separately approved; broad truncate/drop, sequence reset,
and database recreation are not allowed by this proposal.

The proposal is materially consistent with SOP/SLICES/FRD's local-only
direction, but those files currently label it proposed/not effective and S5-05
remains blocked. No policy document or runner gate was changed to make the
standing write scope active. The current first repository test already has a
bounded fixture and cleanup implementation; its offline source is prepared,
but it has not been run against PostgreSQL. The completed sequence inspection
reported no collision for that test at its captured snapshot only. The
database has 171 public objects; no evidence in this review establishes that
its contents satisfy the proposal's assumption that it contains no valuable
customer, personal, or production data.

**Exact outstanding owner decision:** approve or reject the unchanged policy
body at the hash above, limited to the stated target and named local provider
tests/fixture cleanup, and confirm before activation that this database is
disposable development/test data with no valuable customer, personal, or
production data. Approval of this standing scope would not approve any schema
migration batch or destructive operation. No approval reference or timestamp
is inferred. Upon explicit adoption, reconcile only the local S5-01/S5-05
wording and enable the bounded provider-test path; keep all other environments
and operations under their existing controls.

**First test after adoption:**
`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`.
The fixture uses a generated `N_TICKET_ID` and `PERSIST-{ticketId}` number;
checks ticket, work-item, assignment, event, audit and audit-lock collisions
before writes; and cleans only rows for that exact ID. Cleanup invokes the
existing audit-maintenance function, which leaves one durable maintenance
record and consumes its sequence; PostgreSQL sequence advances are not rolled
back. If persistence/cleanup outcome is uncertain, stop without retry or
additional cleanup and obtain separately authorized read-only reconciliation.
The exact test command is
`dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`.
Do not run it until the standing write scope is adopted or a separate Approval
B authorizes this exact test and cleanup.

### Standing local E2E policy adoption (2026-10-29)

**Decision: OWNER-ADOPTED.** Mr. H explicitly adopted the unchanged
`Docs/Genesis/LOCAL_E2E_POLICY_PROPOSED.md`, raw-byte SHA-256
`A171054FB8440B06B3A15EE5242A6DF6402C31647FC128F0B8007924A24798B8`, and
confirmed that `pa_tickets_codex_e2e` contains only disposable development/test
data, with no valuable customer, personal, or production data requiring
preservation. Approval reference: Mr. H's explicit message in the current
conversation. Its message timestamp is unavailable; this record was entered at
`2026-10-29T12:03:17Z`, which is not asserted as the owner's approval time. No
cryptographic signature is asserted.

The adopted standing scope is prospective and limited to
`[::1]:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, using existing local
credential handling. It covers the proposal's fixed pinned read-only
inspections and bounded named local provider tests after per-test target,
schema, sequence, fixture-isolation and cleanup preconditions. Test cleanup
may touch only that test's uniquely identified fixtures; durable
audit-maintenance effects and nontransactional sequence advances must be
reported. Uncertain writes or cleanup stop without retries or broader cleanup.
Application authentication/RBAC and audit-chain integrity remain mandatory.
One explicit owner approval for each reviewed exact-hash migration batch is
still required; destructive operations remain separately approved. All other
environments and targets are excluded. This adoption does not alter the
consumed one-shot Approval A, its evidence, or any migration artifact, and it
does not mark S5-01, S5-03, or M1 complete.

The owner-approved standing policy now covers the first bounded provider
invocation, `Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`,
once the existing test guard variables match the approved target. The test
has not been run as part of this adoption record.

**Lifecycle reference preflight operation activation (2026-10-29):** The
adopted standing local E2E policy above covers additions to the fixed read-only
inspection allowlist when the SQL artifact is reviewed and hash-pinned and
focused guard tests pass. `M1_LIFECYCLE_REFERENCE_PREFLIGHT` is therefore
enabled in the native runner for the exact target
`[::1]:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, artifact
`Docs/reconciliation/proposed/postgresql/M1_lifecycle_reference_and_sequence_preflight_REVIEW.sql`,
SHA-256 `C719C6C0204527F262FAB51A3C4138FC09057744990DF242F232BA9FC88AE47D`.
It remains one explicit read-only invocation with same-connection identity and
read-only checks, timeout, rollback, connection disposal, and sanitized result
capture. This activation authorizes only that fixed read-only preflight under
the standing policy; it does not authorize migrations, fixture writes, grants,
or provider tests. No live preflight was performed by this record.

**First provider invocation attempt — pre-connection guard rejection
(2026-10-29):** The owner-directed single-test command was started with the
adopted policy hash verified and the exact approved target descriptor set in
the process. `PA_TICKETS_TEST_CONNECTION_STRING` was present, but
`PostgreSqlTestEnvironment` rejected its parsed target as a prohibited
database or role before connection creation. No PostgreSQL connection was
opened, no test fixture ID was generated, and no database write or cleanup
occurred. The actual connection string and credential were not recorded or
printed. The temporary test opt-in and approved-target environment variables
were cleared in `finally`. The test result is **BLOCKED / NOT RUN AGAIN**;
there is no database evidence or test pass. Record-entry time:
`2026-10-29T12:07:41Z` (the test process did not emit an execution timestamp).
The next owner action is to correct the existing process connection-string
configuration so its Npgsql-parsed, non-secret descriptor is exactly
`host=::1;port=5432;database=pa_tickets_codex_e2e;user=pa_tickets_codex`,
then explicitly invoke the same single test once. Do not disable or bypass the
guard.

**First repository persistence provider test — PASS (2026-10-29):** The
connection-source issue was resolved for this invocation without changing the
test guard or persistent configuration. Npgsql parsed the inherited process
connection variable as `localhost:5432 / pa_tickets_h_verify /
pa_tickets_test_user`, which the guard correctly rejected. The temporary test
process instead received a connection string built in memory from
`appsettings.json` with `appsettings.Development.json` precedence and
`PA_TICKETS_E2E_DB_PASSWORD` from `.env.test.local`. Npgsql verified
`::1:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, SSL disabled. No secret
or completed connection string was printed or persisted. The parent process
environment and local files were not changed.

Exactly one test ran: `Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`;
1 passed, 0 failed, exit code 0. The test persisted and reloaded ticket
`1793276001988800` / `PERSIST-1793276001988800`, verified its ticket fields,
single `TICKET_CREATED` audit row, and creation history. Its exact-ID cleanup
transaction completed and committed; no uncertain outcome or retry occurred.
Cleanup targets assignment/work-item/event rows for the fixture ID, invokes
`pa_tickets_maintenance_delete_audit_for_ticket`, deletes the exact ticket
audit-chain lock, and deletes the ticket row. The test creates no work item or
assignment. The maintenance function's durable maintenance-audit record is an
expected residual effect; no post-cleanup readback was performed. Event, audit
and maintenance sequences advanced; exact post-run sequence values were not
captured and none was manually reset or advanced. Ticket ID and number were
explicit, so the ticket ID/number sequences were not consumed.

Sanitized execution summary:
`test-results/evidence/m1-repository-persistence-20261029.json`, SHA-256
`77BB6028B97B5C0E256F107027850F90D05062DFAB3D5A9405E200A280AEADB0`.
It records the result and limitations; exact UTC test start/end were not
emitted by the runner. Record-entry time: `2026-10-29T12:16:03Z`.

**Acceptance:** the first PostgreSQL-backed repository persistence and reload
test is satisfied. This does not establish core lifecycle, Incident atomicity,
API/frontend, or full M1 acceptance.

### M1 lifecycle reference and sequence preflight (2026-10-29)

Owner-invoked operation `M1_LIFECYCLE_REFERENCE_PREFLIGHT` completed at
`2026-10-29T12:36:09.5601389Z` against
`[::1]:5432 / pa_tickets_codex_e2e / pa_tickets_codex`. Start time was
`2026-10-29T12:36:09.032006Z`. The pinned SQL artifact
`Docs/reconciliation/proposed/postgresql/M1_lifecycle_reference_and_sequence_preflight_REVIEW.sql`
matched SHA-256
`C719C6C0204527F262FAB51A3C4138FC09057744990DF242F232BA9FC88AE47D`.
Evidence:
`test-results/evidence/local-e2e-readonly-84da991294bf47968862fccf77e4936b.json`,
SHA-256 `DEBA3EFBD05C89832ED577D30E013D7B13BDCC0D68A530C6ECAA08C3B2BDAAC2`;
sanitized result SHA-256
`05549A72412A8BCD26A75946D968276AC72E31FCD7526E9298EC4DB430804C7E`.
Outcome was `COMPLETED_READ_ONLY_ROLLED_BACK`: connection opened, same-session
identity and read-only transaction verified, five result sets/13 rows
completed, rollback succeeded, and connection was disposed.

Prerequisite results from the captured rows:

- **Actor 1001: FAIL.** The actor query returned no row; existence and active
  state are not established. Do not use this actor or create a real-person
  identity by inference. A deterministic disposable test-user fixture is
  required before the lifecycle provider test.
- **Statuses: PASS.** `NEW` (ID 1, active, initial), `TRIAGE` (ID 2, active),
  and `ASSIGNED` (ID 3, active) were present.
- **Transitions: PASS.** `NEW -> TRIAGE` and `TRIAGE -> ASSIGNED` were allowed
  with null conditions.
- **Priority: PASS.** Priority ID 2 (`MEDIUM`) was active and default.
- **Sequence collision check: PASS at the recorded snapshot.** Ticket ID
  sequence next value was 5 with no current ticket maximum; ticket-number next
  value was 5 and maximum numeric suffix 4; work-item and assignment sequences
  were uncalled at 1 with no reported maximum; event and audit sequences next
  values were 11 with no reported maxima; maintenance sequence next value 11
  exceeded maximum 10. The report found no collision. It does not reserve these
  values or protect against concurrent writers. No sequence was changed.
- **Sequence ownership: REVIEW.** The audit-maintenance sequence was owned by
  its column. The other six inspected sequences had no reported owned-by
  relation/column; this was observed, not changed, and is not itself evidence
  of an allocation collision.

The next lifecycle attempt remains blocked by the absent actor fixture and the
unimplemented FR-021 rule evaluator at the time of the inspection. No
reference data, authorization fixtures, migrations, or provider tests were
written or run by this inspection.

### M1 FR-021 own-team assignment source implementation (2026-10-29)

Source now performs assignment checks in distinct layers: it requires a
positive authenticated actor and persisted source-ticket ID; checks effective
`TICKET_ASSIGN_TEAM` on the source ticket before resolving destinations; checks
`TICKET_UPDATE`; validates an active destination team and any named assignee's
active department/team membership; then checks assignment permission and
update permission against the destination resource. Finally it evaluates the
latest effective persisted `ASSIGN` + `OWN_TEAM` rule and requires an active,
effective TEAM scope linked to one of the actor's active memberships. It does
not use `N_MANAGER_USER_ID` or role-name bypasses. Unsupported relations,
delegate mode, notification, manager acceptance, missing/revoked/expired rules,
and missing scope fail closed. Incident creation now performs commander
assignment authorization against the newly persisted ticket ID within its
existing transaction, rather than passing placeholder ticket ID 0.

The permission catalogue definition for source reconciliation is
`TICKET_ASSIGN_TEAM / TICKETS / ASSIGN / RECORD / BUSINESS / N_READ_ONLY=false`.
This is a permission definition only. It is not implicitly granted to USER,
ADMIN, ADMIN_HEAD, SUPER_ADMIN, or any other role. Existing unknown grants stay
unknown/inactive. The current FR-021 schema addition is a separate review-only
candidate, not part of the approved 034/035 batch:
`Docs/reconciliation/proposed/postgresql/FR21_assignment_authority_rules_M1_REVIEW.sql`,
SHA-256 `6662E8B6CF9E1BA3ED7E8200C184168F7DB6C8550A0B7D7F022AA0A7DF0C2B19`.
It creates the rule relation and sequence only; it seeds no catalogue,
permission, rule, grant, membership, or scope rows. The existing 034 and 035
candidate bytes remain unchanged at hashes
`54C60EA8C53525FB46DC1539F100A800CEE656F5BC5E53807DED582E81F02C82` and
`247E77707A282CFB7CEA348120B46A687E947488E8558A14FE139629C03EAA13`.

Offline validation:
`dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~Fr021AssignmentRuleEvaluatorTests|FullyQualifiedName~DatabaseTicketAssignmentAuthorizationTests"`
passed 9/9. API build succeeded with 0 warnings and 0 errors; `git diff --check`
passed. No provider test or PostgreSQL operation was run. FR-021 remains
PARTIAL: only the own-team explicit-assignment shape is implemented in source;
full FRD rules R1/R3, delegation, notifications, and acceptance behavior remain
unimplemented.

Lifecycle provider fixture requirements remain unexecuted. Actor ID 1001 was
absent in the preflight, so a disposable test-user row must first be created
under the adopted local test-write scope. Then a temporary test role and
effective membership may receive only explicit `TICKET_READ`, `TICKET_UPDATE`,
and `TICKET_ASSIGN_TEAM` grants after the canonical permission definition is
present and approved; no unknown historical grants are activated. Add exact
ticket-record scopes for generated ticket IDs and an explicit, effective TEAM
scope for the intended own team. Add one enabled/effective version-1
`TICKET_ASSIGN_TEAM / OWN_TEAM / ASSIGN` rule with notification and acceptance
false. The target team and named assignee must be active and match the selected
department/team. No scope or grant may cover unrelated ticket IDs or teams.
Use transactional setup and exact-ID cleanup; retain audit-chain/maintenance
records and account for sequence consumption. Sequence collision results are
snapshot-only and must be rechecked immediately before approved writes.

**Readiness:** reference statuses, transitions, priority and captured sequence
collision check passed, but the lifecycle provider test is **BLOCKED** pending
actor fixture, the unexecuted 034/035 schema batch, the separate FR-021 rule
table candidate and approved catalogue definition, plus narrowly scoped test
fixtures. No migration or provider test is authorized by this source change.

### M1 lifecycle schema and generated fixture preparation (2026-10-29)

Review-only batch order for one future owner decision:

1. 034 permission/grant metadata —
   `034_rbac_permission_metadata_M1_CORE_LIFECYCLE_REVIEW.sql`, SHA-256
   `54C60EA8C53525FB46DC1539F100A800CEE656F5BC5E53807DED582E81F02C82`.
   Adds metadata/constraints/index only, preserving IDs and rows and making
   historical grants UNKNOWN/inactive.
2. Canonical M1 permission metadata —
   `M1_TICKET_ASSIGN_TEAM_PERMISSION_CATALOGUE_REVIEW.sql`, SHA-256
   `B9FDD7D00DA392642359C9F442E71E01806036944CAFA466A3B9FF3BE7D7F10D`.
   Defines `TICKET_ASSIGN_TEAM / TICKETS / ASSIGN / RECORD / BUSINESS / false`,
   fills only missing metadata for TICKET_READ and TICKET_UPDATE, inserts no
   role grants, and stops on duplicate/conflicting/inactive catalogue facts.
3. 035 scope-only foundation —
   `035_rbac_role_scopes_M1_CORE_LIFECYCLE_REVIEW.sql`, SHA-256
   `247E77707A282CFB7CEA348120B46A687E947488E8558A14FE139629C03EAA13`.
4. FR-021 assignment-rule relation —
   `FR21_assignment_authority_rules_M1_REVIEW.sql`, SHA-256
   `6662E8B6CF9E1BA3ED7E8200C184168F7DB6C8550A0B7D7F022AA0A7DF0C2B19`.

Each SQL artifact has its own transaction; the batch is ordered, not globally
atomic. Before each artifact, verify its exact raw-byte hash, fixed target,
prerequisite definitions, object absence/compatibility, and sequence safety.
If any artifact fails or its commit outcome is uncertain, stop and perform
separately authorized read-only reconciliation before any retry. Do not create
033 history, seed grants/memberships/scopes/rules beyond the explicitly
bounded test fixture, or modify 026/038. No candidate was applied.

The canonical metadata contract is consumed by the existing authorization
reader: metadata must be active and exact; role grants must be ACTIVE, active,
effective-dated and provenance-bearing; permission metadata alone never
authorizes. `RoleAuthorizationPolicy` now prohibits USER from receiving
`TICKET_ASSIGN_TEAM` under FR-021 R4. Other role codes receive no bypass: they
need explicit effective grants, exact record scope, team scope and a valid
persisted assignment rule.

The core lifecycle provider test now prepares a generated test actor, target
assignee, department, team, custom role, effective membership, three explicit
grants, own-team scope, per-created-ticket RECORD scopes and a version-1
OWN_TEAM/ASSIGN rule. It allocates only from fixed named sequences and stops
if a returned candidate is not above the existing key maximum; consumed
sequence values are not rolled back or retried. Cleanup is transactional and
restricted to captured test IDs, first removes fixture ticket dependents and
uses the audit-maintenance function, then removes only the generated rule,
scopes, grants, membership, role, users, team and department. Permission
catalogue rows are intentionally retained as shared reference data. No
provider test was run.

Before fixture writes, the provider test now selects one active, published,
effective request type joined to an active/effective service and category; it
fails before setup if no valid combination exists. The earlier reference
preflight did not capture these catalogue rows, so their current live
availability remains UNKNOWN. Priority ID 2 and the required statuses and
transitions were confirmed by the completed preflight.

Focused offline command covering evaluator, effective scope/membership,
permission ceiling, catalogue artifact safety/hash, and test-fixture source
compilation passed 73/73:
`dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~Fr021AssignmentRuleEvaluatorTests|FullyQualifiedName~DatabaseTicketAssignmentAuthorizationTests|FullyQualifiedName~RoleAuthorizationPolicyTests"`.
The API build passed with 0 warnings/errors and `git diff --check` passed. No database connection, migration,
grant, fixture setup, provider test or cleanup was executed. At the time this
preparation section was written, the batch awaited explicit owner adoption;
the standing local E2E test policy did not itself adopt schema changes. The
subsequent adoption and current execution gate are recorded below.

### Four-artifact M1 schema batch — owner adoption and pre-execution review (2026-10-29)

**Owner adoption:** Mr. H explicitly adopted and authorized the exact four-
artifact scope in the current conversation, bound to `[::1]:5432 /
pa_tickets_codex_e2e / pa_tickets_codex`, in the order and with the hashes
listed above. Approval reference: owner's explicit adoption message in the
current conversation. Adoption recorded at `2026-10-29T13:08:42Z` (UTC); this
is the record-entry timestamp and is not represented as a cryptographic
signature. Scope remains limited to the four named artifacts and the stated
preconditions/exclusions.

**Artifact verification:** all four raw-byte SHA-256 values matched the
adopted values before any database change. `git diff --check` passed (Git
reported existing LF-to-CRLF working-copy notices).

**Fresh read-only evidence:**
- S5-03 fingerprint: `test-results/evidence/local-e2e-readonly-d05af4552bef4fdf8c1486624acb3510.json`, SHA-256 `350CA746C4A8AE0A3125B72BBC26E64F1093342E2DF5E9467D684FF5D18017DA`. Completed at `2026-10-29T13:07:36.7570513Z`; fixed target/role verified; read-only transaction, catalog query, rollback and disposal all succeeded. It captured 13 result sets / 1,450 rows. It confirms the 034 metadata columns and the 035 scope and FR-021 rule objects/sequences are absent; required parent RBAC/user/membership relations are present. Migration history remains `UNKNOWN`. This catalog snapshot does not establish sequence current allocation state.
- Lifecycle reference/sequence preflight: `test-results/evidence/local-e2e-readonly-115ba742525e4280bc04b8e482c32a85.json`, SHA-256 `F78A32E64108D73AB1BF504F4B15CD4A80EBDCA120A9DC142F3D3AB24202B0FE`. Completed at `2026-10-29T13:08:13.5350217Z`; fixed target/role verified; read-only transaction, catalog query, rollback and disposal all succeeded. Statuses `NEW`, `TRIAGE`, `ASSIGNED`, the `NEW -> TRIAGE` and `TRIAGE -> ASSIGNED` transitions, and default priority were returned. Actor 1001 was absent; current fixture code uses generated isolated actors. The captured sequence result set contains seven sequences and does not include `PA_TICKETS_PERMISSION_SEQ`.

**Pre-execution findings (2026-10-29):** a fresh fixed-target read-only
preflight is recorded at
`test-results/evidence/local-e2e-readonly-bfab310fa23a4572940004ac4ddcb131.json`,
SHA-256 `0A989FBA8FE2CC386DB37C431D3F31D16EA23C7396CB326204077AC2F2A9CB2F`,
executed at `2026-10-29T13:12:16.8410241Z` using the pinned artifact
`M1_permission_catalogue_sequence_preflight_REVIEW.sql`, SHA-256
`795838E1CE3A02ADFBBE755197DB2228D52ED16C457046F57818E3912DC88609`.
The operation completed read-only with exact target identity, transaction
read-only `on`, successful rollback and disposal. It reported the permission
sequence uncalled at start/current value `1`, increment/cache `1/1`, and
permission maximum `NULL`; the conservative next value is `1` and does not
conflict with an existing key. The sequence has no `OWNED BY` association,
while the column default references it. No sequence was advanced or reset.
The same evidence reports zero rows for `TICKET_READ`, `TICKET_UPDATE`, and
`TICKET_ASSIGN_TEAM` (no duplicates, but the two required existing rows are
absent).

**Execution status: BLOCKED BEFORE ARTIFACT 1.** The exact adopted catalogue
artifact explicitly requires exactly one existing `TICKET_READ` and
`TICKET_UPDATE` row and rejects otherwise. The live table has neither. The
approved batch only completes missing metadata for compatible existing
`TICKET_READ`/`TICKET_UPDATE` rows; it does not authorize creating those
permission definitions. Starting 034 would commit a partial batch that is
known to fail at artifact 2, so no artifact was run. No migration outcome is
claimed; migration history remains `UNKNOWN`.

**Next gate:** the owner must separately decide whether to approve an exact,
reviewed additive catalogue artifact defining `TICKET_READ` and
`TICKET_UPDATE` (without role grants or activation of historical grants), or
identify another authoritative source that supplies those rows. Any new
artifact requires its own hash review and authorization before execution. Once
the two permission definitions exist with compatible active/code semantics,
re-run the read-only row/sequence preflight, then verify all four batch
preconditions and hashes immediately before artifact 1. After each artifact
commits, independently verify its expected schema effects before continuing.
On any failure or uncertain commit, stop and reconcile read-only before retry.

**Lifecycle fixture readiness:** no fixture, grant, schema or provider-test
write has been performed. After the batch is successfully applied and
verified, recheck the reference rows and sequence state needed by the test,
then use only generated test-owned identities, explicit temporary grants and
bounded exact-ID cleanup under the adopted local E2E testing policy.

### M1 permission catalogue bootstrap successor (2026-10-29)

To resolve the confirmed empty-catalogue dependency without editing any of the
four adopted artifacts, a distinct review-only artifact was prepared:

`Docs/reconciliation/proposed/postgresql/M1_TICKET_READ_UPDATE_PERMISSION_BOOTSTRAP_REVIEW.sql`

Raw-byte SHA-256:
`182A710F31BD27E8111932701F099C56D795D38C83A69318DEF4920C9DF4A8F3`.

The latest fingerprint reports the existing permission table columns as
`N_PERMISSION_ID bigint NOT NULL`, `S_PERMISSION_CODE varchar(150) NOT NULL`,
`S_PERMISSION_NAME varchar(250) NOT NULL`, nullable `S_DESCRIPTION text`, and
`N_ACTIVE boolean NOT NULL DEFAULT true`; the five metadata fields required by
034 are absent. Migration 034 must therefore precede this bootstrap. The
canonical draft catalogue in migration 031 and the current EF reader agree on
these definitions:

| Code | Module | Action | Scope | Security | Read-only |
|---|---|---|---|---|---|
| `TICKET_READ` | `TICKETS` | `VIEW` | `RECORD` | `READ_ONLY` | true |
| `TICKET_UPDATE` | `TICKETS` | `EDIT` | `RECORD` | `BUSINESS` | false |

The bootstrap inserts only missing rows for those two codes, using the
existing permission sequence. It rejects duplicate codes, inactive or
conflicting existing definitions, incompatible key/default/metadata shape,
and a sequence allocation that could collide with existing permission IDs.
It locks the permission table during the transaction, verifies postconditions,
and rolls back on any failure. It creates no `TICKET_ASSIGN_TEAM` row, grants,
memberships, scopes, roles, or migration-history entries. It must be placed
immediately after 034 and before the existing permission-catalogue artifact.
Because `nextval` is nontransactional, a failed/rolled-back bootstrap may
consume one or two sequence values; record that outcome and perform a fresh
read-only sequence/key check before any newly authorized retry. Never reset
the sequence to remove gaps.

**Revised proposed five-artifact order (not yet authorized):**

1. 034 metadata — unchanged hash `54C60EA8C53525FB46DC1539F100A800CEE656F5BC5E53807DED582E81F02C82`.
2. New `TICKET_READ`/`TICKET_UPDATE` bootstrap — hash `182A710F31BD27E8111932701F099C56D795D38C83A69318DEF4920C9DF4A8F3`.
3. Existing `TICKET_ASSIGN_TEAM` catalogue — unchanged hash `B9FDD7D00DA392642359C9F442E71E01806036944CAFA466A3B9FF3BE7D7F10D`.
4. 035 scope-only schema — unchanged hash `247E77707A282CFB7CEA348120B46A687E947488E8558A14FE139629C03EAA13`.
5. FR-021 rule schema — unchanged hash `6662E8B6CF9E1BA3ED7E8200C184168F7DB6C8550A0B7D7F022AA0A7DF0C2B19`.

The owner's prior four-artifact adoption remains attributable to the four
unchanged files and their stated effects, but it does not authorize the new
bootstrap or this changed five-step order. Execution is not authorized until
the owner explicitly approves the new artifact's exact hash and the revised
ordered batch. The prior four-artifact approval must not be treated as cover
for this added operation. No database connection or migration was performed
while preparing this successor.

Focused offline validation passed 22/22 tests covering the new artifact,
the existing assignment-permission catalogue artifact, and canonical-code
reader behavior. `git diff --check` passed with existing line-ending notices.
