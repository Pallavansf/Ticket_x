# PA_TICKETS Gate T54 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** provider-validation readiness and safe future test plan  
**Review mode:** read-only planning; no provider operation is authorized

## Inspection performed

Inspected the master implementation/E2E roadmap, RBAC coverage analysis, T50–T53 results, authorization-facts and delegation readers, authorization service, ticket mutation/repository paths, EF persistence mappings, audit transaction/lock components, migration-026 execution proposal/contract, and PostgreSQL integration guards. Read-only commands included `git branch --show-current`, `git status --short --untracked-files=all --ignored`, `Get-Content`, `Get-ChildItem`, and `rg` source searches. No tests, builds, SQL, database connections, migrations, provisioning, seeding, cleanup, or provider-backed operations were run.

The roadmap explicitly records provider persistence, API E2E, concurrency, and migration-026 work as PARTIAL or BLOCKED. T53 identified the authorization TOCTOU concern; T50–T52 provide provider-independent hosted evidence only.

## Provider-validation matrix

| Capability/risk | Source-backed boundary | Current evidence | Future provider test and assertions | Prerequisites/target | Status |
|---|---|---|---|---|---|
| EF actor, permission, role, grant, and scope materialization | `DatabaseAuthorizationFactsReader.ReadAsync` performs separate `AsNoTracking` queries for actor, permission, active roles, effective grants, and scopes | Hosted fakes and policy tests; no provider result | Seed approved rows; run each query through Npgsql; assert generated/materialized values, null handling, effective dates, and no accidental client evaluation | Approved isolated PostgreSQL schema with compatible RBAC tables and known fixture IDs | BLOCKED |
| Effective grant validity | `EffectiveGrantAuthorization` plus reader predicates for active/status/date windows | Policy tests and T44 hosted unsupported-grant denial | Persist active, inactive, revoked, future, expired, missing-start, and unknown-status rows; assert exact allow/deny outcomes and SQL/provider behavior | Approved schema baseline; time control; cleanup/recovery plan | BLOCKED |
| Scope and organization matching | `DatabaseAuthorizationService.HasStandardScopeAsync`/`HasCustomRoleScopeAsync`; organization scope requires explicit organization fact | T45/T47 hosted fakes and scope policy tests | Persist department/team/client/record/organization/NONE scopes; assert cross-scope denial, in-scope allow, explicit organization requirement, and invalid-scope fail closed | Approved RBAC data model and target identity; fixture isolation | BLOCKED |
| Admin Head delegation | `DatabaseDelegationFactsReader` separately reads target privilege, role codes, and active delegation | T46 hosted missing-delegation denial; policy tests | Persist active, expired, revoked, target-role, and delegated-admin rows; assert allow/deny and race outcomes | Approved delegation fixtures and owner-approved role semantics | BLOCKED |
| Missing/invalid persisted facts | `DatabaseAuthorizationService` returns false for missing actor/metadata and invalid grant/policy facts | T51 hosted null-actor denial; T44 invalid-grant denial | Remove or corrupt each required row in an approved fixture; assert HTTP 403/no protected operation and no false success audit | Approved negative-fixture cleanup and evidence capture | BLOCKED |
| T53 revocation/scope TOCTOU | Authorization facts are read before `EfTicketRepository.SaveAsync` opens persistence work; no shared transaction is established in source | T53 source analysis only; no race test | Operation A pauses after facts; operation B commits grant/scope/delegation revocation; A attempts mutation. Assert the approved invariant (deny, or explicitly approved atomic outcome), commit ordering, and audit result under controlled isolation | Owner-approved transaction/lock/revalidation design, two connections, disposable target | BLOCKED |
| Mutation/audit atomicity | `EfTicketRepository.SaveAsync` writes ticket/events/audit through `Commit`; audit chain lock must be inside transaction | T52 proves denied mutation does not call save; provider audit tests are unexecuted | Commit success: state/event/audit all durable; rollback/fault: none or defined recovery state; verify hash chain and no partial success | Approved fault injection, transaction policy, retention/evidence custody | BLOCKED |
| Schema and migration compatibility | `TicketsDbContext` maps RBAC, tickets, audits, locks, sequences, and constraints; migration 026 is separately gated | Roadmap/PERSIST rows and migration contract documents; no target schema result | On a disposable approved baseline, verify schema version, mappings, indexes, sequences, and candidate-specific prerequisites without applying migration 026 unless separately authorized | Independent schema fingerprint, migration approval, backup/restore and stop plan | BLOCKED |

