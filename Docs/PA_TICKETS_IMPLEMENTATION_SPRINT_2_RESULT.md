# PA_TICKETS Implementation Sprint 2 Result

**Repository:** `G:\\WSS\\aw\\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source-level PostgreSQL reconciliation and one database-independent guard increment

## Executive result

The checked-in source supports the foundational waiting-reason and work-item
column names used by the application. It does not establish a complete,
adoptable PostgreSQL baseline: the repository contains multiple schema
variants, later RBAC migrations, and no source evidence of the history of the
existing catalog. No database or provider operation was performed.

The smallest safe implementation increment was to make the PostgreSQL test
target guard reject a multi-host connection string. A comma-separated host
list could otherwise allow failover to an endpoint whose identity was not the
approved descriptor. The change is fail-closed, does not broaden access, and
was covered by a focused unit test.

## Source-to-schema reconciliation

| Area | Classification | Evidence and consequence |
|---|---|---|
| Work-item public identifier | `NO_CHANGE_REQUIRED` | `src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs:19` maps `WorkItemRow.PublicId` to `S_PUBLIC_WORK_ITEM_ID`; both `tests/scripts/postgresql-schema.sql:36-59` and `tests/scripts/postgresql-foundational-ddl.sql:49-72` define that column. |
| Waiting expected date | `NO_CHANGE_REQUIRED` | `TicketsDbContext.cs:19` maps `WaitingExpectedDate` to `D_WAITING_EXPECTED_AT`; both PostgreSQL scripts add the same nullable `timestamptz` column at lines 59 and 72. |
| Waiting-reason requirement flag | `NO_CHANGE_REQUIRED` | `TicketsDbContext.cs:20` maps `RequireExpectedDate` to `N_REQUIRE_EXPECTED_DATE`; the scripts define a non-null boolean with default `false` at lines 70 and 83. The application catalog reader uses the value in `src/PA.Tickets.Infrastructure/Tickets/PersistentWaitingReasonCatalog.cs:15-22`. |
| Waiting-reason key generation | `SOURCE_CONTRACT_AMBIGUITY` | `TicketsDbContext.cs:20` maps `WaitingReasonRow.Id` but declares no EF `HasDefaultValueSql`; the PostgreSQL scripts create `PA_TICKETS_WAITING_REASON_SEQ` and use it as the column default (`postgresql-schema.sql:60-62`, `postgresql-foundational-ddl.sql:73-75`). No production write path proving whether IDs are always explicit was found. Adding a default to EF or changing the schema would be a provider/ownership decision, so no change was made. |
| Relation persistence | `NO_CHANGE_REQUIRED` | `TicketsDbContext.cs:24` maps `RelationRow` to `PA_TICKETS_RELATION` and its sequence. The phase-5 PostgreSQL script defines the table, sequence, unique index, and related lookup index at `tests/scripts/postgresql-phase5-schema.sql:17,250-262`. |
| Approver and approval fields | `NO_CHANGE_REQUIRED` | `TicketRow.ApprovalApproverUserId` is mapped to `N_APPROVER_USER_ID` (`TicketsDbContext.cs:102`); the foundational and current PostgreSQL scripts include the nullable column (`postgresql-foundational-ddl.sql:44`, `postgresql-schema.sql:31`). Approval rows also map the same column (`TicketsDbContext.cs:30` area), so this is not evidence for a new DDL change. |
| Later RBAC and migration history | `EXISTING_SCHEMA_DIFFERENCE` / `OWNER_DECISION_REQUIRED` | Source-controlled migrations `database/migrations/postgresql/033_rbac_migration_ledger.sql` through `038_rbac_audit_actor_compatibility_BLOCKED.sql` exist, but no ledger/history evidence ties them to the observed catalog. Migration 038 intentionally blocks on unresolved audit-actor compatibility; 037 requires an explicit provisioning setting; 033-036 add later RBAC structures. A source-only review cannot adopt or upgrade an unknown catalog. |
| Audit actor | `OWNER_DECISION_REQUIRED` | The application maps audit actors (`TicketsDbContext.cs` audit mappings) while the blocked migration 038 remains an intentional abort. Actor nullability, FK semantics, and compatibility with existing audit rows require an owner decision before that migration can be reconsidered. |
| Reporting migration 026 | `OWNER_DECISION_REQUIRED` | Migration 026 remains a separately governed review candidate. Its hash and target authorization are not changed by this sprint; no runner or SQL was executed. |

The foundational scripts are not interchangeable: `postgresql-schema.sql`
contains reference-data inserts and triggers, `postgresql-foundational-ddl.sql`
is DDL-oriented, and `postgresql-phase5-schema.sql` adds later communication,
attachment, and relation objects. Their presence does not prove live
provenance or migration order.

## Implementation change

`tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironment.cs` now rejects a
comma-separated PostgreSQL host list while constructing the approved target
descriptor. This prevents a multi-host/failover connection string from being
treated as one independently verified endpoint. The existing opt-in,
credential-redaction, exact descriptor, and prohibited database/role checks
remain intact.

`tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironmentTests.cs` adds
`Target_guard_rejects_multi_host_connection_strings`, asserting fail-closed
behavior and that the password is absent from the exception. The guard and
test files already contained Sprint 1 changes; this is the Sprint 2
increment only.

## Validation performed

The following commands were run without a database connection:

```text
dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\t62-sprint2-final\\bin\\ -v minimal
```

Result: build succeeded, 0 warnings, 0 errors.

```text
dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~PostgreSqlTestEnvironmentTests.Missing_postgresql_connection_configuration_is_a_test_failure|FullyQualifiedName~PostgreSqlTestEnvironmentTests.PostgreSQL_tests_require_explicit_write_opt_in|FullyQualifiedName~PostgreSqlTestEnvironmentTests.PostgreSQL_tests_require_an_exact_independent_target_descriptor|FullyQualifiedName~PostgreSqlTestEnvironmentTests.Target_descriptor_excludes_credentials_and_contains_identity_fields|FullyQualifiedName~PostgreSqlTestEnvironmentTests.PostgreSQL_tests_reject_prohibited_database_or_role_even_when_descriptor_matches|FullyQualifiedName~PostgreSqlTestEnvironmentTests.Target_guard_rejects_multi_host_connection_strings" -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\t62-sprint2-final\\bin\\ -v minimal
```

The focused filter for the PostgreSQL test-environment guard ran 8 tests;
**8 passed, 0 failed**. The set covered missing configuration, explicit
write opt-in, exact target matching, credential exclusion, all three
prohibited database/role vectors, and the new multi-host rejection. No test
opened a provider connection; the reachability test was excluded.

## Remaining blockers and next step

The following remain unresolved and are not fixed by this sprint:

- baseline adoption and provenance of the existing catalog;
- least-privilege schema operator selection (the prior metadata gate observed
  an over-privileged account, but this sprint did not reconnect);
- waiting-reason sequence/default ownership and existing-key reconciliation;
- canonical RBAC codes, historical effective dates, client/scope/delegation,
  and built-in role decisions;
- unknown migration history and ledger representation;
- audit actor contract and migration 038's intentional block;
- provider-backed EF translation, transaction, concurrency, and recovery
  evidence;
- migration 026's separate authorization, target fingerprint, and execution
  gates.

The next smallest safe step is an owner-reviewed, source-only exact definition
and baseline decision packet, followed—only after explicit authorization—by a
separately guarded read-only provider reconciliation. This sprint does not
authorize schema changes or PostgreSQL-backed E2E tests.

## Preservation and safety

No database, Oracle, SQL, migration, provisioning, seed, cleanup, sequence,
privilege, or provider-backed test operation occurred. Protected databases and
roles were not accessed. Existing modified, untracked, and ignored work was
preserved; no reset, clean, stash, branch switch, commit, or push was used.

**IMPLEMENTATION SPRINT 2 COMPLETE — SOURCE RECONCILIATION AND SAFE GUARD INCREMENT VALIDATED; DATABASE OPERATIONS NOT PERFORMED**

