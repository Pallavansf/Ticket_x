# PA_TICKETS Owner Approval Request

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`

## Purpose

The existing PostgreSQL target has a foundational catalog whose exact
definitions broadly align with reviewed scripts, but migration provenance,
sequence allocation safety, historical RBAC semantics, audit compatibility,
and operator privilege scope are unresolved. Explicit owner decisions are
needed before a schema-change request can be drafted for authorization.

This request asks owners to approve, reject, or defer the decisions below with
rationale and evidence references. It authorizes no database change, schema
creation, migration, seed, cleanup, privilege change, or test.

## Priority prerequisites

### D01 — Foundational schema baseline

**Decision requested:** Adopt or reject the proposed foundational baseline after
reviewing exact definitions and hashes.  
**Review:** T62B.1 exact-definition matrix, T62B.3 baseline packet, source
artifact hashes, normalized expected-versus-observed catalog report.  
**Owner role:** Database/release authority.  
**Downstream:** All schema changes and migration 033 ledger adoption.

Decision: `PENDING`  
Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale and evidence references: ______________________________________

### D02 — Least-privilege schema operator

**Decision requested:** Approve a dedicated non-superuser role or document an
explicitly scoped, time-bounded exception for `postgres`.  
**Review:** T62A.2 privilege metadata and required database/schema privilege
inventory.  
**Owner role:** Security/database authority.  
**Downstream:** Any schema operation.

Decision: `PENDING`  
Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale and evidence references: ______________________________________

### D03 — Sequence allocation safety

**Decision requested:** Approve a non-destructive method to reconcile existing
keys, sequence defaults, ownership, and future allocation. No reset or advance
is permitted in this request.  
**Review:** EF/provider sequence mappings, T62B.1 sequence metadata, and the
primary-key/sequence reconciliation proposal.  
**Owner role:** Database/provider authority.  
**Downstream:** Inserts, backfills, and migrations 028–037.

Decision: `PENDING`  
Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale and evidence references: ______________________________________

### D05 — Historical grant validity

**Decision requested:** Approve effective-date/backfill policy and fail-closed
treatment of unknown grant history.  
**Review:** Migrations 031/034, authorization contracts, data lineage, and
backfill/rollback evidence.  
**Owner role:** Security/compliance/data authority.  
**Downstream:** Migrations 031/034 and persisted authorization validation.

Decision: `PENDING`  
Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale and evidence references: ______________________________________

### D09 — Unknown migration history

**Decision requested:** Approve how unknown historical status is represented
without inventing applied-migration records.  
**Review:** T62B.1 catalog evidence, artifact hashes, release records, and
migration 033 ledger contract.  
**Owner role:** Release authority.  
**Downstream:** Migration 033 and all applied-history claims.

Decision: `PENDING`  
Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale and evidence references: ______________________________________

### D10 — Audit actor compatibility

**Decision requested:** Approve actor types, nullability/FK behavior,
writer/reader compatibility, and audit-chain preservation for historical rows.
Migration 038’s intentional abort remains in force.  
**Review:** Migration 038, audit mappings, chain coordinator, and historical
preservation evidence.  
**Owner role:** Audit/compliance authority.  
**Downstream:** Migration 038 and audit schema changes.

Decision: `PENDING`  
Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale and evidence references: ______________________________________

## Independent decisions

### D04 — Canonical RBAC codes

Approve or reject uppercase/alias and duplicate-resolution policy for role and
permission codes. Review migrations 031/037, mappings, and duplicate evidence.

Decision: `PENDING`  Owner role: Security/RBAC authority  Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale/evidence: ______________________________________  Downstream: 031, 034, 037.

### D06 — Client and scope relationships

Approve or reject the authoritative client identity and FK semantics deferred
by migration 035. Review the client contract, migration 035, and scope readers.

Decision: `PENDING`  Owner role: Authorization/data authority  Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale/evidence: ______________________________________  Downstream: 035 and client-scoped authorization.

### D07 — Scope and delegation derivation

Approve whether scope/delegation rows are derived from memberships or explicitly
authored. Review migration 035, delegation readers, and data lineage.

Decision: `PENDING`  Owner role: Authorization authority  Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale/evidence: ______________________________________  Downstream: 035 and delegation validation.

### D08 — Built-in roles and grants

Approve role identities and any grant matrix separately. Review migration 037,
the role matrix, and security policy; do not infer grants from role names.

Decision: `PENDING`  Owner role: Security authority  Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale/evidence: ______________________________________  Downstream: 037 and RBAC acceptance.

## Separate migration-026 decision

### D11 — Reporting migration 026

Record a separate approve/reject/defer decision for the exact artifact,
approved target, authorization envelope, target identity, lock/recovery,
provider validation, and execution window. Baseline approval does not authorize
026. Review the original SHA-256
`791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4`, review
candidate records, and the separate authorization/target/lock contracts.

Decision: `PENDING`  Owner role: Migration authority  Owner identity: ______  Date: ______  Scope: ______  Approval reference/signature: ______  
Rationale/evidence: ______________________________________  Downstream: migration 026 only.

## Response instructions

For each item, provide exactly one decision: `APPROVED`, `REJECTED`, or
`DEFERRED` (recorded as `PENDING` until supplied), with rationale, evidence
references, owner identity, decision date, scope, and approval reference.
Partial approval must identify the exact fields, artifacts, targets, and expiry
covered. Silence or an unsigned proposal does not constitute approval.

**No database changes are authorized by this request.**
