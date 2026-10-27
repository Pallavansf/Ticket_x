# S5-09 Mapped PostgreSQL schema gaps ? review only

**Slice:** S5-09 ? FR-002, FR-016, FR-039 ? Status: PARTIAL (source inspection and review draft only).

## Inventory boundary

Reviewed `TicketsDbContext`, `ExecutionDbContext`, the ticket-type row mappings, the root migrations 014?016 and 027, the current PostgreSQL migration directory, and the phase-5 PostgreSQL test adapter. The source maps these five ticket-type relations, but the current PostgreSQL migration directory and baseline DDL review artifact do not define them. `postgresql-phase5-schema.sql` contains test-adapter definitions; that alone is not a numbered installable migration.

**Migration history:** `UNKNOWN`. Repository migration files and test adapters are source inputs, not evidence that any migration was applied; no trustworthy deployment ledger or catalog evidence was inspected.

| Mapped relation | Mapping source | Current PostgreSQL migration definition | Draft owner | Required sequence / dependencies |
|---|---|---|---|---|
| `PA_TICKETS_INCIDENT` | `IncidentRows.cs` `[Table]` and explicit `[Column]` attributes; `TicketsDbContext.Incidents` | Absent from baseline and `database/migrations/postgresql/` | `039_ticket_type_foundations_REVIEW.sql` | `PA_TICKETS_INCIDENT_SEQ`; FK/unique `N_TICKET_ID` ? `PA_TICKETS.N_TICKET_ID`; incident indexes for impact/urgency, commander, idempotency |
| `PA_TICKETS_PROBLEM` | `Phase5Rows.cs` `[Table]` and explicit `[Column]`; `TicketsDbContext.Problems` | Absent from baseline and PostgreSQL migration directory | Same draft | `PA_TICKETS_PROBLEM_SEQ`; FK/unique ticket; status and partial idempotency indexes |
| `PA_TICKETS_CHANGE` | `Phase5Rows.cs` `[Table]` and explicit `[Column]`; `TicketsDbContext.Changes` | Absent from baseline and PostgreSQL migration directory; root 016 only extends an assumed table | Same draft | `PA_TICKETS_CHANGE_SEQ`; FK/unique ticket; status/type/impact/risk constraints; status, planned-window, partial idempotency indexes |
| `PA_TICKETS_CHANGE_IMPLEMENTATION` | `Phase5Rows.cs` `[Table]`; `TicketsDbContext.ChangeImplementations` | Absent from baseline and PostgreSQL migration directory | Same draft | `PA_TICKETS_CHANGE_IMPLEMENTATION_SEQ`; FK to change; unique `(N_CHANGE_ID,N_STEP_NO)` |
| `PA_TICKETS_RELATION` | `TicketsDbContext.Relations` fluent table/column mapping and `RelationRow` | Absent from baseline and PostgreSQL migration directory; root 015 adds an index that presumes it exists | Same draft | `PA_TICKETS_RELATION_SEQ`; two ticket FKs; no-self relation check; unique relation tuple; reverse lookup index |

## Context-wide mapping scan

A source-level scan found 30 explicitly mapped relations across `TicketsDbContext` and `ExecutionDbContext`. Every mapped relation has a `CREATE TABLE` definition somewhere in the PostgreSQL source scripts or numbered migration directory. The five ticket-type relations above are present in the phase-5 test adapter but absent from the baseline build artifact and numbered migration inputs; draft 039 promotes these current mappings into one guarded review artifact. Seven other mapped relations are present only in test adapters relative to the baseline plus numbered PostgreSQL migrations: `PA_TICKETS_COMMUNICATION`, `PA_TICKETS_COMMUNICATION_TEMPLATE`, `PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG`, `PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG`, and the three notification orchestration relations. Root migration 013 already contains PostgreSQL definitions for the three notification orchestration tables; those communication objects belong to their own schema reconciliation and are outside the ticket-type draft. Raw SQL references still need a separate complete inventory.

## Root migration reconciliation

- Root 014 and 015 contain PostgreSQL syntax and ticket-type definitions despite their placement outside `database/migrations/postgresql/`; they are source inputs, not evidence that those changes were applied. Root 016 contains PostgreSQL `ALTER TABLE` statements for `PA_TICKETS_CHANGE`, but the table foundation is missing from the current PostgreSQL build inputs.
- Root 027 is Oracle-dialect (`NUMBER`, Oracle `ALTER TABLE ... ADD (...)`). Its two mapped effects (`PA_TICKETS.N_APPROVER_USER_ID` and `N_ROW_VERSION`) already exist in the reviewed foundational baseline DDL. No Oracle artifact was changed or translated as part of this slice.
- Migration numbering was verified from the directory: 038 is the highest existing number, so 039 is the next candidate.
- The draft has a prerequisite check for `public.PA_TICKETS` and an unconditional review stop before all candidate DDL. It is additive, contains no data writes or ledger changes, and uses quoted PostgreSQL identifiers, one sequence per relation, declared FKs, indexes, and root 016 change constraints.

## Mapping and compatibility notes

- The draft covers every persisted property on `IncidentRow`, `ProblemRow`, `ChangeRow`, `ChangeImplementationRow`, and `RelationRow`; relationship and unique-index behavior is based on root migrations and the phase-5 test adapter.
- `S_PROBLEM_STATEMENT` is nullable in the current EF property while root 015 declared it `NOT NULL`; the draft follows the current phase-5 adapter and EF nullability. This remains a review decision before approval.
- Existing relation definitions or sequence state cannot be safely inferred from catalog shape. Any future provider work must first reconcile exact definitions and migration history under the owner process.
- No SQL was executed, no database was accessed, and no migration was applied. Provider behavior, catalog compatibility, and failure recovery remain unverified.

