# PA Tickets test classification

## UNIT

The existing domain suite covers ticket lifecycle, evidence, SOD, waiting/resume, SLA clock behavior, workflow joins/conditions, and sequential approval rules.

## APPLICATION

`ExecutionConfigurationTests` executes the real `ConfigurationWorkflowRouter` and validates deterministic specificity precedence and equal-specificity ambiguity rejection. These tests use the application/infrastructure implementation with in-memory configuration only; they do not verify database persistence.

## API / CONCURRENCY / E2E

PostgreSQL API, persistence, concurrency, lifecycle, workflow, approval, SLA-operation, and durable-job E2E tests require an explicitly approved test-database identity and pre/post identity verification. They were not run in the latest continuation. Oracle-specific migration, locking, rollback, and deployment validation remain intentionally unexecuted.

## ANGULAR

The Angular project has a Vitest unit-test target configured in `client/angular.json`. Run
`npm.cmd test` from `client` for the full component/service/guard suite, or use
`npm.cmd test -- --include src/app/path/to/file.spec.ts` for a focused spec. The initial 2026-10-14
baseline passed 40 tests across 12 files; the latest approval-continuation run passed 51 tests
across 12 files (recorded under Configurable RBAC below). Unit tests and a production build do not
count as real browser-to-API-to-PostgreSQL acceptance.

## ORACLE

Oracle migration, persistence, locking, concurrency, rollback, and deployment validation tests are final-gate tests and are intentionally not executed in this environment.

## Configurable RBAC

The earlier test counts below are historical for the initial RBAC editor; the newer independent
approval continuation and current focused/full-suite results are recorded afterward.

