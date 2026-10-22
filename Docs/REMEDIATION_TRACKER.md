# Remediation tracker

## Purpose

This tracker records the audit findings as hypotheses, verifies each one against source and tests, and distinguishes between confirmed defects, partially confirmed defects, and findings not reproduced in the current repository state.

## Summary

| Finding ID | Severity | Area | Status | Evidence |
|---|---|---|---|---|
| RBAC-MILESTONE | Project milestone | Five-role RBAC and module controls | IMPLEMENTATION MILESTONE COMPLETE | USER, ADMIN, SUPER_ADMIN, ADMIN_HEAD, and SPECTATOR source controls, custom-role and membership administration, module/action controls, centralized authorization, and Angular administration are recorded as implemented. PostgreSQL/Oracle migration reconciliation, provider testing, and full API/Angular E2E remain separate gates. |
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
| F1 | High | Audit-chain verification and integrity | PARTIAL; supported-format verification passes, but historical formats and provenance remain unresolved | Focused audit tests pass on the verified disposable PostgreSQL target. Git history starts after the recorded `STATUS_TRANSITION` row 4258 timestamp; that row is absent from the current disposable database, so its prior recorded mismatch cannot be re-examined there. Unsupported formats and the lack of an independently retained anchor remain open |
| G1 | High | Local Angular integration and acceptance | PARTIAL; scoped username/password, bootstrap, throttling, and profile-hierarchy implementation/tests exist, but current-session browser acceptance, role-governance policy, and email-verification policy remain open | Added an interactive one-shot PostgreSQL administrator utility, forced first-login password rotation, 5/minute per-IP login throttling, generic auth-error/rotation regressions, and backend self-or-explicit-subordinate profile authorization. Generic role administration blocks Super Admin grants/account role changes and self-role mutations. Email verification has no approved delivery/policy contract; live browser work-item completion, operator bootstrap, client selection, and broader lifecycle UI remain unverified |
| H1-H4 | High | Workstream H contract audit and documentation | CLOSED; verified on disposable PostgreSQL database | Public work-item ID is mapped distinctly from the display number; API rejects missing/blank idempotency keys before service invocation; PostgreSQL API E2E, idempotency, and H regression suites all passed on the disposable `pa_tickets_h_verify` database; the EF Core mapping defect was fixed in `TicketsDbContext` and validated by the focused test runs |
| I1 | High | Lifecycle and workflow authority | CLOSED for scoped backend remediation; full domain test project, broader I1/H batch, and solution build pass | Designated approver is persisted and enforced; stale writes conflict; communication fixture corrected; lifecycle concurrency, workflow, communication, API, persistence, and H regressions pass; Oracle compatibility and production readiness are not claimed |
| RBAC-1 | High | Configurable RBAC migration, scope history, and audit | PARTIAL; explicit organization-scope source path implemented; migration/provider validation remains blocked | Super Admin role-code resource bypass removed; organization-wide ticket/search/report access requires active permission plus active effective ORGANIZATION scope with null organization ID (single-organization sentinel). No scope is provisioned automatically; scope grant remains blocked pending independent approval. Read-only `pa_tickets_h_verify` reconciliation found client ID 872002 on five SLA rows, no ticket/user-client IDs, 44 role-permission rows without dates, 21 user-role rows with known dates, 2967 audit rows with non-null resolvable actors and no actor FK, absent client/scope/delegation/proposal relations, and no migration ledger. No migration executed; authenticated API/provider acceptance remains open. |
| RBAC-2 | High | Atomic permission administration and route parity | PARTIAL; atomic endpoint/editor integration implemented; complete route-to-permission parity remains unverified | Batch endpoint compares expected effective grant IDs, validates all changes, and saves grants/revocations with audit in a serializable transaction. Earlier isolated-output .NET policy suite passed 19/19, Angular component tests 8/8, and Angular production build passed. No database-backed atomicity/concurrency test was run. Current route inventory is 232; no definitive route-to-permission/action/scope parity test exists. |
| RBAC-3 | Critical | Independent approval for eligible sensitive permission grants | PARTIAL; constrained workflow remains unverified with a real provider or authenticated actor | Serialized payload covers proposal/envelope fields, target role/permission metadata, and expected permission snapshot; approval rebuilds and compares it with current target rows. This is application-level consistency, not tamper evidence against a database writer able to update both payload and SHA-256 digest. Direct/system/high-impact grant gates remain server-side. Decision handlers share a decision-time expiry guard; list-triggered expiry records the authenticated caller who triggered lifecycle processing, with an explicit no-human-decision reason. The focused database-independent suite passed 50/50; its 51-candidate policy test is not service/provider behavior. No DB/API/migration/browser test was run. New SUPER_ADMIN grants, privileged memberships, role scopes, and delegation changes remain blocked. |
| RBAC-4 | Critical | Independent approval workflow provider validation and recovery | BLOCKED pending approved provider tests and eligible independent actors | The corrected 24-scenario evidence table records 8 PASS (narrow source/unit/UI claims), 3 PARTIAL, and 13 BLOCKED; no scenario or route is end-to-end PASS. The read-only catalog preflight confirms proposal schema is absent and exposes an unexecuted 031 incompatibility. Remaining: verify authority revocation and proposal read isolation under provider transactions; concurrent approve/create/cancel races; uniqueness/concurrency behavior; audit/grant/status rollback and ambiguous commit recovery; migration repair/rehearsal; persistence/audit behavior for >50 expired proposals; list-scale/read cost and audit-user FK behavior; and availability of a second eligible Super Admin (do not expose identity). No database writes or account changes are authorized. |

## Findings register

### Hosted authentication and durable work-item replay authorization — 2026-10-15

- Added a hosted Kestrel test around the actual `AuthenticationController`, ASP.NET Core endpoint
  routing/authorization, login rate-limit endpoint metadata, and
  `PasswordRotationRequiredMiddleware`. Its authentication service is a stub; it performs no
  database or application-state writes. It proves anonymous login reaches the controller,
  anonymous password change receives HTTP 401, and authenticated password-change requests with
  the required-rotation claim reach the controller. It also proves the authenticated subject ID,
  rather than request data, is passed to the password service; correct current password succeeds
  (204) and incorrect current password fails (401). The earlier pure metadata test remains as a
  narrower regression. This is hosted HTTP coverage of a test-only pipeline, not full Program.cs
  or database-backed API acceptance.
- Strengthened denied legacy ticket execution coverage to assert assigned user, last executor,
  status, save count, and event collection remain unchanged on authorization denial.
- Durable work-item execution review found the idempotency replay lookup preceded the explicit
  `AssignedToUserId == actor` guard. A caller with ticket update permission could therefore receive
  `Already processed` for a key belonging to a completed work item without satisfying the
  operation-specific current-assignee check. The explicit assignee check is now factored into
  `EnsureAssignedActor` and applied before replay evaluation. Deterministic tests cover unassigned,
  mismatched, and assigned actor cases. This is pure guard coverage; a database-backed replay
  regression was not run because database connections are prohibited in this pass.
- Workflow instance creation and activation were traced: both require current ticket-update
  permission against a ticket loaded from persistence before idempotency replay; configured
  request-type/workflow-version matching is server-side. Durable approval decision checks
  effective assigned approver/delegation, active participant status, and requester/creator/executor
  separation before mutation; RBAC proposal approval rechecks current Super Admin membership and
  permission before retry/application. Provider isolation, audit transaction behavior, and replay
  races remain unverified. Privileged administration operations remain subject to service-level
  permission checks and documented migration/provider constraints.
- Final focused selection:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~DatabaseAuthorizationScopeTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests|FullyQualifiedName~DurableWorkflowAuthorizationTests" -p:BaseOutputPath="<unique-temp>\bin\"`
  passed **129/129**, 0 failed, 0 skipped. API build
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo --verbosity:minimal -p:BaseOutputPath="<unique-temp>\bin\"`
  succeeded with 0 warnings and 0 errors. Test/build output was isolated and removed.
- One earlier run of the same focused selection had 128 passes and one failure caused by xUnit
  supplying an `Int32` inline value to a nullable-`Int64` theory parameter. The test data was
  corrected to `Int64`, then the complete focused selection passed; this was not a production
  behavior failure.
- No database-backed scope/replay proof or broader authenticated E2E was performed. Acceptance
  remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

### Authentication endpoint authorization metadata — 2026-10-15

- Production path: `AuthenticationController` had class-level `[AllowAnonymous]` and an
  action-level `[Authorize]` on `ChangePassword`. ASP.NET Core's endpoint authorization sees
  `IAllowAnonymous` metadata from the controller and bypasses authorization middleware for all
  actions, so the action attribute did not enforce authentication. The method's own subject
  validation remained an additional guard, but did not replace the declared endpoint policy.
- Reproduction: `Only_login_is_anonymous_and_password_change_requires_authorization` failed before
  the fix because the controller inherited `AllowAnonymousAttribute`.
- Fix: removed controller-level `[AllowAnonymous]` and applied it only to `Login`; retained
  `[Authorize]` on `ChangePassword` and kept its existing user-ID/current-password checks. The
  login request contract remains public; password change now has effective authorization metadata.

### Ticket execution assignment authorization — 2026-10-15

- Production path: authenticated `POST /api/v1/tickets/{id}/work-items/{workItemId}/execute`
  passes the authenticated actor and request-body `ExecutorUserId` separately from
  `TicketsController.Execute` to `TicketLifecycleService.ExecuteAsync`. The previous service
  implementation checked only ticket mutation permission, then `Ticket.StartExecution` assigned
  the work item to the supplied executor and recorded that identity as `LastExecutor`. Explicit
  reassignment already uses `ITicketAccessAuthorizer.EnsureCanAssignAsync`.
- Requirement: the supplied executor is not proof of assignment authority. Assignment target
  identity/team must be checked against the authenticated actor's ticket and destination scope;
  the controller's authenticated actor remains the event actor. This follows the RBAC design's
  individual-record/assignment rule and existing scoped reassignment path.
- Reproduction: added `Execution_rejects_an_executor_outside_the_actor_assignment_scope` and
  `Execution_preserves_authorized_actor_and_executor_identity` to
  `TicketLifecycleConfiguredTransitionTests`. Before the production fix the focused test run failed
  both cases: the unauthorized executor was accepted and persisted, and the allowed execution did
  not invoke assignment authorization.
- Fix: `TicketLifecycleService.ExecuteAsync` now loads the ticket/work item and calls
  `EnsureCanAssignAsync(actor, Resource(ticket), item.ToDepartmentId, item.ToTeamId, executor, ct)`
  before mutation. Existing actor/executor distinction and endpoint DTO are preserved; allowed
  execution continues to record the authenticated actor as event actor and the authorized executor
  as assignee/last executor. Denial occurs before state mutation/save.
- Validation:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~DatabaseAuthorizationScopeTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests" -p:BaseOutputPath="<unique-temp>\bin\"`
  passed **112/112**, 0 failed, 0 skipped. The focused API build
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo --verbosity:minimal -p:BaseOutputPath="<unique-temp>\bin\"`
  succeeded with 0 warnings and 0 errors. Output was isolated to unique temporary directories.
- After the authentication metadata correction, the final combined database-independent selection
  added `ApiSecurityConfigurationTests` to the filter and passed **125/125**, 0 failed, 0 skipped;
  the final API build also succeeded with **0 warnings and 0 errors**. `ApiSecurityConfigurationTests`
  now asserts only login is anonymous and password change carries effective action authorization.
- This is unit/service-layer authorization evidence only; it does not prove database-backed
  destination scope, HTTP, provider, authenticated E2E, or concurrency behavior. Acceptance remains
  **8 PASS / 3 PARTIAL / 13 BLOCKED**. Migration 031/032 remain blocked.

### Code-only lifecycle regression continuation — 2026-10-15

- Revalidated the existing `PasswordRotationRequiredMiddleware` without changing production code:
  it blocks only authenticated principals with `password_change_required=true`, returns 403, and
  exempts only POST `/api/v1/auth/login` and POST `/api/v1/auth/change-password` using
  case-insensitive exact path comparison. The tests cover wrong methods/trailing slash, both
  exemptions, authenticated and unauthenticated requests (including an unauthenticated principal
  carrying the claim), and an authenticated principal without the claim. In `Program.cs` its
  registration remains after `UseAuthentication()` and before `UseAuthorization()`.
- Identified one focused lifecycle test gap: `Ticket.Resolve` rejects resolution while any
  work-item is incomplete, but existing domain tests did not directly assert that this rejection
  leaves ticket status, resolution timestamp, and emitted events unchanged. Added that
  regression-only test to `TicketLifecycleAuthorityTests.cs`; production lifecycle behavior was
  not changed.
- Exact database-independent test command:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -p:BaseOutputPath="<unique-temp>\bin\" -v minimal`
  passed **16/16**, 0 failed, 0 skipped. The command compiled the API dependency; no separate API
  build was needed because no production code changed. Unique temporary outputs were removed.
- Updated this tracker, the acceptance matrix, and `tests/TESTING.md` additively. Scoped
  `git diff --check` passed for changed files. No database/provider/browser/API scenario was
  exercised or promoted; acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

### PostgreSQL RBAC migration repair continuation — 2026-10-15

- Reviewed the PostgreSQL 030/031/032 drafts, canonical schema, Oracle predecessor fragments,
  EF rows/context, administration write paths, and test guidance. No SQL migration or runtime EF
  contract was changed: a deployable repair cannot truthfully choose client import/ID allocation,
  unknown historical creator/effective dates, or system-audit actor semantics without the listed
  owner decisions.
- Added the non-executable source-level repair decision record at
  `Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`. It resolves the canonical client entity
  from source (`PA_TICKETS_CLIENT.N_CLIENT_ID`) while documenting that PostgreSQL client data and
  ID mapping remain unknown. It records canonical role/permission creator fields, role-permission
  creation fields, the mismatch between canonical and previously observed PostgreSQL audit actor
  contracts, every relevant draft's dependency/constraint/index references, proposed EF changes,
  preservation requirements, and approval gates.
- Confirmed source-level hazards in PostgreSQL 031: a `NOT NULL DEFAULT CURRENT_TIMESTAMP` on
  `D_VALID_FROM` masks existing `D_CREATED_AT` values before its later `COALESCE`; user ID `0` is
  assumed for created/updated-by metadata; and scope backfill attributes a migration-created scope
  to the membership creator. These are unsafe provenance/effective-date defaults, not resolved
  business rules. Do not execute 031/032 until the repair decisions and separate database
  authorization are complete. 030 remains superseded.
- The canonical source schema allows null `PA_TICKETS_AUDIT.N_ACTOR_USER_ID`, provides
  `S_ACTOR_TYPE`, and declares a user FK; the earlier read-only PostgreSQL catalog report instead
  recorded non-null actor ID and no FK. EF `AuditRow` currently has a non-null `long ActorId` and
  no actor type. This discrepancy is explicitly unresolved; no system actor or human identity was
  fabricated.
- Added one direct middleware regression case proving an unauthenticated request is not blocked
  even if its constructed principal carries the rotation-required claim. The existing middleware
  and allowed route/method behavior were not changed. The database-independent
  `ApiSecurityConfigurationTests` selection passed **12/12**, 0 failed/skipped, with unique
  temporary build output that was removed. This project compiles the API dependency; no separate
  API build was needed for a test-only source change.
- `git diff --check` passed for the task-touched files. Repository-wide diff checking still reports
  pre-existing whitespace in the untouched `tests/scripts/provision-postgresql-schema.ps1`; that
  script was not run or changed. The acceptance matrix remains **8 PASS / 3 PARTIAL / 13 BLOCKED**;
  no database/provider/browser/end-to-end status was promoted.

