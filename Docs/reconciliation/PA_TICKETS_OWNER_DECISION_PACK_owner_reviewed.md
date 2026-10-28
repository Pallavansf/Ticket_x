# PA_TICKETS Owner Decision Pack

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Purpose:** owner completion of the decisions required before a PostgreSQL
schema-change request can be considered.

This pack records decisions, owner directions, and evidence. **The owner has
approved the policy direction across this pack on 28 October 2026**, as communicated
by Mr. H (Genesis, Developer & DBA). This is an **owner-declared approval of
policy choices**, NOT independent identity verification, approval of a grant-matrix
version, acceptance of missing technical evidence, or authorization to connect to
a database. Formal decision gates retain their evidence requirements; no schema
operation, migration, role/grant provisioning, provider test, or migration 026
is authorized by this document alone.

**Approval classification — applicable throughout this document:**
- **OWNER-APPROVED POLICY DIRECTION:** D01–D10 and the conservative V1 choices,
  only to the extent each is specifically articulated and not marked unresolved.
- **FORMAL / EVIDENCE-GATED:** A decision remains pending for implementation or
  external reliance wherever exact matrices, compatibility, independent review,
  signed operation scope, or other stipulated evidence is missing.
- **NOT AUTHORIZED:** all database/SQL execution, schema application, provider
  writes/cleanup, and migration 026. No blanket approval is implied.
- **Attribution:** Mr. H; Genesis; Developer & DBA; owner-stated approval date
  28 October 2026. Date is not a verified UTC timestamp; an exact independent
  signature/reference and separate independent reviewer have not been supplied.

Existing historical statements that a choice was only proposed describe the
status *before* this owner endorsement. Their technical caveats remain effective.

## Owner-selected intended V1 policy

The owner **approves the conservative intended V1 policy direction** listed below
for design and review. This endorsement does not independently establish a
signature, immutable approved RBAC grant matrix, technical compatibility,
operational authorization, or formal acceptance where prerequisites remain.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D01 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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
**Decision:** `APPROVED`  
**Rationale/evidence reference:** No approval record found.  
**Owner identity:** Mr.H  **Date:** 28/oct/2026 **Scope:** 30/NOV/2026  **Approval reference/signature:** Mr.H  
**Blocks:** All schema-change requests and migration 033 ledger adoption.

**Proposed decision (Slice 35 recommendation; not approval):** Adopt Strategy A: use the historically verified foundational artifact tests/scripts/postgresql-foundational-ddl.sql at Git commit 1bcc493d267e23e3e126b9b698fa4344ebe8588a as the baseline, identified by SHA-256 (raw file bytes) 4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19. Keep current expanded worktree additions as separately versioned changes unless independently reviewed and approved.

**Rationale and trade-offs:** Slice 33 verified the historical bytes and Git path; Slice 34 found that the expanded artifact mixes source-required additions with DML, setval operations, reference data, and privileged audit-maintenance behavior. Strategy A preserves reproducible provenance and narrower approval scope while requiring an explicit additive sequence for current lifecycle compatibility. The untraceable 5CAA... value is not a candidate artifact.

**Explicit exclusions:** This proposal authorizes no schema execution, migration, seeding, sequence operation, grant, privilege change, cleanup, or provider test. It does not assert live catalog equivalence or resolve migration history.

**Owner attestation (required to change Decision from PENDING):**
- Response (APPROVE, REJECT, or DEFER): APPROVE
- Owner identity and role:Mr.H , Dev & DBA
- UTC decision date: 28/oct/2026
- Decision scope/expiry: 30/NOV/2026
- Approval reference or signature: Mr.H
- Rationale and evidence references: ____________________

**Technical validation status:** Raw-byte hash and Git provenance are source-verified. Live catalog equivalence, sequence state/ownership, operator privileges, and provider behavior remain unverified and require separate TGT/OP/PRE authorization.

**User-confirmed decision input (Slice 58; not formal approval):** The user explicitly confirmed the recommended `APPROVE` selection for review, subject to verification of the exact foundational baseline and its compatibility with the existing database. The formal `Decision` remains `PENDING`; owner identity/role, UTC decision date, scope/expiry, approval reference or signature, and technical validation evidence are still required.

**Explicit owner-supplied decision (Slice 67; formal evidence incomplete):**
Owner identity: `Mr. H`; role: `SE`. The owner explicitly approves the
recommended D01/BASE decision in principle, subject to exact baseline and
compatibility verification. No approval reference/signature, UTC date, expiry,
or compatibility evidence was supplied. Formal `Decision` remains `PENDING`.
### D02 — Least-privilege schema operator *(prerequisite)*

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D02 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

**Question:** Approve a dedicated non-superuser operator, or document a
strictly scoped exception for `postgres`  
**Why:** T62A.2 found `postgres` is superuser; this exceeds minimum authority.  
**Options:** Dedicated role with CONNECT, target-schema USAGE/CREATE, and
required object/sequence rights; time-bounded exception with explicit scope;
defer all operations.  
**Evidence:** T62A.2 privilege metadata and DDL privilege inventory.  
**Owner role:** Security/database authority.  
**Decision:** `APPROVED`  
**Rationale/reference:** No approval found.  
**Owner identity:**Mr.H  **Date:** 28/OCT/2026  **Scope:** 30/NOV/2026  **Approval reference/signature:** Mr.H  
**Blocks:** Any schema operation.

