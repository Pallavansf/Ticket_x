# PA_TICKETS Stage 2 Owner Confirmation Checklist

**Branch:** `workstream-i-lifecycle-authority`
**Stage 2:** `PARTIAL`
**Provider execution:** `NO-GO`

This checklist extracts the existing candidate decisions and evidence gates. It does not approve a decision, select a baseline, authorize a provider operation, or change any formal status. Formal responses remain `PENDING` until the accountable owner supplies attributable evidence in the existing decision packet fields.

## A. Policy decisions D01–D10

| ID | Existing candidate choice | Exact owner confirmation required | Dependency or conflict | Attribution fields | Approval consequence | Rejection/deferral consequence | Prohibited until accepted |
|---|---|---|---|---|---|---|---|
| D01 | Adopt one immutable foundational baseline only after exact definition, raw-byte hash, provenance, and expected-versus-observed review; otherwise fail closed. | Adopt or reject the named artifact/version and its exact hash. | Three competing references remain unresolved; object presence does not prove provenance. | Owner identity, UTC date, scope, response, rationale/evidence, approval/signature reference. | Enables a baseline-bound schema-change request and ledger review. | Keep baseline/history unknown; no reconciliation or migration claims. | Baseline adoption, schema changes, migration 033. |
| D02 | Use a dedicated non-superuser schema operator; no implicit `postgres` exception. | Approve the role, exact grants, target scope, and expiry, or approve a separately scoped exception. | T62A.2 observed `postgres` superuser; this exceeds least privilege. | Role/owner identity, date, scope, grants, expiry, reference, rationale/evidence. | Enables an operator-specific request. | No schema operation or privilege change. | DDL, migrations, writes, cleanup, grants. |
| D03 | Preserve existing sequence state; reconcile metadata without blind reset or advance. | Approve the non-destructive key/default/ownership allocation method. | Existing values, sequence metadata, and EF/provider allocation still require reconciliation. | Owner identity, date, scope, reference, rationale/evidence. | Allows a controlled insert/backfill plan. | Inserts/backfills remain blocked. | Sequence reset/advance and migrations 028–037. |
| D04 | Canonical uppercase role/permission codes with explicit alias and duplicate handling. | Approve canonical codes and the remediation/mapping rule. | Migrations 031/037 reject ambiguous identities and depend on protected codes. | Owner identity, date, scope, reference, rationale/evidence. | Enables RBAC catalogue migrations and validation. | Authorization upgrades remain deferred. | Migrations 031, 034, 037 and RBAC acceptance. |
| D05 | Preserve unknown grant dates/provenance and fail closed; do not invent backfills. | Approve effective-date, provenance, backfill, and unknown-history policy. | Historical rows lack sufficient verified lineage; authorization must not infer access. | Owner identity, date, scope, reference, rationale/evidence. | Enables persisted grant validation. | Historical upgrades and authorization acceptance remain blocked. | Migrations 031/034 and historical-data writes. |
| D06 | Defer client-scoped authorization until authoritative client identity and FK semantics are validated. | Approve the client source/key and FK/nullability behavior. | Migration 035 intentionally defers client FKs; readers depend on parent lineage. | Owner identity, date, scope, reference, rationale/evidence. | Enables client/scope migration and tests. | Client-scoped authorization remains fail-closed/deferred. | Migration 035 and client-scoped tests. |
| D07 | Explicitly authored, lifecycle-managed scope/delegation rows with author, dates, revocation, auditability, and fail-closed absence. | Approve authored versus derived records and the lifecycle/provenance contract. | Migration 031 `$scope_backfill$` derives rows from memberships, conflicting with this candidate. | Owner identity, date, scope, reference, rationale/evidence; existing owner-proposed field remains non-approval. | Enables an approved scope/delegation design and migration. | Delegation/scope migration and provider validation remain blocked. | Migration 035 and delegated authorization tests. |
| D08 | Approve five built-in role identities separately from a permission-grant matrix. | Approve role identities and, separately, the exact grant matrix. | Migration 037 inserts roles only and must not infer grants from names. | Owner identity, date, scope, reference, rationale/evidence. | Enables controlled role/grant provisioning. | RBAC provisioning and acceptance remain blocked. | Migration 037 and grant data changes. |
| D09 | Represent unknown migration history explicitly outside fabricated applied-migration rows. | Approve the unknown-history representation and review process. | Migration 033 ledger is absent; schema shape cannot prove applied history. | Owner identity, date, scope, reference, rationale/evidence. | Enables an evidence-backed ledger decision. | No applied-history claim or migration 033 adoption. | Migration 033 and ledger writes. |
| D10 | Explicit actor type with nullable user FK where appropriate, preserving user/system/legacy actors and hash-chain inputs. | Approve actor types, nullability/FK rules, writer/reader compatibility, and chain preservation. | Migration 038 intentionally aborts; current mapping uses non-null user actor and compatibility is unresolved. | Owner identity, date, scope, reference, rationale/evidence; existing owner-proposed field remains non-approval. | Enables an approved audit schema change. | Migration 038 and audit-schema changes remain blocked. | Migration 038, audit writes, and historical conversion. |

