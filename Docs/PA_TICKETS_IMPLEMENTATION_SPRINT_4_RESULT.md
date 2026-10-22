# PA_TICKETS Implementation Sprint 4 Result

**Repository:** `G:\\WSS\\aw\\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** PostgreSQL-specific read-only catalog adapter, implemented without a provider run

## Adapter design

`tests/PA.Tickets.Domain.Tests/PostgreSqlCatalogReader.cs` extends the Sprint 3
snapshot and comparison seam. `PostgreSqlCatalogReader.CreateTarget` first
uses the existing `BuildTargetDescriptor` guard, requires an explicit baseline
artifact, rejects prohibited databases/roles and multi-host descriptors, and
returns a non-logging `PostgreSqlReadOnlyTarget` value. It does not enable the
write opt-in used by mutating integration tests.

`ReadAsync` uses only fixed PostgreSQL catalog queries against
`information_schema` and PostgreSQL system catalogs for identity, tables,
columns, constraints, indexes, and sequences. It does not query application
rows, execute DDL/DML, run migrations, or select a baseline implicitly.
Identity is checked for database, user, and server port before metadata is
returned. Any mismatch or incomplete synthetic metadata fails closed.

`FromRows` is provider-independent mapping logic used by the tests. It sorts
objects and all metadata categories before constructing the existing snapshot
type. Catalog shape is reported separately from migration history; no object
is treated as proof that a migration ran.

## Tests

Added tests cover catalog-row mapping, nullable/default fields, deterministic
ordering, explicit baseline selection, target mismatch, prohibited descriptor,
multi-host rejection, and credential redaction. Existing Sprint 1 and Sprint 2
guard tests remain in place. No test calls `ReadAsync` or opens a PostgreSQL
connection.

## Validation

```text
dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\t64-sprint4-final2\\bin\\ -v minimal
```

Result: **build succeeded, 0 warnings, 0 errors**.

The focused database-independent test filter covered the existing guard and
reconciliation tests plus the new adapter tests. Result: **13 passed, 0
failed**. The provider reachability test was not selected.

## Limitations and future authorization

The adapter has not been validated against a live PostgreSQL server. In
particular, provider-specific catalog column types, permissions, server-version
differences, DNS/peer endpoint evidence, and behavior of `pg_sequences` still
require a separately approved read-only provider run. The current identity
check compares database, user, and port; a future owner-approved policy must
define how hostname aliases and resolved peer addresses are authenticated.

A future run must explicitly authorize the exact target, operation (read-only
catalog metadata only), access boundary, selected baseline artifact and hash,
and evidence-retention location. It must use the existing target guard and may
not access protected databases or roles. No baseline adoption, migration
execution, or persistence E2E is authorized by this sprint.

## Preservation and safety

No database/provider connection, SQL execution, DDL, DML, migration,
provisioning, seeding, cleanup, sequence operation, privilege change, or
provider-backed test occurred. No credentials or complete connection strings
were logged. Existing modified, untracked, and ignored work was preserved; no
reset, clean, stash, branch switch, commit, or push was used.

**IMPLEMENTATION SPRINT 4 COMPLETE — READ-ONLY CATALOG ADAPTER IMPLEMENTED AND SYNTHETICALLY VALIDATED; LIVE PROVIDER RUN REMAINS SEPARATELY AUTHORIZED**

