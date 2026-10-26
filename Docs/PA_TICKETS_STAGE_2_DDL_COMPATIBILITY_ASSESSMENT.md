# PA_TICKETS Stage 2 DDL-to-Application Contract Compatibility Assessment

**Branch:** `workstream-i-lifecycle-authority`
**Stage 2:** `PARTIAL`
**Provider execution:** `NO-GO`

This is a source-only comparison. It does not select a baseline, authorize SQL, or establish live database state.

## Verified comparison inputs

The current worktree artifact is `tests/scripts/postgresql-foundational-ddl.sql`, SHA-256 `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`. The historical artifact is the exact Git path `1bcc493d267e23e3e126b9b698fa4344ebe8588a:tests/scripts/postgresql-foundational-ddl.sql`, SHA-256 `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19`. Both were hashed from raw bytes with SHA-256. The `5CAA...` reference remains untraceable.

The deterministic Git diff has 106 added lines and zero removals. The current additions are therefore semantic extensions, not replacements of historical statements. No live catalog or migration history was inspected.

## Source-to-DDL compatibility matrix

| Contract/object | Source evidence | Historical artifact | Current artifact | Classification and consequence |
|---|---|---|---|---|
| Ticket core | `TicketsDbContext.cs:103` `TicketRow`; `EfTicketRepository.cs:26` maps ticket columns. | Present. | Present unchanged, plus nullable `N_REQUEST_TYPE_VERSION` and `N_SUB_CATEGORY_ID` at current DDL lines 49–53. | Foundational ticket persistence. The two additions are source-required shadow properties (`TicketsDbContext.cs:76,92`) but nullable and without backfill. |
| Status definitions | `TicketsDbContext.cs:107–125` `StatusDefinitionRow`; `OperationalMetadataRepository.cs:18–70,79–121,177–192`; `TicketLifecycleService.cs:12,34` uses configuration. | Absent. | `PA_TICKETS_STATUS` and `PA_TICKETS_STATUS_SEQ` at lines 59–78. | Operational schema required by current lifecycle/configuration code. Historical artifact alone cannot satisfy these queries. Current table lacks the EF default on the PK and the check constraint present in `tests/scripts/postgresql-slice6-schema.sql`; repository currently allocates IDs explicitly with `NextIdAsync`. |
| Status transitions | `TicketsDbContext.cs:127–139`; `OperationalMetadataRepository.cs:130–192`; `TicketStatusTransitionPolicy.cs:17`. | Absent. | `PA_TICKETS_STATUS_TRANSITION` and sequence at lines 60,79–88. | Operational schema required for configured transition checks. Current DDL supplies uniqueness/FKs but omits explicit check constraints and PK default; provider compatibility remains unverified. |
| Priority definitions | `TicketsDbContext.cs:141–158`; `OperationalMetadataRepository.cs:257–470`; `EfTicketRepository.cs:15,27`. | Absent. | `PA_TICKETS_PRIORITY` and sequence at lines 61,90–104. | Operational schema required for priority lookup and writes. Current DDL has code uniqueness and indexes but no PK default/check constraint; explicit repository sequence allocation is provider-specific. |
| Incident persistence | `IncidentRows.cs:7` `IncidentRow`; `IncidentService.cs:36–58,290–308`. | Absent from both foundational artifacts. | Absent from foundational artifact. | Required by Incident service but supplied by migration `database/migrations/014_incident_foundation.sql`; not evidence that foundational DDL is incomplete by itself. Migration dependency remains. |
| Problem persistence | `Phase5Rows.cs:164` `ProblemRow`; `ProblemService.cs:27–53,215–242`. | Absent from both foundational artifacts. | Absent from foundational artifact. | Required by Problem service but supplied by migration `015_problem_management.sql`; separate operational migration dependency. |
| Work items, events and audit | `TicketsDbContext.cs:103–105`; `EfTicketRepository.cs:5–25`; incident/problem services stage `Events` and `Audits`. | Present. | Present unchanged. | Foundational persistence contract is represented in both artifacts, subject to provider validation of types/defaults and transaction behavior. |
| Waiting reasons | `TicketsDbContext.cs:20`; `PersistentWaitingReasonCatalog.cs:8–22`. | Present. | Present unchanged. | Required for waiting validation; T61 reconciliation already identified the `RequireExpectedDate` schema discrepancy. Provider compatibility remains unresolved. |
| Sequences/defaults | `TicketsDbContext.cs:89–91,100`; `OperationalMetadataRepository.cs:53,158,229–249`; `EfTicketRepository.cs:26`. | Core ticket sequences present; operational status/priority sequences absent. | Operational sequences added, but status/priority table PK defaults are not declared in this artifact. | Operational key management. The source uses both EF defaults and explicit `nextval`; sequence ownership/current state cannot be inferred from files alone. |
| Audit-maintenance token and cleanup | Provider-independent production code has no direct call to the cleanup function; provider tests call `pa_tickets_maintenance_delete_audit_for_ticket` (for example `PostgreSqlProblemTests.cs:167`). | Token table exists, but initialization/guard function block is absent. | Lines 213–246 initialize the token, revoke public access, and define trigger/function guards. | Test/operations security behavior, not established as a production domain contract. Ownership, grants, token custody, and hash-chain impact require owner/provider review. |

