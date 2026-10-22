# PA_TICKETS Implementation Sprint 1 Result

**Branch:** `workstream-i-lifecycle-authority`

## Increment implemented

The existing PostgreSQL integration guard now rejects the prohibited databases
`pa_tickets_test` and `pa_tickets_h_verify`, and the prohibited role
`pa_tickets_test_user`, even when a caller supplies a matching approved-target
descriptor. This closes the gap where descriptor equality alone could permit a
protected target. Credentials remain excluded from error messages.

Added database-independent unit vectors for all three prohibited-target cases.
The guard still requires the existing explicit write opt-in and independent
target descriptor; no permissive fallback or mock provider was introduced.

## Files changed by this increment

- `tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironment.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironmentTests.cs`

Both files already contained unrelated pre-existing worktree modifications;
only the prohibited-target check and its focused tests were added in this
sprint. No application business logic, migration, environment, or database
configuration was changed.

## Safe validation

Executed:

```text
dotnet build tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\t62-sprint1-final\bin\ -v minimal
```

Result: **build succeeded, 0 warnings, 0 errors**.

Executed only the seven focused `PostgreSqlTestEnvironmentTests` cases covering
missing configuration, opt-in, descriptor mismatch, credential redaction, and
all prohibited database/role vectors, with `--no-build --no-restore` against
the isolated output. Result: **7 passed, 0 failed**.

Database reachability, provider-backed tests, SQL, migrations, provisioning,
seeding, cleanup, sequence operations, Oracle, and protected targets were not
accessed. The database-reachability test was explicitly excluded.

## Remaining dependency

The next E2E implementation step still requires separately approved schema
baseline/adoption, least-privilege operator, sequence, RBAC-history, audit
actor, and migration authorization decisions. Once those exist, a distinct
provider-validation gate may select the approved target and operation. This
sprint does not claim PostgreSQL persistence or API/Angular E2E success.

## Preservation

The branch remained `workstream-i-lifecycle-authority`. All modified,
untracked, and ignored work was preserved; no reset, clean, stash, branch
switch, commit, push, database operation, or permission change occurred.
