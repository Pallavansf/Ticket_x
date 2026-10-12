# Remediation tracker

## Purpose

This tracker records the audit findings as hypotheses, verifies each one against source and tests, and distinguishes between confirmed defects, partially confirmed defects, and findings not reproduced in the current repository state.

## Summary

| Finding ID | Severity | Area | Status | Evidence |
|---|---|---|---|---|
| A1 | Critical | Authorization / resource isolation | Confirmed and fixed | Ticket search, foreign-scope resource membership, and related-ticket access have focused PostgreSQL regressions |
| A2 | High | Assignment / work-item authorization | Confirmed and fixed | Target membership, persisted work-item scope, and incident commander scope have negative regressions |
| A3 | High | Approval authorization | Confirmed and fixed | Current-approver delegation, active/in-scope participants, and separation of duties have API/PostgreSQL coverage |
| A4 | High | Specialized tickets, attachments, and reporting | Verified | Problem/Change related-ticket access, attachment/evidence ticket scope, department report isolation, and auditor permission/read-only behavior are covered |
| A5 | High | Administration and catalogue mutation | Verified | Existing negative permission-gate tests remain passing |
| B1 | High | Scheduled report execution | Confirmed and fixed | Due schedules were never enqueued by the worker; payload casing also broke handler lookup; execution/replay regressions pass |
| C1 | High | SLA deadline detection | Runtime detection implemented and verified; policy semantics remain open | Worker-driven response/resolution detection, pause extension, terminal completion, idempotency, and escalation have focused regressions |
| D2 | Medium | Ticket persistence atomicity and orchestration recovery | Verified; PostgreSQL regression, full suite, and build pass | Ticket plus idempotency result persist transactionally; downstream orchestration resumes the same ticket without duplicate workflow/SLA work |
| D1 | Medium | UI visual defects | Confirmed and remediated | White-first design remediation was applied and Angular build/test validation passed |
| E1 | High | Catalogue enforcement, request-type versions, and form integrity | Partial; confirmed validation/UI eligibility defects fixed; dynamic-form contract gaps remain | PostgreSQL regressions cover unassociated values, mismatched field types, invalid options, and malformed payloads; Angular tests cover active/effective hierarchy and published/effective version filtering |
| F1 | High | Audit-chain verification and integrity | Partial; ticket, status-definition, and priority-definition formats verified; remaining historical formats unresolved | PostgreSQL persisted-writer/tampering/API/transaction/concurrency regressions pass; transition history contains an unexplained mismatch; no external immutable anchor exists |
| H1-H4 | High | Workstream H contract audit and documentation | Confirmed in source and local .NET regressions; Angular browser execution blocked by missing test runtime | Public work-item ID is mapped distinctly from the display number; blank/whitespace Idempotency-Key rejection is enforced at the ticket-create boundary; communication activation remains fail-closed; local .NET regressions pass while Angular browser runner is unavailable in this environment |

## Findings register

### H1-H4 — Workstream H contract corrections

- Severity: High
- Affected layer: Angular ticket completion flow, ticket-create API contract, communication delivery activation, and remediation documentation
- Exact file(s):
  - `client/src/app/features/tickets/ticket-detail.page.ts`
  - `client/src/app/core/tickets-api.service.ts`
  - `src/PA.Tickets.Api/Controllers/WorkItemsController.cs`
  - `src/PA.Tickets.Api/Controllers/TicketsController.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/CommunicationDeliveryProcessor.cs`
  - `tests/PA.Tickets.Domain.Tests/WorkstreamHRegressionTests.cs`
  - `Docs/WORKSTREAM_H_CRITICAL_CONTRACT_CORRECTIONS.md`
- Status: Confirmed in source and local unit tests; Angular browser validation remains blocked by the environment
- Reproduction evidence: The working tree was inspected to verify the actual contract: display number and public ID are distinct values; the UI and service use the public ID for completion; blank `Idempotency-Key` handling was not safe to overclaim beyond the controller guard; communication activation logic was validated only through local safe unit tests.
- Expected behavior: Work-item completion must use the public work-item identifier, ticket creation must reject blank `Idempotency-Key` values before any service invocation, and communication delivery must only activate when the channel/provider is enabled inside a valid date window.
- Root cause: The main H1 contract mismatch was a source contract issue rather than a stale report artifact; the earlier report overclaimed Angular validation without a runnable browser harness. The repository state needed careful reconciliation against the actual working tree.
- Fixes implemented: H1 contract alignment in the API/UI/service path uses `publicId`; H2 controller boundary rejects blank/whitespace idempotency keys with a `400` response; H3 communication activation remains fail-closed when configuration is missing, disabled, or outside the window; H4 documentation was corrected to reflect actual evidence.
- Verification evidence:
  - `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter WorkstreamHRegressionTests --nologo -v minimal` passed 5/5 (exit code 0)
  - `dotnet build PA.Tickets.sln --nologo -v minimal` succeeded with 0 warnings and 0 errors (exit code 0)
  - `node .\node_modules\@angular\cli\bin\ng test --watch=false --include src/app/features/tickets/ticket-detail.page.spec.ts --browsers=ChromeHeadless` could not start because the required Vitest browser packages were not installed; exit code 1
  - `npx ng build --configuration production` was blocked by PowerShell execution policy before Angular started; exit code 1
- Notes: No Oracle connection was attempted. No shared persistent PostgreSQL validation was used. Workstream I was not started.

### A1 — Authorization / resource isolation

- Severity: Critical
- Affected layer: API controllers, application services, repositories, and authorization services
- Exact file(s):
  - `src/PA.Tickets.Application/Tickets/TicketCommands.cs`
  - `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlAdministrationAuthorizationTests.cs`
- Status: Confirmed and fixed
- Reproduction evidence: A new regression test proved that a non-admin user with `TICKET_READ` permission could call `TicketLifecycleService.SearchAsync` without a scope filter and receive a valid result instead of a 401/403-style rejection.
- Expected behavior: Users should only access resources within the authorized scope and must provide an explicit departmental/team/client/assignee search scope when not an administrator.
- Root cause: `TicketLifecycleService.SearchAsync` validated only the permission code and did not require a scoped filter; `DatabaseAuthorizationService` also treated `resource == null` as globally readable for any user with the permission.
- Proposed minimal fix: Require explicit search scope for non-admin users, validate the requested department/team/client/assignee against their membership, and apply the actor’s matching scope when appropriate.
- Regression test: `Ticket_search_requires_explicit_scope_filters_for_non_admin_users` in `PostgreSqlAdministrationAuthorizationTests.cs`.
- Additional regression coverage: `Reports_do_not_return_tickets_from_a_foreign_department_filter`, cross-scope assertions in `Database_authorization_enforces_permission_scope_effective_dates_and_auditor_read_only_access`, and specialized ticket/related-ticket reads.
- Verification evidence: focused authorization batch passed 55/55; full `dotnet test PA.Tickets.sln --no-restore --nologo` passed 167/167 (exit code 0).

### A2 — Assignment / scope enforcement