## Classification of the 106 added lines

- **Foundational source-required:** the two nullable ticket shadow columns, because the EF mapping reads/writes them during ticket round-tripping.
- **Operational schema:** status, transition, and priority tables, their sequences, indexes, and canonical rows, because lifecycle/configuration repositories query and mutate them.
- **Reference data:** ten statuses, twenty-one transitions, and four priorities. Their values are application behavior, not merely structural DDL; their adoption requires policy approval.
- **Operational key management:** the three `setval` statements. They inspect maxima and advance sequence state; they are not read-only and must be reviewed separately from baseline identity.
- **Security/audit maintenance:** token initialization, public revocation, security-definer trigger guard, and maintenance-delete function. The explicit `search_path` is `pg_catalog, public`; function ownership, execution grants, token storage, session-setting exposure, and audit-chain effects are not established by source alone.
- **Optional/unexplained:** none can be safely called optional without owner review; the maintenance block is test/operations-facing but is referenced by provider cleanup tests and therefore needs an explicit operational decision.

## Neutral baseline options

| Option | Compatibility | Provenance/reproducibility | Risks and unknowns | Additional evidence |
|---|---|---|---|---|
| A. Historical artifact plus separately approved additive changes | Reproducible starting point, but status/priority and ticket shadow contracts require later migrations. Incident/Problem remain migrations 014/015. | Strong Git provenance and verified raw-byte hash. | Requires explicit ordering, migration ledger treatment, sequence reconciliation, and separate security review. | Owner-approved artifact and additive migration list, expected/observed report, sequence and reference-data plan. |
| B. Current expanded artifact as one adopted baseline | Covers more current source requirements, but still does not include Incident/Problem tables and has provider-sensitive defaults/constraints. | Current hash is reproducible; worktree provenance is not an immutable adoption record. | DML and `setval`, fixed reference rows, security-definer cleanup, and no live equivalence evidence. | Immutable version, owner adoption, full object/constraint review, privilege/token policy, and live read-only comparison. |
| C. Another immutable reviewed artifact | May resolve omissions or reconcile contracts if repository evidence identifies it. | No third artifact is established by current evidence; `5CAA...` is not traceable. | Risk of inventing provenance or silently changing scope. | Exact artifact, hash, commit/repository provenance, definition report, and owner adoption. |

No option is recommended by recency or object count. D01 must remain `PENDING` until the owner chooses an immutable artifact and records attributable approval or rejection.

## D01 decision brief

The owner must answer: (1) which immutable artifact/version is the baseline; (2) whether status/transition/priority tables and reference rows are foundational or separately versioned operational changes; (3) how the two nullable ticket columns are reconciled; (4) how Incident/Problem migrations 014/015 relate to the baseline; (5) whether the current PK defaults/check constraints are acceptable or require a reviewed additive correction; (6) whether `setval` operations are allowed and how sequence state is reconciled; (7) who owns the audit-maintenance token/function and what exact grants and retention apply; and (8) how unknown migration history is represented without fabricated ledger rows.

These are owner decisions. Independent technical evidence still required includes a raw-byte hash, provenance, expected-versus-observed definition comparison, and—only after authorization—a read-only provider catalog result. No live database fact is claimed here.

## Reconciled discrepancies and required classification

