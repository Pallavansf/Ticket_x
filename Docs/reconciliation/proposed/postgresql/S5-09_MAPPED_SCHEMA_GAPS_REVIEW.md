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

A source-level scan found 30 explicitly mapped relations across `TicketsDbContext` and `ExecutionDbContext`. Every mapped relation has a `CREATE TABLE` definition somewhere in the PostgreSQL source scripts or numbered migration directory. The five ticket-type relations above are present in the phase-5 test adapter but absent from the baseline build artifact and numbered migration inputs; draft 039 promotes these current mappings into one guarded review artifact. Eight other mapped relations are present only in test adapters relative to the baseline plus numbered PostgreSQL migrations: `PA_TICKETS_COMMUNICATION`, `PA_TICKETS_COMMUNICATION_ATTEMPT`, `PA_TICKETS_COMMUNICATION_TEMPLATE`, `PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG`, `PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG`, and the three notification orchestration relations. Root migration 013 already contains PostgreSQL definitions for the three notification orchestration tables; those communication objects belong to their own schema reconciliation and are outside the ticket-type draft. Raw SQL references still need a separate complete inventory.

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

## Communication schema reconciliation (2026-10-28)

The guarded candidate is `Docs/reconciliation/proposed/postgresql/S5-09_communication_schema_REVIEW.sql` SHA-256 `EA2938C32C1E5225EC8F8EEB9E302ACE7CAFC352ECCACF762D82650813EB09D5`. It is under the reconciliation review directory, outside `database/migrations/`, and begins with an unconditional exception before all DDL. It contains no inserts, updates, deletes, drops, truncation, sequence resets, or migration-ledger writes. Notification-rule/execution foreign keys that depend on root migration 013 appear only as commented post-013 review statements. The candidate remains review-only and is not approved or executable.

EF maps the five communication relations without an explicit schema, so the model's schema is `NULL` and PostgreSQL resolves unqualified names through `search_path`. The candidate follows the unqualified names used in the source scripts; `public` resolution and any target `search_path` remain unverified. The candidate contains the following source-backed columns and PostgreSQL declarations (full nullability/default/constraint details are in the SQL artifact):

| Relation | Mapping source | Columns and source DDL coverage | Keys and relations |
|---|---|---|---|
| `PA_TICKETS_COMMUNICATION` | `CommunicationRow`, `TicketsDbContext` | Numeric/time fields follow CLR types. All mapped strings have no EF max length and use PostgreSQL `text`; nullability follows nullable-reference metadata (`PublicId` required; `Channel`, `Recipient`, and `IdempotencyKey` optional). Status initializer/default is `QUEUED`. | PK; unique idempotency and public ID; status/schedule, lease, notification indexes. Ticket/work-item/event dependencies are explicit; notification FKs follow root 013. |
| `PA_TICKETS_COMMUNICATION_TEMPLATE` | `CommunicationTemplateRow`, `TicketsDbContext` | All unbounded strings use `text`; required/optional status follows CLR nullability. `Status` initializer/default is `DRAFT`. | PK; unique code/channel/version; active lookup index; status check retained from source test schema. No communication-to-template FK was found. |
| `PA_TICKETS_COMMUNICATION_ATTEMPT` | `[Table]`/`[Column]` attributes on `CommunicationAttemptRow`, plus explicit ID sequence mapping in `TicketsDbContext` | Numeric/time fields follow CLR types. Unbounded strings use `text`; nullable-reference metadata determines nullability. | PK/default from the named attempt sequence; unique communication/attempt number; cascade FK to communication; lookup index. |
| `PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG` | `CommunicationProviderConfigurationRow`, `TicketsDbContext` | Unbounded provider/channel strings use `text`; bool, integer, timestamp and actor fields follow CLR types and nullability. | PK/default sequence; unique provider/channel; retry/channel checks; active lookup index. |
| `PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG` | `CommunicationChannelConfigurationRow`, `TicketsDbContext` | Unbounded channel string uses `text`; bool, timestamps, actor and row-version fields follow CLR types and nullability. | PK/default sequence; unique channel; channel check; active lookup index. |

The communication-linked notification relations are separate existing source artifacts, not copied into this candidate: `PA_TICKETS_NOTIFICATION_RULE`, `_VERSION`, and `_EXECUTION` are mapped by `Phase5Rows.cs`/`TicketsDbContext` and defined in root PostgreSQL migration 013; its associated FKs from communication are present in the slice 5 test script. Root 013 adds notification lineage columns to a pre-existing communication table using `ADD COLUMN IF NOT EXISTS`; the candidate already includes those EF-mapped nullable columns, so that specific addition is idempotent when the candidate precedes 013. Therefore the technical dependency is staged: communication table before root 013, then the notification FKs. The proposed S5-10 review order omits 013 and does not authorize this ordering. The separate `PA_TICKETS_NOTIFICATION` relation/sequence is in the phase-5 test adapter and is not required by the identified communication store path; it remains outside this candidate.