- Severity: High
- Affected layer: ticket assignment, workflow work items, and incident commander assignment
- Exact file(s):
  - `src/PA.Tickets.Application/Tickets/TicketCommands.cs`
  - `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs`
  - `src/PA.Tickets.Infrastructure/Execution/DurableWorkflowApprovalRepositories.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlAdministrationAuthorizationTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlWorkflowExecutionE2ETests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlIncidentTests.cs`
  - `tests/scripts/postgresql-slice1-schema.sql`
- Status: Confirmed and fixed
- Reproduction evidence: Assignment could override mismatched organizational scope; durable work-item checks used mutable ticket scope rather than persisted work-item scope; incident creation accepted an initial commander without target validation. Initial focused execution also exposed inconsistent routing fixture memberships.
- Root cause: Scope checks were bypassable by assignment identity, work-item authorization used a different resource scope than the assigned work item, and creation omitted commander validation.
- Fix: Check resource scope before assignment identity; authorize completion/reassignment using persisted work-item department/team/assignee and parent ticket client; validate active target membership; validate an initial incident commander before ticket creation. Test routing users now match configured department/team routes.
- Regression tests: `Database_authorization_enforces_permission_scope_effective_dates_and_auditor_read_only_access`, unauthorized work-item completion/reassignment API assertions, and the out-of-scope incident commander regression.
- Verification: focused authorization batch 55/55 and complete .NET suite 167/167, both exit code 0.

### A3 — Approval eligibility and delegation

- Severity: High
- Affected layer: durable approval creation, decision, and delegation
- Exact file(s):
  - `src/PA.Tickets.Infrastructure/Execution/DurableWorkflowApprovalRepositories.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlWorkflowExecutionE2ETests.cs`
- Status: Confirmed and fixed
- Reproduction evidence: Delegation did not consistently require the actor to be the current approver; participant validation did not establish an active user in ticket scope.
- Fix: Delegation requires actor identity to match the current approver; active/in-scope approval participants and requester/creator/executor separation of duties are checked.
- Regression tests: Durable approval API flows cover unauthorized and valid delegation, SOD, duplicate decisions, and concurrent decisions; workflow audit-chain tests seed active delegates.
- Verification: Included in focused authorization batch 55/55 and full suite 167/167.

### A4 — Specialized resource, attachment, and reporting access

- Severity: High
- Affected layer: Incident, Problem, Change, attachments/evidence, reporting, and auditor APIs
- Exact file(s):
  - `src/PA.Tickets.Infrastructure/Tickets/ProblemService.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/ChangeService.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs`
  - `src/PA.Tickets.Infrastructure/Reporting/ReportingQueryService.cs`
  - `src/PA.Tickets.Infrastructure/Reporting/AuditorReportingService.cs`
  - `src/PA.Tickets.Application/Reporting/AuditorContracts.cs`
  - `src/PA.Tickets.Api/Controllers/AuditorReportsController.cs`
  - `src/PA.Tickets.Api/Controllers/AuditorReportsController.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlProblemTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlChangeTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlIncidentTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlAdministrationAuthorizationTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlAttachmentEvidenceTests.cs`
  - `tests/PA.Tickets.Domain.Tests/AuditorReportingTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`
- Status: Verified
- Evidence: Relationship reads check both source and related records; incident commander targets are scoped; attachment/evidence metadata, versions, downloads, and review enforce parent-ticket access; ordinary reports intersect caller filters with authorized ticket scope; auditor definitions require `AUDITOR_VIEW`, output is limited to authorized ticket IDs, and auditor access remains read-only.
- Regression tests: `Problem_creation_is_idempotent_and_relationships_are_authorized_and_unique`, `Change_relationship_reads_require_access_to_related_tickets`, `Attachment_and_evidence_access_uses_ticket_authorization_for_metadata_versions_download_and_review`, `Reports_do_not_return_tickets_from_a_foreign_department_filter`, plus auditor permission and out-of-scope API tests.
- Verification: Focused authorization batch 55/55 and full suite 167/167, exit code 0.

### A5 — Administration and catalogue mutation

- Severity: High
- Affected layer: administration/catalogue application services and API controllers
- Status: Verified
- Evidence: `Administration_mutations_are_permission_gated_persisted_and_audited` and `Database_authorization_enforces_permission_scope_effective_dates_and_auditor_read_only_access` verify non-admin denials, administrator mutation, and auditor read-only behavior. Full .NET suite passed.

### Authorization workstream A disposition

Verified against ticket detail/search, assignment/reassignment, work-item completion, approvals/delegation, Incident/Problem/Change, attachment/evidence, reports/exports, auditor, and administration/catalogue paths. Evidence is PostgreSQL repository-level integration coverage; production identity-provider and deployment acceptance remain outside scope. The original audit report itself is not present in `Docs/`, so the supplied audit/workstream description and retained baseline served as the finding inventory.

### B1 — Scheduled report execution

- Severity: High
- Affected layer: Persistent worker, due-schedule selection, job payload, execution history
- Exact file(s):
  - `src/PA.Tickets.Infrastructure/Execution/PersistentBackgroundWorker.cs`
  - `src/PA.Tickets.Infrastructure/Reporting/ReportScheduleJobHandler.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlReportScheduleExecutionTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlWorkflowExecutionE2ETests.cs`
- Status: Confirmed and fixed
- Reproduction evidence: The worker contained due-schedule enqueue logic but never called it before leasing jobs. Once wired, the integration test exposed a payload casing mismatch: the enqueuer emitted `scheduleId`, while the handler deserialized into case-sensitive `ScheduleId`, producing a missing-schedule failure.
- Root cause: Disconnected due-schedule scanner and incompatible internal JSON property names.
- Fix: Each worker cycle enqueues eligible schedules before leasing; due schedules are ordered oldest-first and limited to 25; a dedupe key uses schedule ID plus persisted due timestamp; the JSON payload uses the handler's exact property name. Completed execution rows continue to short-circuit replay.
- Regression test: `Worker_enqueues_only_due_active_schedules_and_retries_without_duplicate_execution` covers active-due, paused-due, and future schedules, repeated enqueue, worker execution, next-run advancement, and replay of a completed job without a second execution/export.
- Restart/recovery evidence: `Durable_jobs_dedupe_concurrent_leasing_and_retry_backoff_are_postgresql_safe` in `PostgreSqlWorkflowExecutionE2ETests` covers recovery of a persisted lease after a simulated crash; the schedule integration test processes the persisted deduplicated job through a newly constructed worker.
- Verification: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~PostgreSqlReportScheduleExecutionTests" --nologo` passed 1/1 (exit code 0); `dotnet test PA.Tickets.sln --no-restore --nologo` passed 167/167 (exit code 0).
- Remaining risk: A permanently exhausted job leaves the schedule's original `NextRunAt` due; operator recovery/retry policy for that case is not changed in this slice.

## Files changed in this remediation pass

