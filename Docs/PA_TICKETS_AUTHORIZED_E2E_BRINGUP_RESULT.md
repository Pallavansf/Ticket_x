# PA_TICKETS Authorized PostgreSQL E2E Bring-Up Result

**Branch:** `workstream-i-lifecycle-authority`  
**Authorized target:** `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`  
**Provider:** PostgreSQL 18.6  
**Scope:** target verification, read-only catalog discovery, and one contained persistence test

## Target verification

The ignored `.env.test.local` file was inspected without printing values. Its
generic `PA_TICKETS_TEST_CONNECTION_STRING` still describes the older
`pa_tickets_e2e_verify`/`postgres` target and was not used. The approved
`PA_TICKETS_E2E_DB_PASSWORD` value was read only into the current process. An
ephemeral connection descriptor was constructed for the explicitly authorized
target; no configuration file was changed.

Before each provider operation, the process rejected `pa_tickets_test`,
`pa_tickets_h_verify`, and `pa_tickets_test_user`, then issued a read-only
identity query. It returned:

- database: `pa_tickets_codex_e2e`
- role: `pa_tickets_codex`
- server endpoint: loopback IPv6 `::1`, port `5432`
- PostgreSQL version: `18.6`
- `transaction_read_only`: `off`
- `pg_is_in_recovery()`: `false`

No password, connection string, or secret was printed or written.

## Read-only catalog findings

The target is not empty. Catalog queries found **43 public tables, 40
sequences, and 88 indexes**. The foundational checked-in PostgreSQL DDL
declares 40 tables and 36 sequence declarations. The three additional table
objects are consistent with later RBAC/workflow migration artifacts, but their
provenance cannot be inferred from shape alone.

The foundational ticket, status, priority, waiting-reason, workflow, audit,
user, and relationship objects are present. The RBAC tables are present, but
the read-only statistics query reported zero rows for:

- `PA_TICKETS_PERMISSION`
- `PA_TICKETS_ROLE`
- `PA_TICKETS_ROLE_PERMISSION`
- `PA_TICKETS_USER`
- `PA_TICKETS_USER_ROLE`
- `PA_TICKETS_USER_CLIENT`
- `PA_TICKETS_WAITING_REASON`
- `PA_TICKETS_AUDIT`

The catalog contains no relation whose name identifies a migration ledger.
History-shaped objects observed were approval/workflow history tables only;
they do not establish migration application history. Existing objects and
their provenance therefore require an owner-approved baseline/adoption decision
before any schema change.

Migration 030 remains blocked by its reviewed active permission-catalog guard;
no RBAC rows were fabricated. Migrations 031–038, migration 026, and all
review candidates remain unexecuted in this bring-up. Migration 038 remains an
intentional blocked design gate for audit-actor compatibility.

## Provider tests executed

After the identity guard, one existing test with understood effects was run:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~PostgreSqlTicketPersistenceTests.Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts" -v minimal
```

Result: **1 passed, 0 failed, 0 skipped**. The test created one uniquely
identified ticket, verified fresh-context reload and its `TICKET_CREATED` audit
row, then deleted only its own ticket, event, work-item/assignment, and audit
records in its existing `finally` cleanup. No fixture, seed, migration, or
shared-data cleanup was performed.

A second reviewed test was attempted after another exact identity guard:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~PostgreSqlTicketPersistenceTests.Ticket_lifecycle_service_persists_and_reloads_core_ticket_flow_in_postgresql" -v minimal
```

Result: **0 passed, 1 failed, 0 skipped**. Ticket creation reached the
approved database, but `ClassifyAsync` failed closed in
`DatabaseAuthorizationFactsReader` because its permission query references the
absent `p.S_MODULE_CODE` column (Npgsql SQLSTATE `42703`). The test's existing
`finally` cleanup executed for its test-owned identifiers; no schema or
authorization rows were changed.

No other provider-backed tests were run because their fixture, RBAC, cleanup,
or migration dependencies are not yet authorized or are not safe to infer
from the unknown baseline. No database-independent suite was rerun in this
bring-up.

## Remaining gates

The next schema-changing operation still requires separate written
authorization specifying the exact SQL or migration, expected effects,
backup/recovery and rollback evidence, baseline/provenance treatment, and
cleanup boundary. In particular, no permission catalogue, roles, grants,
scope/delegation rows, migration-ledger entries, or audit-actor data may be
invented to unblock migration 030 or later migrations. Migration 026 remains a
separate approval chain.

The repository-level persistence proof is currently the next V1 milestone
supported by the verified target. Full lifecycle/API E2E remains blocked until
the owner-approved RBAC schema contract supplies the permission metadata
required by `DatabaseAuthorizationFactsReader`; after that prerequisite, the
same test-owned lifecycle path can be rerun under the existing identity guard.

## Preservation and restrictions

The branch and all pre-existing tracked, untracked, ignored, and local files
were preserved. No `.env.test.local` or `.vscode/mcp.json` change was made.
`tests/scripts/provision-postgresql-schema.ps1` was not run. No Oracle access,
migration execution, migration-026 operation, privilege change, broad cleanup,
sequence reset, or commit/push occurred.