The supporting notification relation mapping is:

| Relation | Mapped columns and PostgreSQL declarations | Constraints and source |
|---|---|---|
| `PA_TICKETS_NOTIFICATION_RULE` | ID bigint; code varchar(200), name varchar(300), description text, event type varchar(100); enabled boolean, priority/version/row-version integers; effective/created/updated timestamps timestamptz. | PK/default sequence, unique code, event/enabled/effective index; root 013 plus row-version addition in phase-6 slice-5 test script. |
| `PA_TICKETS_NOTIFICATION_RULE_VERSION` | ID/rule/actor IDs bigint; version number/priority/row-version integers; event type varchar(100); condition text; recipient/channel/template definitions varchar(200); dedup scope varchar(50); enabled boolean; status varchar(20); effective/created/published/retired timestamps timestamptz. | PK/default sequence; FK to rule with cascade; unique rule/version; active lookup index. Root 013 provides base columns; phase-6 slice-5 adds event/priority/status/publish/retire/row-version fields. Its script also performs a data backfill, excluded from this candidate. |
| `PA_TICKETS_NOTIFICATION_EXECUTION` | ID/ticket/event/rule/version IDs bigint; status varchar(30); matched boolean; created-communication count integer; skip/failure/rule/template/channel/dedupe strings varchar(200) except failure code varchar(100); failure/context text; evaluated/created/completed timestamps timestamptz. | PK/default sequence; FKs to ticket, event, rule, and rule version; partial unique event/version and ticket/time indexes. Defined in root 013. |

`PA_TICKETS_NOTIFICATION` is also mapped, with ID and ticket/work-item/user IDs bigint, notification type/channel/status strings, subject/message text, scheduled/sent/read/created timestamps, correlation/idempotency strings, and a sequence in the phase-5 test adapter. It is not referenced by the communication row, store, or administration paths examined here, so it remains outside this candidate.

### Communication sequence dependency map

| Sequence | Source call site and dependent relation/operation | PostgreSQL definition source | Required relation ordering / unresolved state |
|---|---|---|---|
| `PA_TICKETS_COMMUNICATION_SEQ` | `TicketsDbContext` default for `CommunicationRow.Id`; `CommunicationStore` adds communication rows. | Candidate plus `postgresql-phase5-schema.sql`; historical migration 002 is Oracle and is not PostgreSQL authority. | Sequence before communication table default. Ownership and current state unknown. |
| `PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ` | `TicketsDbContext` default for `CommunicationTemplateRow.Id`; `CommunicationAdministrationRepository` adds templates. | Candidate plus `postgresql-slice3-schema.sql`; historical migration 021 is Oracle. | Sequence before template table default. Ownership/current state unknown. |
| `PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ` | `CommunicationAttemptRow.Id` is generated on insert; `CommunicationStore` adds attempts. | Candidate plus `postgresql-slice3-schema.sql`. | Sequence before attempt table default. Ownership/current state unknown. |
| `PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ` | `TicketsDbContext` default for provider configuration ID; `CommunicationAdministrationRepository` adds providers. | Candidate plus `postgresql-slice5-schema.sql`; historical migration 021 is Oracle. | Sequence before provider configuration table default. Ownership/current state unknown. |
| `PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ` | `TicketsDbContext` default for channel configuration ID; `CommunicationAdministrationRepository` adds channel configurations. | Candidate plus `postgresql-slice5-schema.sql`; historical migration 021 is Oracle. | Sequence before channel configuration table default. Ownership/current state unknown. |
| `PA_TICKETS_NOTIFICATION_RULE_SEQ`, `_VERSION_SEQ`, `_EXECUTION_SEQ` | EF defaults in `TicketsDbContext`; `CommunicationAdministrationRepository` adds rules/versions and `NotificationOrchestrator` adds executions. | Root PostgreSQL migration 013 and phase-5 test adapter. | Root 013 creates each sequence before its table. Root 013 placement/adoption is absent from the proposed order. |

The PostgreSQL test scripts declare the first five sequences with bare `CREATE SEQUENCE IF NOT EXISTS`; their implicit PostgreSQL defaults apply only if a new sequence is created. The candidate does not specify a start, increment, owner, or current value and contains no `setval`. Existing migration 021 has Oracle sequence statements and is not used to infer PostgreSQL deployed state. A static source/test-adapter scan found no matching definitions in the S5-08 foundational DDL draft, 028–039 PostgreSQL migration directory, or approved review sequence. No claim is made about live existence or state.