### RBAC independent-approval source verification — 2026-10-15

- The authenticated API request cannot provide proposal payload JSON or its digest. The controller
  derives the actor from the authenticated principal; the service builds immutable payload values
  from that actor, validated request fields, and current role/permission rows. The service requires
  active Super Admin membership and current permission to create, approve, or reject proposals; an
  active initiator may cancel only their own proposal without reviewer authority.
- The payload uses the current `System.Text.Json` record representation and is not specified as a
  standards-based canonical JSON format. Approval reconstructs and compares the same application
  representation against current rows; this is a consistency check.
- SHA-256 is unkeyed. A database writer with update access to both proposal payload and digest can
  recompute a matching digest; a principal with direct RBAC-table write access may also bypass the
  service. Actual database grants were not inspected. A privileged database operator may alter
  proposal and audit rows; the digest/hash chain is not an independent signature or anchor.
- No ad hoc key was added. Stronger tamper evidence needs separately approved key management: a
  versioned signed/HMAC envelope with signing material outside the database (preferably a
  separately controlled signing service or managed key), key IDs and rotation/revocation,
  verification/failure policy, and an independent immutable audit anchor. This still does not
  replace least-privilege DB grants or decision-time revalidation.
- Decision-time expiry is enforced before approve/reject/cancel transitions even when a row remains
  `PENDING` in storage. The list query is unpaginated and expires at most 50 rows per request; this
  is a bounded lifecycle-write batch, not pagination. Later expired rows can remain persisted as
  `PENDING`, but cannot be decided successfully. The authenticated caller that triggers list expiry
  is recorded as the audit actor, and the reason states that the transition was automatic rather
  than a human decision. The 51-candidate policy test does not prove database persistence or
  provider behavior.
- PostgreSQL/Oracle concurrency, rollback, migration, audit-FK behavior, authenticated API, and
  browser acceptance remain BLOCKED. No database, live API, migration, or account/role/permission/
  proposal record was accessed or changed for this source-level continuation.

### Project-wide continuation and PostgreSQL catalog preflight — 2026-10-15

- The worktree remains on `workstream-i-lifecycle-authority`, with no staged changes. At the
  preflight snapshot it contained 52 modified tracked files and 37 untracked files; these are
  existing/shared work and were preserved. The newly observed `.vscode/mcp.json` was not opened,
  copied, or changed.
- A read-only catalog inspection used profile `pa_tickets_h_verify_ro` and first verified
  `current_database() = pa_tickets_h_verify` and `current_user = pa_tickets_mcp_ro`. Catalog
  table names were enumerated; column, constraint, and index details were inspected for the
  `public` application schema. No application rows were read. No migration history table was
  observed in `public`, so object shape does not prove which migration files were applied.
- The public schema contains the base role, permission, membership, role-permission, user-client,
  department, team, and audit tables. It does not contain
  `PA_TICKETS_USER_ROLE_SCOPE`, `PA_TICKETS_ADMIN_HEAD_DELEGATION`, or
  `PA_TICKETS_RBAC_PROPOSAL`; it also lacks `PA_TICKETS_CLIENT`, which PostgreSQL migration 031
  references for the scope-table foreign key.
- The current permission table lacks the metadata columns added by 031. The current
  role-permission table lacks the effective-date/active/update columns added by 031 and also lacks
  `D_CREATED_AT` and `N_CREATED_BY`, which the migration's seed/backfill uses. The current role
  and permission tables lack `N_CREATED_BY`, which migration 031's built-in-role and permission
  seed inserts reference. These are source/catalog incompatibilities against this database's
  observed pre-031 schema; migration 031 was not run. Do not treat migration readiness as PASS.
- The audit table has a non-null `N_ACTOR_USER_ID` and primary key, but the catalog shows no
  foreign key from the audit actor to `PA_TICKETS_USER`. The independent-approval proposal table
  and its checks, user/role/permission foreign keys, and indexes are absent, consistent with
  migration 032 not having established its required shape. No migration, DDL, DML, provisioning,
  API call, or application-state change occurred.
- Consolidated project inventory:

  | Area | Status | Evidence and outstanding work |
  |---|---|---|
  | Authentication, sessions, provisioning | PARTIAL | Local login/token, Angular auth, and a one-shot bootstrap utility exist (`AuthenticationController`, `LocalAuthenticationService`, `InitialAdministratorProvisioner`, `tools/PA.Tickets.Bootstrap`; PostgreSQL 028). Bootstrap operational acceptance, email-verification policy/delivery, and fresh authenticated acceptance remain open. |
  | RBAC catalogue, protected operations, scopes | PARTIAL | Effective permissions and fail-closed policy exist (`RoleAuthorizationPolicy`, `DatabaseAuthorizationService`, `RoleAuthorizationPolicyTests`; migrations 030/031). The observed DB lacks 031 metadata/scope/delegation structures; route-to-permission parity is incomplete. |
  | Independent approval and privileged-change lifecycle | PARTIAL | Proposal service/policy and 50 database-independent tests exist (`AdministrationService`, `SensitiveRbacApprovalPolicyTests`; migration 032). The proposal schema is absent; provider races, rollback, audit FK, and authenticated actor behavior are unverified. |
  | Ticket creation, assignment, lifecycle, authorization | PARTIAL | Backend scope, lifecycle, idempotency, and regression work is documented under A1/A2/D2/I1. The current safe run covers policy/lifecycle tests only; full API/browser acceptance is not current evidence. |
  | Workflow configuration and execution | PARTIAL | Durable workflow and approval execution and regression suites exist (`PersistentWorkflow*`, `PostgreSqlWorkflowExecutionE2ETests`). Configuration UI, current provider-backed execution, and recovery acceptance remain open. |
  | SLA configuration, calculation, escalation | PARTIAL | Worker-driven deadline detection and SLA regressions are documented under C1 (`SlaDeadlineMonitor`, `PersistentSlaConfigurationService`). Policy semantics, calendar-version history, DST/provider behavior, and operational enablement remain open. |
  | Approval configuration and delegation | PARTIAL | Durable approval administration/execution exists, but privileged delegation mutations and current database/API validation remain blocked (`ApprovalRuleAdministrationController`, `DurableWorkflowApprovalRepositories`). |
  | Notifications, delivery, retry, audit | PARTIAL | Persistent notification/communication services and PostgreSQL regression tests exist. Current provider retry, failure-boundary, delivery configuration UI, and operational recovery are not established by this continuation. |
  | Administration and catalogue management | PARTIAL | Administration/catalogue services, APIs, and regressions exist. The current Angular admin surface is limited; broader catalogue/operational configuration acceptance remains open. |
  | Angular screens, API integration, validation, errors | PARTIAL | Auth, ticket, and role-administration screens/specs exist. Current Angular tests/build are reported below; no browser-to-API acceptance was performed in this continuation, and dynamic forms/client selection remain open. |
  | PostgreSQL migration compatibility and integrity | FAIL (unexecuted draft incompatibility confirmed by catalog comparison) | Migration 031 references absent `PA_TICKETS_CLIENT` and absent role/permission/role-permission columns; 032's proposal structure is absent. No migration history table was observed. Migration execution and repair require separate review/approval. |
  | API, integration, browser, security, concurrency, fault injection | BLOCKED | Current run executed only database-independent tests/builds. Authenticated API/browser evidence is historical; provider concurrency, rollback, migration, and fault injection were not run. |
  | Documentation, deployment readiness, operations | PARTIAL | Existing tracker, acceptance matrix, security/configuration and testing guidance describe known gaps. No production-readiness claim is made; bootstrap, identity, migration rehearsal, and recovery remain release blockers. |

- Fresh safe validation: the focused database-independent .NET policy/security/lifecycle selection
  passed **75/75**; the API project build passed with **0 warnings and 0 errors**. The aggregate
  Angular test command stalled after 3 of 12 specs and was stopped. Running each of the 12 spec
  files separately then returned exit code 0 for every invocation and reported **51/51 tests
  passed**. An intermediate PowerShell summary wrapper returned 1 because its count parser failed;
  the captured per-file Vitest results show 12 files passed and no test failures. The Angular
  production build passed with a stylesheet budget warning: `role-administration.page.scss` is
  4.53 kB, 529 bytes above its 4.00 kB budget. No PostgreSQL-backed tests were run.
- The 24-scenario RBAC acceptance counts remain **8 PASS / 3 PARTIAL / 13 BLOCKED**. Catalog
  structure and unit tests do not promote provider-dependent or end-to-end scenarios.

### Code-only security continuation and migration repair proposal — 2026-10-15

- Selected the forced-password-rotation API boundary as the highest-priority independently
  verifiable code-only gap: the existing middleware denied protected API requests for an
  authenticated rotation-required token, while allowing only POST login/password-change paths,
  but had no direct regression coverage. Extracted it to
  `src/PA.Tickets.Api/PasswordRotationRequiredMiddleware.cs`, kept it in the same middleware
  position in `Program.cs`, and added deterministic allow/deny tests in
  `ApiSecurityConfigurationTests`. This is source/unit evidence only; it does not prove JWT
  validation, hosted API routing, or end-to-end password rotation.
- The focused suite
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~ApiSecurityConfigurationTests" -p:BaseOutputPath="<unique-temp>\bin\" -v minimal`
  passed **11/11** after also asserting that an authenticated session without the rotation claim
  continues to its next middleware. The isolated API build
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo -p:BaseOutputPath="<unique-temp>\bin\" -v minimal`
  passed with **0 warnings and 0 errors**. Both commands used isolated temporary output paths; the
  focused test output was removed after successful validation.
- RBAC acceptance counts remain **8 PASS / 3 PARTIAL / 13 BLOCKED**. No route or scenario was
  promoted by middleware unit tests. No database, migration, provisioning script, API, Oracle, or
  shared database was accessed in this continuation.

#### PostgreSQL migrations 031/032 repair proposal — not implementation approval

The following is a repair design for review, not an instruction to edit or apply the migrations.
The PostgreSQL 031/032 drafts remain unexecuted. Decisions marked for confirmation must be made
before producing a revised migration.

1. **Client entity and FK**
   - `031_configurable_rbac.sql` creates `PA_TICKETS_USER_ROLE_SCOPE.N_CLIENT_ID` and references
     `PA_TICKETS_CLIENT(N_CLIENT_ID)`, but the observed PostgreSQL schema has no client table.
     `database/schema/001_PA_TICKETS_SCHEMA.sql` defines a client catalogue with client ID, unique
     code, name, description, active/inactive status, contact/classification fields, and
     created/updated actor/time fields; it also relates `PA_TICKETS_USER_CLIENT.N_CLIENT_ID` to
     that table. This is static source evidence, not evidence that the PostgreSQL deployment has
     that entity or that all those fields are the approved cross-provider contract.
   - **Confirm** whether PostgreSQL should add a corresponding `PA_TICKETS_CLIENT` catalogue and
     whether both role-scope and existing user-client `N_CLIENT_ID` values must reference it. If
     yes, specify which columns/status rules are canonical, how IDs are allocated, whether
     inactive clients may remain referenced by historical scopes/memberships, and `ON DELETE`
     behavior (restrict/soft-retire is the preservation-safe default, not yet approved). If no,
     identify the actual authoritative PostgreSQL client table and its key. Do not create a
     client table or constraint by assumption.
2. **Creator/audit metadata ownership**
   - 031 adds `D_UPDATED_AT` and `N_UPDATED_BY` to role-permission, but its backfill reads
     `D_CREATED_AT`; its grant seed writes `D_CREATED_AT` and `N_CREATED_BY`. Its built-in role
     and permission inserts also write `N_CREATED_BY`. Those fields are absent in the observed
     PostgreSQL role, permission, and role-permission tables. The role/permission EF rows do not
     currently model creator/update metadata. The static canonical schema does include creation
     time/actor on role, permission, and role-permission.
   - **Confirm** whether PostgreSQL should add nullable or non-null `D_CREATED_AT`/`N_CREATED_BY`
     (and role/permission update metadata) and which service owns their values. Choose truthful
     actor semantics for migration-seeded records rather than assuming actor `0` is a valid
     human. Decide whether actor references are FKs, and if so how system/migration actors are
     represented. Existing audit `N_ACTOR_USER_ID` is non-null but no FK was observed; adding an
     audit FK is a separate compatibility decision because prior/system audit actors may not map
     to persisted users. Alternatively, revise seed/backfill SQL not to depend on creator columns
     that the approved schema does not own.
3. **Predecessors and ordering**
   - Establish the exact PostgreSQL baseline/schema-adapter revision first; the observed tables
     already include some authentication/profile fields, but no migration-history table was
     found, so do not infer applied versions from that alone.
   - PostgreSQL 028 adds local-password fields; 029 adds user profile hierarchy fields and manager
     FK. They are prerequisites only if those fields are not already supplied by the verified
     baseline. 031 also requires role, permission, role-permission, user-role, user, department,
     team, and the approved client relation plus role/permission sequences and built-in-role
     fields. `030_five_role_rbac.sql` is explicitly marked **SUPERSEDED DRAFT — DO NOT EXECUTE**;
     do not use it as a predecessor. Decide whether any of its required built-in catalogue work
     must be incorporated into a reviewed replacement/031.
   - Apply a corrected and reviewed 031 before a corrected 032. 032 depends on the role,
     permission, and user keys established by that compatible base and defines proposal
     references/uniqueness/check constraints. No migration is authorized by this proposal.
4. **Required EF Core alignment**
   - If the confirmed client entity is added, introduce its row/DbSet and provider mapping; map
     user-client and role-scope client FKs consistently with the selected deletion policy.
   - Align `AdministrationRoleRow`, `AdministrationPermissionRow`, and
     `AdministrationRolePermissionRow` metadata properties/column mappings with the approved
     SQL, including the role-permission effective-date and update fields. Keep proposal and scope
     mappings, indexes, concurrency token and delete behavior aligned with 032/031. Align audit
     actor FK metadata only if that policy is explicitly chosen.
   - Add provider-neutral EF model metadata tests that assert table/column/key/index/FK/nullability
     contracts without connecting to a database.
5. **Compatibility/data preservation**
   - Preserve existing role, permission, membership, mapping, user-client, and audit identities.
     Before case-folded uniqueness or canonical role-code constraints, detect collisions and
     resolve them only through an approved mapping; do not silently rename or merge records.
   - Review replacement of the full `(role, permission)` unique constraint with the active-only
     unique index: establish how duplicate historical inactive grants are represented and ensure
     no existing rows are discarded. Backfill metadata without inventing a person/actor and
     without retroactively deriving historical scope from mutable current organization fields.
   - Confirm migration transaction/locking expectations and a recoverable provider-specific
     backup/forward-repair plan before any execution. A partial migration is not repaired by
     blindly rerunning or dropping objects.
