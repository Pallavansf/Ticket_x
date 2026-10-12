# Audit hash lifecycle

`PA_TICKETS_AUDIT.S_HASH` and `S_PREVIOUS_HASH` are tamper-evidence metadata. They do not make the database cryptographically immutable.

## Persistence and common hashing rules

The audit row is stored in `PA_TICKETS_AUDIT` with a global numeric `N_AUDIT_ID`, entity type and string ID, action, actor ID, timestamp, optional old/new/reason values, hash, and predecessor hash. The Oracle baseline also has actor type/role, IP, user-agent, correlation, and request columns; the current `AuditRow` mapping and writers do not include those columns in the canonical hash. PostgreSQL test schema uses the same mapped fields and enforces ordinary update/delete restrictions with a maintenance-only deletion procedure.

All inspected writers compute lowercase hexadecimal SHA-256 over UTF-8 bytes of `System.Text.Json` output using default serializer options (compact JSON, no custom converters or naming policy). Canonical property names and order are manually defined by each writer. The predecessor is the latest hash selected by descending global audit ID for that exact `(entity type, entity ID)`; verification orders rows by ascending audit ID. A stream's first available row has a null predecessor. The schema does not provide an independent stream-head anchor, so deletion of a tail cannot be detected.

## Format inventory from writer source

| Canonical format | Entity types / writers | Ordered canonical properties | Timestamp and reconstruction evidence |
|---|---|---|---|
| Ticket/lifecycle full shape | `TICKET`, `EfTicketRepository.AddAudit` | `entityType`, `entityId`, `action`, `actorUserId`, `auditAtUtc`, `oldValue`, `newValue`, `reason`, `previousHash` | UTC, truncated to PostgreSQL microsecond precision (10 ticks) before both hashing and persistence. Persisted rows are reconstructible; PostgreSQL lifecycle tests verify writer-produced chains. |
| Operational metadata full shape | `STATUS_DEFINITION`, `PRIORITY_DEFINITION`, `STATUS_TRANSITION`; `OperationalMetadataAuditWriter.AppendAsync`, called by `TicketStatusConfigurationRepository` and `TicketPriorityConfigurationRepository` | Same nine-property order as ticket format | UTC, truncated to microsecond precision before hashing/persistence. Status and priority streams are supported and PostgreSQL tests verify records produced by the actual repository/writer. `STATUS_TRANSITION` is deliberately not supported; see the persisted mismatch evidence below. |
| Incident full shape | `INCIDENT`, `IncidentService.AddAuditAsync` | Same nine-property order; writer supplies null old/new values and the reason | `TimeProvider.GetUtcNow()` converted to UTC `DateTime`; no explicit database-precision normalization. PostgreSQL may round stored timestamps, so exact historical hashes are not reliably reconstructible from persisted columns. |
| Problem/change short shape | `PROBLEM`, `ProblemService.AddAuditAsync`; `CHANGE`, `ChangeService.AddAuditAsync` | `entityType`, `entityId`, `action`, `actorUserId`, `auditAtUtc`, `reason`, `previousHash` (old/new properties are omitted, not null) | UTC `DateTime` from `TimeProvider`; no explicit precision normalization. |
| Catalogue short shape | `CATALOGUE`, `SUB_CATEGORY`; `CatalogueAdministrationService.AddAuditAsync` | `entityType`, `entityId`, `action`, `actorUserId`, `auditAtUtc`, `newValue`, `previousHash` (old/reason are omitted) | UTC `DateTime` from `TimeProvider`; no explicit precision normalization. |
| Communication-store short shape | Communication entity types passed to `CommunicationStore.AddAudit` | `entityType`, `entityId`, `action`, `actorUserId`, `auditAtUtc`, `previousHash`, `reason` (previous hash precedes reason; old/new omitted) | `DateTimeOffset.UtcNow`; no explicit precision normalization. |
| Administration full shape | `ADMINISTRATION`, `AdministrationService.AddAuditAsync` | Same nine-property order as ticket; old/reason are null, `newValue` is the supplied value | UTC from `TimeProvider`, no explicit precision normalization. |
| Attachment full shape | Attachment/evidence audit entity types, `AttachmentService.AddAudit` | Same nine-property order as ticket; old is null, new value/reason are writer inputs | `DateTimeOffset.UtcNow`; no explicit precision normalization. |
| Nested-value full shape | `WORKFLOW`, `WORKFLOW_VERSION`, `APPROVAL_RULE`, `APPROVAL_RULE_VERSION`, `COMMUNICATION_TEMPLATE`, `NOTIFICATION_RULE`, `NOTIFICATION_RULE_VERSION`, `SLA_CALENDAR`, `SLA_POLICY`, `SLA_POLICY_VERSION`; `DurableWorkflowConfigurationRepository`, `DurableApprovalConfigurationRepository`, `PersistentSlaAdministrationRepository`, `CommunicationAdministrationRepository` | Same nine-property outer order as ticket; `newValue` is itself JSON serialization of `{ value, correlationId }` | UTC from `DateTimeOffset.UtcNow` or `TimeProvider`, no explicit precision normalization. `SLA_POLICY` has a second, non-nested writer in `PersistentSlaConfigurationService`, so entity type alone cannot select one canonical format. |
| Durable execution full shape | `WORK_ITEM`, `APPROVAL`, `APPROVAL_INSTANCE`; work-item/approval audit paths in `DurableWorkflowApprovalRepositories` | Same nine-property order as ticket; writer-specific old/new/reason values | Passed UTC timestamp; no common precision normalization is established. |
| SLA escalation full shape | `SLA`, `SlaEscalationJobHandler` | Same nine-property order as ticket; old value is null, new value is escalation rules | `DateTimeOffset.UtcNow`; no explicit precision normalization. |
| SLA configuration full shape | `SLA_POLICY`, `PersistentSlaConfigurationService` | Same nine-property order as ticket; old/new values are null and reason is present | `DateTimeOffset.UtcNow`; no explicit precision normalization. `SLA_POLICY` overlaps the nested-value format above; `SLA_CALENDAR` is written by `PersistentSlaAdministrationRepository` using the nested-value format. |

