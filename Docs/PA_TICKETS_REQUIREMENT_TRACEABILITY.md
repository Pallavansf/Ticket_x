# PA_TICKETS Requirement-to-Code Traceability

**Branch:** `workstream-i-lifecycle-authority`  
**Evidence boundary:** source inspection and database-independent tests only

Statuses describe the evidence currently available: `PASS`, `PARTIAL`,
`BLOCKED`, `NOT STARTED`, or `NOT VERIFIED`. A source implementation or fake
test does not prove provider, browser, or release behavior.

| Requirement / expected behavior | Implementation evidence | Existing test evidence | Status | Gap, next evidence, and restrictions | Safe source improvement now? |
|---|---|---|---|---|---|
| Angular routes, screens, forms, validation, API integration | `client/src/app/app.routes.ts`; `client/src/app/features/**`; auth interceptor/services and ticket pages | Angular specs under `client/src/app/**/*.spec.ts` | `PARTIAL` | Full browser-to-API journeys, validation matrix, and persisted identities remain unverified. Provider/browser gates required. | Yes, only targeted UI correctness changes with focused specs. |
| Authentication and USER, ADMIN, SUPER_ADMIN, ADMIN_HEAD, SPECTATOR roles | `src/PA.Tickets.Api/Controllers/AuthenticationController.cs`; `src/PA.Tickets.Api/ApiSecurityConfiguration.cs`; `src/PA.Tickets.Application/Foundations/RoleAuthorizationPolicy.cs`; `AdminHeadDelegationPolicy.cs` | Hosted RBAC role/denial tests and auth service specs | `PARTIAL` | Persisted identity, password/session behavior, role claims, and cross-provider semantics require provider/API evidence. | Yes, source policy defects only. |
| Permissions, effective grants, scope, delegation, fail-closed authorization | `DatabaseAuthorizationFactsReader.cs`; `DatabaseDelegationFactsReader.cs`; `EffectiveGrantAuthorization.cs`; `OrganizationScopeAuthorization.cs`; `DatabaseAuthorizationService.cs` | Hosted denial/positive and scope/delegation tests | `PARTIAL` | Historical dates, revocation races, client lineage, and SQL translation are unverified; owner decisions D04-D08 remain pending. | Yes, pure policy corrections with tests. |
| REQUEST, INCIDENT, PROBLEM, TASK, CHANGE lifecycle/workflow | `src/PA.Tickets.Domain/Tickets/Ticket.cs`; lifecycle services; `WorkflowModel.cs`; ticket/workflow controllers; `Docs/PA_TICKETS_LIFECYCLE_TRANSITION_MATRIX.md` | Domain, hosted, and workflow unit tests; Stage 2 cancellation and transition-matrix regressions | `PARTIAL` | Workflow cancellation and specialized terminal-transition guards are covered at source level; provider persistence, configured transition-row behavior, and full lifecycle API/browser acceptance remain incomplete. | Yes, source-level lifecycle gaps after inventory. |
| Durable transitions, transactions, audit, concurrency, recovery | `TicketLifecycleService.cs`; `AuditChainCoordinator`; `AuditTransaction.cs`; execution repositories and retry services | Unit/fake transaction and audit tests | `BLOCKED` | Provider atomicity, row-version races, audit failure, uncertain commit, and recovery require owner/provider validation. | Limited to pure invariants; no schema/migration change. |
| Attachments, ownership, storage isolation, download authorization | `AttachmentService.cs`; `EfAttachmentDownloadReader.cs`; `LocalFileStorage.cs`; attachment controller | T57/T58 hosted seam and storage safety tests | `PARTIAL` | Provider relationship integrity, storage ACL/TOCTOU and persisted replacement behavior remain unverified. | Yes, source seam or error handling only. |
| SLA, escalation, communications, notifications | SLA services/resolvers, `NotificationOrchestrator.cs`, communication repositories/controllers | Focused policy/service tests and guarded provider suites | `PARTIAL` | Provider scheduling, retry, persistence and worker evidence remain unverified. | Yes, isolated source behavior. |
| Reports, exports, schedules | `ReportingQueryService.cs`; `ReportExportService.cs`; `ReportExportFormatter.cs`; schedule services/jobs | Formatter and authorization tests; Sprint 1-7 evidence | `PARTIAL` | Export download was previously a concrete source defect; this task now reads authorized completed artifacts and verifies hashes. Persisted worker/storage/provider behavior remains unverified. | Completed here: download path + regression test. |
| API errors, validation, logging, operational safeguards | API controllers, exception middleware, request validation, target guards and preflight | Controller/service and guard tests | `PARTIAL` | Complete error contract, observability, secret-redaction review and production telemetry remain unverified. | Yes, focused source corrections. |
| Persistence mappings, schema contracts, migrations, provider tests | `TicketsDbContext.cs`, `ExecutionDbContext.cs`, schema scripts, migrations 028-038, Sprint 2-7 adapters | Source reconciliation and synthetic catalog/preflight tests; [authorized E2E bring-up](PA_TICKETS_AUTHORIZED_E2E_BRINGUP_RESULT.md) records one repository persistence proof passed and lifecycle authorization blocked by missing `S_MODULE_CODE` | `BLOCKED` | Baseline provenance/adoption, sequence state, migration history, RBAC metadata/owner decisions, and full lifecycle provider validation remain absent. Migration 026 remains separate and unapproved. | Source-only reconciliation may continue; no further provider tests until the RBAC contract and schema operation are separately authorized. |
| Angular-to-API-to-PostgreSQL E2E and release readiness | Hosted API tests, guarded PostgreSQL suites, Angular specs, roadmap stages 5-7 | One isolated repository persistence test passed against the authorized target; lifecycle/API path failed closed at `DatabaseAuthorizationFactsReader`; browser E2E not established | `BLOCKED` | Requires approved RBAC metadata and persisted authorization facts, provider lifecycle fixtures, browser matrix, recovery evidence, release approvals, and Oracle remains deferred. | No full E2E claim is supported. |

## Source-only improvement delivered

`ReportExportService.DownloadAsync` now preserves its existing authorization and
ownership checks, rejects expired/incomplete exports, reads the authorized
storage object, verifies its SHA-256 against the persisted artifact hash, and
returns a read-only stream with the report filename. The regression test uses
SQLite only as a database-independent in-memory test provider and a synthetic
storage implementation; it does not connect to PostgreSQL or execute
application schema operations.

## Evidence and restrictions

Sprints 1-7, T60, T62A, and T62B remain historical evidence and were not
rerun. Their limits remain: no owner approvals are inferred, no migration
history is inferred from object shape, migration 026 is separate, and Oracle is
deferred. Protected targets, `tests/scripts/provision-postgresql-schema.ps1`,
`.env.test.local`, `.vscode/mcp.json`, and migration files were not modified.