6. **Required tests before migration approval**
   - Static SQL/model contract checks and PostgreSQL parser/DDL validation in a disposable,
     isolated environment, after the schema decisions above.
   - Read-only preflight for required columns, sequences, client relation, duplicate/case
     collisions, unique constraints, FK targets, actor semantics, and existing migration state.
   - Migration rehearsal from the actual approved baseline with preservation assertions for
     existing IDs/rows, scope backfill, historical inactive mappings, repeated execution policy,
     and rollback/recovery. Rehearsal writes/DDL and needs separate explicit authorization.
   - After separately approved application, provider tests for idempotency, FK enforcement,
     duplicate active grants, audit actor compatibility, concurrent approval/create/cancel,
     audit/grant/status rollback, and ambiguous commit/retry. None can be replaced by unit tests.

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
- Status: CLOSED — the Workstream H source contract and PostgreSQL-backed verification all passed on the disposable database `pa_tickets_h_verify`.
- Reproduction evidence: The working tree was inspected to verify the actual contract: display number and public ID are distinct values; the UI and service use the public ID for completion; blank `Idempotency-Key` handling was verified via the controller and API regressions; communication activation logic was validated through safe local tests; the two HTTP 500 failures were traced to an EF Core column mapping defect that was fixed in `TicketsDbContext`.
- Expected behavior: Work-item completion must use the public work-item identifier, ticket creation must reject blank `Idempotency-Key` values before any service invocation, and communication delivery must only activate when the channel/provider is enabled inside a valid date window.
- Root cause: The produced HTTP 500s were not a shared business-rule bug; they were caused by a broken EF Core mapping that queried `p0.PublicId` while the PostgreSQL schema exposes `S_PUBLIC_WORK_ITEM_ID`.
- Fixes implemented: H1 contract alignment in the API/UI/service path uses `publicId`; H2 PostgreSQL ticket-creation idempotency and API regression suites pass on the disposable database; H3 communication activation remains fail-closed when configuration is missing, disabled, or outside the window; H4 documentation now records the verified disposal-local evidence.
- Verification evidence:
  - `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~Authenticated_http_pipeline_executes_workflow_and_enforces_auth_idempotency_and_concurrency|FullyQualifiedName~Approval_and_sla_http_pipeline_persists_history_and_enforces_authorization" --nologo -v minimal` passed 2/2 (exit code 0)
  - `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~PostgreSqlApiE2ETests" --nologo -v minimal` passed 9/9 (exit code 0)
  - `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~PostgreSqlTicketCreationIdempotencyTests" --nologo -v minimal` passed 9/9 (exit code 0)
  - `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~WorkstreamHRegressionTests" --nologo -v minimal` passed 5/5 (exit code 0)
- PostgreSQL isolation evidence: The test environment was verified using a process-scoped `PA_TICKETS_TEST_CONNECTION_STRING` pointing at `pa_tickets_h_verify` and role `pa_tickets_test_user`; the read-only identity check returned exactly `pa_tickets_h_verify|pa_tickets_test_user` before the suites ran. No shared-database or Oracle commands were used.
- Remaining limitations: The documented production-mode resolver-through-processor success case and disabled/out-of-window provider rows remain separate coverage gaps; no claims are made for production readiness or Oracle compatibility.
- Safety: PostgreSQL access was limited to the disposable database and verified read-only identity checks; no shared database was updated, no Oracle connection was attempted, and Workstream I was not started.

### I1 — Lifecycle and workflow authority

- Severity: High
- Status: **PARTIAL** — the two confirmed backend defects below are fixed and covered by focused PostgreSQL tests. This local evidence does not establish production readiness or full lifecycle/workflow conformance.
- Findings and root causes:
  - Legacy ticket approval did not persist the nominated approver. `Ticket.Approve` checked generic separation-of-duties conditions but did not require a pending request or bind the decision actor to the nominated approver. The designated approver is now persisted, only that user can decide a pending request, repeated approval requests and out-of-order decisions fail, and requester/creator/executor separation is enforced when requesting and deciding approval.
  - Ticket persistence reloaded the current row and wrote a previously loaded aggregate without checking its original version. Audit-chain append locks serialize audit writes but did not prevent stale ticket-state replacement. A shared ticket row-version concurrency token now guards ticket writes through both ticket and workflow execution contexts; stale repository writes become conflicts (HTTP 409).
  - Source review found authorization and configured status-transition checks on lifecycle mutations; assignment uses its scoped authorization path. Durable approval decision/delegation and published workflow-version controls remain server-side. Angular behavior was not changed; UI action visibility is not treated as an authorization boundary.
- Files changed for I1:
  - `src/PA.Tickets.Domain/Tickets/Ticket.cs`
  - `src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs`
  - `src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs`
  - `src/PA.Tickets.Infrastructure/Execution/ExecutionDbContext.cs`
  - `src/PA.Tickets.Api/Program.cs`
  - `tests/PA.Tickets.Domain.Tests/TicketLifecycleAuthorityTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlProblemTests.cs` — align manual workflow selection fixture by leaving RequestTypeId unset.
  - `tests/PA.Tickets.Domain.Tests/PostgreSqlCommunicationDeliveryTests.cs` — seed the channel/provider activation rows required by the delivery contract and clean up only those fixture rows.
  - `tests/scripts/postgresql-schema.sql`
  - `database/migrations/027_ticket_lifecycle_authority.sql` (source added; not run against Oracle)
  - `Docs/REMEDIATION_TRACKER.md`
- PostgreSQL evidence: Before each database-backed test batch, a read-only identity check returned exactly `pa_tickets_h_verify|pa_tickets_test_user`. The additive PostgreSQL columns `N_APPROVER_USER_ID` and `N_ROW_VERSION` were applied only to that disposable database after identity verification. No provisioner or reset was run.
- Verification:
  - `TicketLifecycleAuthorityTests` — 3 passed, 0 failed; covers designated approver/state, missing approval request, and requester/creator/prior-executor separation.
  - `Authenticated_http_pipeline_executes_workflow_and_enforces_auth_idempotency_and_concurrency` — passed with the API scenario proving that a non-designated caller receives 409 and the designated approver succeeds; persisted actor/approval state is asserted.
  - `PostgreSqlTicketPersistenceTests` — 6 passed, 0 failed, including one winning lifecycle write, 11 stale-write conflicts, persisted approval identity/decision, and audit-history checks.
  - `PostgreSqlWorkflowExecutionE2ETests` — 12 passed, 0 failed, including effective published-version selection and no version switch for an existing workflow instance.
  - `PostgreSqlWorkflowApprovalAdministrationTests` — 3 passed, 0 failed, including published workflow/approval immutability and configured approver behavior.
  - `PostgreSqlApiE2ETests` — 9 passed, 0 failed after adding the HTTP designated-approver regression.
  - `Problem_ticket_uses_effective_sla_and_workflow_instances_without_changing_domain_logic` — passed 1/1 after correcting its fixture: it creates a manual workflow for a ticket without a configured RequestTypeId, rather than pairing the ticket with a different request type's workflow.
  - `PostgreSqlCommunicationDeliveryTests.Communication_template_queue_delivery_and_audit_are_durable` — original fixture reproduced the expected `Queued` result because it omitted the enabled, effective EMAIL channel and `deterministic` provider configuration required by the processor. After adding those rows to the fixture, the test passed three consecutive isolated runs; communication-delivery tests plus `WorkstreamHRegressionTests` passed 8/8.
  - Combined persistence/workflow/configured-transition/Workstream-H filter — 36 passed, 0 failed.
  - A later broad Workstream I regression filter passed 50 tests and failed 1: `Concurrent_ticket_lifecycle_saves_allow_only_one_transition` timed out at its 10-second snapshot-preparation barrier, before asserting concurrent save outcomes. The test passed 1/1 when rerun alone against the verified disposable database (9-second reported duration). The barrier now allows 30 seconds, signals readiness even when preparation throws, releases workers before observing task failures, and surfaces preparation exceptions through `Task.WhenAll`; after this adjustment, the concurrency test and communication delivery test passed together (2 passed, 0 failed, 0 skipped).
  - Current continuation, broad I1/H batch: before starting the batch, `F:\PROGRAMS\Postgres\bin\psql.exe -X -h <host> -p <port> -U pa_tickets_test_user -d pa_tickets_h_verify -A -t -v ON_ERROR_STOP=1 -c "SELECT current_database(), current_user;"` returned exactly `pa_tickets_h_verify|pa_tickets_test_user`; the test process used the same process-scoped disposable connection. Command: `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~PostgreSqlTicketPersistenceTests|FullyQualifiedName~PostgreSqlWorkflowExecutionE2ETests|FullyQualifiedName~PostgreSqlCommunicationDeliveryTests|FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlWorkflowApprovalAdministrationTests|FullyQualifiedName~Problem_ticket_uses_effective_sla_and_workflow_instances_without_changing_domain_logic|FullyQualifiedName~WorkstreamHRegressionTests"` — passed 42/42, 0 failed, 0 skipped, exit code 0. This includes the corrected lifecycle concurrency barrier and relevant H regressions. No assertions were weakened.
  - Full I1 closure batch: immediately before execution, the same live `current_database(), current_user` identity query returned `pa_tickets_h_verify|pa_tickets_test_user`, and the process-scoped test connection used that disposable target. Command: `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --logger "console;verbosity=minimal"` — complete domain test project passed 189/189, 0 failed, 0 skipped, exit code 0. This supersedes the previous incomplete/stopped full-project runs and the 50/1 result as current validation; the earlier failures remain historical but were not reproduced. No assertions were weakened or tests skipped.
  - Current continuation build: `dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal` — succeeded, 0 warnings, 0 errors.
  - Current continuation diff check: `git diff --check -- Docs/REMEDIATION_TRACKER.md Docs/AUDIT_HASH_DESIGN.md src/PA.Tickets.Api/Program.cs src/PA.Tickets.Domain/Tickets/Ticket.cs src/PA.Tickets.Infrastructure/Execution/ExecutionDbContext.cs src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs tests/PA.Tickets.Domain.Tests/PostgreSqlCommunicationDeliveryTests.cs tests/PA.Tickets.Domain.Tests/PostgreSqlProblemTests.cs tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs tests/PA.Tickets.Domain.Tests/TicketLifecycleAuthorityTests.cs` — exit code 0, no whitespace errors.
  - Solution build `dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal` — succeeded, 0 warnings and 0 errors.
  - Current continuation status: the 42/42 broad I1/H batch, full 189/189 domain test project, solution build (0 warnings, 0 errors), and task-scoped diff check pass. The earlier 50/1 result and previously stopped full-project run are retained as historical investigation evidence; the concurrency barrier correction and complete project rerun now pass. No current I1 regression failure remains in the executed scope.
- Status: **CLOSED for the scoped backend lifecycle/workflow-authority remediation**, based on the designated-approver and stale-write corrections, the broader 42/42 I1/H regression batch, the complete 189/189 `PA.Tickets.Domain.Tests` project, and the clean solution build. This is not a production-readiness determination.
- Remaining limitations: Oracle migration execution/compatibility is unverified. Angular lifecycle/action behavior was not changed or tested in this backend pass. No claim of production readiness is made.
- Safety: Test connections were process-scoped and identity-checked. No connection was made to `pa_tickets_test` or Oracle; no GitHub remote, commit, push, or database drop was performed.

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
- Current verification note (2026-10-13): the verifier unit tests pass 8/8 and the F1-focused PostgreSQL batch passes 15/15 (0 failed, 0 skipped) on the verified disposable target; the solution build passes with 0 warnings and 0 errors. The earlier stale-configuration diagnosis was confirmed: the process environment had pointed at the shared database while `.env.test.local` held the disposable target. For those database-backed batches, the process-scoped setting was set from the local disposable configuration and the real identity check returned `pa_tickets_h_verify|pa_tickets_test_user`. In this continuation, that identity was rechecked before read-only metadata inspection; the disposable database returned no rows for audit IDs 4257 or 4258. No tests or build were rerun in this documentation-focused continuation.
- Database safety / configuration diagnosis:
  - `PA_TICKETS_TEST_CONNECTION_STRING` in the active environment was classified as `shared` (`pa_tickets_test`), while `.env.test.local` was classified as `disposable` (`pa_tickets_h_verify`). The active env var was stale and was corrected before any database-backed regression run.
  - Verification before DB-backed batch: `F:\PROGRAMS\Postgres\bin\psql.exe -h <host> -p 5432 -U pa_tickets_test_user -d pa_tickets_h_verify -A -t -c "SELECT current_database(), current_user;"` returned `pa_tickets_h_verify|pa_tickets_test_user` and the process-scoped env var was reset to the same disposable target.
  - No shared-database writes, destructive schema operations, or Oracle access were performed.
- Historical writer inventory and canonicalization evidence:
  - All inspected writers use compact default `System.Text.Json` output, UTF-8 bytes, lowercase SHA-256 hex, and predecessor selection by descending audit ID per `(EntityType, EntityId)`. The canonical field order, stream-to-writer mapping, timestamp normalization, and overlaps are recorded in `Docs/AUDIT_HASH_DESIGN.md`.
  - Production verification now additionally supports `STATUS_DEFINITION` and `PRIORITY_DEFINITION`, written through `OperationalMetadataAuditWriter`. That writer truncates UTC timestamps to PostgreSQL microsecond precision before hashing and persistence. PostgreSQL regressions verify actual rows emitted by the status and priority repositories, including full-chain links and tampering outcomes.
  - `STATUS_TRANSITION` is excluded despite using the same current writer: earlier recorded PostgreSQL evidence from the persistent shared test database reports that row 4258 fails recomputation under the source-visible canonical fields while adjacent row 4257 validates. That historical row was not present in the verified disposable database inspected in this continuation, and the shared database was not accessed. The historical writer version/cause remains unproven; no alternate format is guessed.
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
  - The earlier investigation recorded no TRX artifact mapping audit IDs 4257–4258 to a test run. This continuation found no `*.trx` files in the current checkout, so that earlier artifact review could not be independently repeated. Current Git history is available, but its earliest commit (`0b59513`, 2026-10-12 13:07:45 +0530) postdates the recorded 2026-10-08 row timestamp. The writer/test source snapshots therefore cannot establish what was deployed when the row was written. The current checkout contains no earlier writer source, row-to-run correlation, or other provenance artifact; no alternate serialization was treated as proof or enabled.
  - The earlier evidence query ran against `PA_TICKETS_AUDIT` inside `BEGIN READ ONLY` / `ROLLBACK`; it reported 22,063 rows across 24 entity types and matching persisted values on a repeat read. This continuation did not reconnect to that shared test database. In the verified disposable database, a read-only transaction found neither IDs 4257 nor 4258 and no matching `STATUS_TRANSITION` audit-disposal maintenance row; this does not corroborate the earlier row provenance or explain the mismatch.
  - Writer inventory recommendations and the complete persisted mismatch fields are recorded in `Docs/AUDIT_HASH_DESIGN.md`. Keep each remaining format unsupported until actual writer-produced PostgreSQL rows, timestamp precision, field omission/null semantics, and chain linkage are proven. `WORKFLOW_STEP_INSTANCE` exists in local persisted data but no direct current writer was found; its creating source/version and provenance must be recovered.
  - Closure condition for `STATUS_TRANSITION`: leave it unsupported unless authoritative historical writer source plus row-to-run provenance (or equivalent trusted evidence) explains ID 4258 and persisted-row tests prove the exact format. Do not enable based only on matching test payloads.
- Evidence required to resolve the historical provenance gap:
  - An authenticated, dated source or release artifact for the writer deployed when the row was recorded, including its exact canonical serialization and timestamp precision behavior.
  - A trustworthy correlation from audit IDs 4257–4258 to a writer build and invocation, such as retained test output tied to generated audit IDs, application logs carrying transaction/request identifiers, or an independently validated database transaction/audit trail.
  - If the question is whether persisted values changed after append, a trusted pre-change snapshot/backup, WAL record, or equivalent independently protected evidence. Current row fields and linked hashes alone cannot distinguish a legacy format difference from later modification.
  - A separate isolated fixture using the identified historical writer version, where equivalent persisted values reproduce the known stored digest and predecessor. Do not alter or recreate the historical row to manufacture provenance.