- `src/PA.Tickets.Application/Tickets/TicketCommands.cs`
- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`
- `src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs`
- `src/PA.Tickets.Infrastructure/Execution/DurableWorkflowApprovalRepositories.cs`
- `src/PA.Tickets.Infrastructure/Execution/PersistentBackgroundWorker.cs`
- `src/PA.Tickets.Infrastructure/Reporting/ReportingQueryService.cs`
- `src/PA.Tickets.Infrastructure/Reporting/AuditorReportingService.cs`
- `src/PA.Tickets.Infrastructure/Tickets/ProblemService.cs`
- `src/PA.Tickets.Infrastructure/Tickets/ChangeService.cs`
- `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs`
- `src/PA.Tickets.Api/Controllers/AuditorReportsController.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlAdministrationAuthorizationTests.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlWorkflowExecutionE2ETests.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`
- `tests/PA.Tickets.Domain.Tests/AuditorReportingTests.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlChangeTests.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlIncidentTests.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlProblemTests.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlReportScheduleExecutionTests.cs` (new)
- `tests/PA.Tickets.Domain.Tests/UnitTest1.cs`
- `tests/scripts/postgresql-slice1-schema.sql`
- `src/PA.Tickets.Application/Execution/DurableExecutionContracts.cs`
- `src/PA.Tickets.Domain/Sla/SlaExecution.cs`
- `src/PA.Tickets.Infrastructure/DependencyInjection.cs`
- `src/PA.Tickets.Infrastructure/Execution/DurableSlaJobRepositories.cs`
- `src/PA.Tickets.Infrastructure/Execution/SlaDeadlineMonitor.cs` (new)
- `Docs/REMEDIATION_TRACKER.md`
- `Docs/REMEDIATION_BASELINE.md`

### C1 — SLA deadline detection and escalation

- Severity: High
- Affected layer: SLA policy evaluation, escalation jobs, waiting reasons, policy versioning
- Exact file(s):
  - `src/PA.Tickets.Domain/Sla/SlaExecution.cs`
  - `src/PA.Tickets.Application/Execution/DurableExecutionContracts.cs`
  - `src/PA.Tickets.Infrastructure/DependencyInjection.cs`
  - `src/PA.Tickets.Infrastructure/Execution/DurableSlaJobRepositories.cs`
  - `src/PA.Tickets.Infrastructure/Execution/PersistentBackgroundWorker.cs`
  - `src/PA.Tickets.Infrastructure/Execution/SlaDeadlineMonitor.cs`
  - `tests/PA.Tickets.Domain.Tests/UnitTest1.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlWorkflowExecutionE2ETests.cs`
- Status: Confirmed; automated runtime detection implemented and verified. Breach-rule configuration semantics remain unresolved.
- Reproduction evidence: `PersistentBackgroundWorker` scanned report schedules but did not scan SLA deadlines. The durable `Breach` operation was only reached through the manual SLA operations endpoint. `SlaClock.CheckBreach` considered only the resolution target, and `Resume` accumulated waiting minutes without extending persisted deadlines. The new domain regression failed before the domain correction because the response deadline remained at 09:30 instead of shifting to 10:00 after a 30-minute business-time pause.
- Root cause: Deadline evaluation was not wired into a runtime worker, and the domain clock did not treat response targets or pause time consistently.
- Fix: Every persistent-worker cycle now checks eligible persisted SLA rows before leasing jobs. Response and resolution deadline events are written with stable per-target idempotency keys; escalation uses the selected policy version and the existing once-per-SLA dedupe key. Completed pause intervals extend deadlines using the selected calendar's working minutes. Resolution, closure, or cancellation completes the SLA at the persisted lifecycle event time; the chosen policy is that reopening requires a new SLA.
- Regression tests: `Sla_clock_uses_business_calendar_pause_extensions_and_checks_both_deadlines` covers both targets, pause extension, weekdays, and a holiday boundary. `Background_worker_detects_both_sla_deadlines_once_and_escalates_after_restart` exercises both deadline events and escalation across fresh worker instances. `Sla_deadline_monitor_applies_pause_extensions_and_completes_at_terminal_resolution` covers a persisted pause, resolved/closed/cancelled ticket states, and repeated monitor runs. The existing SLA administration/escalation regression remains in the focused batch.
- Verification:
  - Pre-fix: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~Sla_clock_uses_business_calendar_pause_extensions_and_checks_both_deadlines" --nologo` failed as expected (1 failed; response deadline did not move after pause).
  - Focused: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~Sla_deadline_monitor|FullyQualifiedName~Background_worker_detects_both_sla|FullyQualifiedName~Sla_clock_uses_business_calendar|FullyQualifiedName~Sla_administration_persists_version" --nologo` passed 4/4 (exit code 0).
  - Full solution: `dotnet test PA.Tickets.sln --no-restore --nologo` passed 169/169, 0 failed, 0 skipped (exit code 0).
  - Build: `dotnet build PA.Tickets.sln --no-restore --nologo` succeeded with 0 warnings and 0 errors (exit code 0).
- Remaining risks / decisions: `BreachRules` and `NotificationRules` are stored but have no evaluator elsewhere in the source; this slice uses the persisted response/resolution targets and existing `EscalationRules`, without inventing new interpretation for those JSON fields. SLA instances pin policy ID/version but only persist a calendar code, not an immutable calendar version/snapshot, so pause adjustment after a calendar is edited cannot reconstruct historical hours/holidays. DST-boundary behavior and Oracle provider behavior were not exercised. Automatic checks run only when the existing `Jobs:Enabled` worker is enabled.

### D2 — Atomic ticket creation and downstream recovery

- Severity: Medium
- Affected layer: ticket persistence, idempotency state, and ticket creation orchestration
- Exact file(s):
  - `src/PA.Tickets.Application/Foundations/CatalogueContracts.cs`
  - `src/PA.Tickets.Application/Tickets/TicketCommands.cs`
  - `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/TicketCreationIdempotencyStore.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs`
  - `src/PA.Tickets.Infrastructure/Execution/PersistentSlaOrchestrationService.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlTicketCreationIdempotencyTests.cs`
- Status: Persistence atomicity and retry recovery confirmed; full automated validation passes. Recovery remains fail-safe if original catalogue configuration has changed and no longer resolves.
- Reproduction evidence: A PostgreSQL regression removes the selected SLA calendar immediately before ticket creation. Ticket persistence and workflow setup succeed, but SLA startup throws `KeyNotFoundException`. The test verifies the request becomes `FAILED` while retaining its persisted ticket ID. After restoring the same calendar, retrying the same actor/key/payload must resume the persisted ticket and finish downstream orchestration. Before the recovery correction, a `FAILED` idempotency row with an existing ticket was replayed as success and skipped the missing SLA/work item completion.
- Root cause: Ticket creation persisted the ticket and idempotency result separately, and failed downstream orchestration could leave a durable ticket that replay logic returned as completed rather than safely resuming.
- Fix: Added an idempotent repository persistence overload that writes the ticket and its result ticket ID in the same EF transaction; prevent replacing a result with a different ticket; distinguish completed replay from failed/incomplete recovery; resume using the original ticket and creation timestamp only after validating its original request/catalogue configuration; and make SLA start return an existing matching started SLA without resetting it. Workflow, approval, and SLA persistence still span their existing infrastructure contexts; recovery is idempotent rather than one cross-context database transaction.
- Regression: `Failed_downstream_sla_start_resumes_creation_without_duplicate_work` in `PostgreSqlTicketCreationIdempotencyTests.cs` asserts persisted-ticket retention after failure, same-ticket recovery, one workflow instance, one SLA, one work item, one `SLA_STARTED` event, and final `COMPLETED` idempotency state. Existing same-key replay, payload conflict, concurrent-key, and failed-before-persist retry tests remain in that class.
- Verification:
  - `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~PostgreSqlTicketCreationIdempotencyTests"` passed 9/9 (0 failed, 0 skipped; exit code 0).
  - `dotnet test PA.Tickets.sln --no-restore --nologo` passed 169/169 (0 failed, 0 skipped; exit code 0).
  - `dotnet build PA.Tickets.sln --no-restore --nologo` succeeded (0 warnings, 0 errors; exit code 0).