### D03 — Sequence allocation safety *(prerequisite)*

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D03 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D04 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D05 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D06 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D07 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D08 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D09 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner policy disposition (28 October 2026):** **APPROVED IN PRINCIPLE** by Mr. H, Genesis, Developer & DBA, for the conservative D10 direction described in this pack. This is an owner-supplied decision, not an independently authenticated signature. Existing formal `Decision` and technical/operational gates below remain controlling until their distinct evidence requirements are met. No database operation or data/grant change is authorized.

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

**Owner disposition:** **NOT APPROVED FOR EXECUTION**. This is outside the blanket policy endorsement and retains its independent gate.


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

### S5-01 E2E operation authorization packet — owner completion required

**Owner endorsement:** The local E2E target and the four-scope authorization *design* are **APPROVED AS POLICY DIRECTION** by Mr. H (Genesis, Developer & DBA) on 28 October 2026. **No operation-specific permission is issued**: identity/peer assurance, immutable artifact approval, signed scope, expiry/revocation, recovery, evidence custody and independent review remain unresolved.


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
| S5-03 read-only fingerprint | **NOT AUTHORIZED** | Owner-signed S5-01 packet naming this read-only operation and exact artifact/hash; independently evidenced operator and target identity; least-privilege scope; peer/TLS policy; validity/expiry and revocation; recovery/uncertain-outcome handling; evidence custody. |
| S5-10 schema application | **NOT AUTHORIZED** | Separate signed schema-application scope naming each final artifact/hash, expected effects and order; D01 adoption and prerequisite decisions; operator/target/grant evidence; validity/revocation; recovery and evidence custody. |
| Provider tests and cleanup | **NOT AUTHORIZED** | Separate signed test-write scope naming exact tests, generated identifiers, allowed writes and cleanup boundaries, plus operator/target/grant evidence, validity/revocation, recovery and evidence custody. |

#### Proposed reusable E2E authorization profile (PENDING governance review)

**Purpose:** enable bounded development on the owner-declared dedicated E2E
instance without converting environment intent into standing database access.
This is a proposal only. It does not amend SOP v3, authorize an operation, or
change D01/S5-01/S5-03/S5-10 status.

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

**Trust and governance gap:** review found no existing trustworthy mechanism
that verifies approver/operator signatures and current revocation status for a
machine-consumable profile, nor a client-side peer/TLS guard suitable for the
runner. The existing S5-03 runner therefore remains fail-closed. Do not build or
enable an execution validator until the security/database authorities select
and approve the trust root, signed profile format, revocation source/freshness,
identity verification method, and connection guard. A profile containing
self-asserted fields is not sufficient evidence.

**Offline implementation evidence (2026-10-27):**
`tests/scripts/invoke-postgresql-foundational-fingerprint.ps1` now validates
explicit target/profile structure and the pinned S5-03 artifact. It marks
repository/user-supplied identity, signature, and revocation references as
unverified and still has no database-client execution path. The focused command
`dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~PostgreSqlFoundationalPreflightScriptTests`
passed 22 tests. This is source-level/offline evidence only; no provider result
or authorization was established.
**Proposed change path:** SOP v3 does not specify a distinct amendment workflow.
Use the existing owner decision pack to record the authorized project owner's
amendment decision and attribution, with database/security review and an
independent reviewer. Only after that approval should a versioned SOP amendment
be prepared, reviewed for consistency with FRD/SLICES/roadmap, and adopted with
its approval reference and effective date. No approver identity or approval is
inferred here.

**Approval required:** project governance owner for any SOP amendment; database
and security authorities for the operation profile and trust/least-privilege
controls; independent reviewer for identity, revocation, and connection-guard
evidence. The owner-supplied accountable approver is `Mr. H`, Genesis,
Developer & DBA; identity remains independently unverified. UTC approval date,
exact profile version/scope, signature or approval reference, rationale, and
evidence references remain outstanding.
#### Target and identity

| Field | Prepared value | Owner evidence still required |
|---|---|---|
| Host / port | `localhost:5432` (owner-identified target) | Resolve and approve the actual endpoint/peer identity, address family, and hostname/peer/TLS policy for this operation. A prior owner-supplied report states `::1` and SSL off; it is not independently verified here. |
| Database / role | `pa_tickets_codex_e2e` / `pa_tickets_codex` | Fresh, independent target and role identity evidence; verify the dedicated role's current least-privilege grants and scope. The role name alone does not establish operator identity. |
| Accountable approver / operator | Owner-supplied: `Mr. H`, Genesis, Developer & DBA; identity is not independently verified. | Verified identity and evidence reference; UTC approval timestamp; signed/attributable approval reference; operation validity and expiry; revocation authority and method. This declaration does not authorize S5-03. |
| Independent verifier / approvers | Not established | Named independent verifier; separate database owner, release authority, and security reviewer attestations for the scopes below. |

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

#### Proposed S5-10 source-level artifact manifest (not execution authorization)

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

The source-level sequence is baseline candidate -> 028 -> 029 -> 033 -> 034 ->
035 -> 036 -> 037 -> 039. Migration 039 has only the direct baseline dependency
identified above; placing it last is a review convention. This sequence must
not be executed until each artifact is finalized, approved, and authorized for
the exact target and operation.
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