- Acceptance decision: safe verifier/API and focused PostgreSQL regression criteria are met for `TICKET`, `STATUS_DEFINITION`, and `PRIORITY_DEFINITION`. F1 cannot be closed: `STATUS_TRANSITION` remains unsupported with unproven provenance; other persisted writer formats remain unsupported pending writer-produced persisted-row tests for their exact canonical contracts; and no independent trusted anchor exists. Hash-chain consistency is not proof of immutable history.
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

## Workstream G — Local Angular integration and acceptance

- Verdict: **PARTIAL**. Browser acceptance verified regular username/password login, in-memory token handling, bearer-authenticated list/search/detail/create/catalogue requests, immediate logout, and protected-route rejection against the local Development API. A live work-item completion was not possible because the newly created and safely inspectable tickets exposed no work items. Bootstrap, rate limiting, client selection, broader lifecycle UI, and production readiness remain unresolved.
- Acceptance scope: implemented Angular ticket search/list, detail, ticket creation with an idempotency key, catalogue selectors, and work-item completion where a work item is present. Dynamic fields remain excluded. API E2E fixtures establish backend contract behavior; live browser evidence now additionally covers the available local flows.
- Explicit exclusion: E1 dynamic request-type fields, field definitions/options, and dynamic value serialization are unsupported and excluded from acceptance. Do not infer support from catalogue selectors or API-side validation.
- Environment facts: the existing `http` launch profile binds `http://localhost:5018` and selects Development. The prior health failure was because no API process was listening; starting that profile with process-scoped PostgreSQL provider, disposable connection, and local JWT key returned HTTP 200 for liveness/readiness. Development selects local authentication and requires `Auth:Local:SigningKey` from protected configuration.
- Authentication implementation: `POST /api/v1/auth/login` trims the supplied login name and verifies it against the active user and ASP.NET Core Identity password hash in `PA_TICKETS_USER.S_PASSWORD_HASH`; failed/unknown accounts receive the same generic 401. Successful login issues a short-lived HMAC-SHA256 token with subject, issuer, audience, expiry, and current role/permission claims. Angular login calls this route; the token remains in memory and the interceptor attaches it. `POST /api/v1/admin/users/{id}/password` requires `USER_ADMIN`, hashes passwords, and audits the operation without storing password/hash in the audit payload. Initial administrator bootstrap remains an operational gap.
- Authentication decision: regular username/password with application-managed RBAC is authoritative. OIDC/SSO remains a future optional add-on, not a local acceptance gate. A disposable fixture credential and signing key were generated in process-only environment variables, not committed or printed; no existing user's credential was overwritten.
- RBAC verification: the authenticated fixture had an effective Operations role and a `TICKET_READ` claim with department/team scope 501/601. The API accepted an in-scope search and rejected a foreign department search with 403. Anonymous access returned 401; wrong and unknown credentials returned equivalent 401 responses; the authenticated non-admin was denied user administration and password reset with 403. The filtered PostgreSQL administration regressions additionally passed. Client/tenant selection remains API-filterable but has no UI selector.
- Live browser verification: login through `http://localhost:4200` returned to the protected Angular shell. The token was absent from both localStorage and sessionStorage; a protected ticket search carried an Authorization bearer header and returned 200. Ticket list/search rendered 25 rows, a number-filtered search rendered its matching row, and its detail request rendered the selected ticket. Ticket creation returned 200, included both bearer and `Idempotency-Key` headers, navigated to the created detail, and displayed it. Services, categories, and request types loaded successfully; dynamic form fields remain unavailable.
- Origin and UI findings: the configured Development CORS origin is `http://localhost:4200`; opening Angular at `http://127.0.0.1:4200` caused the login preflight to fail, while using the configured `localhost` origin succeeded. The live list initially stayed in its loading state despite a 200 response because async component state updates were not reflected in the view. `markForCheck()` was added to ticket list/detail/create async state paths. Sign out previously cleared the AuthService but left the protected shell visible; it now navigates immediately to `/login`. Browser logout cleared storage and a subsequent protected-route visit redirected to login.
- Work-item completion: not live-verified. The created ticket and inspected detail had no work items; a type-filtered search exposed none that could be safely used as a disposable completion fixture. Do not mutate unrelated test rows to manufacture evidence. Backend completion/idempotency regressions remain the evidence for the API contract only.
- Provisioning and abuse controls: `Docs/SECURITY_CONFIGURATION.md` contains an operator-reviewed initial Super Admin runbook and explicitly states that the one-shot provisioning utility and enforced first-login rotation are not implemented. The API has no login throttling, lockout, persistent failed-attempt counter, or abuse-monitoring workflow. These remain deployment/security blockers, not accepted features.

### A–F evidence and disposition

| Workstream | Classification | Evidence and remaining condition |
|---|---|---|
| A — Authorization and resource isolation | Implemented; PostgreSQL-tested; deployment provisioning deferred | `DatabaseAuthorizationService` resolves active users, effective role grants, department/team/client scope, and administrator behavior. Ticket search applies non-admin scope constraints; resource fetch/mutation routes use the ticket access authorizer. PostgreSQL authorization/API/workflow regressions are recorded above. Production user/role/scope provisioning and Oracle authorization behavior remain unverified; OIDC configuration is only needed if the optional scheme is enabled. |
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
- Production requires protected local JWT signing-key management/rotation, explicit HTTPS CORS origins, a configured trusted-proxy allowlist if forwarded headers are enabled, and a real malware scanner. OIDC issuer/authority configuration applies only if the optional OIDC scheme is enabled. `DeterministicContentScanner` is limited to Development/Testing; the default `UnconfiguredContentScanner` fails closed when scanning is unavailable. Production communication-provider delivery also requires a registered/configured adapter. None of these are established by local PostgreSQL integration.
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

### Workstream G acceptance matrix and fresh validation

| Acceptance item | Result | Evidence / limit |
|---|---|---|
| Angular client build | PASS | `npm.cmd run build` from `client` succeeded; production bundle generated. |
| Angular UI/service tests | PASS | `npm.cmd test`: 11 files, 32 passed, 0 failed, 0 skipped. The suite uses mocked HTTP; live connectivity is evidenced separately below. |
| Ticket API contract/persistence and authorization | PASS, scoped | Latest API, administration-authorization, workflow-execution, and communication-delivery batch: 32 passed, 0 failed, 1 fixture-dependent local-login test skipped. A focused API regression for profile hierarchy passed 1/1. |
| API health / live Angular-to-API | PASS, local only | Development `http` profile binds `http://localhost:5018`; `/health/live` returned 200. Browser calls from `http://localhost:4200` succeeded. `127.0.0.1:4200` is not an allowed CORS origin and fails preflight; no CORS policy was weakened. |
| Regular username/password authentication | PASS, scoped | Live browser login succeeded with the disposable account. A bearer-authenticated protected search returned 200; the token was not stored in localStorage/sessionStorage. Login errors remain generic by implementation and the focused PostgreSQL auth regression passed. Initial Super Admin provisioning remains a deployment blocker. |
| OIDC / SSO | DEFERRED | Future optional add-on only; it is not a prerequisite for local acceptance. |
| Client/tenant selection | PARTIAL | The API search filter supports `clientId`, but the current list UI exposes no client/tenant selector. Backend authorization tests do not establish an Angular tenant-selection flow. |
| Ticket search, detail, create | PASS, local only | Live list and number-filtered search rendered, detail returned 200, and ticket creation returned 200 with bearer and `Idempotency-Key` headers. No claim is made for every ticket type or provider. |
| Catalogue selectors | PASS, scoped | Live service/category/request-type options loaded through authenticated API requests. Subcategory options were empty for the selected disposable catalogue path. E1 dynamic fields are excluded. |
| Work-item completion / broader lifecycle UI | PARTIAL | No work items were available on the created/inspected disposable tickets; no unrelated work item was mutated. Broader classify/route/approval/resolve actions are not wired into Angular. |
| Authorization boundaries and error handling | PASS, scoped / PARTIAL | PostgreSQL tests cover work-item unauthorized completion, generic wrong/unknown-login 401, and normal RBAC denial after password rotation. A previously recorded fixture test proved scope denial and disabled-account 403 for an issued token; its optional local-auth fixture test was skipped in the latest run because fixture credentials were unavailable. Live browser results remain historical. Client/tenant selection remains absent. |
| Super Admin person/profile authorization | PASS, scoped | Direct API regression covers self and explicit transitive-subordinate reads/updates; unrelated-user and other-Super-Admin denial; ordinary-admin list/detail filtering; unauthenticated/direct API attempts; profile-payload role/permission/hierarchy fields; invalid and case-insensitive duplicate email rejection; audit actor/target/fields/before-after evidence; and manager assignment/cycle checks. |
| Role/account administration boundaries | PARTIAL, hardened | Distinct `USER_ADMIN`, `ROLE_ADMIN`, and `CLIENT_ADMIN` gates are applied inside serializable mutations. Direct API regressions deny `ADMINISTRATOR` grants, other-admin role removal, and self-role grants; standard membership regrant remains covered. No approved hierarchy/SoD model exists for other configured roles, so `ROLE_ADMIN` may still grant those roles to other users. |
| Email verification | BLOCKED / not implemented | Profile/create paths validate format and case-insensitive uniqueness, the unique database index enforces concurrent uniqueness, and changed addresses are set unverified. No identity-email sender contract, configured provider, confirmation endpoint, expiry/replay policy, or delivery configuration exists; no token is generated or exposed. |
| Logout and protected-route handling | PASS | Browser logout returned immediately to `/login`; browser storage was empty, and a subsequent `/app/tickets` visit redirected to sign-in. |
| Bootstrap and account-abuse controls | PARTIAL; not production-ready | One-shot PostgreSQL-only operator utility, rotation-required tokens, password-change endpoint, and five-per-minute per-IP limiter are implemented. Utility execution and live owner/second-review acceptance remain unverified; no persistent account lockout, distributed limit, or abuse monitoring exists. |
| E1 dynamic fields | EXCLUDED / UNSUPPORTED | No field-definition/options contract or dynamic form UI/serialization exists; excluded from G acceptance. |

Fresh validation commands and results:

```text
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests"
Exit code: 0; 14 passed, 0 failed, 1 skipped (optional local-auth fixture environment was absent in this invocation).

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac"
Exit code: 0; 1 passed, 0 failed, 0 skipped (process-only test fixture values; includes disabled-account denial).

dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code: 0; 0 warnings, 0 errors.

client> npm.cmd test -- --watch=false
Exit code: 0; 11 files; 30 passed, 0 failed, 0 skipped.

client> npm.cmd run build
Exit code: 0; Angular production build completed.

git diff --check -- Docs/REMEDIATION_TRACKER.md client/src/app/features/tickets/ticket-list.page.ts client/src/app/features/tickets/ticket-detail.page.ts client/src/app/features/tickets/create/ticket-create.page.ts client/src/app/layout/shell.ts
Exit code: 0; no whitespace errors. The new untracked shell.spec.ts also passed a trailing-whitespace check.
```

Live browser observations: login succeeded at the configured `localhost` origin; protected ticket search and detail and catalogue requests returned 200; filtered search rendered the requested result; ticket creation returned 200 with bearer and idempotency headers; logout cleared the in-memory session and immediately navigated to login. The unauthenticated ticket request returned 401. No work item was available for safe live completion. The UI exposed no client/tenant selector.

Before each PostgreSQL-backed batch and fixture cleanup, the live `psql` query returned exactly `pa_tickets_h_verify|pa_tickets_test_user`; cleanup removed exactly one disposable browser fixture user and the same identity was verified afterward. One test ticket created through the UI remains in the disposable database; it was not deleted to avoid rewriting its generated audit history. `.env.test.local` targets the approved disposable database. The inherited process environment variable was stale and named the forbidden shared database; it was never used for a connection. The API server log separately confirmed its startup identity as `pa_tickets_h_verify|pa_tickets_test_user`. The shared `pa_tickets_test` database and Oracle were not connected to or modified by this work; no destructive schema provisioner was run. Angular tests/build do not use PostgreSQL. `dotnet test PA.Tickets.sln --no-restore --nologo` was not run; no full-suite-clean claim is made.

The API launch command was `dotnet run --project src/PA.Tickets.Api/PA.Tickets.Api.csproj --no-build --launch-profile http`, with process-scoped `Database__Provider=PostgreSQL`, `PA_TICKETS_TEST_CONNECTION_STRING`, and `Auth__Local__SigningKey`. Startup confirmed Development and `http://localhost:5018`; `/health/live` and `/health/ready` returned 200, while anonymous `GET /api/v1/tickets?page=1&pageSize=1` returned 401. The API process was stopped after checks. ASP.NET logged its HTTPS-redirection warning because the HTTP-only launch profile does not configure an HTTPS port. The npm PowerShell shim initially failed because `npm.ps1` was blocked by the active execution policy; `npm.cmd` succeeded and execution policy was not changed.

### G continuation — bootstrap, rotation, and login-abuse protection

Current acceptance remains **PARTIAL**. The implementation now adds:

- Operator-only initial administrator provisioning using the existing `ADMINISTRATOR` role (no separate `SUPER_ADMIN` role exists). `tools/PA.Tickets.Bootstrap` is interactive, PostgreSQL-only, refuses command-line arguments, verifies the live database/user against process-only expected identity settings, requires typed confirmation, prompts for a temporary password without echo, refuses an existing active administrator or prior bootstrap marker, and records user/role/bootstrap events in a serializable transaction. No public unauthenticated bootstrap route was added.
- Additive `N_PASSWORD_ROTATION_REQUIRED` schema support. Initial bootstrap and admin-set passwords require rotation. Rotation-required tokens are blocked from other API routes until authenticated password change; public login remains usable even if a stale rotation token is sent. A successful change writes an audit event and requires login again for a token without the rotation claim.
- Login throttling at five requests per minute per observed remote IP, no queue, generic invalid-credential 401 responses, and generic 429. This is in-process/per-instance only; there is no persistent per-account lockout or monitoring.

Verification commands and results:

```text
dotnet restore tools/PA.Tickets.Bootstrap/PA.Tickets.Bootstrap.csproj --ignore-failed-sources --nologo
Exit code: 0; bootstrap project restored.

dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code: 0; 0 warnings, 0 errors.

dotnet build tools/PA.Tickets.Bootstrap/PA.Tickets.Bootstrap.csproj --no-restore --nologo --verbosity:minimal
Exit code: 0; 0 warnings, 0 errors.

client> npm.cmd test
Exit code: 0; 11 files, 32 passed, 0 failed, 0 skipped. Includes password-rotation UI validation and bearer/session regression coverage.

client> npm.cmd run build
Exit code: 0; Angular production build completed.

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --no-build --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~Local_login_rate_limits_repeated_failures_and_keeps_invalid_login_response_generic"
Exit code: 0; 1 passed, 0 failed, 0 skipped.

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --no-build --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~Authenticated_http_pipeline_executes_workflow_and_enforces_auth_idempotency_and_concurrency"
Exit code: 0; 1 passed, 0 failed, 0 skipped; API work-item completion, authorization, idempotency, and concurrency scenario.

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --no-build --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~Forced_password_rotation_blocks_other_requests_until_password_change_and_relogin"
First run: 0 passed, 1 failed. The API middleware incorrectly blocked relogin when the client resent its stale rotation-required bearer token. The middleware was corrected to allow the public login route.
Rerun: 1 passed, 0 failed, 0 skipped; verifies forced access denial, authenticated password change, stale-token restriction, a successful new login with the rotation claim cleared, and ordinary RBAC denial afterward.
```