Source corrections: all five direct communication sequence names are in `PostgreSqlProviderCapabilities.SequenceNames`; the five IDs now have explicit EF `nextval` defaults matching the guarded candidate. This is model/source metadata only. It does not create or change a database sequence, assign ownership, establish deployed state, or determine an existing sequence's current value.

### Reconciled contract and remaining decisions

- Resolved from current EF nullable-reference metadata and Npgsql's unbounded string mapping: `PublicId` is required; `Channel`, `Recipient`, and `IdempotencyKey` are nullable; all mapped unbounded strings are `text`, with no invented varchar limits. Status defaults in the review candidate now match the entity initializers (`QUEUED`, `DRAFT`). The candidate reflects these source contracts but remains unvalidated against PostgreSQL.
- `CommunicationAttemptRow.Id` now explicitly maps to `PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ`, matching the other four communication IDs. Sequence ownership, start/increment settings and existing state remain unknown; no ownership or `setval` operation is proposed.
- Root PostgreSQL migration 013 requires the communication relation and ticket/event baseline relations; it creates notification rule/version/execution sequences and tables and adds notification lineage columns to communication. The candidate stages only the communication table before 013. Notification FKs to execution/rule/version must follow 013. The existing S5-10 order omits root 013, so release/governance review must determine whether and where it is admitted; no executable migration order is asserted here.
- Sequence ownership, actual state, and provider behavior require separately authorized catalog/provider validation. Migration history remains `UNKNOWN`.

The offline test `CommunicationSchemaReviewTests` verifies all five EF model tables/columns are represented, all five direct communication sequences appear in both EF's provider sequence catalog and the review candidate, and the abort guard precedes DDL with no data or cleanup statements. These checks do not validate SQL syntax, type/nullability compatibility, constraints, or PostgreSQL compatibility.

## Source and artifact hashes

SHA-256 is over raw file bytes. The migration draft hash identifies the exact review artifact only; it is not an approval or execution reference.

| Artifact | SHA-256 |
|---|---|
| `src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs` | `0E6A1CDDDCAF814C25E98324B41232BCE2D3D9CF6379B892F6E27E4D23BE9B15` |
| `src/PA.Tickets.Infrastructure/Persistence/DatabaseProviderCapabilities.cs` | `B20EBD5F24925186F81E24084AA95898DCF0283252EBA90EB05807922A18C42F` |
| `src/PA.Tickets.Infrastructure/Persistence/IncidentRows.cs` | `26830B43F80CC84D5EE88A04F22414CBC07F54423FBAB34CD128833AB174FC78` |
| `src/PA.Tickets.Infrastructure/Persistence/Phase5Rows.cs` | `20E270BB376BDD1ECD977E8F60D77F2F594BEE666571914A784863B392DBDE73` |
| `src/PA.Tickets.Infrastructure/Execution/ExecutionDbContext.cs` | `5AFF1A22782EFCA86F06556EE7B4CF31CEC540D63E224037FE1650794C5C19DA` |
| `database/migrations/014_incident_foundation.sql` | `32B9ECF15658E3667FB5F8645F4FCC6703EF43C2E14221E356BBB8DAF47272F9` |
| `database/migrations/015_problem_management.sql` | `3BFC0300FDEC54D41D0860B41610846351783D8564B9F517451C78F4733E24F8` |
| `database/migrations/016_change_management.sql` | `50C99419475028A43DFFF84E6FBA94C22AB90CC8954039F462426F11F0650A5A` |
| `database/migrations/027_ticket_lifecycle_authority.sql` | `809F7DF4FD0C24BEDE3D89369A3769D875430B3CF1303123EAB6DEC8FACAC097` |
| `tests/scripts/postgresql-phase5-schema.sql` | `6AD8F4E72470D71A7906065E4EFE1CD934429CBB7EB85FA8CB7C02FEF9592FD7` |
| `database/migrations/postgresql/039_ticket_type_foundations_REVIEW.sql` | `0B6B82CA634A00997FA99B0D76C7FE805B9FBECEBED94243A95C08F77F7C4681` |
| `Docs/reconciliation/proposed/postgresql/S5-09_communication_schema_REVIEW.sql` | `EA2938C32C1E5225EC8F8EEB9E302ACE7CAFC352ECCACF762D82650813EB09D5` |
| `tests/PA.Tickets.Domain.Tests/CommunicationSchemaReviewTests.cs` | `223C7D7CB90B526408F3336F1B8A84866934E1632123DE67CF45D9826AAA3E0B` |

