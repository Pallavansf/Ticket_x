# PA_TICKETS Stage 2, Slice 7 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** provider-validation readiness only  
**Status:** `PARTIAL` — no provider operation was authorized or performed.

## Existing provider tests

| Scenario | Existing test(s) | Test-owned data/cleanup | Current readiness |
|---|---|---|---|
| Repository persistence and reload without authorization facts | `PostgreSqlTicketPersistenceTests.Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts` | Generated ticket ID/number; deletes assignments, work items, events, audit function output, then ticket | Most independent candidate; requires exact target guard and compatible foundational objects. |
| Core lifecycle persistence/reload | `PostgreSqlTicketPersistenceTests.Ticket_lifecycle_service_persists_and_reloads_core_ticket_flow_in_postgresql` | Generated IDs for all ticket types; cleanup in `finally` | Blocked by authorization/catalogue configuration and missing `S_MODULE_CODE` when authorization facts are read. |
| Concurrent lifecycle transition | `PostgreSqlTicketPersistenceTests.Concurrent_ticket_lifecycle_saves_allow_only_one_transition` | Generated ticket and cleanup | Requires provider concurrency evidence plus authorization facts/schema. |
| Wait/resume/resolve/close/reopen/cancel | `PostgreSqlTicketPersistenceTests.Ticket_lifecycle_service_persists_wait_resume_resolution_close_reopen_and_cancel` | Generated ticket/work item and cleanup | Blocked by lifecycle authorization facts and provider transaction evidence. |
| Incident and Problem persistence | `PostgreSqlIncidentTests.*`, `PostgreSqlProblemTests.*` | Per-test generated tickets/configuration; `finally` cleanup | Requires subtype schema, RBAC facts, and provider transaction evidence. |
| Caller-owned transaction rollback | `PostgreSqlAuditTransactionTests.Operational_audit_append_shares_callers_transaction_with_mutation` | Generated ticket; explicit transaction rollback; cleanup | Candidate for provider validation after target/schema authorization; does not itself validate IncidentService. |
| Audit-chain concurrency | `PostgreSqlAuditTransactionTests.Concurrent_operational_audit_appends_preserve_one_ticket_chain` | Generated ticket; chain verification; cleanup | Requires provider lock/isolation behavior and audit-chain objects. |
| API/browser persistence and authorization | `PostgreSqlApiE2ETests.*` | Trackers and explicit cleanup helpers | Blocked by persisted RBAC catalog/grants and Stage 5 authorization gates. |

The provider suite calls `PostgreSqlTestEnvironment.EnsureConfigured()` before
opening a connection. It rejects prohibited databases/roles and requires an
explicit opt-in and approved target descriptor. `PostgreSqlProviderPreflight`
separately requires owner-approved operation, expiry, target evidence,
least-privilege decision, repository-local baseline, and matching SHA-256.

## Failure-injection coverage

Provider tests currently cover explicit rollback and concurrent audit appends,
but do not inject a PostgreSQL `SaveChangesAsync` failure, commit failure,
connection loss, or disposal failure for IncidentService. The provider-
independent `TransactionOutcomeTests` model these classifications, while
`AuditTransactionOwnershipTests` verifies only commit ownership. These fakes
cannot establish Npgsql behavior. A future provider test must use an approved
fault-injection mechanism or a controlled provider failure and must record
whether the outcome is confirmed aborted, confirmed committed, or unknown.

## Missing `S_MODULE_CODE` impact

The missing permission metadata column blocks tests that invoke
`DatabaseAuthorizationFactsReader` or authenticated API authorization. The
repository-only persistence test deliberately bypasses authorization facts and
is the only existing independent candidate identified by source review. It
still requires a verified target and compatible schema, and its cleanup SQL
must be reviewed before execution. No test was run in this slice.

## Ordered validation matrix

1. Obtain attributable TGT/OP/BASE authorization and verify the target as
   `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex` immediately before
   any connection.
2. Verify the selected baseline artifact and SHA-256 without changing it;
   capture a read-only catalog snapshot and migration-history result.
3. Run the repository-only persistence/reload test, if the owner-approved
   schema baseline and cleanup boundary permit it.
4. Run the caller-owned rollback and audit-chain concurrency tests with
   generated records and `finally` cleanup, recording provider identity and
   exact rows affected.
5. Run lifecycle, Incident, and Problem provider tests only after the RBAC
   permission metadata/grant contract is approved and the schema is reconciled;
   do not fabricate `S_MODULE_CODE`, role, grant, scope, or migration-ledger
   data.
6. Run API/browser acceptance only after persisted authorization, target
   identity, transaction/recovery, and cleanup evidence satisfy Stage 5.

No step authorizes DDL, migrations, RBAC data changes, privilege changes,
sequence operations, or migration 026.

## Harness and cleanup assessment

No harness change was justified without provider access. Existing tests use
generated identifiers and explicit `finally` cleanup, but cleanup itself issues
SQL and therefore requires the same target guard and operation authorization as
the test. The provisioning script remains prohibited. Any future fixture must
prove it cannot touch `pa_tickets_test` or `pa_tickets_h_verify`, must not infer
migration history, and must retain a redacted evidence record.

## Validation performed in this slice

- Read-only inspection of the roadmap, dependency register, Slice 1–6 reports,
  RBAC reconciliation, provider guard, test environment, lifecycle persistence,
  and audit transaction tests.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported.
- No source or test code was changed, so no build or test command was run.

No PostgreSQL/Oracle connection, SQL, migration, DDL/DML, provisioning,
seeding, cleanup, privilege change, sequence operation, or provider-backed test
was performed. All existing tracked, untracked, ignored, and local worktree
changes were preserved; no reset, clean, stash, branch switch, commit, or push
occurred.

Stage 2 remains `PARTIAL`. The next concrete provider action is the separately
authorized identity-guarded repository persistence test; if its baseline or
cleanup authorization is not supplied, stop before connecting.
