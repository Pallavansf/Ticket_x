# PA_TICKETS E2E Master Plan

Status baseline: **8 PASS / 3 PARTIAL / 13 BLOCKED**. This document is a reviewable roadmap. It does not authorize database access, migrations, provisioning, fixtures, cleanup, or E2E execution.

## Evidence boundary

Source behavior is confirmed only where code and database-independent tests cover it. PostgreSQL persistence, migration compatibility, hosted HTTP behavior, and Angular workflows against a live API remain unverified. The prior `pa_tickets_h_verify` incident database is excluded from all future testing and cleanup unless separately approved.

## Five-Role RBAC + Module Controls - IMPLEMENTATION MILESTONE COMPLETE

The implementation milestone is recorded complete for the five built-in roles `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and `SPECTATOR`. Completed scope includes the RBAC foundation, custom-role management, role-membership administration, module/action permission controls, centralized authorization, and the Angular administration interface across `APPROVALS`, `AUDIT`, `CATALOGUE`, `CLIENTS`, `COMMUNICATIONS`, `CONFIGURATION`, `NOTIFICATIONS`, `SECURITY`, `SLA`, `TICKETS`, `USERS`, and `WORKFLOWS`.

Remaining PostgreSQL/Oracle migration reconciliation, provider-backed persistence tests, and full API/Angular end-to-end acceptance are release gates for the completed implementation; they are not unfinished RBAC implementation. The acceptance baseline remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

## Feature inventory

| Area | Status | Evidence and gap |
|---|---|---|
| Five role codes | IMPLEMENTED / persistence UNVERIFIED | `RoleAuthorizationPolicy.cs`; `037_builtin_role_provisioning.sql` is approval-gated and inserts no grants |
| Custom roles | IMPLEMENTED / persistence UNVERIFIED | `AdministrationService`, `AdministrationController`, role administration page |
| Permission catalogue | IMPLEMENTED / schema UNVERIFIED | `AdministrationPermissionRow`, permission APIs, migration 034; existing catalog requires metadata reconciliation |
| Role-permission management | IMPLEMENTED / effective-date compatibility NEEDS CHANGES | Service supports grants/revocation; EF row is non-nullable while historical rows lack dates |
| User creation/profile/authentication | IMPLEMENTED / persistence UNVERIFIED | authentication and administration services; hosted tests pending |
| Membership assignment/revocation | IMPLEMENTED / persistence UNVERIFIED | `AssignRoleAsync`, membership endpoints; no DB evidence |
| Explicit scopes | IMPLEMENTED in source / schema UNVERIFIED | `DatabaseAuthorizationService`, `OrganizationScopeAuthorization`; migration 035 adds no rows |
| Organization-wide Super Admin approval | BLOCKED | No scope-proposal endpoint; direct scope assignment is rejected by `AssignRoleScopeAsync` |
| Admin Head delegation | IMPLEMENTED in source / persistence UNVERIFIED | service and migration 035; role/target validity is service-enforced |
| Permission proposal approval | IMPLEMENTED in source / persistence UNVERIFIED | `AdministrationService` uses serializable transaction, row version, expiry, payload hash, independent approver |
| Audit actor compatibility | BLOCKED | migration 038 intentionally aborts; historical actor contract unresolved |
| Ticket lifecycle | IMPLEMENTED in source / DB E2E BLOCKED | `TicketLifecycleService`, repositories, transition/authority tests; hosted persistence pending |
| SLA/policy/workflow | PARTIAL / DB E2E BLOCKED | services and workers exist; policy precedence, calendar and recovery evidence pending |
| Reports/KPIs/search/exports | IMPLEMENTED in source / hosted authorization UNVERIFIED | reporting services and explicit scope checks; provider and browser evidence pending |
| Attachments/evidence | PARTIAL | source paths exist; authorization, storage and download E2E pending |
| Notifications/integrations | PARTIAL | queues/services exist; provider delivery, retry and duplicate evidence pending |
| Angular administration | IMPLEMENTED in source / hosted workflow UNVERIFIED | `client/src/app/features/admin/role-administration.page.*`; browser/API integration pending |
| Security regression coverage | PARTIAL | database-independent scope/guard tests pass; cross-role hosted and concurrency tests pending |

## Migration compatibility review

| Migration | Result | Static finding |
|---|---|---|
| 033 ledger | READY FOR HUMAN REVIEW | Additive, idempotent ledger/index DDL; does not infer applied history. Baseline must be approved externally and ledger rows must be written only after fingerprint verification. |
| 034 permission metadata | READY FOR HUMAN REVIEW | Source now uses nullable `ValidFrom`, explicit `EffectiveStatus`, and non-null fail-closed `Active`; authorization requires ACTIVE status plus known dates. PostgreSQL type/materialization and existing-data assertions remain unverified. |
| 035 scopes/delegation | NEEDS CHANGES | Tables and names match EF rows and source queries. No client FK is correctly deferred. The database does not enforce that delegation users hold the expected roles; the service currently enforces that rule. Add a reviewed constraint or retain the service-only contract explicitly. |
| 036 proposals | READY FOR HUMAN REVIEW | Corrected to the implemented `ROLE_PERMISSION_GRANT` contract with non-null target permission, required by `RbacProposalRow`. Serializable approval, self-approval prevention, expiry, row-version checks, payload hash, and atomic grant application are service behavior, not fully database constraints. Scope proposals remain blocked. |
| 037 built-in roles | NEEDS CHANGES | Role codes and duplicate-safe ID preservation align with source. It is explicitly gated and inserts no grants. Before execution, verify `N_CREATED_BY=0` is valid on the target and add a preflight for exact role column types/constraints. |
| 038 audit actors | BLOCKED BY DECISION | Deliberately aborts. No historical actor, nullable FK, actor type, or writer/reader change is approved. |

The superseded 030–032 drafts must not be replayed. They assume absent relations/columns and perform unsafe historical-date, scope, or grant backfills.

## Decisions and approval gates

1. **Client `872002`:** five SLA rows reference it, but repository evidence does not establish its authoritative identity or semantics. Required: source record and owner approval. Blocks client FKs and client-scoped E2E.
2. **Forty-four unknown role-permission dates:** no dates or provenance exist. Required: per-row evidence or an approved inactive legacy policy. Unknown rows must not authorize. Blocks effective-grant migration and permission persistence E2E.
3. **Audit actors:** current legacy rows resolve to users, but future unknown/system actor semantics are unresolved. Required: actor-type/nullability/FK/writer-reader contract. Blocks migration 038 and actor E2E.
4. **Migration baseline:** no deployed ledger was found. Required: read-only catalog fingerprint, release record, hashes and stable-ID manifests. Blocks migration execution.
5. **Organization scope approval:** explicit `ORGANIZATION` + null organization ID is implemented in source, but no independent scope-proposal API exists. Required: application workflow and two-person approval contract. Blocks organization-wide grant E2E.

## Database phase runbook (future, separately approved)

1. Name the exact target host, port, database and user; do not disclose credentials.
2. Obtain a target-consistent backup/snapshot.
3. Capture relation, column, type, nullability, constraint, index, sequence and migration-history fingerprints.
4. Export stable IDs and pair manifests for roles, permissions, grants, memberships, user-client rows, SLA references, tickets and audit rows.
5. Abort on baseline ambiguity, duplicate canonical codes, orphan references, invalid dates, unresolved client semantics, or actor incompatibility.
6. Apply only approved files in order `033 → 034 → 035 → 036 → 037`; never include 038.
7. Run schema and data assertions after each transaction.
8. Confirm no historical ID/value changed, unknown grants cannot authorize, no organization scope was inserted, and no role seed added grants.
9. Record file hashes, target fingerprint, applied status and evidence.
10. If new rows exist, use forward repair or approved backup restoration; do not delete legacy rows to simulate rollback.

## E2E test matrix after migration approval

Every test must name identity, endpoint, expected HTTP status, expected database state, expected audit event, negative case, boundary case and cleanup authorization.

### RBAC and user administration

- Create/edit/deactivate users; temporary password and forced rotation.
- Assign, revoke and reassign all five roles; repeated initialization is duplicate-safe.
- Create, edit, retire and remove custom roles without changing built-ins.
- Retrieve module/action metadata; grant and revoke permissions; verify effective dates and revocation on subsequent requests.
- Verify department, team, client and record scopes.
- Verify Super Admin organization access requires explicit permission plus approved effective scope.
- Verify Admin Head delegation, independent approval, audit, self-approval rejection, expiry, replay and concurrency.
- Verify Spectator reads succeed only within scope and all mutations are denied.
- Verify denied requests leave no mutation.

### Ticket lifecycle

Create and validate tickets, enforce unique IDs, assign/reassign users and teams, add comments/attachments/evidence, change priority/status, apply valid transitions, reject invalid transitions, resolve/close/reopen where supported, and verify events/audit and idempotency. Any unspecified reopening or transition rule is a product decision.

### SLA, policy, workflow and approvals

Verify applicability, response/resolution targets, calendars where implemented, start/pause/resume/completion/breach, reassignment and priority effects, escalation and notification retries, policy precedence, invalid workflow transitions, approval/rejection/expiry, initiator/approver separation, audit integrity, worker recovery and duplicate prevention.

### Reports, integrations and UI

Verify search/filter/pagination, KPIs, reports, exports, scheduled workers, attachment/evidence authorization, email/WhatsApp/push/in-app delivery, retries and redaction. Browser journeys cover login/rotation, user and role administration, ticket work, SLA/policy, approvals, reports/dashboards, downloads, forbidden states, expired sessions, responsive layout, accessibility, loading/error states and navigation.

### Security

Test malformed/missing/expired tokens, direct API bypass of UI guards, cross-user/team/department/client access, escalation attempts, Spectator mutation, Super Admin scope, delegation, independent approval, export/report/attachment access, actor spoofing, revocation, replay, sensitive logs/errors and no unintended mutation. Destructive concurrency, fault injection and penetration testing require separate approval.

## Dependency-aware roadmap

| Phase | Entry | Work and evidence | Exit |
|---|---|---|---|
| 1. Inventory | Source tree available | Maintain feature/file matrix and current 29/29 + API build evidence | All gaps classified |
| 2. Migration review | Drafts present | Resolve SQL/EF/service compatibility; update review files | Human-reviewable migrations |
| 3. Design decisions | Owners available | Resolve client, dates, actors, baseline, scope approval | Written decisions and approval records |
| 4. Disposable DB approval | Target and backup named | Approve exact host/port/database/user, files, provisioning, fixtures and cleanup separately | Written operation authorization |
| 5. Schema execution | Approved backup and preflight | Apply 033–037 with assertions and fingerprints | Preservation and schema evidence |
| 6. Backend RBAC E2E | Schema and app release compatible | Run identity, roles, permissions, scopes, delegation and approval tests | Hosted authorization evidence |
| 7. Ticket E2E | RBAC evidence | Run lifecycle, assignment and authorization journeys | Ticket evidence and audit proof |
| 8. SLA/workflow | Ticket lifecycle stable | Run policy, worker, approval and recovery matrix | SLA/workflow evidence |
| 9. Reporting/integrations | Scope and audit stable | Run reports, exports, attachments and delivery tests | Integration evidence |
| 10. Angular E2E | Hosted API stable | Run browser journeys and accessibility/security checks | UI functional evidence |
| 11. UI polish | Functional defects closed | Address only essential usability/accessibility issues first | Reviewable UX changes |
| 12. Security regressions | All journeys available | Cross-role, revocation, replay and boundary tests | Security sign-off |
| 13. Full regression/handoff | All evidence collected | Run approved regression suite and archive fingerprints/results | Complete handoff and updated acceptance matrix |

Parallelizable work before database approval: source-level Angular/API test inspection, transition-rule specification, migration documentation, and test-case authoring. Hard blockers remain database baseline, unresolved historical data contracts, organization-scope approval workflow, and a dedicated disposable target.

## Current safe verification

The latest established evidence is 29 database-independent tests passed and an API build with zero warnings/errors. This review added no database access and did not execute migrations, provisioning, fixtures, cleanup, PostgreSQL integration tests, or the full .NET test project.

Effective-date contract update (2026-10-16): role-permission starts are nullable with explicit UNKNOWN/ACTIVE/EXPIRED/REVOKED status; only ACTIVE rows with a known inclusive start and exclusive end can authorize. Focused provider-independent tests now cover these cases (52 passed); PostgreSQL translation/materialization remains unverified.


Authorization-path audit: DatabaseAuthorizationService, LocalAuthenticationService, AdministrationService snapshots/grants, and role-permission reads now share ACTIVE + known-start + inclusive-start/exclusive-end semantics. PostgreSQL translation/materialization and Oracle schema compatibility remain unverified.

Final static readiness review (2026-10-16): runtime source has PostgreSQL/Oracle provider selection, but checked-in role-permission schemas lack N_ACTIVE, S_EFFECTIVE_STATUS, D_VALID_FROM, and D_VALID_TO on both baselines. PostgreSQL 034 is not Oracle parity. Oracle DDL ownership and migration baseline remain blockers; acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

### API authorization coverage phase — 2026-10-16

Static inventory found 232 routes across 26 controllers. All business controllers are server-authorized; auth login and system status remain intentionally public. The complete grouped inventory and expected 401/403/2xx outcomes are in `Docs/PA_TICKETS_API_ENDPOINT_INVENTORY.md`. Confirmed source protections include effective-grant fail-closed checks, SPECTATOR mutation denial, explicit organization scope, password rotation gating, ticket/assignment/execution checks, report/export authorization, and approval/idempotency checks. No route was promoted to provider-backed E2E evidence.

Angular inspection found existing mocked-HTTP specs for login and forced rotation, auth error handling, user and role administration, permission proposals, scope/delegation display, and API error states. These tests verify client behavior only; they do not establish backend authorization or persistence. No browser framework was added.

Focused provider-independent evidence remains 52/52 tests passed and API build 0 warnings/0 errors. No database-backed test or full test project was run. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

### Intended module mapping audit — 2026-10-16

Added `Docs/PA_TICKETS_MODULE_MAPPING_AUDIT.md`. The audit reconciles the intended catalogue recoverable from the unexecuted 031 drafts with actual server literals, controller routes, Angular navigation, role ceilings, and scope enforcement. It identifies confirmed matches for ticket, user, and core security flows, and owner-confirmation gaps for reporting/KPI, attachments/evidence, change/problem, configuration metadata, and communication/notification boundaries. No correction was implemented; acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

### Phase 1 module mapping validation — 2026-10-16

Operational metadata was validated against source and migration 031. Status, transition, and priority definitions are ticket operational configuration: `TICKET_READ` protects reads and `TICKET_ADMIN` protects mutations. `CONFIG_ADMIN` is not established for these routes. No application change was made; the earlier mismatch finding was corrected as an audit interpretation. Other module decisions remain owner-confirmation blockers.

### Reporting/KPI/export authorization review — 2026-10-16

Static review completed in `Docs/PA_TICKETS_REPORTING_AUTHORIZATION_MAPPING_REVIEW.md`. Existing requirements support `TICKET_READ` for report/KPI/dashboard queries and export creation with ticket resource filtering. No reporting-specific or general export permission is approved. Export artifact rechecks, schedule management permissions, sensitive export audit/approval, and Angular journeys remain owner-confirmation blockers.

### Final export lifecycle decision proposal — 2026-10-16

The export lifecycle decision matrix is in `Docs/PA_TICKETS_EXPORT_LIFECYCLE_AUTHORIZATION_DECISIONS.md`. It recommends current permission/scope checks for status/download without inventing a distinct permission, separates expiry blocking from cleanup, and keeps retention and sensitive-export approval unresolved pending owner sign-off.

### Export lifecycle implementation update — 2026-10-16

Source implementation now applies current report-permission checks to export status/download and fails closed for downloads when artifact-level authorization cannot be proven. Expiry observation no longer performs ordinary GET cleanup writes. Audit events and asynchronous cleanup remain unresolved persistence/operations work; no new permission or grant was added.

### Consolidated export lifecycle implementation plan (2026-10-16)

`Docs/PA_TICKETS_EXPORT_LIFECYCLE_IMPLEMENTATION_PLAN.md` consolidates historical authorization, audit, expiry, cleanup, dependency, and release-gate decisions. Downloads remain fail-closed; audit/cleanup implementation, provider validation, and full export E2E remain blocked pending owner approvals. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

### PostgreSQL schema reconciliation audit - 2026-10-16

Source-only reconciliation confirms that `ExecutionDbContext` maps report exports, schedules, and schedule executions to `PA_TICKETS_REPORT_EXPORT`, `PA_TICKETS_REPORT_SCHEDULE`, and `PA_TICKETS_REPORT_SCHEDULE_EXECUTION`; the checked-in reporting DDL is `database/migrations/026_phase8_6_reporting.sql`. No PostgreSQL-specific copy of migration 026 exists, and the PostgreSQL provisioning script invokes the root migration directly. No checked-in PostgreSQL DDL currently defines ticket-aging evidence state or contributor rows. The effective-grant source contract also requires columns introduced by PostgreSQL migration 034, while the original schema baseline lacks them.

The provider path is selectable in `DependencyInjection.AddInfrastructure`: PostgreSQL uses Npgsql and Oracle uses Oracle EF Core 19c compatibility. No repository migration runner was found; migration history is represented only by the review-draft ledger in migration 033, which does not infer or record applied history by itself. Existing reporting objects and effective-grant migrations therefore require target catalog fingerprints before execution.

Required ordering remains: establish the authoritative PostgreSQL baseline and ledger adoption procedure; reconcile the reporting 026 objects; apply only approved RBAC migrations 033 through 037 in order with preservation assertions; keep 038 blocked; then design and approve a separate ticket-aging evidence migration. Existing exports, users, roles, memberships, grants, tickets, and audit rows require pre/post preservation checks. No provider-backed or E2E status changed; acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
### PostgreSQL migration-plan continuation - 2026-10-21

The existing reconciliation record now contains the current source-grounded dependency order and reporting/ticket-aging drift inventory in `Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`. No migration is approved or applied; the next prerequisite is an explicitly approved read-only catalog fingerprint for a dedicated disposable PostgreSQL target. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
### Definitive PostgreSQL schema-build plan - 2026-10-21

The current repair record now defines the proposed source-only order: approved PostgreSQL baseline adapter, reporting migration 026, PostgreSQL 028 and 029, then ledger/RBAC migrations 033 -> 034 -> 035 -> 036 -> 037. Drafts 030-032 are superseded, root 027 lacks a PostgreSQL counterpart and requires separate review, 038 remains blocked, and ticket-aging evidence remains a separately gated future migration. The reported `pa_tickets_e2e_verify` target is not independently verified here. No database operation was performed; acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
### Foundational PostgreSQL dependency review - 2026-10-21

The final source-only review found that the checked-in foundational adapters are data-bearing, not pure schema stages. `postgresql-schema.sql` and `postgresql-slice1-schema.sql` include inserts, while slice 1 overlaps migrations 028/029. The first separately approvable stage therefore requires a DDL/data split and explicit ownership of overlapping columns before any build operation. The reported `pa_tickets_e2e_verify` emptiness remains unverified by Codex. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.