## Source and artifact hashes

SHA-256 is over raw file bytes. The migration draft hash identifies the exact review artifact only; it is not an approval or execution reference.

| Artifact | SHA-256 |
|---|---|
| `src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs` | `0A6CDBDFD79677BB7A69C27516E7883F43AF2A4BB7068C876E2927EAF5303729` |
| `src/PA.Tickets.Infrastructure/Persistence/IncidentRows.cs` | `26830B43F80CC84D5EE88A04F22414CBC07F54423FBAB34CD128833AB174FC78` |
| `src/PA.Tickets.Infrastructure/Persistence/Phase5Rows.cs` | `20E270BB376BDD1ECD977E8F60D77F2F594BEE666571914A784863B392DBDE73` |
| `src/PA.Tickets.Infrastructure/Execution/ExecutionDbContext.cs` | `5AFF1A22782EFCA86F06556EE7B4CF31CEC540D63E224037FE1650794C5C19DA` |
| `database/migrations/014_incident_foundation.sql` | `32B9ECF15658E3667FB5F8645F4FCC6703EF43C2E14221E356BBB8DAF47272F9` |
| `database/migrations/015_problem_management.sql` | `3BFC0300FDEC54D41D0860B41610846351783D8564B9F517451C78F4733E24F8` |
| `database/migrations/016_change_management.sql` | `50C99419475028A43DFFF84E6FBA94C22AB90CC8954039F462426F11F0650A5A` |
| `database/migrations/027_ticket_lifecycle_authority.sql` | `809F7DF4FD0C24BEDE3D89369A3769D875430B3CF1303123EAB6DEC8FACAC097` |
| `tests/scripts/postgresql-phase5-schema.sql` | `6AD8F4E72470D71A7906065E4EFE1CD934429CBB7EB85FA8CB7C02FEF9592FD7` |
| `database/migrations/postgresql/039_ticket_type_foundations_REVIEW.sql` | `0B6B82CA634A00997FA99B0D76C7FE805B9FBECEBED94243A95C08F77F7C4681` |

## Source-level raw SQL inventory (2026-10-27)

A read-only source scan of `src/**/*.cs` for `ExecuteSql`, `FromSql`, `SqlQuery`, `DbCommand`, `CommandText`, and provider SQL builders found these PostgreSQL-relevant call sites. This is a call-site/object inventory only; it does not validate generated SQL or prove that a deployed schema has these objects.

| Source | PostgreSQL-relevant object references | Static artifact correspondence / remaining note |
|---|---|---|
| `Persistence/AuditChainCoordinator.cs` | `PA_TICKETS_AUDIT_CHAIN_LOCK` (insert and row lock) | Present in foundational DDL; provider behavior not tested. |
| `Tickets/OracleTicketServices.cs` (`PostgreSqlTicketIdentityGenerator`) | `PA_TICKETS_SEQ`, `PA_TICKETS_TICKET_NO_SEQ`, `PA_TICKETS_WORK_ITEM_SEQ` | Sequences present in foundational DDL; class file also contains a separate Oracle implementation, excluded from this PostgreSQL review. |
| `Foundations/AttachmentService.cs` | `PA_TICKETS_ATTACHMENT` row lock; dynamically selected attachment sequence | Table and sequence are present in the phase-5 test adapter; current baseline/numbered migration coverage remains part of broader schema reconciliation. The sequence argument is selected by source code and was not resolved per call. |
| `Foundations/CommunicationStore.cs` | `PA_TICKETS_COMMUNICATION`, `PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG`, `PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG` (lease query) | Definitions occur in later PostgreSQL test schema scripts; communication baseline, migration ordering and full mapped-column compatibility remain unreconciled. This confirms the previously recorded communication-schema gap is still open. |
| `Execution/DurableSlaJobRepositories.cs` | `PA_TICKETS_BACKGROUND_JOB` through `BuildJobLeaseSql` | Table and sequence are in foundational DDL; SQL text is generated by provider capabilities, so exact emitted SQL is outside this source scan. |
| `Foundations/OperationalMetadataRepository.cs` | Dynamic sequence selection; `PA_TICKETS_PRIORITY_SEQ` | Priority sequence exists in foundational DDL. The other sequence names flow from repository configuration/source call sites and require a complete per-caller mapping before provider acceptance. |
| `Foundations/AdministrationService.cs`, `Foundations/InitialAdministratorProvisioner.cs` | Dynamic sequence selection | Quoted PostgreSQL `nextval` form is present; exact object coverage depends on call-site sequence values and remains tied to the wider mapped-object inventory. |
| `Execution/DurableWorkflowConfigurationRepository.cs` | Allow-listed workflow sequence names | Source allow-list contains four workflow sequences; emitted provider expression and deployed sequence state are unverified. |
| `Execution/DurableApprovalConfigurationRepository.cs` | Allow-listed approval-rule sequence names | Source allow-list contains three approval-rule sequences; emitted provider expression and deployed sequence state are unverified. |

The scan also returned the Oracle generator and provider-neutral raw command construction; only the explicit PostgreSQL branches were classified above. EF-generated LINQ/`ExecuteUpdate` SQL is not included. The source-level raw-SQL inventory is therefore improved but not exhaustive for all SQL emitted by EF/provider code. No source code or migration was changed, and migration history remains `UNKNOWN`.