- Remaining risks: PostgreSQL-only integration coverage was used; Oracle was not accessed. Cross-context downstream recovery currently depends on the caller retrying the same idempotency key after a failure; there is no automatic recovery sweeper. Recovery deliberately fails safely if the original catalogue configuration no longer resolves or no longer matches; operational remediation for that state is not automated.

### E1 — Catalogue enforcement, request-type versions, and dynamic form integrity

- Severity: High
- Affected layer: ticket create API/application validation, catalogue runtime, request-type selection client
- Status: PARTIAL. Confirmed validation and version-selection defects are fixed and tested. End-to-end dynamic field rendering and unambiguously typed support for every configured field type remain unresolved because existing contracts do not define them.
- Baseline and request-path evidence:
  - `git status --short --branch` exited 128 with `fatal: not a git repository`; Git diff/status review is unavailable in this working directory. Source, tests, tracker, baseline, and PostgreSQL scripts were inspected directly.
  - The actual path is `TicketCreatePage.submit()` -> `TicketsApiService.create()` -> `TicketsController.Create()` -> `TicketLifecycleService.CreateAsync()` -> `TicketCatalogueRuntime.ResolveForCreationAsync()` -> `EfTicketRepository.AddAsync()` -> workflow/approval/SLA orchestration. Catalogue resolution and validation run before ticket persistence.
  - Request types are versioned rows. Publication sets `Status=PUBLISHED` and `Active=true`; published versions are immutable, their effective intervals are checked during creation, overlapping published intervals are rejected, and tickets persist the selected request-type row ID and version number. PostgreSQL coverage in `PostgreSqlCatalogueSubCategoryTests.Catalogue_paths_resolve_validate_persist_and_preserve_published_versions` verifies hierarchy, inactive paths, immutability, and historical version persistence.
  - Service/category/subcategory ancestry is checked by the runtime and constrained by PostgreSQL foreign keys. The API permits the request-type ID plus optional catalogue path IDs; priority/workflow/SLA values are resolved from server-side configuration. Existing permission gates remain in place.
  - Dynamic-field retrieval is incomplete: the only request-fields GET returns associations, no GET endpoint returns field definitions/options, and the create page explicitly does not render dynamic controls. No new endpoint or payload was introduced.
- Confirmed defects and reproduction:
  - Before the fix, the runtime returned immediately when `RequestTypeId` was absent, silently skipping validation even if request-specific field values were supplied. The new PostgreSQL regression failed before the fix at the missing exception assertion: `Assert.Throws() Failure: No exception was thrown` (1 failed; exit code 1).
  - Before the fix, required-field validation only checked that a field ID was present. A required `NUMBER` field supplied as `StringValue` passed validation; multiple typed value slots could also be stored together. Invalid SELECT options were already rejected.
  - The Angular selector filtered on `status === 'ACTIVE'`, but the published API contract returns `PUBLISHED`; it also did not filter by the effective interval even though those dates are returned.
- Fixes and files changed:
  - `src/PA.Tickets.Infrastructure/Foundations/TicketCatalogueRuntime.cs` — reject request-specific values without a request type; require exactly one populated value slot; enforce the unambiguous existing slot mappings for STRING/SELECT/MULTISELECT, NUMBER, DATE/DATETIME, and TEXT; retain active option validation; reject blank required string/text values.
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlCatalogueAdministrationTests.cs` — added PostgreSQL coverage for missing request type, incorrect numeric slot, invalid select option, multiple populated slots, missing required fields, failed idempotency state with no result ticket, and valid typed values.
  - `client/src/app/core/tickets-api.service.ts` — declare the effective dates already returned in request-type DTOs.
  - `client/src/app/features/tickets/create/ticket-create.page.ts` — filter active service/category/subcategory options by current effective interval and request-type versions by active + PUBLISHED status + current effective interval.
  - `client/src/app/features/tickets/create/ticket-create.page.spec.ts` — regressions for active/effective hierarchy options and published/effective request-type selection.
  - `Docs/REMEDIATION_TRACKER.md` — record this evidence and the partial disposition.
- Regression guarantees: Invalid creates fail before ticket persistence; tests assert unchanged ticket count and a `FAILED` idempotency record with no `ResultTicketId`. A valid request-type create with an allowed SELECT option and numeric value still persists the selected version and typed values. Existing authorization, hierarchy, versioning, and A–D regressions remain covered by the full solution suite.
- Verification:
  - Pre-fix: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~Catalogue_hierarchy_fields_publication_and_audit_persist_in_postgresql"` failed as expected at the new no-request-type field-values assertion (1 failed; exit code 1).
  - Focused PostgreSQL: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~PostgreSqlCatalogueAdministrationTests|FullyQualifiedName~PostgreSqlCatalogueSubCategoryTests"` passed 6/6 (0 failed, 0 skipped; exit code 0).
  - Focused Angular: `npm.cmd test -- --no-watch --no-progress --include=src/app/features/tickets/create/ticket-create.page.spec.ts` passed 1 file/5 tests (exit code 0).
  - Full .NET: `dotnet test PA.Tickets.sln --no-restore --nologo` passed 169/169 (0 failed, 0 skipped; exit code 0).
  - Full Angular: `npm.cmd test -- --no-watch --no-progress` passed 10 files/28 tests (0 failed; exit code 0).
  - .NET build: `dotnet build PA.Tickets.sln --no-restore --nologo` succeeded (0 warnings, 0 errors; exit code 0).
  - Angular production build: `npm.cmd run build` succeeded (exit code 0).
- API/schema implications: No API route, request payload, database schema, migration, or authorization rule changed. Argument validation continues to use the established HTTP 400 error format with trace ID. The client type now models effective-date fields already returned by the API.
- Remaining risks and decisions:
  - Dynamic fields cannot be end-to-end rendered until a backward-compatible field-definition/options read and submission contract is specified and authorized.
  - The existing allowlist includes MULTISELECT, BOOLEAN, USER, DEPARTMENT, TEAM, and CLIENT, but the ticket field-value request has no explicit collection, boolean, or identifier serialization rule. This remediation does not invent their representations; only one populated value slot is enforced for these types.
  - Catalogue field and option schema has no department/team/client scope attributes, so no narrower restricted-option policy can be inferred.
  - PostgreSQL only was exercised. Oracle was not connected to and no Oracle scripts/migrations were run. Git diff/status review was unavailable.

### D1 — White-first enterprise UI and visual defects