For every row, the owner must record one response (`APPROVE`, `REJECT`, or `DEFER`) with rationale and evidence. A candidate, unsigned document, or conversation statement is not formal approval.

## B. Foundational baseline evidence

| Reference | Current evidence | Status |
|---|---|---|
| `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64` | Raw-byte SHA-256 reproducible from the current working-copy foundational DDL. | Candidate only; not adopted. |
| `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19` | Exact historical Git blob identified from commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a`. | Historical evidence; adoption unresolved. |
| `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7` | Repeated documentation reference with no identifiable byte artifact or provenance. | Untraceable; cannot authorize anything. |

The owner must review the immutable artifact/version, exact bytes, provenance, expected-versus-observed definition report, and adoption scope, then record one attributable D01 decision. No hash is recommended merely because it is newer, current, or easier to reproduce.

## C. Provider execution gates

| Gate | Owner confirmation | Independent technical evidence still required | Current status | Prohibited until complete |
|---|---|---|---|---|
| TGT | Approve `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex` and server-authentication/hostname-peer-TLS policy. | Fresh identity evidence independently identifying server, database, role, host/port, recovery/read-only state, and authentication policy. | `BLOCKED` | Any provider connection or operation. |
| OP | Approve a dedicated least-privilege operator, exact database/schema/object/function grants, scope, and expiry. | Independently verified effective grants sufficient only for the named operation and cleanup. | `BLOCKED` | Schema changes, writes, cleanup, privilege changes. |
| PRE | Approve the fail-closed preflight bound to TGT, OP, BASE, and operation, with custody/retention and expiry. | Consistent, unexpired records and verifier rejection of missing/mismatched evidence. | `BLOCKED` | Provider execution. |
| OP-TEST | Authorize only `Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`, including generated writes, cleanup, recovery boundary, and retained evidence. | Independently verified target and operator gates; test-owned cleanup and outcome evidence. | `PARTIAL` | All other provider tests, migrations, DDL, RBAC changes. |

`READ_ONLY_CATALOG_RECONCILIATION` is a separate preflight operation and does not authorize the write test. Owner-supplied policy and attribution do not replace independently generated technical evidence.

## D. Separate D11/P26 chain

D11 requires an independent decision for migration 026's exact immutable artifact and hash, approved target and identity, authorization envelope, lock/recovery approach, provider validation, execution window, and evidence retention. This checklist does not authorize migration 026, alter it, or merge it with D01 or Stage 2 approval.

## Owner response fields

For each decision/gate: `Decision: ______`  `Owner identity: ______`  `UTC date: ______`  `Scope: ______`  `Expiry: ______`  `Approval reference/signature: ______`  `Rationale/evidence references: ______`.

## Current conclusion

Known conflicts are explicit (D07 versus migration 031 scope backfill; D10 versus migration 038 and current audit mapping; unresolved baseline hashes). No new inconsistency was found in the candidate-policy extraction. Formal statuses remain unchanged, Stage 2 remains `PARTIAL`, and provider execution remains `NO-GO`.