## Source-level raw SQL inventory (2026-10-27)

A read-only source scan of `src/**/*.cs` for `ExecuteSql`, `FromSql`, `SqlQuery`, `DbCommand`, `CommandText`, and provider SQL builders found these PostgreSQL-relevant call sites. This is a call-site/object inventory only; it does not validate generated SQL or prove that a deployed schema has these objects.

| Source | PostgreSQL-relevant object references | Static artifact correspondence / remaining note |
|---|---|---|
| `Persistence/AuditChainCoordinator.cs` | `PA_TICKETS_AUDIT_CHAIN_LOCK` (insert and row lock) | Present in foundational DDL; provider behavior not tested. |
| `Tickets/OracleTicketServices.cs` (`PostgreSqlTicketIdentityGenerator`) | `PA_TICKETS_SEQ`, `PA_TICKETS_TICKET_NO_SEQ`, `PA_TICKETS_WORK_ITEM_SEQ` | Sequences present in foundational DDL; class file also contains a separate Oracle implementation, excluded from this PostgreSQL review. |
| `Foundations/AttachmentService.cs` | `PA_TICKETS_ATTACHMENT` row lock; dynamically selected attachment sequence | Table and sequence are present in the phase-5 test adapter; current baseline/numbered migration coverage remains part of broader schema reconciliation. The sequence argument is selected by source code and was not resolved per call. |
| `Foundations/CommunicationStore.cs`; `Persistence/TicketsDbContext.cs` | Raw SQL references `PA_TICKETS_COMMUNICATION`, `PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG`, and `PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG` (lease query). EF also maps `CommunicationAttemptRow` to `PA_TICKETS_COMMUNICATION_ATTEMPT`. | The review candidate now reconciles the five EF table/column mappings and their source-derived nullability and unbounded string types. The five ID sequence expressions are represented in EF and candidate SQL. Root migration 013 and its notification relations remain a separate ordering dependency; sequence ownership/state and provider compatibility remain unverified. This source reconciliation does not establish deployable schema compatibility. |
| `Execution/DurableSlaJobRepositories.cs` | `PA_TICKETS_BACKGROUND_JOB` through `BuildJobLeaseSql` | Table and sequence are in foundational DDL; SQL text is generated by provider capabilities, so exact emitted SQL is outside this source scan. |
| `Foundations/OperationalMetadataRepository.cs` | Dynamic sequence selection; `PA_TICKETS_PRIORITY_SEQ` | Priority sequence exists in foundational DDL. The other sequence names flow from repository configuration/source call sites and require a complete per-caller mapping before provider acceptance. |
| `Foundations/AdministrationService.cs`, `Foundations/InitialAdministratorProvisioner.cs` | Dynamic sequence selection | Quoted PostgreSQL `nextval` form is present; exact object coverage depends on call-site sequence values and remains tied to the wider mapped-object inventory. |
| `Execution/DurableWorkflowConfigurationRepository.cs` | Allow-listed workflow sequence names | Source allow-list contains four workflow sequences; emitted provider expression and deployed sequence state are unverified. |
| `Execution/DurableApprovalConfigurationRepository.cs` | Allow-listed approval-rule sequence names | Source allow-list contains three approval-rule sequences; emitted provider expression and deployed sequence state are unverified. |

The scan also returned the Oracle generator and provider-neutral raw command construction; only the explicit PostgreSQL branches were classified above. EF-generated LINQ/`ExecuteUpdate` SQL is not included. The source-level raw-SQL inventory is therefore improved but not exhaustive for all SQL emitted by EF/provider code. No source code or migration was changed, and migration history remains `UNKNOWN`.

## Consolidated migration dependency manifest (review only)

This is a static dependency record, not an executable deployment order. Repository presence, numbering, and source hashes do not prove approval or applied history. No approved executable artifact set or migration ledger is evidenced in this worktree.