Angular browser-to-API acceptance was not repeated in this continuation. The required local authentication fixture login, password, and signing-key variables are absent from the process environment, and `.env.test.local` contains only the database setting. No account or credential was invented and the bootstrap utility was not executed. The prior browser-verified list/search/detail/create/catalogue/logout flows remain historical evidence, but this continuation does not prove a live browser password-rotation or work-item completion flow. The new work-item completion evidence is the focused PostgreSQL API E2E plus the existing Angular mocked completion test only.

Before the PostgreSQL migration/test batches, a real `psql` connection returned `pa_tickets_h_verify|pa_tickets_test_user`. The local `.env.test.local` target was `pa_tickets_h_verify|pa_tickets_test_user`; the inherited `PA_TICKETS_TEST_CONNECTION_STRING` instead named `pa_tickets_test|pa_tickets_test_user` and was overridden in the test process with the approved local-file value. The additive PostgreSQL migration `database/migrations/postgresql/028_local_password_authentication.sql` was applied only after the identity check and the new rotation column was confirmed present. No Oracle script or destructive schema provisioner was run. The shared database was not connected to or modified. The forced-rotation test leaves only its generated audit evidence in the disposable database after removing its temporary user; it does not delete audit rows.

### G disposition and next step

Keep G **PARTIAL**. The operator provisioning tool is built but its actual one-shot execution, account-owner password delivery, second-review, and `USER_ADMIN` operation have not been exercised. Login throttling and generic failures have focused regression evidence, but account-based persistent lockout, distributed throttling, trusted-proxy production deployment, and monitoring are unresolved. Client/tenant selection is absent; the live Angular work-item completion flow and broader classify/route/approval/resolve UI remain unverified or unwired. OIDC/SSO stays deferred, E1 dynamic fields stay excluded, I1 stays CLOSED for scoped backend remediation, and F1 stays PARTIAL. Full solution tests were not run and production readiness is not claimed.

### G continuation — Super Admin profile hierarchy and final validation

The person/profile rule is implemented using explicit reporting relationships, not department/team membership. The base schema already has the self-referencing `N_MANAGER_USER_ID`; the EF row mapping and PostgreSQL test schema now map it, and additive migration `database/migrations/postgresql/029_user_profile_hierarchy.sql` adds the PostgreSQL-side phone, manager, email-verification, and normalized-email-uniqueness support. The corresponding Oracle migration is additive but was not executed or provider-validated.

`ADMINISTRATOR` is the current Super Admin marker; there is no separate `SUPER_ADMIN` role. Super Admin list/detail reads are limited to self and explicit transitive descendants, excluding other active administrators. Profile PATCH is limited to self or a non-administrator subordinate. Unrelated profiles and other Super Admins are denied at the service/API layer. Profile payloads cannot change role, permission, client, department/team, or manager fields. A separate `USER_ADMIN`-gated reporting-manager operation records explicit links and rejects cycles, inactive managers, self-management, and Super Admin targets. Profile and manager changes use serializable transactions. Separate role/client administration remains protected by its dedicated permissions; deactivation/password reset cannot target another Super Admin.

Email values are syntax-checked and unique case-insensitively. A changed email is marked unverified, but the repository has no configured verification policy, confirmation route, or delivery workflow. The implementation therefore does not claim verification acceptance; policy and confirmation remain open. Sensitive profile audit events include actor, target, changed field list, timestamp, and before/after values. The audit data includes personal information and requires restricted access.

The focused API regression exercises self/direct/transitive-subordinate read and update, unrelated and other-Super-Admin denial, unauthenticated/direct HTTP attempts, ignored privilege/hierarchy fields, case-insensitive duplicate rejection, audit evidence, explicit manager assignment, and cycle/other-administrator restrictions. One broader batch exposed an over-broad hierarchy check initially applied to dedicated role/client/active/password administration. The check was narrowed to profile access; explicit protection against deactivating or resetting another Super Admin was retained. The existing membership-regrant regression and the focused profile regression then passed. No assertions were weakened.

Fresh verification for this continuation:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~Administrator_profile_api_is_limited_to_self_and_explicit_transitive_subordinates"
Exit code: 0; 1 passed, 0 failed, 0 skipped.

dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~Effective_dated_memberships_can_be_regranted_without_losing_history_or_granting_duplicates"
Exit code 0; 1 passed, 0 failed, 0 skipped after narrowing the hierarchy rule to profile operations.

dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests|FullyQualifiedName~PostgreSqlWorkflowExecutionE2ETests|FullyQualifiedName~PostgreSqlCommunicationDeliveryTests"
Final run: exit code 0; 32 passed, 0 failed, 1 skipped (optional local-password fixture was not configured in this process).

dotnet build tools\PA.Tickets.Bootstrap\PA.Tickets.Bootstrap.csproj --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.

dotnet run --no-build --project tools\PA.Tickets.Bootstrap\PA.Tickets.Bootstrap.csproj
Expected fail-closed result: exit code 2 and "Run interactively without command-line arguments."; rejected before opening a database connection. No administrator was provisioned.

client> npm.cmd test
Exit code 0; 11 files, 32 passed, 0 failed, 0 skipped.

client> npm.cmd run build
Exit code 0; Angular production build completed.
```

During the first API/administration batch, the existing effective-membership regrant test failed because profile hierarchy had been incorrectly applied to a separate permission-gated role-assignment path (16 passed, 1 failed, 1 skipped). That failure was reproduced alone (1 failed), the scope was corrected, and the isolated test passed. A later run then identified the required denial for disabling another Super Admin (31 passed, 1 failed, 1 skipped); explicit other-Super-Admin protection was restored for account disable/password reset, after which the profile test passed alone and the final 33-test batch passed with only the optional fixture skip.

Database configuration was rechecked without printing credentials. `.env.test.local` identifies `pa_tickets_h_verify|pa_tickets_test_user`; the inherited process `PA_TICKETS_TEST_CONNECTION_STRING` identifies the forbidden shared target `pa_tickets_test|pa_tickets_test_user`. The stale inherited value was not used. Before every PostgreSQL-backed batch and isolated rerun, a real `psql -X -w -At` identity query returned exactly `pa_tickets_h_verify|pa_tickets_test_user`; the disposable-file connection was scoped to each test process. No shared database, Oracle connection, Oracle migration, or destructive provisioner was used. The test fixture and regression batches write only to the disposable database.

No current-session browser login, bootstrap execution, forced-rotation browser flow, or Angular work-item completion was verified in this continuation: the process-only authentication fixture was unavailable, and no approved live work-item/browser fixture was used. Prior browser login/search/detail/create/catalogue/logout results remain historical evidence, not newly reproduced results. Client/tenant selection and broader Angular lifecycle actions remain absent or incomplete. The one skipped test is disclosed; the full solution test suite was not run. Run the solution build and task-scoped diff checks after the final documentation/source edits before treating this report as current.

Keep G **PARTIAL**. Keep I1 **CLOSED for scoped backend remediation** and F1 **PARTIAL**; this continuation changes neither status. No production-readiness claim is made.

### RBAC scope-mapping regression coverage — 2026-10-15

The authorization service maps a permission's scope class to the effective membership scope
before evaluating the resource-specific scope. Added direct database-independent coverage for
the defined organization, department, department-admin, team, admin-group, record, and no-scope
mappings, including denial cases for unrelated or unknown scope types/classes. The pure mapping
was made `internal` for tests; its decision behavior is unchanged. These tests do not exercise
EF queries, scope persistence, client membership, or actual resource isolation.

Files changed for this continuation:

- `src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs`
- `tests/PA.Tickets.Domain.Tests/DatabaseAuthorizationScopeTests.cs`
- `Docs/REMEDIATION_TRACKER.md`
- `Docs/PA_TICKETS_API_E2E_ACCEPTANCE_MATRIX.md`
- `tests/TESTING.md`

The focused database-independent scope/role/approval selection passed **93/93**, with 0 failed
and 0 skipped; the API project build passed with **0 warnings and 0 errors**. Exact commands and
the corrected initial test expectation are recorded in the top RBAC scope-mapping entry of the
acceptance matrix. RBAC scope/provider scenarios remain unverified, migration 031/032 decisions
remain unresolved, and the acceptance baseline remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

### G authorization/email review — fresh continuation

Reviewed all `AdministrationController` user/profile/manager/account/role/client-membership routes and their `AdministrationService` operations. Profile authority remains distinct from account administration: users may update their own permitted profile; a Super Admin may read/update self and explicit transitive descendants only; ordinary `USER_ADMIN` list/detail paths exclude administrator profiles; department/team membership is never used as reporting authority. The separate manager route requires `USER_ADMIN`, denies administrator targets, self-management and cycles, and writes audit evidence. Account create, active/deactive, password reset, manager, role, and client-membership changes have separate permission gates and are executed with serializable transaction boundaries. Other-admin deactivation/password reset/role membership changes and self-role assignment/removal are denied. User profile input cannot alter role, permission, active status, or reporting manager.

The generic role assignment route now denies `ADMINISTRATOR` grants; initial Super Admin creation remains in the guarded bootstrap path. PostgreSQL role metadata inspection (read-only, after identity validation) found the configured built-in roles `ADMINISTRATOR`, `AUDITOR`, and `OPERATIONS`, all marked `N_SYSTEM_ROLE=true`. That column therefore does not encode a safe role hierarchy. There is no role hierarchy, approval/dual-control workflow, or general separation-of-duties policy for assigning other built-in roles. A holder of `ROLE_ADMIN` can still assign non-`ADMINISTRATOR` roles to other active users. This is an explicit remaining authorization-policy gap; do not infer the missing hierarchy or SoD policy.

Email update and account creation validate email syntax and perform a case-insensitive duplicate check; the database's unique index on `UPPER(S_EMAIL)` remains the race-safe integrity guard. Changing an address sets `N_EMAIL_VERIFIED=false`. No identity email sender contract or configured email provider exists (`Communication:Providers:Email` is empty in Development), and there is no verification-confirmation route or approved expiry/resend policy. Creating a challenge without delivery would either strand the account or require disclosing a token in an ordinary response. Therefore no challenge token/endpoint was added. Expiration, replay, and successful-confirmation behavior are not implemented or tested; add them only after an approved provider, policy, and delivery configuration are established.

Fresh final verification:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests|FullyQualifiedName~PostgreSqlWorkflowExecutionE2ETests|FullyQualifiedName~PostgreSqlCommunicationDeliveryTests"
Exit code 0; 32 passed, 0 failed, 1 skipped (Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac requires process-only auth fixture settings).

dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.

dotnet build tools\PA.Tickets.Bootstrap\PA.Tickets.Bootstrap.csproj --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.

dotnet run --no-build --project tools\PA.Tickets.Bootstrap\PA.Tickets.Bootstrap.csproj
Exit code 2, expected fail-closed non-interactive rejection before database connection; no provisioning occurred.

client> npm.cmd test
Exit code 0; 11 files, 32 passed, 0 failed, 0 skipped.

client> npm.cmd run build
Exit code 0; production build completed.

git diff --check -- <task-scoped files>
Exit code 0; no whitespace errors.
```

The bootstrap source was also reviewed: it requires explicit expected database and role, verifies the live identity, refuses if an active administrator or prior bootstrap audit marker exists, requires exact typed confirmation, reads passwords without echo or command-line arguments, and commits user/role/audit changes together. Only the non-interactive fail-closed path was exercised; no administrator was created.

No fresh browser acceptance was possible. Both `http://localhost:4200` and `http://localhost:5018/health/live` were unavailable at the check, and process-only `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `Auth__Local__SigningKey` were absent; no secrets were exposed or fabricated. The earlier browser login/search/detail/create/catalogue/logout evidence remains historical, not a result of this pass. No standalone product-spec document was present in `Docs`; the API contract map and current Angular service show `clientId` as an API search filter but no client/tenant selector. Dynamic fields remain excluded; broader lifecycle UI remains incomplete.

Every PostgreSQL batch/read-only role inspection was preceded by a real identity query returning exactly `pa_tickets_h_verify|pa_tickets_test_user`; identity was verified again after the final regression batch. `.env.test.local` was used as the source. The inherited process connection setting still points to `pa_tickets_test|pa_tickets_test_user` and was not used. No shared database, Oracle connection/migration, or destructive provisioner was used. The full solution test suite was not run. G stays **PARTIAL**; I1 remains **CLOSED** for scoped backend remediation, and F1 remains **PARTIAL**.

### G continuation — authorization review and current-session acceptance check (2026-10-13)

Reviewed the current `AdministrationController` routes and `AdministrationService` operations for user list/detail, profile update, reporting-manager changes, user creation, activation/deactivation, password administration, department/team administration, role and client membership changes. No alternate API user/profile mutation route was identified. The direct API regression `Administrator_profile_api_is_limited_to_self_and_explicit_transitive_subordinates` exercises self and explicit descendant access, unrelated-user and other-Administrator denial, ordinary `USER_ADMIN` list/detail filtering, unauthenticated access, profile field restrictions, duplicate/invalid email handling, hierarchy cycles, and protection against modifying another Administrator. Role tests cover denial of generic `ADMINISTRATOR` grants, self-role grant, other-Administrator role removal, and denied role administration without permission. Department/team membership is not used to establish reporting authority.

The focused database-backed API/admin regression batch was rerun against the approved disposable database:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests"
Exit code 0; 17 passed, 0 failed, 1 skipped. The skipped local-password login/RBAC test requires process-only fixture and signing-key settings that were absent; it is not counted as a pass.

dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.

dotnet build tools\PA.Tickets.Bootstrap\PA.Tickets.Bootstrap.csproj --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.

git diff --check -- Docs/REMEDIATION_TRACKER.md Docs/SECURITY_CONFIGURATION.md
Exit code 0; no whitespace errors.
```

Before and after the PostgreSQL test batch, a live `psql` query returned exactly `pa_tickets_h_verify|pa_tickets_test_user`; the connection configuration was derived from `.env.test.local`, not the inherited process connection setting. The shared `pa_tickets_test` database, Oracle, and the destructive schema provisioner were not used. Bootstrap source/build safety checks were reviewed, but the utility was not run interactively and no administrator was provisioned.

At this continuation, `localhost:4200` and `localhost:5018` were not listening. Process-only `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `Auth__Local__SigningKey` were absent (presence only was checked). No fresh browser or live authentication, password-rotation, logout, work-item completion, or profile-authorization flow was performed; any earlier browser observations remain historical. Angular tests/build were not rerun because no frontend files changed in this continuation. The full solution test suite was not run.

Email verification remains blocked: no identity-mail adapter/configuration or approved confirmation policy exists, so no challenge workflow or delivery claim is made. The security configuration now names the required provider and policy decisions. General role hierarchy and role-combination/SoD policy remain unapproved; current `ROLE_ADMIN` permits non-Administrator role assignment to other users, so this behavior remains an explicit governance gap rather than an inferred hierarchy. Client/tenant selection and broader lifecycle UI remain unverified/incomplete. G remains **PARTIAL**; I1 remains **CLOSED** for scoped backend remediation and F1 remains **PARTIAL**.

Follow-up authorization coverage in the same continuation: the ordinary `USER_ADMIN` direct-API profile test now also attempts to deactivate a Super Admin, reset that account's password, and change another Super Admin's reporting manager. All three requests returned `403 Forbidden`; existing assertions continue to cover list/detail/profile denial. No production authorization policy changed.

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~Administrator_profile_api_is_limited_to_self_and_explicit_transitive_subordinates"
Exit code 0; 1 passed, 0 failed, 0 skipped.

dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.

dotnet build tools\PA.Tickets.Bootstrap\PA.Tickets.Bootstrap.csproj --no-restore --nologo --verbosity:minimal
Exit code 0; 0 warnings, 0 errors.
```

