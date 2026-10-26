# PA_TICKETS Stage 2, Slice 8 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Status:** `BLOCKED — provider prerequisites not satisfied`

## Preflight result

The repository dependency register and Slice 7 report still show TGT, OP, and
BASE as pending. No attributable owner approval, target-identity evidence,
least-privilege operator decision, baseline adoption decision, retention record,
or operation-specific authorization is present in the inspected repository
artifacts. The register explicitly says that owner policy selection is not a
signed approval and that no submission channel is configured.

The provider guard also requires an explicit opt-in, approved target
descriptor, owner-approved operation, expiry, target evidence, least-privilege
reference, and a repository-local baseline hash. Those inputs are not safely
available for this one-test execution.

## Test and cleanup scope reviewed

`PostgreSqlTicketPersistenceTests.Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`
would:

1. open the configured target after `EnsureConfigured()`;
2. insert one generated ticket through `ITicketRepository.AddAsync`;
3. open a fresh scope and reload the ticket, audit row, and history; and
4. in `finally`, issue deletes restricted to that generated ticket ID for
   assignments, work items, events, maintenance-audit rows, and the ticket.

The generated ID/number provide test-level uniqueness, but the cleanup is still
SQL against the target and therefore requires the same explicit authorization.
No manual cleanup was attempted.

## Baseline/hash evidence

The current working-copy hash of
`tests/scripts/postgresql-foundational-ddl.sql` is:

`21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`

The existing provider-preflight unit test contains a different illustrative
hash (`5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`),
and no owner-approved baseline artifact/hash pair is recorded for this test.
The discrepancy is additional evidence that BASE approval cannot be inferred.
No file was modified.

## Stop decision

Because the required TGT/OP/BASE approvals and operation authorization are
absent, the process stopped before reading any connection configuration for
execution, opening a PostgreSQL connection, verifying server identity, or
running the test. No lifecycle, RBAC, migration, schema, or unrelated provider
test was run.

## Validation

- Read-only inspection of Slice 7, the dependency register, provider guard,
  provider preflight, the exact persistence test, and its cleanup.
- `Get-FileHash tests/scripts/postgresql-foundational-ddl.sql -Algorithm SHA256`
  — `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported.
- No database/provider connection, SQL execution, migration, DDL/DML,
  provisioning, seed, cleanup, privilege, sequence, or schema operation.

## Required next action

An accountable owner must provide attributable TGT, OP, and BASE decisions,
the exact approved baseline artifact and SHA-256, the operation authorization
covering this test's writes and test-owned cleanup, and valid target evidence
for `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`. Until then,
Stage 2 remains `PARTIAL` and repository persistence is unverified.

All existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
