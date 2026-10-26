# PA_TICKETS Enterprise Release Dependency Register

**Branch:** `workstream-i-lifecycle-authority`  
**Authority:** `Docs/PA_TICKETS_MASTER_ROADMAP.md`  
**Status:** release preparation only; no approval or execution is implied

This register consolidates existing decision records into executable owner and
provider actions. It does not replace the owner decision pack, authorize a
database operation, or claim release readiness.

The database owner selected the conservative recommendations in the owner
instructions as the intended V1 policy. That selection is not a signed
repository approval: D01–D10, PRE, TGT, OP, and BASE retain their formal
approval and validation gates. The selected policy requires fail-closed
handling of unknown history, ambiguous RBAC facts, missing target evidence,
and unavailable audit/identity data.

## Owner and provider actions

| ID | Accountable role | Decision/evidence required | Current evidence | Codex can complete now | External dependency | Closure criterion |
|---|---|---|---|---|---|---|
| D01 | Database/release authority | Adopt or reject the exact foundational PostgreSQL baseline | T62B.1 definition matrix, hashes, expected/observed evidence | Prepare comparison package | Owner decision; target catalog evidence | Signed baseline decision with provenance and exact definitions |
| D02 | Security/database authority | Dedicated least-privilege operator or scoped exception | T62A.2 shows current `postgres` is superuser | Document required grants and operator boundary | Owner approval; privilege metadata | Named role, scope, expiry, and grant evidence |
| D03 | Database/provider authority | Preserve-safe sequence/default/ownership allocation method | T62B.1 and EF/provider mappings | Prepare non-mutating reconciliation procedure | Owner approval; provider validation | Approved method with no unsafe reset/advance |
| D04 | Security/RBAC authority | Canonical role/permission codes and duplicate/alias policy | Migrations 031/037 and authorization mappings | Maintain source traceability | Owner decision | Signed catalogue and duplicate policy |
| D05 | Security/compliance/data authority | Historical grant dates, provenance, and fail-closed treatment | Migrations 031/034 and reader rules | Specify negative authorization cases | Owner decision; data lineage | Approved effective-date/backfill policy |
| D06 | Authorization/data authority | Client identity source and scope FK/nullability semantics | Migration 035 and scope readers | Map source dependencies | Owner decision | Approved lineage and relationship contract |
| D07 | Authorization authority | Explicitly authored versus derived scope/delegation | Owner proposal plus 031 `$scope_backfill$` conflict | Preserve conflict and draft implementation impact | Owner decision | Formal resolution of backfill strategy and lifecycle |
| D08 | Security authority | Five roles and separate permission-grant matrix | Migration 037 and role policy | Prepare duplicate-safe provisioning checks | Owner decision | Signed role identities and grant matrix |
| D09 | Release authority | Representation of unknown migration history | Missing ledger evidence; migration 033 contract | Prepare explicit unknown-history record format | Owner decision | Release-owned record without fabricated applied rows |
| D10 | Audit/compliance authority | Actor types, nullable FK, writer/reader compatibility, chain preservation | Migration 038 intentional abort and audit mappings | Prepare compatibility test plan | Owner decision; provider validation | Approved actor contract and chain-preservation evidence |
| PRE | Application architecture/transaction owner | Cross-service Incident/Problem read-only transition preflight | `PA_TICKETS_STAGE_2_TRANSITION_PREFLIGHT_DECISION.md` | Source seam implemented from selected policy; provider validation remains | Formal contract attribution and provider evidence | Approved contract plus denied/allowed regression evidence |
| TGT | Security/database/release authority | PostgreSQL target identity, hostname/peer/TLS policy, and isolation | T62A/T62B target guards and metadata reports | Maintain verifier and redacted evidence format | Owner policy and provider preflight | Independently authenticated target evidence |
| OP | Security/database authority | Least-privilege role and permitted operation scope | T62A.2 privilege report | Document grant minimums | Explicit operator approval | Scoped role authorization |
| BASE | Database/release authority | Baseline adoption, provenance, and evidence custody | T62B.1–T62B.6 packets | Preserve hashes and expected snapshot | Owner adoption and read-only catalog result | Signed expected/observed reconciliation |
| P26 | Migration authority | Migration 026 artifact, target, authorization, lock/recovery, provider, execution window | Separate T13–T34 contracts and fixed hashes | Maintain separate preflight package | Independent approval chain | Signed execution authorization for exact artifact/target |

The repository does not identify an authorized contact, ticketing queue, or
submission integration for these owners. The blank owner fields in
`PA_TICKETS_OWNER_APPROVAL_REQUEST.md` therefore remain the required handoff
mechanism; no request is represented as submitted.

## Minimum release dependency chain

1. **Source readiness:** build, database-independent tests, authorization
   ordering, lifecycle invariants, audit/error redaction, and Angular checks.
2. **Owner decisions:** D01–D10 and PRE as applicable. D11/P26 remains separate.
3. **Target/provider authorization:** TGT, OP, BASE, backup/baseline evidence,
   and an explicitly named isolated target.
4. **Read-only provider reconciliation:** exact catalog comparison, sequence
   and provenance evidence, and preservation record.
5. **Authorized schema/data operation:** only the approved operation and
   migration sequence, with rollback/recovery evidence and no inferred history.
6. **Persistence/API/browser acceptance:** authenticated RBAC, lifecycle,
   attachment, audit, concurrency, and Angular-to-API-to-PostgreSQL tests.
7. **Release sign-off:** security, backup/restore, monitoring, configuration,
   operational runbook, and owner sign-off.

Stages 3–7 cannot be declared complete by source or fake-provider tests alone.

## Next executable milestone

The next safe milestone is **owner-ready dependency handoff**: obtain
attributable responses for D01–D10 and PRE, while preserving D11/P26 as a
separate chain. Once PRE is approved, implement the read-only preflight seam;
once the baseline/operator/target decisions are approved, perform only the
separately authorized read-only provider reconciliation.

## Source-readiness checkpoint

The source-only checkpoint was executed against the current branch:

- Database-independent .NET tests, excluding `PostgreSql` test classes: **311
  passed, 0 failed, 0 skipped**.
- API project build with `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj
  --no-restore --nologo`: **0 warnings, 0 errors**.
- Angular production build with `npm.cmd run build -- --no-progress`: **passed**
  with the existing `role-administration.page.scss` budget warning (4.53 kB
  against a 4.00 kB budget).
- `npm run` through the PowerShell shim was blocked by the machine execution
  policy; `npm.cmd` provided the equivalent build command without changing
  permissions or policy.

The reviewed authentication, authorization-before-mutation, lifecycle denial,
audit redaction, and route/API guard paths had no new source defect that could
be corrected safely without changing an unresolved transaction, RBAC, or
provider contract. No production code change was justified by this checkpoint.

Owner handoff capability remains limited to the repository documents: no
authorized contact, ticketing integration, or submission channel is configured
in the repository. The approval request is therefore ready for a human owner
to submit, but it was not submitted.

No database/provider connection, SQL, migration, schema/RBAC change,
provisioning, seeding, cleanup, sequence operation, privilege change, test,
commit, or push was performed for this artifact. Existing worktree changes and
local configuration were preserved.