`RoleAuthorizationPolicyTests` are safe, database-independent tests of role ceilings, custom-role
limits, fail-closed metadata, Spectator read-only grants, and legacy Auditor/Operations behavior.
`DatabaseAuthorizationScopeTests` directly verifies the pure mapping from permission scope class
to effective membership scope, including fail-closed unrelated/unknown values; it does not exercise
database scope persistence or resource filtering.
`TicketLifecycleConfiguredTransitionTests` includes database-independent service-level execution
authorization tests: denied executor targets must not mutate/save the ticket, and an authorized
target retains the distinct authenticated actor and executor identities. These tests do not prove
provider-backed destination scope or authenticated HTTP behavior.
`ApiSecurityConfigurationTests` verifies the authentication controller exposes only login as
anonymous while password change retains action-level authorization; this is endpoint-metadata
coverage, not a hosted authentication pipeline test.
`HostedAuthenticationAuthorizationTests` starts a local Kestrel host around the actual auth
controller, ASP.NET Core endpoint authorization, rate limiting, and password-rotation middleware,
with a stub `ILocalAuthenticationService`. It verifies anonymous login/change-password, subject
forwarding, and current-password outcomes without database access. It is not the full API host or
database-backed acceptance.
`DurableWorkflowAuthorizationTests` verifies the pure current-assignee guard used before work-item
completion idempotency replay. It does not exercise EF persistence or replay behavior against a
provider.
`PostgreSqlAdministrationAuthorizationTests` include database-backed scope/delegation and
membership-history coverage; these must not be run unless the separately approved test-database
identity and pre/post identity checks are in effect. A role-removal audit assertion was added to
that PostgreSQL suite and remains unverified. The role-editor component spec now expects a single
atomic permission-batch request, rejected-snapshot reload, no submission for high-impact grants,
and allows revoking existing sensitive mappings to narrow access. On 2026-10-14, the focused .NET
role-policy suite passed 19/19 using isolated build output, the focused Angular role-administration
suite passed 8/8, and the Angular production build passed. No database-backed atomicity test was run.
The latest independent-approval continuation adds database-independent tests for proposal target
eligibility, direct-grant gating, built-in role ceilings, every defined transition edge, expected
version checks, retry actor/reason rules, active-permission snapshot comparison, expiry boundary,
and digest mutation across every immutable payload field. The 2026-10-15 continuation adds a
database-independent 51-candidate expiry-predicate test and a test demonstrating that recomputing
an unkeyed digest over modified payload data passes the hash check. These prove policy/hash
behavior only; they do not execute the administration service or a provider. The prior focused
.NET proposal-policy suite passed **48/48**; the 2026-10-15 isolated-output continuation run passed
**50/50**. The test suite compiles the API/Application/Infrastructure projects but performs no
database-backed integration.
The focused Angular role-administration suite passed **17/17** and the full Angular suite passed
**51/51 across 12 files**. The API project build passed with 0 warnings/errors. The Angular
production build succeeded with a style-budget warning (role-administration stylesheet exceeds
its 4 kB budget by 529 bytes). A first focused Vitest invocation concurrent with .NET tests failed
before collection with `[vitest-pool]: Timeout starting forks runner`; a serial retry passed.
Problems checks found no errors in touched files. Migrations 031/032 remain unexecuted and
unrehearsed. No database-backed service/API, migration, audit-failure, concurrency, or browser
acceptance is claimed. Direct sensitive grants remain blocked; the proposal flow covers only grants
permitted by current eligibility and ceilings. SUPER_ADMIN grants, privileged memberships,
role-scope grants, and Admin Head delegation mutations remain blocked by the service.
The payload digest covers immutable proposal data but is unkeyed and cannot protect against a
database principal able to rewrite both proposal and audit data. Concurrent provider behavior,
transaction rollback, and ambiguous commit/retry still require approved provider-backed tests.
Create idempotency keys are kept in same-tab `sessionStorage` by a SHA-256 request fingerprint,
survive reloads until a successful response, and are cleared after success; the component-recreation
test covers this browser behavior only. Closing the tab before recovery and provider commit
ambiguity still require API/provider validation.
The corrected 24-scenario evidence table in the acceptance matrix records 8 PASS (narrow
source/unit/UI behavior only), 3 PARTIAL, and 13 BLOCKED; no route or scenario is end-to-end PASS.
The service returns all authorized proposal rows without pagination but persists expiry for at
most 50 per list call; beyond-batch rows can remain stored as PENDING until a later list or decision
call. Approve/reject/cancel use the same expiry predicate regardless of list position. List-triggered
expiry records the authenticated caller that triggered lifecycle processing (not a decision-maker),
and explicitly states that no human decision was made. The audit-user foreign key and transactional
behavior remain provider-unverified.

The focused database-independent security/lifecycle selection
`dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -p:BaseOutputPath="<unique-temp>\bin\" -v minimal`
passed 16/16. Middleware tests directly invoke the existing forced-password-rotation gate and cover
blocked protected paths, the two allowed POST paths, case-insensitive matching,
trailing-slash/wrong-method rejection, unauthenticated requests (including a constructed rotation
claim), and authenticated requests without that claim. The lifecycle test asserts that a ticket
with evidence cannot be resolved while a work item remains incomplete and that the failed
transition does not set `ResolvedAt` or emit a resolution event. These are direct unit tests, not
hosted API, provider, or browser acceptance.

## PostgreSQL integration environment

The local PostgreSQL service is available. A credential-safe verification harness is available at `tests/scripts/verify-postgresql.ps1`; it requires `PA_TICKETS_TEST_CONNECTION_STRING` and never prints it. Connection/database setup is environment-dependent; use the harness output rather than assuming a configured connection. No PostgreSQL result is evidence of Oracle compatibility. See `tests/POSTGRESQL_INTEGRATION.md`.

## PostgreSQL provider adapter (development/integration only)