| Discrepancy | Evidence | Classification | Safe resolution boundary |
|---|---|---|---|
| Status/transition/priority tables are absent from the historical artifact but required by current configuration repositories. | `TicketsDbContext.cs:89-91,107-158`; `OperationalMetadataRepository.cs:18-192,257-470`; current DDL lines 59-120. | Required for current application compatibility; operational rather than ticket-core foundation. | Keep as separately reviewed additive schema work unless D01 explicitly adopts the expanded artifact. Provider translation and reference-data validation remain required. |
| Current foundational status/transition/priority tables omit PK `DEFAULT nextval(...)` and the check constraints shown in Slice 6. | Current DDL lines 62,79,90; `tests/scripts/postgresql-slice6-schema.sql:7-58`; EF defaults in `TicketsDbContext.cs:89-91`. | Required for a particular insertion/DDL contract if IDs are database-generated; otherwise unresolved because repositories currently call `NextIdAsync` explicitly (`OperationalMetadataRepository.cs:53,158,229-249`). | Resolve in a reviewed artifact or migration after owner/provider decision; do not silently edit the baseline. |
| Ticket status/priority foreign keys are present in Slice 6 but not in the current foundational file. | `postgresql-slice6-schema.sql:153-165`; current foundational file has `N_STATUS_ID`/`N_PRIORITY_ID` but no equivalent FK block. | Required for the stronger integrity/test path; unresolved whether omission is intentional in the adopted baseline. | Owner chooses whether these constraints are part of baseline or additive upgrade; provider preflight must compare exact definitions. |
| `N_REQUEST_TYPE_VERSION` and `N_SUB_CATEGORY_ID` are absent historically and added currently as nullable columns. | `TicketsDbContext.cs:76,92`; current DDL lines 49-53; `EfTicketRepository.cs:5,23,29`. | Required for current ticket round-tripping; no backfill is source-supported. | Add through an approved immutable artifact/additive change; do not infer historical values. |
| Waiting-reason `RequireExpectedDate` was previously questioned. | `PersistentWaitingReasonCatalog.cs:22,26-37`; `TicketsDbContext.cs:20`; current foundational DDL lines 146-158; PostgreSQL proposal `Docs/reconciliation/proposed/postgresql/T61_waiting_reason_reconciliation.sql`. | PostgreSQL source contract is reconciled: `N_REQUIRE_EXPECTED_DATE` is present as non-null boolean default false. No PostgreSQL change is justified. | Any live catalog confirmation is provider work; Oracle remains a separate deferred contract. |
| Incident and Problem tables are absent from both foundational artifacts. | `IncidentRows.cs:7`; `Phase5Rows.cs:164`; `IncidentService.cs:36-58`; `ProblemService.cs:27-53`; migrations `014_incident_foundation.sql` and `015_problem_management.sql`. | Required only for the Incident/Problem migration path, not a reason to fold those migrations into the foundational baseline. | Preserve migration order and unknown history; execute only under separate authorization. |
| Audit-maintenance token/functions are current-artifact additions used by provider cleanup tests. | Current DDL lines 213-246; provider tests such as `PostgreSqlProblemTests.cs:167`; no production invocation found. | Operational/security behavior requiring separate owner approval. | Review ownership, execution grants, token custody, search path, and chain effects before provider use. |
| `setval` calls and canonical reference rows mutate state. | Current DDL lines 109-120; Slice 6 uses different sequence reconciliation (`postgresql-slice6-schema.sql:130-138`). | Operational key/reference-data behavior requiring separate approval. | Reconcile sequences and reference data without reset/advance assumptions; validate only after authorization. |

## Recommended baseline strategy

**Recommendation: Option A - retain the historically verified artifact as the foundational baseline and manage the current additions as separately versioned, individually reviewed changes.** This is the safest technical strategy because the historical bytes and Git path are directly reproducible, while the current expanded file mixes source-required compatibility columns with operational reference data, sequence advancement, DML, and privileged audit-maintenance behavior. Keeping those concerns separate makes migration order, rollback scope, ownership, and approval boundaries reviewable.

Option B is workable only if the owner explicitly adopts an immutable version of the expanded artifact and separately approves its DML, `setval` behavior, reference rows, missing defaults/constraints, and security-definer cleanup contract. The current worktree copy cannot itself serve as an adopted immutable baseline. Option C has no supported candidate: the only other recorded value (`5CAA...`) has no traceable artifact or provenance.

This recommendation does not approve Option A, D01, migrations, or provider execution. It records the engineering trade-off: Option A preserves stronger provenance and narrower change scope at the cost of an explicit additive sequence; Option B reduces artifact count but couples foundational adoption to unresolved operational/security decisions; Option C is unsupported by current evidence.

## Minimum ordered path after owner decisions

1. Record D01 adoption of the historical artifact or an immutable reviewed replacement, with raw-byte SHA-256, provenance, and expected-versus-observed definitions.
2. Approve D02 operator scope and the TGT/OP/PRE evidence envelope; no provider operation is implied by the recommendation.
3. Reconcile the foundational ticket contract, including the two nullable shadow columns and exact status/priority FK/default/check decisions, in a reviewed additive artifact without changing migration history.
4. Review operational status/transition/priority reference rows and sequence allocation; obtain the D03 decision before any `setval`, insert, or backfill operation.
5. Preserve migrations 014/015 as their own ordered Incident/Problem changes; do not claim they are present in the foundational baseline.
6. Resolve the waiting-reason PostgreSQL contract as source-matched, while leaving any live verification and Oracle work deferred.
7. Obtain separate security/audit approval for the maintenance token/functions and D10 audit actor contract.
8. Only after the above evidence is attributable and unexpired, perform an authorized read-only provider comparison, then draft (but do not execute) any schema-change request.

The following remain unknown from repository evidence: live object provenance and state, sequence last values/ownership, actual provider translation of defaults and booleans, function ownership/grants, audit-token custody, migration-ledger status, and whether any candidate artifact was adopted outside the repository.