| Classification | Artifact / exact identity | Dependency and disposition |
|---|---|---|
| Historical baseline reference (Option A) | `tests/scripts/postgresql-foundational-ddl.sql` at commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a`, SHA-256 `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19` | Matches the historical identity recorded in the canonical decision pack. D01 adoption is still `PENDING`; this reference cannot be applied or used as an approved baseline yet. |
| Current source adapter, not the pinned historical bytes | Same path in the current worktree, SHA-256 `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64` | Differs from the pinned historical artifact. It is not a substitute for Option A; the content delta must be reviewed under D01 before baseline adoption. |
| Existing root PostgreSQL migration source | `database/migrations/013_notification_orchestration.sql`, SHA-256 `94E5BDC67749E577C4EC6DEBFB9298EE1995484D4E65170D31EE3731BF4B9D61` | Creates notification sequences/tables and alters `PA_TICKETS_COMMUNICATION` to add notification columns. It therefore requires the communication relation before it runs; the file also references baseline ticket/event relations. It lives outside `database/migrations/postgresql/` and is absent from the proposed S5-10 order. Its inclusion, predecessor baseline, and approval remain unresolved. |
| Review-only communication schema candidate | `Docs/reconciliation/proposed/postgresql/S5-09_communication_schema_REVIEW.sql`, SHA-256 `EA2938C32C1E5225EC8F8EEB9E302ACE7CAFC352ECCACF762D82650813EB09D5` | Abort-guarded, outside the migration directory, and not executable. EF-derived nullability, PostgreSQL `text` string mapping, status defaults, and five ID sequence expressions are reconciled in source. Sequence ownership/current state and schema/search_path remain unverified. Candidate dependency only: selected baseline with required ticket/event objects → reviewed communication-table successor → root 013 if separately approved → notification foreign keys only after 013. This is a technical dependency model, not an authorized deployment order. |
| Proposed migration successors | Proposed technical order remains baseline → 028 → 029 → 033 → 034 → 035 → 036 → 037 → 039 | Candidate source artifacts only; the current-byte hashes are recorded below and are not approvals. The proposal omits root 013 and the communication predecessor. Migration 030 is superseded; 031 and 032 are not in this proposed chain. No deployed history is inferred. Exact prerequisites, conflicting artifacts, decisions, and per-operation authorization remain unresolved; the proposed order is not a complete executable manifest. |
| Review-only ticket-type schema | `database/migrations/postgresql/039_ticket_type_foundations_REVIEW.sql`, SHA-256 `0B6B82CA634A00997FA99B0D76C7FE805B9FBECEBED94243A95C08F77F7C4681` | Has an unconditional review abort guard. It depends on the selected baseline ticket relation; its position in a proposed order does not make it executable. |
| Other explicitly gated artifacts | S5-08 `*_REVIEW.sql` extracts; `038_rbac_audit_actor_compatibility_BLOCKED.sql`; `040_feature_switches_REVIEW.sql` | S5-08 outputs are abort-guarded review drafts; 038 is marked blocked; 040 is review-only. None belongs in an executable deployment sequence. Migration 026 remains excluded. |
| Approved executable artifacts | None evidenced | Requires attributable decisions, exact final artifact hashes, reconciled dependencies, target/operation authorization, and provider evidence. Migration history remains `UNKNOWN`. |

Current SHA-256 identities for the numbered source artifacts in the proposed chain: 028 `0D5291A9B5EC06F0146E947A7B988A87013EE68B69DE1983AB75761FAEE0B45B`; 029 `9F40FB5326CB2F5FC48CD68D73636BC8B3EED5366D6EC58320207FF0F5C7FB3D`; 033 `8ACA3EC0431A91DEE9276B7924C8F8D0FA1E4FF11647B807CE33BA41393C9ABC`; 034 `3C428BB4FC11D1F38C1BB2400FE008360E06C8131504D0F730DC328D0DB9ECB2`; 035 `C0CCC9DA3C3A441CEA6928ADBB8E50030F0D0716A59B9EADAFE2AACA97E15D9D`; 036 `FC65DC8A3E7B86EBCDE3D8E9F9E51FEFA8971921490EC21353E33B356562`; 037 `6853071A8DF0784E2046133B650A87BC84F47C3F5CEBA6276FE8051968A0FD71`.

The smallest external gate for provider work remains formal D01 baseline adoption plus a signed, operation-specific S5-01 authorization packet for the exact S5-03 read-only fingerprint, including independently verified target/operator identity, scope, validity/revocation, and evidence custody. Until then no database-dependent phase is eligible. This manifest records source-level dependencies only and does not change any slice or roadmap status.

### S5-09 communication reconciliation outcome

The source-level communication contract is reconciled for all five EF entities: mapped columns, nullability, unbounded string types, the QUEUED/DRAFT status defaults, and each ID sequence expression are covered by `CommunicationSchemaReviewTests`. The candidate hash above identifies the current guarded bytes. S5-09 remains **PARTIAL**: no provider/catalog validation or authorized baseline/deployment sequence was performed; sequence ownership/current state, schema/search_path, migration-history compatibility, and root 013 admission into an approved sequence remain open. Owner policy is approved in principle only and supplies no database-operation authorization.