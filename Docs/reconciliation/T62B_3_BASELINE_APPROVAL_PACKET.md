# T62B.3 Baseline Approval Evidence Packet

## Purpose and status

This packet prepares decisions; it is not an approval and does not designate a
live baseline. The approved target was not contacted in this gate.

## Evidence required to adopt the foundational catalog

The approving owner must receive and sign a package containing:

1. Exact SHA-256 hashes for `tests/scripts/postgresql-foundational-ddl.sql`,
   `postgresql-schema.sql`, `postgresql-slice1-schema.sql`, the original
   migration 026, and every selected PostgreSQL migration.
2. The T62B.1 catalog definition export: tables/columns/types/nullability,
   defaults, primary/foreign/check/unique constraints, indexes/predicates,
   sequences, ownership, identity columns, views/functions/triggers.
3. A normalized expected-versus-observed diff proving which foundational
   objects match and listing every unknown or semantic difference.
4. EF mapping and raw-SQL dependency trace for ticket/work-item, audit,
   authorization, waiting-reason, sequence, and reporting objects.
5. Evidence that no migration ledger entry is being fabricated and that
   unknown history is recorded as unknown.
6. Preservation and recovery evidence: backup/restore decision, immutable
   preflight fingerprint, sequence/current-key reconciliation method, and
   postflight comparison format.

The packet must be signed by the database owner, release authority, and
security reviewer as separate roles. No signature is present in this document.

## Sequence safety decision

Source mappings use provider-specific sequence defaults for many IDs; ticket
and work-item identity generation also has provider-specific code. The catalog
reported start/increment/cache `1/1/1`, no cycle, no observed ownership
relationship, and unset `last_value`. Required evidence before any insert or
DDL includes a non-row-reading reconciliation of sequence ownership/default
expressions and an owner-approved method to compare existing primary-key
values with future allocation. No sequence may be advanced, reset, or adopted
by assumption.

## Historical RBAC decision questions

- Are role and permission codes canonical uppercase, and how are aliases or
  case collisions handled? (031; security/RBAC owner.)
- What effective-from/to dates are authoritative for existing grants, and may
  unknown history authorize? (031/034; security/compliance and data owner.)
- Is a backfill permitted, with what evidence and rollback? (031/034; release
  and data owner.)
- What client identifier and FK semantics apply to scopes? (035; data owner.)
- Are scope/delegation rows derived from memberships or explicitly authored?
  (035; authorization owner.)
- May built-in roles be provisioned, and may any grants be inserted? (037;
  security owner; explicit session approval required.)
- How are existing role/permission rows with unknown provenance preserved and
  treated? (031/034/037; security/compliance owner.)

## Migration ledger decision

Migration 033 may be considered only after an approved baseline fingerprint and
an explicit statement that prior migration history is unknown. The ledger must
not claim migrations ran based on object names or current shape. The release
authority must choose an adoption record format, status, artifact hashes,
target fingerprint, and audit custody before any ledger DDL is approved.

## Audit actor decision

Migration 038 intentionally aborts because historical audit rows have a
non-null actor contract without a stable actor type. Before reconsideration,
the audit owner must decide whether legacy/system actors are represented by a
typed sentinel, nullable user FK, separate actor table, or another approved
contract; define writer/reader compatibility, FK behavior, and hash-chain
serialization; and provide historical-row preservation and verification
evidence. The abort must remain in force until then.

## Reporting isolation

Migration 026 remains a separate workstream. Its original SHA-256 is
`791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4` and its
review candidate fingerprint is tracked separately. It requires independent
authorization, target identity, lock/recovery, provider-validation, and
execution approvals.
