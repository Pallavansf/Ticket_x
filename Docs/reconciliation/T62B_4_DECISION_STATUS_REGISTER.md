# T62B.4 Decision Status Register

Statuses reflect repository evidence only. No owner approval is recorded.

| Decision | Status | Evidence ready | Accountable owner role | Downstream impact | Next action |
|---|---|---|---|---|---|
| Foundational baseline adoption | EVIDENCE READY | T62B.1 catalog matrix, source hashes, expected/observed comparison requirements | Database/release owner | All schema changes and 033 | Owner reviews exact definition diff and signs adoption or rejects it |
| Sequence ownership/allocation | PENDING | EF sequence defaults, provider generators, sequence metadata observations | Database/provider owner | Any insert, 028-037, reporting | Produce approved key/ownership reconciliation without advancing values |
| Role/permission canonicalization | PENDING | Migrations 031/037 and authorization readers | Security/RBAC owner | 031, 034, 037 | Decide uppercase/alias policy and approve duplicate handling |
| Historical effective-grant dates | PENDING | 031/034 effective fields and fail-closed rules | Security/compliance/data owner | 031, 034, authorization | Approve backfill or unknown-history denial policy |
| Client and scope FK semantics | PENDING | 035 deferred client FK and scope contracts | Authorization/data owner | 035 and scope authorization | Identify approved client relationship and FK behavior |
| Scope/delegation derivation | PENDING | 035 schema and application readers | Authorization owner | 035, delegated authorization | Decide whether rows are derived, authored, or remain unavailable |
| Built-in role provisioning/grants | PENDING | 037 explicit session gate and role matrix | Security owner | 037 and authorization behavior | Approve role identities and any grant policy separately |
| Migration history representation | PENDING | 033 ledger design and T62B unknown-history evidence | Release authority | 033 and all later migration claims | Approve unknown-history/adoption record; do not create ledger yet |
| Audit actor contract | PENDING | 038 intentional abort and audit mappings | Audit/compliance owner | 038 and audit DDL | Approve actor types, nullable/FK behavior, and chain compatibility |
| Reporting migration 026 | PENDING | Original hash and separate contract documents | Migration authority | 026 only | Obtain independent authorization, target, lock/recovery, and execution approvals |
| Least-privilege schema operator | PENDING | T62A.2 metadata showing current `postgres` superuser | Security/database owner | Any schema operation | Choose dedicated non-superuser role or document exception with scope |

## Source-resolvable versus owner-only

Source inspection can prepare hashes, expected definitions, dependency graphs,
and code contracts. It cannot approve baseline adoption, historical values,
sequence allocation safety, client identity, built-in grants, actor semantics,
least privilege, or migration execution. Those remain human-owner decisions.

## Readiness conclusion

No schema-change request is ready for authorization. A later draft may describe
the operation only after the baseline owner, least-privilege operator,
sequence, RBAC-history, audit-actor, and separate migration-026 decisions are
resolved with evidence.