The PostgreSQL inventory contained 24 persisted entity types. The three production-supported types are `TICKET`, `STATUS_DEFINITION`, and `PRIORITY_DEFINITION`. The other observed types are `ADMINISTRATION`, `APPROVAL`, `APPROVAL_INSTANCE`, `APPROVAL_RULE`, `APPROVAL_RULE_VERSION`, `ATTACHMENT`, `CATALOGUE`, `COMMUNICATION`, `COMMUNICATION_TEMPLATE`, `EVIDENCE`, `NOTIFICATION_RULE`, `NOTIFICATION_RULE_VERSION`, `SLA_CALENDAR`, `SLA_POLICY`, `SLA_POLICY_VERSION`, `STATUS_TRANSITION`, `SUB_CATEGORY`, `WORK_ITEM`, `WORKFLOW`, `WORKFLOW_STEP_INSTANCE`, and `WORKFLOW_VERSION`. Their current writer-format families are mapped in the table above except `WORKFLOW_STEP_INSTANCE`, for which no direct writer was identified in the current infrastructure source.

### Read-only evidence for the `STATUS_TRANSITION` mismatch

The read-only inspection used the configured PostgreSQL test database `pa_tickets_test`. At inspection time, `PA_TICKETS_AUDIT` contained 22,063 rows across 24 entity types. Audit ID `4258` is the reproduced mismatch:

| Persisted field | Value |
|---|---|
| Audit ID | `4258` |
| Entity type / ID | `STATUS_TRANSITION` / `TRIAGE:NEW` |
| Action / actor | `STATUS_TRANSITION_UPDATED` / `9001` |
| Persisted UTC timestamp | `2026-10-08T07:17:19.577434Z` |
| Old value | `TRIAGE->NEW:allowed=True:condition=:rowVersion=1` |
| New value | `TRIAGE->NEW:allowed=False:condition=:rowVersion=2` |
| Reason | `deny transition` |
| Previous hash | `f1837f3d039fcf163e9b96c990432f035593494f058f27e39d7816584068879c` |
| Stored hash | `724d88a003857fcc2600344f5f565558961c510c01b9abb56cdc8e358ec16a99` |

The immediately preceding same-stream row is audit ID `4257`, action `STATUS_TRANSITION_CREATED`, at `2026-10-08T07:17:19.505322Z`, with reason `add transition`, null old value, new value `TRIAGE->NEW:allowed=True:condition=:rowVersion=1`, null predecessor, and hash `f1837f3d039fcf163e9b96c990432f035593494f058f27e39d7816584068879c`. That row recomputes under the current operational-metadata canonical shape; ID `4258` does not.

The current `TicketStatusConfigurationRepository.SetTransitionAsync` calls `OperationalMetadataAuditWriter.AppendAsync`. The current PostgreSQL test `Transition_configuration_is_normalized_fail_closed_and_row_versioned` uses actor `9001` and writes the exact `TRIAGE:NEW` create/update reasons and values shown in IDs 4257–4258. The PostgreSQL Slice 6 schema seeds status and transition reference rows but does not seed the `TRIAGE:NEW` pair or audit records. The provisioning script resets the transition table but does not reset `PA_TICKETS_AUDIT`; normal test execution also uses a persistent database. Thus the current test can reproduce the same logical source sequence and is the most plausible source of these two rows, but cannot be mapped to global audit IDs 4257–4258 from the available evidence.

The retained `TestResults/*.trx` files do not associate those audit IDs with a run or report a `HASH_MISMATCH` for them. The available failed transition-test records instead show a PostgreSQL serialization conflict in one run and a precondition failure (`Assert.False`, actual `true`) in another; other retained runs passed. These outcomes are consistent with stateful/non-isolated tests but do not prove row provenance or the cause of the stored hash mismatch. Reproducing the logical test sequence would not prove that the persisted ID 4258 was written by that invocation or explain its stored digest.

