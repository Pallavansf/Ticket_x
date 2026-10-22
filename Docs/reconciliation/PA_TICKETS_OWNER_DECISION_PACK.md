# PA_TICKETS Owner Decision Pack

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Purpose:** owner completion of the decisions required before a PostgreSQL
schema-change request can be considered.

This pack records questions and evidence. It does not approve a baseline,
schema operation, migration, role, grant, or migration 026. Every decision is
`PENDING` because no verifiable owner approval or rejection is present in the
repository.

## Decision records

### D01 — Foundational schema baseline *(prerequisite)*

**Question:** Adopt the existing foundational catalog as the named PostgreSQL
baseline, or reject it and require another baseline?  
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

### D02 — Least-privilege schema operator *(prerequisite)*

**Question:** Approve a dedicated non-superuser operator, or document a
strictly scoped exception for `postgres`?  
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
allocation be reconciled without unsafe reset/advance?  
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
**Blocks:** Inserts, backfills, and migrations 028–037.

### D04 — Canonical RBAC codes

**Question:** Are role and permission codes canonical uppercase, and how are
aliases/case duplicates resolved?  
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
and do unknown historical grants deny by default?  
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
for scope rows?  
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
authored?  
**Why:** This controls whether migration 035 backfills rows and how revocation
is represented.  
**Options:** Deterministic derivation with approved provenance; explicit rows
with lifecycle ownership; defer delegated authorization.  
**Evidence:** Migration 035, `DatabaseDelegationFactsReader`, scope/delegation
contracts and data lineage.  
**Owner role:** Authorization authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 035 and delegation/scope provider validation.

### D08 — Built-in roles and grants

**Question:** Approve the five built-in role identities and any intended grants
separately from role-table creation?  
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
migration records?  
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

### D10 — Audit actor contract *(prerequisite)*

**Question:** What actor types, nullability/FK behavior, writer/reader
compatibility, and hash-chain preservation rules apply to historical audit rows?  
**Why:** Migration 038 intentionally aborts because the current non-null actor
contract cannot safely represent historical/system actors.  
**Options:** Typed legacy/system actor; nullable actor FK with explicit type;
separate actor relation; defer audit DDL.  
**Evidence:** Migration 038, audit EF mappings, chain coordinator, historical
preservation and verification plan.  
**Owner role:** Audit/compliance authority.  
**Decision:** `PENDING`  
**Rationale/reference:** No approval found; intentional abort remains.  
**Owner identity:** ______  **Date:** ______  **Scope:** ______  **Approval reference/signature:** ______  
**Blocks:** Migration 038 and audit schema changes.

### D11 — Migration 026 separate authorization

**Question:** Is migration 026 separately authorized for its exact artifact,
target, lock/recovery, provider validation, and execution window?  
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

Prepared evidence is in T62B.2–T62B.6 documents, especially the T62B.1 exact
definition matrix, T62B.3 baseline packet, T62B.4 register, and T62B.5 action
sheet. Those documents contain proposals and evidence requirements; none is an
owner approval.