The matrix deliberately separates policy/unit and provider-independent hosted evidence from provider-backed proof. Fakes cannot establish PostgreSQL translation, snapshot visibility, transaction isolation, or durable audit behavior.

## T53 concurrency invariant and proposed design

The security invariant must be approved before testing: a protected mutation must not commit using authorization that was invalidated before the mutation's commit, unless an owner-approved transaction/revalidation rule explicitly defines that outcome. The test must use two independent connections and controlled barriers, not sleeps:

1. Operation A begins the approved authorization/mutation flow and records the facts-read barrier.
2. Operation B revokes the relevant grant, scope, or delegation and commits.
3. Operation A proceeds to its final mutation/persistence boundary.
4. The test records both commit outcomes, final ticket state, audit/event state, and whether a retry/recovery decision is required.

Before implementation, owners must decide whether authorization reads and mutation share a transaction, use row/advisory locks, or revalidate immediately before persistence. Isolation level, lock ownership, deadlock handling, cancellation, and uncertain commit recovery cannot be inferred from current source. A provider result must include transaction IDs/timestamps or equivalent evidence sufficient to establish ordering.

## Mutation and audit questions

The application writes ticket state, events, and audit rows in the repository commit path, while audit-chain locks require an active transaction. Provider validation must answer whether all writes share one transaction, whether a failed audit insert rolls back the mutation, whether a failed commit creates an uncertain outcome, and whether concurrent appends preserve one valid hash chain. In-memory spies can prove `SaveAsync` was not called on denial (T52); they cannot prove durability, atomicity, rollback, or recovery after connection loss.

## Schema and migration prerequisites

Future tests require an independently verified, disposable PostgreSQL target with the exact schema version and RBAC/ticket/audit tables mapped by `TicketsDbContext` and `ExecutionDbContext`. The target must have known sequences, indexes, constraints, and fixture identities. Migration 026 remains a separate project: its original and candidate artifacts, authorization/target identity contracts, lock/recovery contract, provider validation, and execution approval must not be folded into RBAC provider testing. A script or candidate SQL file is not evidence that migration 026 was applied.

## Safe future execution prerequisites

- Named database owner and explicit approval for the exact test target and operations.
- Independent target identity verification before any write-capable test; caller-supplied connection descriptors are insufficient.
- Disposable isolated database/schema with explicit protection against `pa_tickets_test` and `pa_tickets_h_verify`.
- Confirmed schema fingerprint and prerequisite migrations, documented without executing them here.
- Reviewed rollback, cleanup, retention, and uncertain-commit recovery plan.
- Bounded test list, timeout, stop condition, and evidence-capture location.
- Approved two-connection concurrency design and invariants.
- Preserved `PostgreSqlTestEnvironment` guard: connection string, explicit opt-in, and exact independent host/port/database/user descriptor; credentials must never be logged.
- Separate approvals for security policy, target identity, PostgreSQL/database owner, provider validation, recovery, and migration execution.

Unknown or unavailable prerequisites are **UNVERIFIED/BLOCKED**. Neither protected database is assumed safe for testing.

## Prioritized future validation sequence

1. **Provider fixture and translation gate (provider approval required):** verify schema fingerprint, EF/Npgsql materialization, effective grants, scopes, delegation, and missing/invalid fact denial. Stop on target mismatch, schema mismatch, unexpected SQL/provider error, or any protected-target indication.
2. **Authorization concurrency and mutation/audit gate (provider plus database-owner approval):** execute the two-connection T53 design, transaction/isolation alternatives, revocation races, rollback/fault cases, and audit-chain durability. Stop on ambiguous commit, partial state, missing evidence, or invariant violation.
3. **API/provider E2E gate (separate approval):** run the bounded route/role matrix, positive and negative object scopes, persistence-backed 401/403/200 behavior, and cross-user isolation after gates 1–2 pass.
4. **Migration-026 gate (independent):** only after its own authorization, target identity, lock/recovery, provider validation, backup/baseline, and execution approvals. This roadmap never authorizes migration execution.

Provider-independent hosted/policy tests may continue under ordinary test approval; no provider gate is implied by their results.

## Worktree and operation confirmation

The branch remained `workstream-i-lifecycle-authority`. Before T54, `git status --short --untracked-files=all --ignored` reported 24,165 entries; all existing modified, staged, untracked, and ignored work was preserved. Only this documentation result was added. No source, migration, schema, environment, configuration, or test behavior was changed. No tests requiring a database/provider were run, and no database/provider operation occurred.

