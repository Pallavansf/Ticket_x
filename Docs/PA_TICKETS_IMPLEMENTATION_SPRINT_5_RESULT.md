# PA_TICKETS Implementation Sprint 5 Result

**Repository:** `G:\\WSS\\aw\\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source review and database-independent hardening of the PostgreSQL catalog adapter

## Reviewed files

Reviewed the Sprint 3 and Sprint 4 reconciliation/report documents, the
proposed upgrade policy, `PostgreSqlCatalogReader.cs`,
`PostgreSqlSchemaReconciliation.cs`, `PostgreSqlTestEnvironment.cs`, the
focused tests, the test project provider references, and the relevant source
schema/mapping context.

## Confirmed issues and fixes

1. **Catalog-row mapping mismatch (fixed).** `ColumnsSql` selects six fields
   (`table_schema`, `table_name`, `column_name`, `data_type`, `is_nullable`,
   `column_default`), but the adapter labeled mapped rows as `table`. That
   caused column metadata to be silently omitted by `FromRows`. Column rows
   now use the explicit `column` kind, matching the mapping branch and all six
   selected fields.

2. **Fail-open malformed metadata (fixed).** Previously unknown kinds,
   duplicate tables, duplicate/contradictory columns, and constraint/index
   rows for undeclared tables could be merged into an incomplete snapshot.
   `FromRows` now rejects unsupported kinds, duplicate table rows, duplicate
   or contradictory column identities, and undeclared table metadata.

3. **Deterministic sequence mapping retained and covered.** `SequencesSql`
   selects `schemaname`, `sequencename`, `data_type`, `start_value`, and
   `increment_by`; the adapter now preserves all selected sequence values in a
   deterministic detail string. PostgreSQL `pg_sequences` availability and
   privilege/version semantics remain unverified and are explicitly not
   claimed as cross-version compatible.

4. **Identity and secret handling reviewed.** Target creation still requires
   the existing descriptor guard, explicit baseline artifact, and exact target
   match. Errors contain no connection string or password. The adapter does
   not enable or bypass the write opt-in. The unresolved policy is how a
   hostname alias and resolved peer address should be authenticated; this
   sprint does not choose that policy.

## Tests added

Added synthetic tests for unsupported/undeclared rows and contradictory
duplicate columns. Existing tests cover query-row mapping, null/default
handling, ordering, baseline requirement, identity mismatch, prohibited and
multi-host descriptors, and credential redaction. No test opens a connection.

## Validation

```text
dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\t65-sprint5-final\\bin\\ -v minimal
```

Result: **build succeeded, 0 warnings, 0 errors**.

```text
dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~Missing_postgresql_connection_configuration_is_a_test_failure|FullyQualifiedName~PostgreSQL_tests_require_explicit_write_opt_in|FullyQualifiedName~PostgreSQL_tests_require_an_exact_independent_target_descriptor|FullyQualifiedName~Target_descriptor_excludes_credentials_and_contains_identity_fields|FullyQualifiedName~PostgreSQL_tests_reject_prohibited_database_or_role_even_when_descriptor_matches|FullyQualifiedName~Target_guard_rejects_multi_host_connection_strings|FullyQualifiedName~Schema_reconciliation_requires_explicit_baseline_and_is_deterministic|FullyQualifiedName~Schema_reconciliation_rejects_target_mismatch_before_comparison|FullyQualifiedName~Schema_reconciliation_rejects_prohibited_descriptor|FullyQualifiedName~Catalog_rows_map_to_sorted_snapshot_metadata|FullyQualifiedName~Catalog_target_factory_requires_explicit_baseline_without_write_opt_in|FullyQualifiedName~Catalog_mapping_fails_closed_for_unsupported_or_undeclared_rows|FullyQualifiedName~Catalog_mapping_rejects_contradictory_duplicate_columns" -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\t65-sprint5-final\\bin\\ -v minimal
```

Result: **16 passed, 0 failed, 0 skipped**. Provider reachability and all
database-backed tests were excluded.

## Remaining limitations

No live PostgreSQL catalog was queried. `pg_sequences` field availability,
permissions, type semantics, PostgreSQL-version differences, actual identity
resolution, TLS/DNS/peer evidence, and provider error behavior still require a
separately approved read-only provider run. Matching catalog shape still does
not establish migration history or baseline adoption. Migration 026 remains a
separate authorization chain.

## Preservation and safety

No PostgreSQL or Oracle connection, SQL execution, DDL, DML, migration,
provisioning, seeding, cleanup, sequence operation, privilege change, or
provider-backed test occurred. Existing modified, untracked, and ignored work
was preserved; no reset, clean, stash, branch switch, commit, or push was
used.

**IMPLEMENTATION SPRINT 5 COMPLETE — CATALOG ADAPTER HARDENED AND SYNTHETICALLY VALIDATED; LIVE PROVIDER VALIDATION REMAINS REQUIRED**