The first compile attempt for the new assertions failed with `CS0136` because a new test local duplicated a later local name. The local was renamed and the focused test rerun successfully; no failing assertion or production-code defect remains from that attempt. The test batch's live PostgreSQL identity was verified immediately before and after as `pa_tickets_h_verify|pa_tickets_test_user`. Local API/UI services and process-only login/password/signing-key fixtures were rechecked and remain unavailable, so this does not constitute fresh browser acceptance.

### G continuation — full regression and role-governance decision required (2026-10-13)

After the added direct-API denial assertions, the broader API/admin PostgreSQL batch was rerun:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests"
Exit code 0; 17 passed, 0 failed, 1 skipped (optional local-password login/RBAC fixture and signing configuration absent).

dotnet test PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code 0; 192 passed, 0 failed, 1 skipped (the same optional local-password login/RBAC test). The solution invocation discovered the PA.Tickets.Domain.Tests assembly; the skip is not counted as a pass.
```

For each database-backed batch, `.env.test.local` was validated to name `pa_tickets_h_verify` and `pa_tickets_test_user`, and a live `psql` query returned exactly `pa_tickets_h_verify|pa_tickets_test_user` both before and after. No shared `pa_tickets_test` database, Oracle connection/migration, or destructive schema provisioner was used.

Role-governance closure requires an explicit product/security owner decision for each item: (1) which actor roles can grant and revoke each target role; (2) whether self-grant or self-revocation is allowed; (3) forbidden or controlled combinations of privileged roles; (4) grants/revocations requiring independent approval, the eligible approver, and separation-of-duties constraints; and (5) required audit fields and retention, including requester/actor, approver if applicable, target, role, action, effective period, reason, timestamp, and outcome. Until decided, do not infer policy from `N_SYSTEM_ROLE` or organizational membership. A temporary deny-by-default freeze on ordinary role assignment/removal (retaining read-only role inspection and the guarded bootstrap path) is proposed for owner approval, but was not implemented because it may disrupt legitimate administration and no approval was provided.

Current-session live browser acceptance remains blocked: `localhost:4200` and `localhost:5018` are not listening, and process-only `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `Auth__Local__SigningKey` are absent. Login, forced rotation/re-login, logout/protected-route denial, profile/account changes, and work-item completion were therefore not freshly verified in a browser. Email verification remains unimplemented pending the delivery and policy prerequisites documented in `SECURITY_CONFIGURATION.md`. G remains **PARTIAL**; I1 remains **CLOSED** for scoped backend remediation; F1 remains **PARTIAL**.

Test-project scope and optional fixture investigation: `dotnet sln PA.Tickets.sln list` reports five projects: API, Application, Domain, Infrastructure, and `tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj`. The solution test invocation discovered one test assembly, `PA.Tickets.Domain.Tests`; no other solution test project exists in this solution. Thus the 192-pass result covers that project, not additional undiscovered test projects or production projects as test assemblies. The skipped fixture test is `Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac`; its custom fact explicitly requires process-only `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`. All three were checked for presence only and were absent; `Auth__Local__SigningKey` was absent as well. No test credential or signing key was fabricated, and the skip remains disclosed.

Role-policy action remains pending owner approval. The proposal to temporarily deny ordinary role assignment/removal is **not approved and not implemented**. Product/security owner: please approve or reject that temporary restriction, and provide the role grant/revoke matrix, self-change rules, privileged-role combination constraints, independent-approval/SoD requirements, and mandatory audit evidence listed in `SECURITY_CONFIGURATION.md`. Do not change role-mutation behavior until this decision is recorded and authorized.

### G availability recheck (2026-10-13 17:11)

No application or test code changed after the full solution/API-admin test runs recorded above. Reconfirmed `dotnet sln PA.Tickets.sln list`: four production projects and one test project (`PA.Tickets.Domain.Tests`). Rechecked live services and process-only auth configuration: neither `localhost:4200` nor `localhost:5018` was listening; `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`, and `Auth__Local__SigningKey` were absent. No test batch was rerun because no code changed and the most recent counts remain current; no browser acceptance can proceed without services and an approved fixture. The temporary role-mutation freeze remains unapproved and unimplemented; role behavior is unchanged. G remains **PARTIAL**.

### G continuation — authentication skip prerequisites reviewed (2026-10-13 17:14)

Reviewed `Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac` and its custom skip attribute. When enabled, the test creates a randomized disposable user and role membership in the configured PostgreSQL test database and removes both in its `finally` cleanup; it does not require an existing administrator account or invoke the bootstrap tool. It requires `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`. The signing key must satisfy the API's local JWT minimum of 32 UTF-8 bytes. The test harness supplies local issuer, audience, and a 15-minute token lifetime itself; it also requires the already-verified `PA_TICKETS_TEST_CONNECTION_STRING`. Inspection of `.env.test.local` found only that connection setting and no auth fixture values. Process-variable presence checks also found all three fixture variables and `Auth__Local__SigningKey`, `Auth__Local__Issuer`, and `Auth__Audience` absent. No credential or key was generated, printed, or installed; the skipped test remains skipped.

At this recheck, `dotnet sln PA.Tickets.sln list` still reports four production projects and exactly one test project, `tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj`. Neither `localhost:4200` nor `localhost:5018` was listening. No code changed after the last reported tests/builds, so no additional test batch or database connection was made in this continuation; previously recorded test totals remain historical verified evidence, not a new run. Browser flows remain blocked. Role-governance approval remains unavailable; no role behavior changed. Email verification remains blocked pending the provider and policy requirements documented in `SECURITY_CONFIGURATION.md`. G remains **PARTIAL**.

### G availability recheck (2026-10-13 17:16)

Rechecked the current process environment and listeners. `localhost:4200` and `localhost:5018` were not listening. `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`, `Auth__Local__SigningKey`, `Auth__Local__Issuer`, and `Auth__Audience` were absent (presence-only checks; no values read or emitted). The skipped authentication test therefore remains unavailable, and no live browser acceptance was attempted. No application/test code changed and no database batch was run; recorded test counts remain the prior verified 192/0/1 full solution invocation and 17/0/1 API/admin batch. No role-policy approval was supplied; the proposed temporary role-mutation freeze remains unapproved and unimplemented. Email verification remains blocked on the provider contract and policy documented above. G remains **PARTIAL**.

### G availability recheck (2026-10-13 17:18)

Freshly checked service ports and process-scoped configuration: `localhost:4200` and `localhost:5018` remain unavailable. `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`, `Auth__Local__SigningKey`, `Auth__Local__Issuer`, and `Auth__Audience` remain absent; only presence was checked. As no code or fixture changed, no test or database batch was repeated. Fresh browser acceptance remains blocked. No product/security-owner role-policy approval was provided, so the proposed role-assignment/removal freeze remains unapproved and unimplemented. Email verification remains unimplemented pending the provider and policy decisions already listed. G remains **PARTIAL**.

### G local service and UI verification (2026-10-13)

- Angular was started with the repository `start` script at `http://localhost:4200`; a port check confirmed it was listening. Fresh Angular unit tests passed (11 files, 32 passed, 0 failed, 0 skipped), and the production build succeeded.
- Fresh browser inspection showed the sign-in page. Visiting `/app/tickets` while unauthenticated redirected to `/login?returnUrl=%2Fapp%2Ftickets`. This verifies only sign-in rendering and unauthenticated route guarding; it does not verify authentication, bearer-token use, logout-after-login, or any protected API/UI operation.
- The API `http` Development profile was attempted with the supported launch settings. It failed closed before binding because `Auth:Local:SigningKey` was absent; `localhost:5018` was not listening, and a fresh `/health/live` request was unavailable. Development configuration already supplies issuer/audience/lifetime, but no signing key; no `UserSecretsId` or approved local signing secret was found. The project owner must provide an approved local secret source and inject `Auth__Local__SigningKey` into the API process as protected configuration (at least 32 UTF-8 bytes), without committing or logging its value. No signing key was invented and authentication was not weakened.
- The required `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY` remain an independent prerequisite for the optional test fixture. No values were printed or provisioned. The local auth/RBAC fixture test and all authenticated browser flows—including password rotation/re-login, logout/protected-route denial after login, profile/account administration, role operations, and work-item lifecycle—remain BLOCKED until the API secret and approved disposable fixture are supplied. No live work-item was created or mutated.
- Two fresh PostgreSQL-backed .NET test batches completed. The API/admin regression filter passed 17, failed 0, skipped 1 (18 total); `dotnet test PA.Tickets.sln` passed 192, failed 0, skipped 1 (193 total). The only skipped test was `Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac`, which has no approved `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY` fixture. Both batches built the four production projects and the sole test project, `tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj`. Live identity was checked before and after each batch; all four outputs were exactly `pa_tickets_h_verify|pa_tickets_test_user`. The filtered batch command text was not retained in the test output; the full invocation was `dotnet test PA.Tickets.sln`.
- Role mutation behavior was not changed. Role-grant/revocation authority, self-change rules, prohibited combinations, independent approval/SoD, and required audit evidence still require explicit product/security-owner decisions. Email verification remains unimplemented pending an approved delivery provider, confirmation contract, and the documented expiry, rate-limit, replay, address-change, access, audit, and failure policies. G remains **PARTIAL**.

### G API availability continuation (2026-10-13 17:41)

- The local `.env.test.local` contains a protected `Auth__Local__SigningKey` and a PostgreSQL connection for the disposable verification database. Values were loaded into the API process environment only; no secret values were printed. Before starting the API, the parsed target and live PostgreSQL identity were both exactly `pa_tickets_h_verify|pa_tickets_test_user`.
- Started the Development API with `dotnet run --project .\src\PA.Tickets.Api\PA.Tickets.Api.csproj --launch-profile http --no-build`. It bound to `http://localhost:5018`; `/health/live` returned HTTP 200 and `/health/ready` returned HTTP 200. Angular remained listening at `http://localhost:4200`.
- From the actual Angular browser page, a cross-origin unauthenticated `GET http://localhost:5018/api/v1/tickets` returned HTTP 401. This confirms the API's protected-route denial from the browser, not successful sign-in or authenticated UI acceptance. The sign-in page remains displayed.
- The live PostgreSQL identity was independently rechecked after readiness verification and remained exactly `pa_tickets_h_verify|pa_tickets_test_user`. No database-backed test batch was run in this continuation.
- The three approved auth fixture variables (`PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`) are absent from `.env.test.local` and the process used for the earlier fixture check. Do not attempt credentialed browser flows until the owner supplies an approved disposable login/password fixture; do not use guessed credentials. The optional auth/RBAC test remains skipped.
- The current working-tree `appsettings.Development.json` also contains a `SigningKey` property. It was not relied on for startup because the protected process environment overrides configuration. Preserve the uncommitted file as-is, but do not commit or share a signing secret from that file; remove it and rotate the key through the approved secret process before any commit if it is a real credential.
- Role-governance decisions and email-verification prerequisites remain unresolved as recorded above. G remains **PARTIAL**; no production-readiness claim is made.

### G acceptance progress update (2026-10-13 17:52)

- The API and Angular services remain available at `http://localhost:5018` and `http://localhost:4200`. The API's `/health/live` and `/health/ready` each returned HTTP 200. The actual Angular browser page remained on the sign-in screen; an unauthenticated browser request to `/api/v1/tickets` returned 401, verifying protected-route denial only.
- Fresh Angular verification from `client` completed: `npm.cmd run test -- --watch=false` passed 32 tests across 11 files (0 failed, 0 skipped); `npm.cmd run build` succeeded.
- A fresh full solution invocation, `dotnet test PA.Tickets.sln`, passed 192, failed 0, skipped 1 (193 total). The single skipped test was `Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac`, because its approved login/password/signing-key fixture is absent. The run built the four production projects and sole test project `tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj`.
- Database identity checks immediately before and after this full solution batch both returned exactly `pa_tickets_h_verify|pa_tickets_test_user`. The PostgreSQL connection was parsed from `.env.test.local`, and only the DB test connection was injected into the test process. The local Development `Auth__Local__SigningKey` was deliberately not injected into the test host.
- One earlier fresh full-suite attempt in this continuation failed the health/correlation test because `.env.test.local`'s Development `Auth__Local__SigningKey` was mistakenly inherited by the test process: the test API ran in `Testing`, selected local JWT configuration, and had no matching local issuer/audience. Isolation reproduction exposed a separate diagnostic defect: the API test host synchronously read `StandardError` after starting asynchronous redirected-stream reads, masking the child startup exception. `ApiHost.StartAsync` now reports its already-captured output queue instead. The focused test reproduced the configuration contamination before the corrected environment; the subsequent full suite passed with proper environment isolation. This was not an application behavior change.
- No authenticated browser flows were attempted because the owner-approved disposable auth fixture (`PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`) is still absent. Login/bearer handling, rotation, post-login logout, authorized/denied profile and account operations, role operations, and work-item lifecycle remain BLOCKED. No credentials were guessed and no work items were created or mutated.
- The only source edit for this progress update is the API test-host diagnostic correction in `tests/PA.Tickets.Domain.Tests/PostgreSqlApiE2ETests.cs`. Role policy remains unapproved/unmodified; email verification remains blocked by the provider and policy decisions. G remains **PARTIAL**.

### G backend configuration update (2026-10-13 18:21)

- Rechecked the current Development configuration without emitting secret values: local authentication is enabled, a local signing key is present and meets the 32-byte minimum, PostgreSQL is selected, and the configured CORS origin matches `http://localhost:4200`.
- The API was already running at `http://localhost:5018`; fresh `/health/live` and `/health/ready` checks both returned HTTP 200. Angular remained listening at `http://localhost:4200`. No restart or database-backed test was necessary in this check.
- The shared browser remains at the sign-in page. The auth fixture variables for a disposable login/password/signing key remain absent from `.env.test.local`; therefore no credentials were entered and authenticated flows remain unverified. The last fresh full-suite result remains 192 passed, 0 failed, 1 skipped; the auth/RBAC test was skipped for that missing fixture.
- Security handling: the current uncommitted `src/PA.Tickets.Api/appsettings.Development.json` contains a local `SigningKey`. Do not commit or share that value. Move it to an approved protected, untracked local secret source and rotate it before any commit if it is a real key. This does not block the currently running local API, but it is not safe configuration for source control.
- Role governance and email-verification decisions remain open. G remains **PARTIAL**; API availability alone does not satisfy authenticated browser acceptance or production readiness.

### G local authentication test continuation (2026-10-13 18:22)

- Product direction for this local test run: continue using the currently configured Development signing key; defer considering a rotation until later. The value was not read out or reproduced. It remains in an uncommitted Development settings file and must not be committed or shared.
- Fresh browser check of the sign-in form: submitting with both fields empty was stopped by client-side validation ("Enter your login name." and "Password must be at least 8 characters."); no login HTTP request was made and no account was tested.
- API health remains the previous fresh 200/200 for liveness/readiness; the browser remains unauthenticated. The approved disposable login/password fixture is still absent locally, so successful login, token storage, password rotation, post-login logout, account/role administration, and work-item acceptance remain blocked. No credentials were guessed.
- No tests, DB writes, or application behavior changes were made in this continuation. Keep Workstream G **PARTIAL** pending approved test credentials and authenticated acceptance, plus the separate governance and email-policy decisions.

### G authenticated acceptance handoff (2026-10-13 18:22)

