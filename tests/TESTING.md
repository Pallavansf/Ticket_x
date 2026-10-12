# PA Tickets test classification

## UNIT

The existing domain suite covers ticket lifecycle, evidence, SOD, waiting/resume, SLA clock behavior, workflow joins/conditions, and sequential approval rules.

## APPLICATION

`ExecutionConfigurationTests` executes the real `ConfigurationWorkflowRouter` and validates deterministic specificity precedence and equal-specificity ambiguity rejection. These tests use the application/infrastructure implementation with in-memory configuration only; they do not verify database persistence.

## API / CONCURRENCY / E2E

PostgreSQL API, persistence, concurrency, lifecycle, workflow, approval, SLA-operation, and durable-job E2E tests are available and run against the configured PostgreSQL 18 database. Oracle-specific migration, locking, rollback, and deployment validation remain intentionally unexecuted.

## ANGULAR

The Angular project has a `test` script but no configured test target/spec suite. The CLI rejects the attempted non-watch flags; production `npm.cmd run build` remains the supported verified command.

## ORACLE

Oracle migration, persistence, locking, concurrency, rollback, and deployment validation tests are final-gate tests and are intentionally not executed in this environment.

## PostgreSQL integration environment

The local PostgreSQL service is available. A credential-safe verification harness is available at `tests/scripts/verify-postgresql.ps1`; it requires `PA_TICKETS_TEST_CONNECTION_STRING` and never prints it. Connection/database setup is environment-dependent; use the harness output rather than assuming a configured connection. No PostgreSQL result is evidence of Oracle compatibility. See `tests/POSTGRESQL_INTEGRATION.md`.

## PostgreSQL provider adapter (development/integration only)

Infrastructure now selects PostgreSQL only when `Database:Provider=PostgreSQL`; it reads `ConnectionStrings:TicketsPostgres` or the process environment variable `PA_TICKETS_TEST_CONNECTION_STRING`. The default path remains Oracle 19c with `UseOracle`. Npgsql EF Core 8.0.11 is referenced alongside the Oracle provider. Oracle migrations are not executed against PostgreSQL. A real PostgreSQL schema/persistence smoke test remains pending because the current model still contains Oracle-specific sequence/default and schema assumptions that require a dedicated PostgreSQL schema strategy before `EnsureCreated` or repository persistence can be claimed.

## PostgreSQL schema adapter

Added `tests/scripts/postgresql-schema.sql` and the scoped `provision-postgresql-schema.ps1` runner. It creates repeatable PostgreSQL-native sequences, core ticket/work-item/assignment/event/audit tables, foreign keys, public-ID uniqueness, work-item uniqueness, and active-assignment uniqueness only in `pa_tickets_test`; Oracle migrations are not used. The provisioner runs real PostgreSQL insert/commit and rollback smoke checks. PostgreSQL repository and API integration tests separately exercise persistence and runtime behavior against the provider-specific schema adapter. These are **POSTGRESQL INTEGRATION** checks, not evidence of Oracle compatibility.