- Severity: Medium
- Affected layer: Angular client styling and shell layout
- Exact file(s):
  - `client/src/styles.scss`
  - `client/src/app/layout/shell.scss`
  - `client/src/app/features/login/login.scss`
  - `client/src/app/features/tickets/ticket-list.scss`
  - `client/src/app/features/tickets/ticket-detail.scss`
  - `client/src/app/features/tickets/create/ticket-create.scss`
- Status: Confirmed and remediated
- Reproduction evidence: Visual audit showed a dark shell, inconsistent spacing/radius values, generic forms, and an unpolished enterprise look across the primary screens.
- Expected behavior: Premium white-first enterprise UI with restrained blue/indigo gradients, glassmorphism accents, visual hierarchy, and accessible motion behavior.
- Root cause: The existing UI used a fragmented ad hoc styling approach with inconsistent tokens and insufficient polish.
- Proposed minimal fix: Introduce a design token system, improve shell hierarchy, unify form/table styles, add reduced-motion handling, and maintain the existing Angular contract boundaries.
- Regression test: Regression checks added for preserved list navigation context in `ticket-detail.page.spec.ts` alongside the Angular suite.
- Verification evidence: `npm test -- --no-watch --no-progress` passed (26/26), and `npm run build` passed with 0 warnings and 0 errors.

## Current disposition

Authorization workstream A and scheduled-report workstream B remain verified/fixed against the reviewed source paths and PostgreSQL regression suite. The SLA runtime path in workstream C is implemented and regression-tested, but the policy JSON/calendar-version limitations listed above remain open; C is not represented as fully closed while those policy semantics are unresolved. Workstream D's ticket/idempotency persistence and retry recovery passed PostgreSQL regression, full-suite, and build validation; downstream orchestration remains cross-context and requires a caller retry after failure, as recorded above. Workstream E fixed server-side field-value validation and request-type UI eligibility with PostgreSQL/Angular regressions, but remains partial pending explicit dynamic-field/options read and serialization contracts. The original end-to-end audit report itself was not found in `Docs/`; the supplied workstream descriptions and retained baseline were used as the finding inventory. Oracle, identity-provider, and deployment validation were not performed.

### F1 — Audit-chain verification and tamper detection

- Severity: High
- Affected layer: audit writers, PostgreSQL audit persistence, auditor reporting
- Exact files changed:
  - `src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs`
  - `src/PA.Tickets.Infrastructure/Persistence/AuditChainCoordinator.cs`
  - `src/PA.Tickets.Infrastructure/Persistence/AuditTransaction.cs`
  - `src/PA.Tickets.Infrastructure/Reporting/AuditChainVerifier.cs`
  - `src/PA.Tickets.Infrastructure/Reporting/AuditorReportingService.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/OperationalMetadataAuditWriter.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/CatalogueAdministrationService.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/AdministrationService.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/CommunicationStore.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/CommunicationAdministrationRepository.cs`
  - `src/PA.Tickets.Infrastructure/Foundations/AttachmentService.cs`
  - `src/PA.Tickets.Infrastructure/Execution/PersistentSlaAdministrationRepository.cs`
  - `src/PA.Tickets.Infrastructure/Execution/PersistentSlaConfigurationService.cs`
  - `src/PA.Tickets.Infrastructure/Execution/DurableWorkflowConfigurationRepository.cs`
  - `src/PA.Tickets.Infrastructure/Execution/DurableApprovalConfigurationRepository.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/ProblemService.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/ChangeService.cs`
  - `src/PA.Tickets.Api/Controllers/AuditorReportsController.cs`
  - `src/PA.Tickets.Application/Reporting/AuditorContracts.cs`
  - `tests/PA.Tickets.Domain.Tests/AuditChainVerifierTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlAuditTransactionTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlOperationalMetadataTests.cs`
  - `Docs/AUDIT_HASH_DESIGN.md`
  - `Docs/AUDITOR_REPORTING.md`
  - `Docs/REMEDIATION_TRACKER.md`
- Status: PARTIAL.
- Historical writer inventory and canonicalization evidence:
  - All inspected writers use compact default `System.Text.Json` output, UTF-8 bytes, lowercase SHA-256 hex, and predecessor selection by descending audit ID per `(EntityType, EntityId)`. The canonical field order, stream-to-writer mapping, timestamp normalization, and overlaps are recorded in `Docs/AUDIT_HASH_DESIGN.md`.
  - Production verification now additionally supports `STATUS_DEFINITION` and `PRIORITY_DEFINITION`, written through `OperationalMetadataAuditWriter`. That writer truncates UTC timestamps to PostgreSQL microsecond precision before hashing and persistence. PostgreSQL regressions verify actual rows emitted by the status and priority repositories, including full-chain links and tampering outcomes.
  - `STATUS_TRANSITION` is excluded despite using the same current writer: persisted PostgreSQL history contains a record that fails recomputation under the source-visible canonical fields while adjacent rows validate. The historical writer version/cause cannot be recovered because Git metadata is unavailable. No alternate format is guessed.
  - `INCIDENT`, `PROBLEM`, `CHANGE`, `CATALOGUE`, `ADMINISTRATION`, attachment, communication, workflow, approval, SLA configuration, SLA escalation, and durable-execution streams remain unsupported. See the format inventory for omitted-vs-null fields, non-normalized timestamp precision, nested values, and ambiguous shared entity types (including SLA streams).
- Confirmed defects and reproduction:
  - The audit hash design document claimed a canonical object containing actor type/role, IP, user-agent, correlation/request IDs, and timestamp-plus-ID ordering. The mapped `AuditRow` and ticket writer persist/hash only `entityType`, `entityId`, `action`, `actorUserId`, `auditAtUtc`, `oldValue`, `newValue`, `reason`, and `previousHash`; the writer reads the latest row by ID. The documentation overstated implemented fields and ordering.
  - No production audit-chain verifier was registered or exposed; auditor reports returned saved hash strings without recomputing either hashes or predecessors.
  - A new PostgreSQL regression verified hashes written by the ticket lifecycle. Before timestamp normalization, the persisted ticket stream failed with `HASH_MISMATCH` at its first row: PostgreSQL stores `TIMESTAMP WITH TIME ZONE` at microsecond precision, while the writer hashed the higher-precision in-memory timestamp. The writer now normalizes the UTC timestamp to microsecond precision before hashing and persisting it.
  - `EfTicketRepository.SaveAsync` called `AddAudit` before beginning its transaction. `AuditChainCoordinator`'s `SELECT ... FOR UPDATE` therefore ran outside the later SaveChanges transaction, releasing the row lock before the latest predecessor was read and the audit row inserted. Ticket event persistence now begins the transaction before lock acquisition and holds it through SaveChanges.