Infrastructure now selects PostgreSQL only when `Database:Provider=PostgreSQL`; it reads `ConnectionStrings:TicketsPostgres` or the process environment variable `PA_TICKETS_TEST_CONNECTION_STRING`. The default path remains Oracle 19c with `UseOracle`. Npgsql EF Core 8.0.11 is referenced alongside the Oracle provider. Oracle migrations are not executed against PostgreSQL. A real PostgreSQL schema/persistence smoke test remains pending because the current model still contains Oracle-specific sequence/default and schema assumptions that require a dedicated PostgreSQL schema strategy before `EnsureCreated` or repository persistence can be claimed.

## PostgreSQL schema adapter

Added `tests/scripts/postgresql-schema.sql` and the scoped `provision-postgresql-schema.ps1` runner. It creates repeatable PostgreSQL-native sequences, core ticket/work-item/assignment/event/audit tables, foreign keys, public-ID uniqueness, work-item uniqueness, and active-assignment uniqueness only in `pa_tickets_test`; Oracle migrations are not used. The provisioner runs real PostgreSQL insert/commit and rollback smoke checks. PostgreSQL repository and API integration tests separately exercise persistence and runtime behavior against the provider-specific schema adapter. These are **POSTGRESQL INTEGRATION** checks, not evidence of Oracle compatibility.

## Resource-read and export authorization

`ReportExportAuthorizationTests` verifies that an export request is denied by the report's existing
permission before `ReportExportService` touches EF state. The test deliberately constructs an
`ExecutionDbContext` without a provider; it does not open a database connection or prove persistence
behavior. The resource-read continuation's database-independent focused selection
(`dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo
--filter "FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests|FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~DurableWorkflowAuthorizationTests|FullyQualifiedName~TicketLifecycleAuthorityTests"
-p:BaseOutputPath="<temp>\bin\" -v minimal`) passed **106/106**. The API build
(`dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo
-p:BaseOutputPath="<temp>\bin\" -v minimal`) passed with **0 warnings and 0 errors**.
Attachment cross-ticket, version, download, workflow-history, approval-history, and
privileged-administration persistence paths still require approved provider-backed/API acceptance
and are not established by this unit result.

`KpiClientMembershipScopeTests` directly exercises the expression predicates used by
`KpiService.Authorized`: effective client membership start/end boundaries and exclusion of assigned
tickets outside the actor's department/team/client scope. These use in-memory `IQueryable` values,
not an EF provider; SQL translation and provider-backed isolation remain unverified. The
2026-10-15 follow-up focused security selection passed **110/110**, including these tests and both
report export/query permission-ordering regressions. The API build passed with **0 warnings and
0 errors**; neither result is provider-backed acceptance.

`ReportScheduleAuthorizationTests` verifies that report-schedule creation checks the selected
report's permission before an owner-scoped schedule can be persisted. Its providerless EF context
ensures a denied request stops at authorization; it does not exercise EF persistence or job
execution. The 2026-10-15 schedule/report follow-up selection was:

`dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo
--filter "FullyQualifiedName~KpiClientMembershipScopeTests|FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~ReportScheduleAuthorizationTests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~TicketLifecycleAuthorityTests"
-p:BaseOutputPath="<temp>\bin\" -v minimal` — **26 passed, 0 failed, 0 skipped**.

The corresponding API build passed with **0 warnings and 0 errors**. The test project has a custom
hosted authentication pipeline test, but no hosted test with the real database-backed ticket
authorization service; a mocked service would not establish object-scope enforcement. Provider
translation, persisted cross-scope isolation, inaccessible-ID HTTP behavior, and scheduled
execution still require approved isolated database/API validation and are not covered by these
tests.

### Authorization evidence limits and deferred test approvals

- `KpiClientMembershipScopeTests` proves in-memory predicate results at inclusive membership start,
  exclusive expiration, future/inactive/other-user membership cases, and assigned-ticket
  department/team/client boundaries. It does not prove EF translation or database isolation.
- `ReportExportAuthorizationTests` and `ReportScheduleAuthorizationTests` use contexts without a
  provider. They prove denial precedes EF access; they do not prove authorized persistence,
  cross-owner export retrieval, file-storage ordering, or HTTP status mapping.
