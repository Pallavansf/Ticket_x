# PA_TICKETS Stage 2, Slice 18 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`  
**Scope:** owner decision package for one repository-persistence test

## Decision summary

Slice 17 identified three blockers that remain unresolved: attributable TGT,
OP, BASE, and PRE evidence; a foundational DDL hash conflict; and
operation-specific authorization for a write test. This result records the
precise owner decisions without connecting to PostgreSQL or changing any
artifact. It links to [Slice 17](PA_TICKETS_STAGE_2_SLICE_17_RESULT.md) rather
than duplicating its full investigation.

| Gate | Decision requested | Evidence required | Responsible role established by repository | Status |
|---|---|---|---|---|
| TGT | Approve `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex` as the exact test target and approve hostname/peer/TLS identity policy | Owner-attributed target decision; independent server/database/role evidence immediately before connection; isolation and freshness evidence | Security/database/release authority | `PENDING` |
| OP | Approve a dedicated least-privilege account and the exact permissions for this test and reviewed cleanup | Named role, database/schema/object/function privileges, scope, expiry, and grant evidence; no superuser exception unless separately justified | Security/database authority | `PENDING` |
| BASE | Adopt one immutable foundational DDL artifact/version and its provenance | Artifact path/version, reconciled SHA-256, source-control provenance, expected-definition comparison, and attributable adoption decision | Database/release authority | `PENDING` |
| PRE | Approve the fail-closed preflight evidence package and retention/expiry rules | `OWNER_APPROVED` record, future expiry, target-evidence reference, least-privilege decision reference, retention boundary, and successful preflight result | Application architecture/transaction and security owners | `PENDING` |
| Operation | Authorize only `Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts` | Exact target, write scope, generated-ID fixture, assertions, cleanup statements/function, recovery and evidence-retention rules | Database/release authority with test owner | `PENDING` |

The `PostgreSqlProviderPreflight` operation constant is
`READ_ONLY_CATALOG_RECONCILIATION`; it does not authorize this write test. It
must not be widened or bypassed implicitly.

## Baseline conflict

The current working-copy hash is
`21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`.
Repository artifacts recording `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`
include:

- `Docs/reconciliation/T62B_1_EXACT_POSTGRESQL_DEFINITION_MATRIX.md`;
- `Docs/PA_TICKETS_IMPLEMENTATION_SPRINT_6_RESULT.md`;
- `Docs/PA_TICKETS_IMPLEMENTATION_SPRINT_7_RESULT.md`;
- `Docs/PA_TICKETS_POSTGRESQL_READ_ONLY_VALIDATION_AUTHORIZATION.md`;
- `tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironmentTests.cs` illustrative
  preflight inputs; and
- `Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`.

The `21D7...` value is recorded in Slice 8 and Slice 17 as the independently
recomputed working-copy hash. No repository artifact establishes an immutable
owner-adopted version or explains the byte difference. The conflict therefore
remains `PENDING`; neither value is approved, and no report or DDL was changed.

## Exact candidate operation and cleanup

The sole candidate is the existing repository test. It generates a ticket ID
from the current time plus a random suffix and a corresponding `PERSIST-*`
number, calls `ITicketRepository.AddAsync`, opens a fresh scope, reloads the
ticket, checks its fields and `TICKET_CREATED` audit/history records, and then
executes these `finally` operations for that generated ticket ID:

1. delete `PA_TICKETS_ASSIGNMENT` rows for `N_TICKET_ID`;
2. delete `PA_TICKETS_WORK_ITEM` rows for `N_TICKET_ID`;
3. delete `PA_TICKETS_EVENT` rows for `N_TICKET_ID`;
4. call `pa_tickets_maintenance_delete_audit_for_ticket(ticketId)`; and
5. delete the `PA_TICKETS` row for `N_TICKET_ID`.

The source constrains direct deletes by the generated ticket ID, but provider
foreign-key behavior, the maintenance function's exact effects, transaction
outcome, permissions, and cleanup verification still require live evidence.
Execution must stop if target identity, write outcome, cleanup outcome, or
evidence capture is uncertain; no manual cleanup is authorized by this result.

## Approval checklist before execution

- TGT decision and fresh independent identity evidence recorded.
- OP decision naming a non-superuser role (or an explicitly scoped exception),
  exact privileges, and expiry.
- BASE artifact/version and hash conflict resolved with provenance and adoption
  evidence.
- PRE authorization record satisfies the verifier's fail-closed fields and
  retention boundary.
- Operation-specific approval names only the repository test, its generated
  records, assertions, cleanup function/statements, recovery behavior, and
  retained evidence.
- A reviewer confirms no protected target, migration, schema change, RBAC data
  change, or unrelated test is included.

The repository contains no configured owner contact, ticket queue, or
submission integration for these decisions. Existing documents provide blank
attribution fields; an owner must submit the checklist through an externally
selected approved channel.

Once all gates are attributable and consistent, the next technical action is a
fresh target-guard and identity check followed by this one test only. Until
then, no provider operation is authorized.

## Validation and preservation

Reviewed the roadmap, Slice 17, dependency register, TGT/OP/BASE/PRE artifacts,
provider verifier and guard, persistence test, cleanup statements, and hash
records. `git diff --check` passed with existing LF/CRLF conversion notices.
No PostgreSQL or Oracle connection, SQL, migration, DDL/DML, provisioning,
seeding, cleanup, privilege change, or sequence operation occurred. All
tracked, untracked, ignored, and local worktree changes were preserved; no
reset, clean, stash, branch switch, commit, or push occurred.