- Implemented:
  - Added an additive auditor route `GET /api/v1/auditor/reports/tickets/{ticketId}/integrity`. It requires `AUDITOR_VIEW`, uses `AuthorizedTicketIds` to enforce existing ticket scope (out-of-scope tickets return not found), verifies hashes and predecessor links in audit-ID order, reports only a boolean/count/failure code/audit-row ID, and does not mutate records.
  - Added a focused ticket-stream verifier. It rejects empty streams, stream mismatch, broken predecessor links, malformed/non-lowercase hashes, and payload/hash mismatches; it never repairs evidence.
  - Extended that verifier to the shared full-nine-field, precision-normalized status/priority configuration format. Unsupported types return `UNSUPPORTED_FORMAT` without attempting a speculative canonicalization.
  - Added `GET /api/v1/auditor/reports/configuration/{entityType}/{entityId}/integrity`, restricted to supported global configuration streams and `AUDITOR_VIEW`; retained the ticket-scoped route unchanged. Both return integrity metadata only and are read-only.
  - Added transaction and timestamp-precision corrections to the ticket writer.
  - Hardened direct audit append helpers: `AuditChainCoordinator` now rejects lock acquisition outside an active transaction; helpers begin a transaction when needed, save the audit row while holding the per-stream lock, and commit only a transaction they created. Helpers invoked inside an existing mutation transaction reuse it.
  - Added PostgreSQL regressions proving an operational audit append participates in a caller-owned transaction (rollback removes both the tracked mutation and audit row) and concurrent operational appends retain one linked ticket chain.
  - Existing PostgreSQL coverage continues to prove ordinary direct UPDATE/DELETE rejection and controlled maintenance logging. Existing concurrent workflow approval append coverage remains in place; new concurrent ticket-save coverage verifies one linked 13-record chain.
- Regression tests:
  - `AuditChainVerifierTests` validates a valid chain and detects payload, hash, and predecessor tampering, and rejects an empty stream.
  - `PostgreSqlApiE2ETests.Auditor_reporting_http_api_reads_persisted_records_and_enforces_controls` checks a valid result, no verification-side mutation, out-of-scope not-found behavior, and denied `AUDITOR_VIEW`.
  - `PostgreSqlTicketPersistenceTests.Ticket_lifecycle_service_persists_and_reloads_core_ticket_flow_in_postgresql` verifies hashes generated by real ticket lifecycle writes.
  - `PostgreSqlTicketPersistenceTests.Concurrent_ticket_saves_preserve_a_single_audit_chain` checks concurrent ticket appends.
  - `PostgreSqlAuditTransactionTests.Operational_audit_append_shares_callers_transaction_with_mutation` checks caller-owned rollback atomicity.
  - `PostgreSqlAuditTransactionTests.Concurrent_operational_audit_appends_preserve_one_ticket_chain` checks concurrent PostgreSQL appends through the operational audit writer.
  - `PostgreSqlOperationalMetadataTests.Status_configuration_crud_audit_chain_authorization_and_concurrency_work` verifies a persisted status-definition chain emitted by the existing repository/writer and checks payload/hash/predecessor/malformed/empty outcomes against detached copies, then confirms database hashes are unchanged.
  - `PostgreSqlOperationalMetadataTests.Priority_defaults_inactive_selection_history_and_audit_chain_work` verifies persisted priority-definition history produced by the repository/writer and exercises the same tamper/read-only checks.
  - `PostgreSqlApiE2ETests.Auditor_reporting_http_api_reads_persisted_records_and_enforces_controls` now verifies the configuration route's valid metadata-only response, no database mutation, denied non-auditor access, and rejection of unsupported stream types.
- Historical-format investigation outcome:
  - `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~Transition_configuration_is_normalized_fail_closed_and_row_versioned"` failed 1/1 (exit code 1) while testing whether `STATUS_TRANSITION` could safely share the current operational-metadata verifier. A persisted row in the local PostgreSQL test database returned `HASH_MISMATCH`; its preceding same-stream row recomputed. The type was excluded from the production allowlist; no alternate canonical form was inferred.
  - Read-only PostgreSQL inspection of `pa_tickets_test` identified the failing record as audit ID `4258`, entity `STATUS_TRANSITION` / `TRIAGE:NEW`, action `STATUS_TRANSITION_UPDATED`, actor `9001`, timestamp `2026-10-08T07:17:19.577434Z`, reason `deny transition`, old value `TRIAGE->NEW:allowed=True:condition=:rowVersion=1`, new value `TRIAGE->NEW:allowed=False:condition=:rowVersion=2`, predecessor `f1837f3d039fcf163e9b96c990432f035593494f058f27e39d7816584068879c`, stored hash `724d88a003857fcc2600344f5f565558961c510c01b9abb56cdc8e358ec16a99`.
  - The preceding same-stream audit ID `4257` is the matching create action (`add transition`, actor `9001`, same entity), has the failing row's predecessor hash, and recomputes under the current operational-metadata shape. The failing row matches the exact `deny transition` payload and sequence in `PostgreSqlOperationalMetadataTests.Transition_configuration_is_normalized_fail_closed_and_row_versioned`; this strongly suggests a persisted test-generated record, but no run log maps the global audit ID to a specific execution. The database is persistent, so original provenance is not proven.
  - Final provenance check inspected `tests/PA.Tickets.Domain.Tests/PostgreSqlOperationalMetadataTests.cs`, `PostgreSqlTestEnvironment.cs`, the retained transition-test `TestResults/*.trx`, `tests/scripts/postgresql-schema.sql`, `postgresql-slice6-schema.sql`, `provision-postgresql-schema.ps1`, and migrations `database/migrations/022_operational_metadata.sql` and `024_workflow_initial_assignment_transition.sql`. The Slice 6 setup does not seed `TRIAGE:NEW` audit rows; the current test writes the matching create/update sequence. The provisioning reset truncates the transition configuration table but leaves `PA_TICKETS_AUDIT` intact, and regular test runs share a persistent database. This makes the test the most plausible source, not an ID-level provenance proof.
  - Retained TRX artifacts do not correlate audit IDs 4257–4258 to a test run or report a hash-mismatch assertion for those rows. Failed transition-test runs in the artifacts show a serialization conflict or an initial `Assert.False` precondition failure; other retained runs passed. The available results do not explain the stored digest. `Docs.zip` contains documentation only, `package-lock.zip` contains only `package-lock.json`, and no source archive/backup or earlier writer source was found. Git history is unavailable. The local provenance investigation is exhausted; do not run another database-mutating reproduction or guess a hash format.
  - The current writer uses the nine-property operational-metadata shape, compact default `System.Text.Json`, UTF-8 SHA-256, lowercase hex, and UTC tick truncation to PostgreSQL microsecond precision. The row alone cannot prove the writer version or distinguish an older precision/canonicalization issue from later data alteration. Git history is unavailable: `git -C G:\WSS\aw\Tickets --no-pager log -5 --oneline` exited 128 with “not a git repository.” No alternate serialization was tested as proof or enabled.
  - The evidence query ran against `PA_TICKETS_AUDIT` inside `BEGIN READ ONLY` / `ROLLBACK`; total row count at inspection was 22,063 across 24 persisted entity types. Audit IDs `4257` and `4258` were queried again and their fields/hashes matched the initial read. No audit records were modified.
  - Writer inventory recommendations and the complete persisted mismatch fields are recorded in `Docs/AUDIT_HASH_DESIGN.md`. Keep each remaining format unsupported until actual writer-produced PostgreSQL rows, timestamp precision, field omission/null semantics, and chain linkage are proven. `WORKFLOW_STEP_INSTANCE` exists in local persisted data but no direct current writer was found; its creating source/version and provenance must be recovered.
  - Closure condition for `STATUS_TRANSITION`: leave it unsupported unless authoritative historical writer source plus row-to-run provenance (or equivalent trusted evidence) explains ID 4258 and persisted-row tests prove the exact format. Do not enable based only on matching test payloads.