- `HostedAuthenticationAuthorizationTests` uses an actual test host for login/password-change and
  ASP.NET authentication middleware. It does not host the ticket/report/attachment services, so it
  cannot prove resource-scope denial. A mocked controller service test must not be reported as that
  evidence.
- The test project references EF Core SQLite, but using even an in-memory SQLite database is
  database-backed testing and was intentionally not done under a no-database-access instruction.
  Existing `PostgreSqlApiE2ETests.Export_and_schedule_http_contracts_persist_and_enforce_owner`
  covers authorized export and schedule creation, owner download, and cross-owner 404 behavior;
  it requires an approved PostgreSQL test connection and was not run here.
  `PostgreSqlReportScheduleExecutionTests` covers persisted worker/export execution, also unrun.
  Neither proves that a denied report permission persists no schedule/export; add that provider-
  backed assertion when database testing is approved.
- Minimum approval before provider/API work: a named disposable PostgreSQL test database/schema;
  explicit permission to provision schema and insert/delete only dedicated test fixtures; approved
  test identities/roles/client scopes and authenticated API test-host configuration; a temporary
  storage root with cleanup permission for export/attachment artifacts; and approval of the exact
  test commands. Keep connection strings in environment variables and never print them. Resolve
  migration 031/032 repair decisions before tests depending on those schema changes. Provider
  translation/cross-client and department isolation should run against PostgreSQL, not be inferred
  from LINQ-to-Objects or SQLite translation. Any concurrency test requires separate approval.

Required next tests after approval:

1. Provider integration: ticket list count/page, ticket detail, KPI/report query scope, client
   membership effective dates, and ticket/attachment parent-resource boundaries with in-scope
   allow and out-of-scope deny fixtures.
2. Hosted HTTP: anonymous 401 plus authenticated permission deny/allow for ticket, attachment,
   reports, exports, schedules, administration, workflow, and approval route groups; assert
   response body/data and mutation/storage access behavior, not only attributes.
3. Export ownership: create as actor A; verify A can read/download, actor B cannot, and denied
   download does not open storage. Use a disposable storage root.
4. Schedule behavior: run the existing authorized create/owner-isolation hosted test and worker
   integration test; add a denied-permission provider assertion proving no row persists; verify
   owner/report/recurrence, worker export ownership, and unchanged recurrence behavior. Both
   existing provider tests are unrun in this pass.
5. RBAC proposals: after migration decisions/approval, verify persisted initiator/approver
   separation, expiry state/audit including >50 expired rows, and endpoint response mapping.
   Concurrency/single-apply coverage is separate and not authorized by the current task.

The 2026-10-15 evidence-review selection passed **26/26** database-independent tests, with 0 failed
and 0 skipped; API build passed with **0 warnings and 0 errors**. Scoped diff/whitespace checks
passed. No provider/API/database acceptance status was promoted.


## User administration screen and current RBAC verification (2026-10-15)

The user administration route uses the existing `USER_ADMIN` API permission. Its focused Angular
spec covers loading and department/team filtering, user creation through the API, and profile
updates. It does not test a hosted API or database persistence.

- `npm.cmd test -- --watch=false` from `client`: **54 passed, 0 failed, across 13 files**.
- `npm.cmd run build` from `client`: succeeded. The existing role-administration stylesheet
  exceeded its configured 4 kB budget by 529 bytes.
- `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests|FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~DatabaseAuthorizationScopeTests" -v minimal`: **107 passed, 0 failed, 0 skipped**.
- `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo -v minimal`: succeeded with **0 warnings and 0 errors**.

A catalog-only, read-only PostgreSQL transaction inspected `pa_tickets_h_verify` before the accidental test invocation; it confirmed that the role-permission metadata/effective-date columns, client catalogue, scope/delegation/proposal tables, and migration ledger are absent in that configured database. The later integration invocation did open PostgreSQL and write fixture rows before failing; its side effects and limits are documented below. No database-backed E2E success is claimed and no acceptance status changed.