- The user intends to configure the approved disposable login/password fixture locally and requested that authenticated testing continue in a separate proper session. No fixture values were supplied in chat or read during this session; the values had not yet been configured at the last presence check.
- Defer login/RBAC integration testing and authenticated browser acceptance to that next session. At that time, verify fixture-variable presence only, verify `.env.test.local` and live PostgreSQL identity before and after every database-backed batch, and then run the focused auth test before broader regression/browser flows. Never put fixture values in chat, logs, or committed files.
- Current known service evidence remains API liveness/readiness HTTP 200 and both localhost ports 4200 and 5018 listening. The sign-in form is visible and empty-submit client validation was verified; no credentialed flow was attempted.
- G remains **PARTIAL**. Role governance and email-verification policy remain outstanding, and the Development signing key still needs approved secret storage/rotation before any commit containing configuration.

### G acceptance readiness recheck (2026-10-13 18:34)

- Both local services are still available: Angular `http://localhost:4200` and API `http://localhost:5018`; `/health/live` and `/health/ready` each returned HTTP 200.
- The browser remains on the sign-in page. The three approved auth fixture variables (`PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`) are absent from both the current process and `.env.test.local` (presence-only check). No authentication attempt or database-backed batch was made.
- Authenticated browser and focused fixture-test acceptance remain blocked until those approved disposable credentials are configured locally. Existing unauthenticated-route denial and prior automated results remain as previously recorded; they are not new authenticated acceptance.
- No code or configuration behavior changed. G remains **PARTIAL**, with role-governance and email-verification decisions still outstanding.

### G authentication fixture and browser acceptance (2026-10-13 18:41)

- Inspected the fixture test and `LocalAuthenticationFixtureFactAttribute`: required environment variables are `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`; the test supplies issuer, audience, and token lifetime itself, inserts a randomized local user/Operations membership, exercises login and RBAC, then deletes its rows in `finally`.
- Confirmed `.env.test.local` is Git-ignored by `git check-ignore -v .env.test.local` (`.gitignore:37`). Generated a unique local login, password, and 64-byte UTF-8 signing key with the OS cryptographic RNG; values were written only to ignored `.env.test.local`, were not printed or transmitted in tool output, and all three settings are now present. Test execution explicitly loaded the database connection and only the fixture variables into its process; it did not inherit Development `Auth__Local__SigningKey`.
- Focused command: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac`. Result: **1 passed, 0 failed, 0 skipped**. The test verified valid login/token issuer, audience, claims and expiry; unauthenticated admin denial; allowed department/team search; foreign-department and admin/password-reset denials; and denial after account deactivation.
- Live PostgreSQL identity immediately before and after the test was exactly `pa_tickets_h_verify|pa_tickets_test_user`. No shared database or Oracle was used.
- Fresh browser login using the local disposable Operations identity succeeded and navigated to `/app`. The Angular ticket UI emitted a GET with an Authorization header, but its initial unfiltered search returned **403**. This is consistent with the server rule requiring an explicit ticket-search scope; the UI request omitted department/team (and all other filters). In the same authenticated browser session, an explicitly scoped read-only GET `/api/v1/tickets?page=1&pageSize=1&departmentId=501&teamId=601` returned **200**, a valid page shape, and one item. The live UI default-search denial remains a user-facing limitation; do not report the ticket-list screen itself as passing.
- Browser logout navigated to `/login`, left zero auth/token-named entries in both localStorage and sessionStorage, a subsequent anonymous API request returned **401**, and visiting `/app/tickets` redirected to `/login?returnUrl=%2Fapp%2Ftickets`.
- For browser login, one separately marked disposable Operations user was seeded using the same fixture credentials and explicit department/team scope. Read-back found exactly one marked active user with an active Operations membership (persistence verified). It was removed immediately after browser checks; post-cleanup read-back found zero marked users. Live database identity before seed, after seed, before cleanup, and after cleanup was exactly `pa_tickets_h_verify|pa_tickets_test_user`. No ticket/work-item was created or changed.
- The temporary fixture utility and its generated `bin`/`obj` directories were removed. `.env.test.local` remains Git-ignored with the generated local fixture available for this verification setup; never commit or disclose its values. No product behavior was modified by this acceptance run.
- Login, bearer-authenticated scoped API read, logout, and protected-route denial are now freshly verified. Unfiltered ticket-list UI returns 403 and remains unresolved. Password rotation, account/profile administration, roles, ticket creation/completion, and work-item lifecycle were not browser-tested. Role-governance approval and email-verification provider/policy remain outstanding. G remains **PARTIAL**.

### Configurable RBAC implementation preparation (2026-10-14)

- Prepared an implementation draft for five protected built-in roles, custom-role administration,
  module/action permission mappings, explicit membership scopes, effective-dated Admin Head
  delegation, and an Angular role/permission editor. Added provider-specific PostgreSQL/Oracle
  migration 031 drafts. Neither migration was executed; no database connection or write was made.
- Hardened the draft backend policy to fail closed for missing/unknown permission metadata and
  missing effective membership scope (except an explicitly `NONE` scope). Super Admin permission
  mappings are locked until a separately reviewed two-person workflow exists. Delegation creation
  and removal require the Super Admin-only scope-governance permission.
- Admin Head authorization is tied to the exact delegated Admin identity and an active `ADMIN_GROUP`
  scope. The new PostgreSQL integration regression covers an assigned cross-department Admin,
  an unassigned Admin, denial to create delegation, and denial of ticket access across department
  boundaries. It was compiled but not executed because the approved live database identity was not
  revalidated and migration 031 is unapplied.
- Verification evidence from the 2026-10-14 implementation-preparation supplement:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter
  FullyQualifiedName~RoleAuthorizationPolicyTests -p:BaseOutputPath="<temp>\pa-tickets-rbac-check\bin\"`
  passed **12/12** and compiled Domain, Application, Infrastructure, API, and test projects into
  isolated temporary outputs. `npm.cmd test` passed **40/40** across 12 Angular test files, and
  `npm.cmd run build` passed. The initially attempted in-tree .NET build hit output-file locks from
  the already-running API process; no process was stopped. Targeted Problems checks reported no
  errors. Provider scripts, PostgreSQL integration, browser flows, and full route-by-route
  acceptance remain **NOT RUN**. The acceptance matrix now includes each new RBAC administration
  endpoint as individually `BLOCKED` and updates the inventory to 225 routes.
- Existing `OPERATIONS`/`AUDITOR` and legacy `ADMINISTRATOR` behavior has not been integration
  tested against the proposed metadata. Runtime policy narrows the legacy Administrator role to
  ticket read/update; no account or membership transition was made. Migration conflicts, exact
  provider schema assumptions, Oracle DDL rollback behavior, scope/list-query coverage, and
  full module/action granularity still require review.
- RBAC work remains **PARTIAL / NOT ACCEPTED**. Do not apply migration 031 or provision/transition
  a privileged account before the permission matrix, migration scripts, rollback limitations, and
  approved target identity have been independently reviewed and explicitly approved.

### Resource-read authorization continuation

- Traced attachment metadata/version/download reads, evidence mutations, communication reads,
  workflow and approval runtime history, ticket-scoped audit reports, operational-metadata audit
  streams, and report-export download paths. Attachment downloads use the authenticated API and
  the attachment service checks the owning ticket's scoped `TICKET_READ` permission before
  retrieving the requested version or opening its content. No signed/presigned download URL
  generator or unauthenticated file-delivery route was found in the source search.
- Communication reads check `TICKET_READ` against the parent ticket before returning recipient,
  message, or latest-attempt data. Workflow/approval runtime-history paths require their
  respective read permission and then scoped `TICKET_READ` before loading child history. Auditor
  ticket streams require `AUDITOR_VIEW` and derive ticket IDs through the service's ticket-scope
  filter; operational-metadata integrity streams are intentionally governed by `AUDITOR_VIEW`.
- Reproduced a report-export authorization-ordering gap: `ReportExportService.CreateAsync`
  inserted and saved a `RUNNING` export row before `ReportingQueryService.QueryAsync` checked the
  report's permission. The regression test initially failed with the export DbContext's
  no-provider error before reaching the denying report-query stub. The service now performs the
  same report-definition permission check before any export-row mutation; report queries retain
  their own check. The deterministic regression uses a DbContext with no provider, so it proves
  denial occurs before EF access without opening any database connection.
- Focused database-independent selection passed **106/106**, including the new
  `ReportExportAuthorizationTests`; API build passed with **0 warnings and 0 errors**. These tests
  do not provide provider, authenticated endpoint, browser, or end-to-end coverage. Existing
  attachment cross-ticket/provider tests were not run under the no-database constraint.
  Commands: focused `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj
  --no-restore --nologo --filter
  "FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests|FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~DurableWorkflowAuthorizationTests|FullyQualifiedName~TicketLifecycleAuthorityTests"
  -p:BaseOutputPath="<temp>\bin\" -v minimal`; API
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo
  -p:BaseOutputPath="<temp>\bin\" -v minimal`.
- No other reproduced code defect was established in the inspected read paths. Privileged
  administration controllers derive actor IDs only from authenticated subject claims, while
  service methods enforce operation-specific user/role/scope permissions. Sensitive proposal
  creation and decisions use approval-authority and independent-decision-maker guards; Admin Head
  delegation writes fail closed until independent approval exists. These are source observations,
  not provider-backed results. Migration 031/032 and all database/live acceptance remain blocked.
  Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

### KPI and route-scope continuation (2026-10-15)

- Continued source tracing across ticket list/detail, specialized ticket reads and relationships,
  attachments/evidence, communications, report/KPI/dashboard retrieval, exports/schedules,
  administration, workflow/approval administration, and runtime workflow/approval endpoints.
  Ticket search applies an actor-derived scope before repository filtering; ticket details and
  specialized records check the owning ticket; attachments and communications check the parent
  ticket; workflow and approval runtime history require both configuration-read authority and
  scoped ticket-read authority. Privileged controllers pass actor IDs from authenticated claims,
  with service-side permissions for reads and writes. No separate comments endpoint or signed
  attachment URL path was found.
- Found a concrete KPI least-privilege inconsistency in `KpiService.Authorized`: its assigned-ticket
  branch granted any ticket whose `CurrentAssignee` matched the actor, without the department,
  team, or effective-client constraints used by `ReportingQueryService.AuthorizedTickets`.
  Client memberships were also filtered only by `Active`, ignoring `ValidFrom`/`ValidTo`. KPI
  aggregates could therefore include out-of-scope assigned tickets and tickets for expired or
  future client memberships. The KPI query now uses the same department/team/client constraints
  for assigned tickets and filters client memberships by their effective window.
- Added database-independent expression tests for effective-client boundaries, assigned-ticket
  department/team/client boundaries, and preservation of export-create and report-query permission
  checks before EF work. The expression predicates are the ones used by the production KPI query;
  these tests do not verify EF provider translation or persisted authorization behavior.
- Final focused command
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo
  --filter "FullyQualifiedName~KpiClientMembershipScopeTests|FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests|FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~DurableWorkflowAuthorizationTests|FullyQualifiedName~TicketLifecycleAuthorityTests"
  -p:BaseOutputPath="<temp>\bin\" -v minimal` passed **110/110**. API build
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo
  -p:BaseOutputPath="<temp>\bin\" -v minimal` passed with **0 warnings and 0 errors**.
  Early test-development iterations included a compile attempt before the new predicate existed, an
  invalid fixture expectation (the queried client itself granted scope), and an invalid
  `ChangeTracker` assertion on a providerless EF context. Those test issues were corrected; no
  application behavior was excluded from final validation.
- A source-level object-ID enumeration consideration remains: an existing inaccessible ticket or
  attachment can map to 403 while a missing ID maps to 404 (`UnauthorizedAccessException` vs
  `KeyNotFoundException`). No existing requirement directs these protected resource routes to
  conceal existence, so the HTTP contract was not changed. Decide whether uniform 404 behavior is
  required before treating this as a defect; no protected resource data is returned on denial.
- The protected controller/service trace is source-only. Cross-client/dept provider-backed
  filtering, ticket/attachment ID enumeration behavior in hosted HTTP, signed-link expiry/signature
  enforcement (no such link path was found), and all privileged/API route behavior still require
  explicit provider/HTTP evidence. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

### 2026-10-15 report-schedule authorization follow-up

- Confirmed a report-scheduling permission inconsistency: `ReportSchedulesController` derives the
  schedule owner from the authenticated principal, and all schedule reads are owner-filtered, but
  `ReportScheduleService.CreateAsync` did not require the permission declared by the selected
  report. The background handler later calls export creation, which does require that permission;
  a user without it could persist an active schedule that predictably fails on execution.
- Schedule creation now checks the selected report's existing permission before adding or saving
  schedule state. A providerless regression test verifies the permission and actor and proves the
  denied path does not reach EF persistence. The change does not grant report access or alter report
  query/export policy.
- Rechecked the adjacent paths: dashboards delegate data to the guarded KPI/report query services;
  report and auditor query paths filter tickets in their query population using effective client
  windows and the same department/team/client assignment constraints; ticket search applies the
  actor-scoped criteria before repository filtering. Export status/download remains owner-scoped by
  `RequestedBy`. No additional code-only defect was confirmed.
- The existing hosted harness can test ASP.NET authentication with a fake authentication handler,
  but it does not provide provider-backed ticket/report fixtures. A service stub could prove the
  `[Authorize]` gate but would not faithfully prove inaccessible-ID resource scope; no such test was
  added. Existing 403-versus-404 behavior remains a product/API policy decision. Provider-backed
  query translation and cross-scope isolation require an approved isolated test database and
  database-test approval; neither was used here.
- Focused command
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo
  --filter "FullyQualifiedName~KpiClientMembershipScopeTests|FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~ReportScheduleAuthorizationTests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~TicketLifecycleAuthorityTests"
  -p:BaseOutputPath="<temp>\bin\" -v minimal` passed **26/26**. API build
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo
  -p:BaseOutputPath="<temp>\bin\" -v minimal` passed with **0 warnings and 0 errors**.
  Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**; no scenario was promoted.

### 2026-10-15 authorization evidence-gap checklist

The following gaps are evidence blockers, not newly inferred source defects. Existing unit and
source-review findings above are not repeated as new implementation work.

