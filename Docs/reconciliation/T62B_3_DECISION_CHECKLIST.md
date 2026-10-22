# T62B.3 Owner Decision Checklist

| Decision | Source evidence | Accountable owner | Required record | Blocked until |
|---|---|---|---|---|
| Foundational baseline adoption | T62B.1 hashes and catalog diff | Database/release owner | Signed baseline fingerprint and definition report | Any schema operation |
| Sequence allocation | EF `SequenceDefault`, provider generators, catalog sequence metadata | Database/provider owner | Ownership/default/key reconciliation | Any insert or migration |
| Role/permission canonicalization | Migrations 031/037, authorization readers | Security/RBAC owner | Canonical code and duplicate policy | 031/034/037 |
| Historical grant dates | 031/034 effective fields and fail-closed rules | Security/compliance/data owner | Backfill or unknown-history policy | 031/034 |
| Client/scope relationship | 035 deferred client FK and scope reader | Authorization/data owner | Approved client identifier and FK decision | 035 |
| Scope/delegation derivation | 035 tables and application readers | Authorization owner | Row-authoring/backfill contract | 035 |
| Built-in roles/grants | 037 explicit session gate and role matrix | Security owner | Provisioning and grant approval | 037 |
| Migration history | 033 ledger prerequisites | Release authority | Unknown-history/adoption record | 033 |
| Audit actors | 038 intentional abort and audit mappings | Audit/compliance owner | Actor-type/FK/chain contract | 038 |
| Reporting migration | 026 contracts and exact hashes | Migration authority | Separate execution approval | 026 |
| Least privilege | T62A.2 superuser metadata | Security/database owner | Dedicated operator role decision | Any schema operation |

No checklist item is approved by this gate. An unchecked item is a hard blocker
for its associated stage.