RBAC repair verification note (2026-10-15): the database-independent scope test class passed
29/29 and the API build passed. An accidentally broad full test-project invocation started
PostgreSQL integration tests on `pa_tickets_h_verify` and was stopped after schema errors. A
read-only check found 10 inserted ticket fixtures (IDs 820–822, 829–834, 840) and 15 additional
audit rows, including associated problem, attachment, and evidence events. No cleanup was run.
Do not rerun PostgreSQL tests or clean these rows without separately approved target and operation.
No database-backed E2E success is claimed.




## PostgreSQL integration safety guard (2026-10-16)

All PostgreSQL test methods call `PostgreSqlTestEnvironment.EnsureConfigured()` before creating a provider, host, fixture, migration, or cleanup scope. The helper now fails closed unless the process explicitly sets `PA_TICKETS_RUN_POSTGRESQL_TESTS=I_UNDERSTAND_POSTGRESQL_TEST_WRITES` and supplies `PA_TICKETS_APPROVED_TEST_TARGET` matching the parsed non-secret descriptor `host=<host>;port=<port>;database=<database>;user=<user>` from `PA_TICKETS_TEST_CONNECTION_STRING`. A database name alone, credentials alone, or test discovery does not authorize execution. The descriptor excludes passwords and connection strings are never logged.

The safe opt-in procedure is to obtain separate approval for the exact disposable target, verify host/port/database/user through an independent release or operator record, set the three process-only variables, and then run a narrowly selected PostgreSQL test command. Provisioning, migrations, fixture creation, and cleanup still require their own explicit target and operation approval; the helper does not perform any of them. Ordinary unit tests remain runnable without PostgreSQL configuration.

The guard tests were run by method-name filter only (the reachability test was excluded): **29 passed, 0 failed** including four pure PostgreSQL guard tests and the organization-scope tests. No PostgreSQL connection was opened in that run. Do not run the full test project while the target remains unapproved.


RBAC capability-test boundary (2026-10-16): the role administration Angular tests and prior focused source tests cover API request construction, validation, proposal routing, reload/error behavior, Spectator restrictions, and explicit scope policy. They do not prove role seeding, EF persistence, permission revocation against a live database, or hosted HTTP authorization. The PostgreSQL opt-in guard described above must be satisfied before any provider-backed test is considered.

Migration review boundary: database/migrations/postgresql/033-038 are design artifacts only. Do not execute them, the superseded 030-032 drafts, provisioning, or PostgreSQL tests without separate target and operation approval.

The E2E master plan classifies provider-backed scenarios as blocked until migration compatibility, target approval, and database-independent application alignment are complete.

Focused effective-grant regression tests passed 52/52 with no PostgreSQL provider setup. They cover unknown, valid, expired, future, and boundary instants; SQL translation/materialization still requires a separately approved disposable target.

Effective-grant audit reviewed DatabaseAuthorizationService, LocalAuthenticationService, and AdministrationService paths. Provider SQL translation/materialization and Oracle schema compatibility are not covered by providerless tests.

Final readiness boundary: checked-in Oracle and PostgreSQL RBAC schemas lack the effective-grant columns required by the current source contract. Provider-backed translation/materialization and migration validation require a separately approved target and operation.

## API authorization coverage phase (2026-10-16)

Read-only source inventory covers 232 routes across 26 controllers. All business controllers are server-authorized; login and system status are intentionally public. Existing focused provider-independent tests cover middleware, effective grants, role policy, scopes, report/export denial, audit permission, and login/rotation. Angular mocked-API specs cover login/rotation, route and permission-driven administration flows, 401/403 messaging, and loading/error behavior. No database-backed test, PostgreSQL test, full test project, or browser E2E was run for this phase. Latest safe results remain 52/52 focused tests and a zero-warning API build; acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.