| Area and current matrix status | Evidence already established | Exact closure evidence still required | Minimum prerequisites/approval |
|---|---|---|---|
| Ticket list/detail and scoped reporting/KPI queries (`PARTIAL` where only anonymous/source/unit evidence exists) | Actor scoping is applied before ticket search repository filtering; KPI/report predicates are exercised over in-memory LINQ; anonymous route checks exist for ticket routes. | Provider-backed tests for in-scope allow and out-of-scope deny across department, team, client, assignment, effective membership dates, list totals/pages, and detail reads; hosted API requests must confirm response codes and no protected body on denial. | Approved disposable PostgreSQL test database/schema, explicitly approved schema provisioning and test-data writes, effective/inactive user-role/client fixtures, hosted API fixture authentication, and cleanup approval. Do not substitute in-memory LINQ for provider translation. |
| Attachments/evidence (`PARTIAL` anonymous checks; authenticated routes `BLOCKED`) | Source trace shows parent-ticket `TICKET_READ` before metadata/version/content access; anonymous requests return 401. | Provider-backed allow/deny for another ticket/client/department, metadata, version selection, and download; hosted HTTP assertion that denial returns no metadata/content; verify missing-versus-inaccessible ID response once the API existence-concealment policy is decided. | Same approved isolated PostgreSQL schema and test actors/tickets, temporary test-only storage root with cleanup approval, authenticated hosted API, explicit decision on 403 versus uniform 404. No signed-link flow exists to test. |
| Report exports and schedules (`PARTIAL`/`BLOCKED` route rows) | Export and schedule creation denial-before-EF unit tests; `PostgreSqlApiE2ETests.Export_and_schedule_http_contracts_persist_and_enforce_owner` already covers authorized export/schedule creation, owner download, and other-owner 404 behavior in a PostgreSQL-backed hosted API test; `PostgreSqlReportScheduleExecutionTests.Worker_enqueues_only_due_active_schedules_and_retries_without_duplicate_execution` covers persisted worker execution/export behavior. Neither PostgreSQL test was run in this pass. | Run the existing provider/API tests only after approval; additionally assert denied report permission persists no schedule/export, worker execution retains the schedule owner as export actor, and denied export download does not open storage. | Approved disposable PostgreSQL schema and fixture accounts, test-only local storage root, authenticated API host, schema/data/storage cleanup approval. Do not claim the existing provider tests passed in this run. |
| Administration, workflow/approval configuration, and runtime history (`PARTIAL` anonymous-only reads; authenticated/write rows `BLOCKED`) | Source-level actor derivation and service guards; deterministic policy tests; hosted test currently covers authentication/password change only. | Route-level hosted tests for authenticated deny/allow, claim-derived actor, permission/role boundaries, scoped resource reads, and denial before mutation; provider-backed persisted proposal initiator/approver separation, expiry, audit, and scope outcomes. | Approved auth fixtures and isolated PostgreSQL schema. Proposal tests require migration 032 architecture/repair decisions resolved and migration approved; configurable permission/scope routes require migration 031 decisions and approval. Independent approver and disposable records must be explicitly authorized. |
| RBAC proposal authority and expiry (scenarios 3, 21 `PARTIAL`; scenarios 4, 19 and proposal route rows `BLOCKED`) | Pure policy tests and source calls establish current-authority/separation checks; >50-candidate test is policy-level only. | Service/provider tests for persisted expired-state/audit across more than 50 records and HTTP model-binding/status/error mapping; transaction/isolation and concurrent single-apply behavior require separately authorized concurrency validation. | Resolve migration 031/032 repair decisions first; approve isolated provider/schema and actor fixtures. Concurrency test explicitly requires separate approval and remains out of scope here. |
| Existing route-table anonymous `PARTIAL` rows across admin, catalogue, workflow/approval, SLA, reports, schedules, ticket, attachment, and export GETs | Anonymous requests were denied with 401 for the listed routes. | Authenticated contract tests for permission-specific allow/deny and authorized response for each route group. These rows are not closed by anonymous denial or a controller `[Authorize]` attribute alone. | Hosted API with test authentication and faithfully bound service fixtures; database-backed dependencies additionally require approved isolated PostgreSQL fixtures as above. |

### Schedule authorization test boundary

`ReportScheduleAuthorizationTests.Create_checks_report_permission_before_persisting_schedule_state`
proves a denied actor is checked against the selected report permission before the providerless
context can be touched. The current constraint prohibits database access, including an ephemeral
SQLite test database, so no provider-backed path was run. The existing
`PostgreSqlApiE2ETests.Export_and_schedule_http_contracts_persist_and_enforce_owner` exercises
authorized creation, owner access, and different-owner denial through a PostgreSQL-backed host;
`PostgreSqlReportScheduleExecutionTests.Worker_enqueues_only_due_active_schedules_and_retries_without_duplicate_execution`
exercises persisted worker execution. Both require approved PostgreSQL access and were not run.
Once approved, run those tests and add/assert the denied-permission no-persist case, storage-not-
opened behavior on denied export download, and persisted schedule owner/report/recurrence. Preserve
the recurrence calculation contract; do not promote the schedule acceptance row until the existing
HTTP/provider test passes with the new permission guard and denial persistence evidence is present.

### Evidence review and validation (2026-10-15)

- Rechecked `KpiClientMembershipScopeTests`: they distinguish a valid membership at its start and
  before expiry from an expired membership at the exclusive end, a future membership, inactive
  membership, and another user's membership. They distinguish one in-scope assigned ticket from
  tickets outside department/team/client constraints. This is in-memory `IQueryable` evaluation,
  not EF SQL translation or persisted isolation.
- Rechecked `ReportExportAuthorizationTests` and
  `ReportScheduleAuthorizationTests`: providerless contexts prove denial precedes EF access. They
  do not prove authorized persistence, export ownership against persisted rows, storage access
  ordering on a provider, or hosted response mapping.
- Rechecked `HostedAuthenticationAuthorizationTests`: actual test-host routing, authentication,
  authorization, login, password-change actor/current-password behavior, and middleware path are
  covered. It does not host ticket/report/attachment services and cannot establish their resource
  scope.
- Focused command
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo
  --filter "FullyQualifiedName~KpiClientMembershipScopeTests|FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~ReportScheduleAuthorizationTests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~TicketLifecycleAuthorityTests"
  -p:BaseOutputPath="<temp>\bin\" -v minimal` passed **26/26**, 0 failed, 0 skipped.
  API build with isolated output passed with **0 warnings and 0 errors**. Scoped `git diff --check`
  and related test whitespace checks passed.
- No database, provider test, migration, provisioning, Oracle, live API, browser, or concurrency
  test was run. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


### 2026-10-15 user administration continuation and catalog preflight

- Added an API-backed user administration page, permission-gated navigation, and focused component
  coverage for user loading, creation, and profile updates. Activation and temporary password
  changes require confirmation; password assignment uses the existing forced-rotation endpoint.
- The authenticated app shell now delegates access decisions to child-route permission guards, so
  `USER_ADMIN` users do not also need the unrelated `TICKET_READ` permission to reach user admin.
- Verified Angular suite: **54/54**; Angular production build succeeded with the pre-existing
  role editor stylesheet budget warning (529 bytes over 4 kB). Focused database-independent .NET
  security/RBAC selection: **107/107**. API build: **0 warnings, 0 errors**.
- A read-only catalog transaction inspected the configured `pa_tickets_h_verify` database. It
  confirmed missing permission metadata/effective dates, client catalogue, role scopes, delegation
  and proposal tables, and migration ledger; role, permission and role-permission rows lack creator
  and effective-history columns; audit actor is non-nullable without actor type. No application rows
  were read or changed; shared `pa_tickets_test` was not accessed.
- Migration history, truthful historical metadata, system audit actor semantics, and client ID
  preservation remain unresolved. Do not execute 030/031/032. Database-backed tests, migrations,
  provisioning and acceptance status changes remain gated on those decisions and explicit approval
  of an exact disposable test target and write operations.
- Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


- Source now removes the Super Admin role-code resource bypass. Active permission grants, including `ScopeClass=NONE`, require an effective explicit organization scope on the membership for organization-wide access. Search, reporting, KPI, and auditor filters also check that scope. The single-organization sentinel is `ORGANIZATION` with null organization ID; it is never provisioned automatically. Scope grants remain blocked pending an independent approval path. DB-backed validation is blocked by the verified schema mismatch and has not passed.



### PostgreSQL incident containment � 2026-10-16

The integration test harness previously treated `PA_TICKETS_TEST_CONNECTION_STRING` as sufficient authorization. Full test discovery therefore selected `pa_tickets_h_verify`, and test setup could write fixture data before a schema error halted execution. The evidence is incomplete beyond the observed 10 tickets and 15 audit rows; no cleanup was authorized.

`PostgreSqlTestEnvironment` now requires both `PA_TICKETS_RUN_POSTGRESQL_TESTS=I_UNDERSTAND_POSTGRESQL_TEST_WRITES` and an exact independent `PA_TICKETS_APPROVED_TEST_TARGET` descriptor matching host, port, database, and user parsed from the connection string. It fails before provider setup and emits no credentials. Four guard tests passed as part of the 29-test database-independent selection. Provider, migration, provisioning, fixture, cleanup, and E2E statuses remain blocked pending separate approval.

Superseding incident note (2026-10-16): earlier catalog-preflight entries saying no application rows were read or changed describe the read-only preflight only. A later accidentally broad integration-test invocation wrote fixture records to `pa_tickets_h_verify` before failing. The observed side effects and incomplete evidence boundary are documented in the incident-containment section; no cleanup or acceptance promotion occurred.

### RBAC role and permission capability review � 2026-10-16

The source audit confirms backend/API/Angular workflows for custom roles, role metadata, persisted permission catalogue reads, atomic grant/revoke requests, sensitive proposals, and backend scope enforcement. It does not establish that the five built-in roles are present by default in the deployed database: the seed drafts are unexecuted and the observed schema is incompatible. Provider persistence, privileged membership/scope writes, and database-backed E2E remain blocked. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

RBAC migration preparation (2026-10-16): added review-only PostgreSQL drafts 033-038 with explicit dependency order, no fabricated historical data, gated built-in role provisioning, and an intentionally blocked audit-actor gate. No database operation was executed.

Master E2E roadmap and static migration compatibility review completed (2026-10-16). Migration 036 was corrected to match RbacProposalRow and AdministrationService; migration 034 requires fail-closed application alignment; organization-scope proposals and audit actors remain blocked.

Effective-date blocker resolved at source level (2026-10-16): nullable starts plus explicit grant status, fail-closed authorization predicates, and 4 focused regression tests. Historical reconciliation and PostgreSQL verification remain blocked.

Effective-grant compatibility audit completed: all role-permission paths require ACTIVE status and known effective start; Oracle equivalent schema is an additional cross-provider blocker.

Final static RBAC readiness review (2026-10-16): documented field-level Oracle/PostgreSQL effective-grant parity gaps, Oracle migration ownership blocker, historical reconciliation specification, audit actor contract blocker, and exact migration approval evidence. No database operations occurred.

### API authorization coverage phase — 2026-10-16

Added `Docs/PA_TICKETS_API_ENDPOINT_INVENTORY.md` (232 routes / 26 controllers). Source review confirmed server-side authorization on business routes, intentional public login/status endpoints, effective-grant fail-closed checks, spectator mutation denial, and explicit organization scope. The remaining representative hosted HTTP 401/403/no-mutation matrix is providerless work only; provider/database behavior remains blocked. Acceptance is unchanged at 8 PASS / 3 PARTIAL / 13 BLOCKED.

### Intended module mapping audit — 2026-10-16

Source-only module mapping audit completed in `Docs/PA_TICKETS_MODULE_MAPPING_AUDIT.md`. The review found no basis to remap legacy role IDs or client references. Reporting/KPI permission ownership, attachment/evidence permissions, change/problem module ownership, configuration metadata mapping, and communication/notification boundaries require owner confirmation. No application or database change was made.

### Phase 1 module mapping validation — 2026-10-16

The operational-metadata discrepancy was resolved by source evidence, not by code change. Migration 031 and the repositories consistently map ticket status/transition/priority reads to `TICKET_READ` and mutations to `TICKET_ADMIN`; `CONFIG_ADMIN` remains reserved for protected configuration. No permission grants, scopes, roles, or schemas changed. Remaining reporting, file/evidence, change/problem, communication/notification, UI, and legacy-role decisions remain blocked pending owner approval.

### Reporting/KPI/export authorization review — 2026-10-16

The 20-route reporting family was reviewed without database access. Current `TICKET_READ` mappings are supported by the operational reports and KPI requirements; `AUDIT_EXPORT` remains limited to audit records. Export status/download and report-schedule authorization semantics, execution auditing, independent approval, and Angular screens remain unresolved. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

### Final export lifecycle decision proposal — 2026-10-16

Added the decision-ready export lifecycle contract document. No implementation is approved: retrieval revocation, artifact authorization context, audit events, cleanup concurrency, sensitive-export approval, and seven-day retention require explicit owners. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

### Export lifecycle implementation update — 2026-10-16

Safe source changes implemented current report-permission rechecks, non-mutating expiry observation, and fail-closed downloads without artifact authorization context. Focused provider-independent tests passed 5/5 and the API build passed with 0 warnings/errors. Audit persistence, cleanup worker, artifact authorization snapshot, retention approval, and provider-backed verification remain blocked. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

## Export lifecycle remaining gaps (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_LIFECYCLE_REMAINING_GAPS.md` for the source-only assessment of historical-artifact authorization, audit lifecycle, and asynchronous cleanup/concurrency. Owner and provider decisions remain blocked; acceptance stays 8 PASS / 3 PARTIAL / 13 BLOCKED.

## Historical export authorization design (2026-10-16)

See `Docs/PA_TICKETS_HISTORICAL_EXPORT_AUTHORIZATION_DESIGN.md`. Regeneration versus an authorization-context snapshot remains an owner and schema decision; downloads remain fail-closed and acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

## Historical export strategy decision (2026-10-16)

The design document now contains the source-backed decision matrix and approval checklist. Immediate implementation remains blocked; downloads remain fail-closed and acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

## Export audit contract assessment (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_AUDIT_CONTRACT_DESIGN.md`. Audit and cleanup implementation remains blocked pending actor, transaction, event, retry, and provider/schema decisions.

## Export cleanup design assessment (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_CLEANUP_DESIGN.md`. Physical cleanup remains blocked pending retention, leasing, concurrency, audit, and provider/schema decisions.

## Consolidated export lifecycle implementation plan (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_LIFECYCLE_IMPLEMENTATION_PLAN.md` for the dependency-aware implementation sequence and release gates. Historical authorization, audit, cleanup, provider/schema, and E2E work remain unresolved; acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

## Export lifecycle owner approval checklist (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_LIFECYCLE_OWNER_APPROVAL_CHECKLIST.md`. All authorization, audit, cleanup, retention, provider, and E2E decisions remain PENDING or BLOCKED; acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.

## PostgreSQL schema reconciliation audit - 2026-10-16

A source-only audit compared current EF mappings, `ExecutionDbContext`, reporting DDL, PostgreSQL migration files, provider selection, and test provisioning. Report export tables are mapped in `ReportExportRows.cs` and created only by root migration 026; no PostgreSQL migration copy exists. The provisioning script calls that root file directly. Ticket-aging evidence tables/columns are absent from the checked-in PostgreSQL DDL. Current effective-grant mappings require the 034 columns and fail-closed semantics, but the original schema baseline does not contain them. Migration 033 is only a review-draft ledger and no migration runner or applied baseline is established.

No database or provider was contacted. Required next action is a read-only catalog fingerprint against an explicitly approved disposable PostgreSQL target, followed by owner approval of the baseline/adoption procedure and immutable migration order. RBAC migrations 033-037 remain review-gated, migration 038 remains blocked, and evidence persistence remains a later schema decision. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
### PostgreSQL migration-plan continuation - 2026-10-21

The existing reconciliation record now contains the current source-grounded dependency order and reporting/ticket-aging drift inventory in `Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`. No migration is approved or applied; the next prerequisite is an explicitly approved read-only catalog fingerprint for a dedicated disposable PostgreSQL target. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
### Definitive PostgreSQL schema-build plan - 2026-10-21

The current repair record now defines the proposed source-only order: approved PostgreSQL baseline adapter, reporting migration 026, PostgreSQL 028 and 029, then ledger/RBAC migrations 033 -> 034 -> 035 -> 036 -> 037. Drafts 030-032 are superseded, root 027 lacks a PostgreSQL counterpart and requires separate review, 038 remains blocked, and ticket-aging evidence remains a separately gated future migration. The reported `pa_tickets_e2e_verify` target is not independently verified here. No database operation was performed; acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
### Foundational PostgreSQL dependency review - 2026-10-21

The final source-only review found that the checked-in foundational adapters are data-bearing, not pure schema stages. `postgresql-schema.sql` and `postgresql-slice1-schema.sql` include inserts, while slice 1 overlaps migrations 028/029. The first separately approvable stage therefore requires a DDL/data split and explicit ownership of overlapping columns before any build operation. The reported `pa_tickets_e2e_verify` emptiness remains unverified by Codex. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.