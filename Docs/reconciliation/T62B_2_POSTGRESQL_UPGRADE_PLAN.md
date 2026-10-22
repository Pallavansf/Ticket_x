# T62B.2 PostgreSQL Baseline Adoption and Upgrade Plan

## Evidence boundary

This is a source-backed, non-executable plan. It relies on T62B/T62B.1
read-only catalog evidence and checked-in SQL. No database connection was made
for this gate, and no migration is treated as applied because an object exists.

## Adoption baseline

### Adoptable only after approval

The catalog names, core columns/defaults, primary keys, foreign keys, indexes,
and sequence defaults for the foundational ticket/work-item, waiting-reason,
workflow, approval, SLA, audit, user, role, permission, membership, and
idempotency objects broadly match the reviewed PostgreSQL foundational scripts.
These are candidates for a formal baseline comparison, not an approved ledger
entry.

### Definition comparison still required

Every existing object requires a saved comparison of column order/types,
nullability, defaults, constraints, indexes/predicates, sequence ownership,
and generated-column settings. The observed sequences use start/increment/cache
`1/1/1`, `NO CYCLE`, and unset `last_value`; no ownership relationship was
observed. This prevents safe allocation conclusions.

### Missing additive objects

The catalog lacks migration-ledger, RBAC scope/delegation/proposal, permission
metadata, and migration-026 reporting objects. Their creation requires separate
approved additive work; no script is replayed by this document.

### Data and semantic decisions

Role/permission canonicalization, effective dates, historical backfills,
scope/client relationships, built-in roles/grants, audit actors, and sequence
allocation all require owner decisions. No timestamps, actors, grants, or
client identities may be invented.

## Dependency graph

| Migration | Preconditions and changes | Data/recovery risk | Status |
|---|---|---|---|
| 028 | Existing user table; adds local-password fields | Existing-user defaults and sensitive data semantics | Prepare source review; execution blocked |
| 029 | 028 user shape; adds profile/hierarchy fields and email index | Backfill/uniqueness and case policy | Prepare source review; execution blocked |
| 030 | Role/user foundations; adds admin delegation and provisioning behavior | Duplicate role identities and writes/grants | Blocked pending owner approval |
| 031 | Canonical uppercase role/permission codes; adds metadata, effective grants, scopes, role/delegation objects and backfills | Reads/writes existing RBAC data; duplicate/case and historical-time risks | Blocked pending baseline and data policy |
| 032 | Parent RBAC tables; adds independent-approval proposal object | No data seed, but workflow contract prerequisite | Blocked pending approval/workflow evidence |
| 033 | Approved baseline fingerprint; creates migration ledger | Recording false history is unsafe | Ledger must follow adoption decision; not executable here |
| 034 | 033 and permission/grant keys; adds effective status/provenance and fail-closed metadata | Updates existing grants; unknown historical rows must not authorize | Blocked pending backfill policy |
| 035 | 033 plus user/role/membership parents; adds scopes/delegations | Client FK intentionally deferred; no scope rows inserted | Blocked pending client/owner decision |
| 036 | 033/034 and proposal parents; adds proposal table | No proposal rows; workflow compatibility required | Blocked pending approval contract |
| 037 | Explicit session approval, role table and role sequence | Inserts built-in roles only; no grants; duplicate identity risk | Separate provisioning approval required |
| 038 | No executable preconditions satisfy unresolved actor contract | Intentionally aborts; no DDL permitted | BLOCKED by design |
| 026 | Exact hash, target identity, lock/recovery, provider and execution approvals | Reporting DDL/data dependencies and unknown commit | Separate migration-026 gate; excluded from ordering |

The only defensible ordering is: definition/adoption evidence, then owner-
approved additive migration design. The ordering among 028-037 remains blocked
where historical data or policy is involved; migration 026 is never implicitly
inserted into this chain.

## Preflight, preservation, and recovery evidence

Before any schema-changing gate, require a target fingerprint, artifact hashes,
catalog definition dump, sequence metadata, row-count/data-preservation policy,
approved operator/owner roles, backup or restore decision, and explicit stop
conditions. Afterward require a fresh target fingerprint, definition diff,
sequence/default report, migration ledger evidence only if separately approved,
and recovery evidence. Any uncertain commit, failed rollback, or evidence gap
must halt further migration and enter the recovery contract.

## Approval criteria for a later schema gate

The later gate must name the exact target/schema, operation and artifact hashes,
baseline/adoption decision, data/backfill rules, sequence policy, rollback and
restore owner, evidence custody, and non-overlap/lock strategy. It must also
prove the operator is not using a prohibited target and must keep migration 026,
provider validation, and application testing as separate approvals.
