# PostgreSQL integration environment

PostgreSQL 18 is the primary development/integration provider and is supported through `Npgsql.EntityFrameworkCore.PostgreSQL`. Oracle 19c remains a supported alternative through `Oracle.EntityFrameworkCore`; Domain and Application assemblies do not reference either provider.

## Secure setup

Set the connection string outside the repository, for example as a process or user environment variable:

```powershell
$env:PA_TICKETS_TEST_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<local-secret>'
```

For database creation, an optional `PA_TICKETS_TEST_ADMIN_CONNECTION_STRING` may be supplied. `PA_TICKETS_TEST_DATABASE` defaults to `pa_tickets_test`. An optional `PA_TICKETS_TEST_USER` is reported for the dedicated login. Password values must never be committed or printed.

Run:

```powershell
powershell -NoProfile -File tests/scripts/verify-postgresql.ps1
```

The script reports server version, configured port, current database/user, database-creation capability, and whether the dedicated database was created. It never prints either connection string.

## Harness boundary

PostgreSQL-backed xUnit tests require `PA_TICKETS_TEST_CONNECTION_STRING`. Missing configuration is a test failure, not a skip or successful early return; an unreachable server also fails the connection preflight. `PA_TICKETS_TEST_ADMIN_CONNECTION_STRING` may be supplied for database provisioning and is not needed when the dedicated test database already exists.

Run the NuGet advisory gate before release:

```powershell
powershell -NoProfile -File tests/scripts/check-nuget-vulnerabilities.ps1
```

The EF infrastructure selects PostgreSQL or Oracle explicitly at startup and fails fast for unsupported providers. Domain and Application logic is shared. Provider-specific sequence defaults, transient/unique error classification, and row-claim SQL are isolated in Infrastructure capabilities. Oracle locking, sequences, `DBMS_CRYPTO`, migration behavior, and final persistence compatibility remain **ORACLE FINAL GATE** items.

## PostgreSQL schema adapter

Added `tests/scripts/postgresql-schema.sql` and the scoped `provision-postgresql-schema.ps1` runner. It creates repeatable PostgreSQL-native sequences, durable execution tables, foreign keys, public-ID uniqueness, work-item uniqueness, and active-assignment uniqueness only in `pa_tickets_test`; Oracle migrations are not used. The provisioner also runs a real PostgreSQL insert/commit and rollback smoke script. These are **POSTGRESQL INTEGRATION** checks, supplemented by API-host and repository E2E tests for durable approvals, concurrency, idempotency, and audit history.

## SLA administration (Phase 6 Slice 4)

SLA administration exposes the existing policy/version rows and the existing `BusinessCalendar` representation; it does not add a second calculation engine. PostgreSQL provisions the additive adapter in `tests/scripts/postgresql-slice4-schema.sql`; Oracle preparation is migration `020_sla_escalation_administration.sql`.

Policy selection first favors the number of specified matching target dimensions. For equal dimension counts, the deterministic rank is RequestType, SubCategory, Category, Service, Client, TicketType, Priority, Department, then Team. An equal-ranked match across different policies is rejected as ambiguous rather than resolved by effective date/version. SubCategory and Category are checked against the catalogue hierarchy. Ticket creation supplies Service, Category, SubCategory, RequestType, type, Priority, Department, and Team. The current ticket-create command does not supply a ClientId, so client-targeted policies are matchable by the selector context but are not selectable through the current ticket-creation path.

Calendar storage continues to use the runtime's single working interval per weekday, timezone ID, and holiday set. When `Jobs:Enabled` is true, `PersistentBackgroundWorker` invokes `SlaDeadlineMonitor` to detect and persist due response/resolution breaches and terminal completion; escalation work is durably queued and processed by the registered handler. This is worker-driven and does not run when the worker is disabled. The current SLA runtime does not interpret the policy `BreachRules` or `NotificationRules` JSON as arbitrary notification or assignment actions. Calendar-version retention, daylight-saving behavior, and Oracle execution remain unverified.