- Focused verification to date:
  - Initial reproduction: focused ticket lifecycle test failed before the timestamp correction (`HASH_MISMATCH`, 1 failed; exit code 1).
  - `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditChainVerifierTests"` passed 5/5 (0 failed, 0 skipped; exit code 0).
  - Combined verifier/API/ticket lifecycle focused run after timestamp correction passed 2/2 (0 failed, 0 skipped; exit code 0).
  - `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~Concurrent_ticket_saves_preserve_a_single_audit_chain"` passed 1/1 (0 failed, 0 skipped; exit code 0).
  - Final combined focused run covering the verifier unit tests, auditor API, ticket lifecycle hash verification, and concurrent ticket saves passed 8/8 (0 failed, 0 skipped; exit code 0).
  - `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditChainVerifierTests|FullyQualifiedName~PostgreSqlAuditTransactionTests|FullyQualifiedName~PostgreSqlTicketPersistenceTests|FullyQualifiedName~Auditor_reporting_http_api_reads_persisted_records_and_enforces_controls"` passed 13/13 (0 failed, 0 skipped; exit code 0).
  - `dotnet test PA.Tickets.sln --no-restore --nologo` passed 177/177 (0 failed, 0 skipped; exit code 0).
  - `dotnet build PA.Tickets.sln --no-restore --nologo` succeeded (0 warnings, 0 errors; exit code 0).
- Latest continuation verification:
  - `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditChainVerifierTests|FullyQualifiedName~Status_configuration_crud_audit_chain_authorization_and_concurrency_work|FullyQualifiedName~Priority_defaults_inactive_selection_history_and_audit_chain_work|FullyQualifiedName~Auditor_reporting_http_api_reads_persisted_records_and_enforces_controls|FullyQualifiedName~PostgreSqlAuditTransactionTests|FullyQualifiedName~Concurrent_ticket_saves_preserve_a_single_audit_chain"` passed 14/14 (0 failed, 0 skipped; exit code 0).
  - `dotnet test PA.Tickets.sln --no-restore --nologo` passed 180/180 (0 failed, 0 skipped; exit code 0).
  - `dotnet build PA.Tickets.sln --no-restore --nologo` succeeded (0 warnings, 0 errors; exit code 0).
  - Investigation-only read-only unit check: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditChainVerifierTests"` passed 8/8 (0 failed, 0 skipped; exit code 0). These tests are in-memory verifier tests; they do not establish the historical writer format or validate the persisted PostgreSQL mismatch.
  - Final provenance check made documentation-only edits. No tests or build were run in that pass: a full PostgreSQL suite would write to the shared persistent audit table and was not needed to validate this source/evidence review. The latest recorded full-suite/build results remain the earlier 180/180 passed (0 failed, 0 skipped) and build with 0 warnings/0 errors; they were not rerun for this final check.
- Remaining gaps and security boundary:
  - Production verification covers `TICKET`, `STATUS_DEFINITION`, and `PRIORITY_DEFINITION` only. Other formats lack evidence-backed historical verification; `STATUS_TRANSITION` additionally has an observed hash mismatch of unresolved origin.
  - This read-only evidence pass changed documentation only. PostgreSQL was queried using the configured `PA_TICKETS_TEST_CONNECTION_STRING` with `psql -X --no-password --set ON_ERROR_STOP=1` (host, port, and user were read from the environment at runtime; the password was not printed). The row query was:
    ```sql
    BEGIN READ ONLY;
    SELECT "N_AUDIT_ID", "S_ENTITY_TYPE", "S_ENTITY_ID", "S_ACTION", "N_ACTOR_USER_ID",
           "D_AUDIT_AT", "S_OLD_VALUE", "S_NEW_VALUE", "S_REASON", "S_HASH", "S_PREVIOUS_HASH"
    FROM "PA_TICKETS_AUDIT"
    WHERE "S_ENTITY_TYPE" = 'STATUS_TRANSITION'
      AND "S_ENTITY_ID" = 'TRIAGE:NEW'
      AND "N_AUDIT_ID" BETWEEN 4256 AND 4260
    ORDER BY "N_AUDIT_ID";
    SELECT count(*) AS audit_rows, count(DISTINCT "S_ENTITY_TYPE") AS entity_types
    FROM "PA_TICKETS_AUDIT";
    ROLLBACK;
    ```
    The query was repeated with UTC formatting for IDs 4257–4258; all persisted values and hashes matched. It returned 22,063 rows and 24 entity types. The only test run in this pass was the in-memory verifier unit check recorded above. No database-mutating integration test, full test suite, or build was run as part of the evidence investigation.
  - A privileged database owner/operator can bypass database-local append-only controls. No external trusted anchor exists, so full-chain rewrites and deletion of only the tail cannot be detected. Sequence gaps are not evidence of missing audit rows because IDs are allocated globally and can have legitimate gaps.
  - No schema or migration was changed, and no Oracle connection or script was used. Runtime validation is PostgreSQL-only.

## Workstream G — Final backend remediation audit and local-integration freeze

- Verdict: **READY WITH EXPLICIT LIMITATIONS** for local Angular integration and acceptance testing in the supported scope. This is not production readiness and does not close any A–F partial status.
- Scope: read-only review of current source, tests, API map, and project documentation. No backend behavior, API route, schema, migration, or persisted database row was changed. Git status/diff remains unavailable because `G:\WSS\aw\Tickets` is not a Git repository.

### A–F evidence and disposition

