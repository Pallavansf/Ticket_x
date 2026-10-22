# T62B.5 Owner Action Sheet

No item below is approved. The T62B.4 register remains the tracking baseline;
all substantive decisions remain `PENDING`.

| Item | Decision required | Evidence to review | Consequence if unresolved | Downstream blocked |
|---|---|---|---|---|
| Baseline adoption | Adopt or reject the foundational catalog as a named baseline | T62B.1 exact hashes, catalog definition export, normalized diff, preservation evidence | History and definition equivalence remain unknown | Any schema change and ledger adoption |
| Least-privilege operator | Approve a dedicated non-superuser or explicitly scope an exception for `postgres` | T62A.2 privilege metadata, required DDL privilege list, role design | Operator may have excessive cluster authority | Any schema operation |
| Sequence allocation | Establish ownership/default/key allocation and safe reconciliation method | EF/provider sequence mappings, sequence metadata, primary-key/sequence comparison plan | Inserts may collide or skip existing identifiers | Any insert, migration, or backfill |
| Role/permission codes | Approve canonical uppercase/alias and duplicate policy | Migrations 031/037, authorization readers, duplicate report | Canonical indexes/backfills may reject or alter data | 031, 034, 037 |
| Historical effective dates | Decide backfill, unknown-history treatment, and fail-closed authorization | 031/034 fields and policy contracts | Existing grants may authorize incorrectly or remain unusable | 031, 034, provider authorization tests |
| Client/scope relationship | Approve client identity and FK semantics | 035 deferred FK, scope readers, client source contract | Scope rows cannot be safely related | 035 and scope authorization |
| Delegation derivation | Decide whether scope/delegation rows are derived or explicitly authored | 035 schema, delegation readers, membership semantics | Delegated access remains unprovable | 035 and delegation tests |
| Built-in roles/grants | Approve role identities and whether any grants may be provisioned | 037 explicit session gate, role matrix, security policy | Roles/grants cannot be safely created | 037 and RBAC acceptance |
| Migration history | Represent unknown history without fabricated ledger rows | 033 ledger design, baseline fingerprint, release records | No migration can be treated as applied | 033 and all later migration claims |
| Audit actors | Approve actor types, nullability/FK, writer/reader and chain behavior | 038 intentional abort, audit mappings, historical preservation plan | Audit compatibility remains blocked | 038 and audit DDL |
| Migration 026 | Grant separate authorization for exact artifact/target/lock/recovery | Original/candidate hashes, authorization/target/lock contracts | Reporting remains unapproved | Migration 026 only |

Each decision must be recorded by the accountable owner role with date,
artifact/evidence references, scope, expiry where applicable, and explicit
accept/reject language. No owner identity or approval is supplied by this
sheet.
