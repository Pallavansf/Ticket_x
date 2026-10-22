# T62B.2 Owner Decision Register

| Decision | Evidence required | Accountable owner | Blocking stage |
|---|---|---|---|
| Adopt current foundational catalog as a named baseline | Exact definition diff, artifact hashes, catalog fingerprint | Database owner/release authority | Any schema change |
| Canonical role and permission code policy | Approved case/alias rules and duplicate report | Security/RBAC owner | 031/034/037 |
| Effective-grant timestamps and historical backfill | Historical validity policy; fail-closed treatment | Security/compliance + data owner | 031/034 |
| Scope/delegation parent and client FK semantics | Client identity decision and FK evidence | Authorization/data owner | 035 |
| Built-in role provisioning and grant policy | Explicit session approval, role matrix, no-invented grants | Security owner | 037 |
| Audit actor contract | Actor types, nullable/FK behavior, chain compatibility | Audit/compliance owner | 038 and audit DDL |
| Sequence ownership and allocation | Existing-row/sequence reconciliation without advancing values | Database owner/provider owner | Any insert/DDL |
| Migration ledger adoption | Proof of applied history or explicit unknown-history state | Release authority | 033 |
| Reporting migration 026 | Exact hash, target fingerprint, lock/recovery and execution approvals | Migration authority | Separate 026 gate |
| Least-privilege operator | Dedicated role design and grants replacing superuser use | Security/database owner | Any schema operation |

No decision in this register is approved by T62B.2. Missing evidence blocks the
corresponding stage; object presence alone cannot satisfy any decision.