| Workstream | Classification | Evidence and remaining condition |
|---|---|---|
| A — Authorization and resource isolation | Implemented; PostgreSQL-tested; deployment identity integration deferred | `DatabaseAuthorizationService` resolves active users, effective role grants, department/team/client scope, and administrator behavior. Ticket search applies non-admin scope constraints; resource fetch/mutation routes use the ticket access authorizer. PostgreSQL authorization/API/workflow regressions are recorded above. Production OIDC issuer/authority/audience and real organizational identity/scope integration remain deployment gates; Oracle authorization behavior is unverified. |
| B — Scheduled reports | Implemented; retry mechanics tested; terminal recovery incomplete | `PersistentBackgroundWorker`, `ReportScheduleJobHandler`, `ReportScheduleService`, and `DurableBackgroundJobRepository` use persistent schedule/job/execution rows, owner checks, leases, bounded attempts and backoff, stale-lease recovery, and an `EXHAUSTED` terminal state. Handler failures are rethrown for durable retry. The exhausted-job operator replay/recovery procedure and duplicate-side-effect behavior across failure boundaries are not established as a complete operational contract. |
| C — SLA detection and escalation | Implemented and PostgreSQL-regression-tested; policy semantics partial | `SlaDeadlineMonitor` is invoked by the worker, computes due response/resolution deadlines with persisted pause/calendar data, writes deduplicated breach events, and completes SLAs at terminal ticket states. The worker must be enabled. `BreachRules`/`NotificationRules` are not evaluated as arbitrary actions; calendar versions are not immutably retained; DST and Oracle semantics are unverified. |
| D — Ticket creation and recovery | Persistence/idempotency implemented and tested; cross-context orchestration recovery partial | `TicketLifecycleService` validates before persistence; ticket and idempotency result persist atomically in the repository. Tests cover same-key replay, payload conflict, concurrent same-key requests, and recovery after a failed attempt. Workflow/approval/SLA orchestration follows ticket persistence in separate contexts; a failed downstream stage relies on retry with the same key, not an automatic reconciler/sweeper. |
| E — Catalogue and dynamic forms | Partially implemented; explicitly not full UI acceptance | Runtime validates request-type hierarchy/version and typed values before persistence; PostgreSQL and Angular regressions cover those fixes and active/effective option filtering. The Angular create page does not render configured dynamic fields. Existing `GET .../request-types/{id}/fields` returns associations, not field definitions/options; BOOLEAN, MULTISELECT, USER, DEPARTMENT, TEAM, and CLIENT submission serialization is not unambiguously contracted. |
| F — Audit integrity | **PARTIAL; excluded historical formats remain unsupported** | Verifier allowlist is `TICKET`, `STATUS_DEFINITION`, `PRIORITY_DEFINITION`; integrity reads are `AsNoTracking`, report metadata only, and do not repair rows. `STATUS_TRANSITION` row 4258 (`TRIAGE:NEW`) remains an unresolved `HASH_MISMATCH`; row 4257 validates and row provenance is not established. Other persisted writer formats are unsupported pending authoritative canonicalization evidence. Hashing is tamper evidence, not immutable storage; no independently retained anchor detects tail deletion. |

### Security/operational boundaries

- Ticket API controllers require authentication and application permission/resource checks. Ticket search adds non-admin scope predicates; inaccessible ticket detail is hidden as not found. Auditor integrity methods require `AUDITOR_VIEW`; ticket integrity checks the actor's authorized ticket IDs, while configuration verification is limited to the explicitly supported global metadata formats. The integrity result does not return audit payload values.
- Verification remains read-only and unsupported streams fail closed. No format was enabled based on inference, and no hashes/history were rewritten.
- Idempotency is operation-specific, not a blanket guarantee: ticket creation uses an optional idempotency header at the controller but the Angular create client supplies it; durable work-item completion/assignment and other operations have their own required keys. Do not generalize ticket-create guarantees to all APIs.
- Durable jobs persist leases, attempt counts, failure classification, retry delay, last error, and exhausted status. Schedules and SLA polling require `Jobs:Enabled`; communication delivery separately requires its worker/provider configuration. Exhausted jobs require operational inspection/recovery; no automatic guarantee of eventual completion is claimed.
- Production requires valid OIDC/JWT configuration, explicit HTTPS CORS origins, a configured trusted-proxy allowlist if forwarded headers are enabled, and a real malware scanner. `DeterministicContentScanner` is limited to Development/Testing; the default `UnconfiguredContentScanner` fails closed when scanning is unavailable. Production communication-provider delivery also requires a registered/configured adapter. None of these are established by local PostgreSQL integration.
- No API endpoint or Angular fake-success behavior was added in this review. Oracle was not accessed and no Oracle script was run.

### Angular-usable contracts and exclusions

The current Angular `TicketsApiService` uses real versioned endpoints:

- `GET /api/v1/tickets` with paged/search filters; `GET /api/v1/tickets/{id}`; `POST /api/v1/tickets` with the current create DTO and `Idempotency-Key`.
- `GET /api/v1/admin/catalogue/services`, `/categories`, `/sub-categories`, and `/request-types` with optional hierarchy query parameters. These routes serve the current catalogue selectors; they do not complete the dynamic-field contract.
- `POST /api/v1/work-items/{publicId}/complete` with `Idempotency-Key`, currently used for completion from the ticket UI.

The backend additionally exposes ticket lifecycle routes for classify/route/approval/approve, execute/reassign/wait/resume/complete, evidence, resolve/reopen/cancel/close, and relationships; those routes are not thereby a complete Angular workflow UI contract. Other real but backend-only route groups in `Docs/API_V1_SCREEN_CONTRACT_MAP.md` include attachments, approvals, workflow execution, reports/exports/schedules, SLA, administration, catalogue administration, and auditor reports.

Unsupported or incomplete Angular actions include dynamic field definition/options rendering and unambiguous serialization for all configured types; complete ticket-detail lifecycle/work-item actions; attachment upload/version/download/review; approval and workflow authoring/runtime; report/export/schedule UI (PDF export is also unsupported by the API); SLA administration/runtime; auditor reporting; and general system administration. No missing read route or response shape is to be fabricated to bridge these gaps.

### Findings by severity

- Critical: none established by this source/test review.
- High: dynamic ticket fields cannot be rendered end-to-end (E remains partial); historical audit formats including the observed `STATUS_TRANSITION` mismatch remain unverifiable (F partial); production identity, malware scanning, proxy/CORS, provider, and Oracle deployment requirements are not satisfied by local integration evidence.
- Medium: downstream ticket orchestration has caller-retry rather than automatic reconciliation; exhausted durable jobs lack a documented operational replay procedure; SLA policy rule JSON and immutable calendar-version/DST semantics remain unresolved.
- Low: `tests/POSTGRESQL_INTEGRATION.md` contained a stale statement that the SLA runtime did not automatically detect deadlines. It has been corrected to match the current worker/monitor path and retain its actual worker/policy limitations.

### Fresh Workstream G validation

- Read-only unit command: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditChainVerifierTests"` — exit code 0; 8 passed, 0 failed, 0 skipped. These are in-memory verifier tests, not a PostgreSQL integration run.
- Build command: `dotnet build PA.Tickets.sln --no-restore --nologo` — exit code 0; build succeeded with 0 warnings and 0 errors.
- Full suite command `dotnet test PA.Tickets.sln --no-restore --nologo` was **not run**. The configured integration target is the persistent shared `pa_tickets_test` database; the current test role cannot create a database, tests issue deletes, and the schema provisioner truncates multiple persistent tables. Running the full suite here would risk shared state. No isolated test database or safe disposable resource was established. Historical 180/180 and build results recorded above are not fresh Workstream G results.
- Angular tests/build were not rerun in this backend-only freeze pass. Prior Angular results remain historical, not fresh.

### Freeze conditions and next approved phase

Local Angular integration may proceed only for implemented, real contracts and explicitly scoped acceptance cases. Exclude dynamic-field-configured create flows and backend-only screens/actions from acceptance until their contracts/UI exist. The next approved activity is the local Angular integration/acceptance phase against the documented existing API, with a separately provisioned disposable PostgreSQL test database before any full integration suite. Do not proceed to Workstream H or treat this as production release approval. Production freeze requires the deployment conditions above, Oracle 19c validation, explicit exhausted-job recovery operations, and resolution or formal risk acceptance of the partial E/F limitations.
