# PA_TICKETS Stage 2 Slice 48 — Approved PostgreSQL Test Configuration

## Result

The approved target configuration is not available through the repository’s accepted test-process inputs. No database connection, SQL, provider test, or migration was attempted.

## Configuration path

`PostgreSqlTestEnvironment.EnsureConfigured()` in `tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironment.cs` consumes:

1. `PA_TICKETS_TEST_CONNECTION_STRING` for the Npgsql connection string;
2. `PA_TICKETS_RUN_POSTGRESQL_TESTS`, which must equal `I_UNDERSTAND_POSTGRESQL_TEST_WRITES`;
3. `PA_TICKETS_APPROVED_TEST_TARGET`, which must exactly match the non-secret descriptor parsed from the connection string (`host=...;port=...;database=...;user=...`).

The infrastructure provider path also accepts `ConnectionStrings:TicketsPostgres`, but the provider tests still call the explicit `PostgreSqlTestEnvironment` guard first. `tests/TESTING.md` documents the same precedence and fail-closed behavior. No code loads `.env.test.local` automatically; it is a local secret source that must be supplied to the test process by the operator.

## Observed sources (values redacted)

- `.env.test.local`: `PA_TICKETS_TEST_CONNECTION_STRING` is present and identifies `localhost:5432 / pa_tickets_e2e_verify / postgres`.
- Current process environment: `PA_TICKETS_TEST_CONNECTION_STRING` is present, but no approved-target opt-in or approved descriptor was present at inspection time.
- User environment: `PA_TICKETS_TEST_CONNECTION_STRING` is present and identifies the prohibited `localhost:5432 / pa_tickets_h_verify / pa_tickets_test_user` target. It must never be used.
- Machine environment: no relevant PostgreSQL test variables were present.
- `PA_TICKETS_E2E_DB_PASSWORD`, `ConnectionStrings__TicketsPostgres`, `PA_TICKETS_RUN_POSTGRESQL_TESTS`, and `PA_TICKETS_APPROVED_TEST_TARGET` were not present in the inspected process, user, or machine environment. No password value was read or reported.

The approved `pa_tickets_codex_e2e / pa_tickets_codex` credentials were therefore not available in an authorized local source. The existing values cannot be repurposed, and the prohibited user-level target must not be used as a fallback.

## Required safe handoff

The accountable owner/operator must place the approved target’s secret-bearing Npgsql connection string in the established local secret mechanism, then provide the test process (without committing or printing the secret) with:

```text
PA_TICKETS_TEST_CONNECTION_STRING=<approved Npgsql connection string>
PA_TICKETS_RUN_POSTGRESQL_TESTS=I_UNDERSTAND_POSTGRESQL_TEST_WRITES
PA_TICKETS_APPROVED_TEST_TARGET=host=localhost;port=5432;database=pa_tickets_codex_e2e;user=pa_tickets_codex
```

The operator must independently verify that the parsed descriptor is exactly the approved target before any provider test. No repository file, `.env.test.local`, `.vscode/mcp.json`, role, grant, or database state should be changed by this slice.

Stage 2 remains `PARTIAL`; incident atomicity and PostgreSQL rollback remain unverified. Existing worktree changes were preserved, with no reset, clean, stash, branch switch, commit, or push.