The checked-out writer's canonical shape is nine properties in this exact order: `entityType`, `entityId`, `action`, `actorUserId`, `auditAtUtc`, `oldValue`, `newValue`, `reason`, `previousHash`. It uses default compact `System.Text.Json`, UTF-8 SHA-256 input, lowercase hexadecimal output, and normalizes UTC ticks to PostgreSQL microsecond precision before hashing and persistence. The stored columns do not establish whether ID `4258` was originally produced by this exact writer version, whether its hash/payload was changed, or whether a prior writer used different timestamp precision or canonicalization. No alternative serialization was treated as historical evidence.

The diagnostic SQL ran inside `BEGIN READ ONLY` / `ROLLBACK`; the row and hash were selected again and matched. No audit row was changed. The final provenance check inspected `PostgreSqlOperationalMetadataTests.cs`, `PostgreSqlTestEnvironment.cs`, the retained transition-test TRX results, `postgresql-schema.sql`, `postgresql-slice6-schema.sql`, `provision-postgresql-schema.ps1`, and migrations `022_operational_metadata.sql` and `024_workflow_initial_assignment_transition.sql`. It found no audit seed or historical writer source. The local `Docs.zip` contains documentation only and `package-lock.zip` contains only `package-lock.json`; no source archive or backup was found. Git history is unavailable (`git -C G:\WSS\aw\Tickets --no-pager log -5 --oneline` reported “not a git repository”), so earlier writer implementations or a row-to-test-run mapping could not be verified. The provenance search is exhausted with the available local evidence.

### Recommendations before enabling unsupported formats

- `STATUS_TRANSITION`: keep excluded. Reconsider only if authoritative earlier writer source and/or a database/test-run correlation artifact establishes row `4258` provenance and explains its mismatch; then validate the exact identified format against actual persisted rows. Current evidence is insufficient, and no alternate canonicalization should be selected by digest matching.
- Incident, problem/change, catalogue/subcategory, administration, attachment/evidence, communication, workflow/approval configuration, SLA, and durable-execution formats: keep excluded until PostgreSQL regressions prove exact hashes and predecessor links from the actual writer for every canonical shape, including timestamp round-trip precision and nullable/omitted-field behavior. Shared entity types must be distinguished by trustworthy persisted evidence; where the row cannot identify its writer format, keep verification unsupported.
- `WORKFLOW_STEP_INSTANCE`: rows exist in the local PostgreSQL test database, but no direct writer for this entity type was identified in the current infrastructure source search. Recover the source/version and provenance that created these rows before defining or enabling a format.

## Production verification support

The read-only `GET /api/v1/auditor/reports/tickets/{ticketId}/integrity` route continues to verify `TICKET` streams, requires `AUDITOR_VIEW`, and uses the existing authorized-ticket scope. It returns integrity metadata only and does not return audit payloads or repair rows.

`GET /api/v1/auditor/reports/configuration/{entityType}/{entityId}/integrity` adds verification for `STATUS_DEFINITION` and `PRIORITY_DEFINITION`. Those configuration streams are global (not ticket-scoped) in the existing model; the route requires `AUDITOR_VIEW` and accepts only the verifier's explicit allowlist. It returns entity identity and integrity metadata only. Unsupported formats return not found; recognized empty streams report `NO_AUDIT_RECORDS`.

The verifier checks entity type/ID, predecessor linkage, lowercase 64-character hexadecimal hash form, and the SHA-256 digest, in audit-ID order. Verification performs only `AsNoTracking` reads and never changes records. PostgreSQL tests mutate detached in-memory copies of persisted writer rows to prove payload, hash, predecessor, malformed-hash, and empty-stream outcomes without attempting to bypass append-only database protections.

Compatibility evidence for this continuation: focused verifier/API/transaction/concurrency/configuration regressions passed 14/14 (0 failed, 0 skipped); `dotnet test PA.Tickets.sln --no-restore --nologo` passed 180/180 (exit code 0); `dotnet build PA.Tickets.sln --no-restore --nologo` succeeded with 0 warnings and 0 errors (exit code 0). The PostgreSQL integration tests exercise status/priority rows persisted by their production repositories and append writer.

## Remaining limitations

- Production verification currently supports only `TICKET`, `STATUS_DEFINITION`, and `PRIORITY_DEFINITION`.
- `STATUS_TRANSITION` has a reproduced historical mismatch of unresolved origin and remains unsupported.
- Incident, problem, change, catalogue/subcategory, administration, attachment/evidence, communication, workflow, approval, SLA configuration/escalation, and durable-execution streams remain unsupported until the recommendations above are satisfied. `WORKFLOW_STEP_INSTANCE` additionally has no identified current direct writer.
- A privileged database owner/operator can bypass database-local append-only controls. There is no external immutable anchor; a privileged full-chain rewrite or deletion of a stream tail is outside the current guarantee.
- No schema change, migration, Oracle connection, or Oracle script is part of this verification work. PostgreSQL evidence does not certify Oracle behavior.
