# PA_TICKETS Proposed PostgreSQL Upgrade Policy

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`

All recommendations below are **PROPOSED — OWNER APPROVAL REQUIRED**. They are
not approvals, execution authority, or changes to the T62B.4 status register.

## Policy defaults and evidence

| Policy area | Proposed default | Supporting evidence | Decision IDs | Remaining requirement |
|---|---|---|---|---|
| Schema baseline | Do not adopt from object resemblance; require exact definitions, hashes, provenance, and preservation evidence. Keep `EVIDENCE READY` until signed. | T62B.1 matrix; T62B.3 packet; T62B.4 register | D01, D09 | Database/release owner adoption or rejection and signed definition report |
| Least privilege | Prefer a dedicated non-superuser with only operation-specific database/schema/object rights; do not use `postgres` by default. | T62A.2 shows `postgres` superuser; T62B.3 packet | D02 | Security/database owner role decision and scoped grant design |
| Migration history | Treat history as unknown unless independently evidenced; never infer execution from object shape or create ledger rows to fill gaps. | T62B.1 absent-ledger evidence; migration 033 | D09 | Release authority representation/adoption decision |
| Sequence allocation | Require metadata/default/ownership/key reconciliation; no reset, advance, restart, or mutation before approval. | T62B.1 sequence report; EF/provider generators | D03 | Database/provider owner allocation and preservation method |
| Historical RBAC data | Unknown, incomplete, future, expired, revoked, or invalid grants remain non-authorizing according to the application contract; no invented dates/grants/roles. | Migrations 031/034/037; authorization readers | D04, D05, D08 | Security/RBAC and data owner canonicalization/backfill policy |
| Client, scope, delegation | Do not infer identity or ownership from names; require an approved source-of-truth and relationship contract before FKs or derived rows. | Migration 035; scope/delegation readers | D06, D07 | Authorization/data owner decision and lineage evidence |
| Audit actors | Preserve migration 038’s intentional abort until actor model, FK/nullability, writer/reader, historical preservation, and chain compatibility are approved. | Migration 038; audit mappings/coordinator | D10 | Audit/compliance owner actor contract and compatibility evidence |
| Built-in roles/grants | Do not provision roles or grants without explicit identities and permission matrix approval; keep role and grant decisions separate. | Migration 037 session gate and role matrix | D08 | Security owner approval and duplicate-safe provisioning evidence |
| Reporting migration 026 | Keep independent with exact artifact, target, authorization, lock/recovery, provider validation, and execution approvals. | Original SHA-256 `791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4`; T13–T34 contracts | D11 | Separate migration authority approval chain |
| Oracle | Keep deferred; no Oracle release assumptions follow from PostgreSQL evidence. | T61.2 and prior Oracle contract findings | Separate owner scope | Explicit reopening and Oracle-specific evidence |

## Proposed phased implementation sequence

### Phase A — Baseline and least privilege

Resolve D01 and D02. Produce the signed exact-definition comparison, artifact
hash set, provenance statement, and approved operator role/scope. Until then,
no schema-change request is ready.

### Phase B — Sequence and history safety

Resolve D03 and D09. Establish an evidence-backed key-allocation method and an
explicit unknown-history representation. Do not create ledger rows or mutate
sequences while preparing this phase.

### Phase C — RBAC, scope, delegation, and audit contracts

Resolve D04–D08 and D10 as their owners become available. Canonicalization,
historical effective dates, client relationships, derived/explicit scope rows,
built-in roles/grants, and audit actors may be decided independently, but each
affected migration remains blocked until its own decision and evidence exist.

### Phase D — Reviewed migration proposal

Only after applicable prerequisites are approved may a narrowly scoped,
non-destructive proposal be drafted. It must include preflight, preservation,
rollback, evidence capture, dependency order, and explicit exclusions. A
proposal is not execution authorization.

### Phase E — Separately authorized execution

Execution requires a new explicit approval naming the exact target, artifact
hashes, operator, lock/recovery protocol, backup/restore decision, stop
conditions, and postflight evidence. Migration 026 must use its independent
authorization chain and cannot be bundled into baseline approval.

## Changes prohibited until authorization

- Creating or altering tables, columns, indexes, constraints, schemas,
  sequences, defaults, ownership, roles, or privileges.
- Resetting, advancing, restarting, or re-owning sequences.
- Creating migration-ledger rows or marking migrations applied.
- Backfilling effective dates, grants, scopes, delegations, actors, or client
  relationships.
- Provisioning built-in roles or permissions.
- Reconsidering migration 038’s abort without an approved actor contract.
- Editing, enabling, executing, or generating migration 026 candidates.
- Running provider-backed tests, migrations, provisioning, seeding, cleanup, or
  Oracle operations.

## Approval state

No D01–D11 decision is approved. The owner decision pack and approval request
provide blank identity/date/scope/signature fields for actual accountable
owners. Existing T62B registers remain unchanged.

No database/provider operation occurred while preparing this policy.
