# PA TICKETS — Complete API and Real E2E Acceptance Checklist

## Purpose and agent instruction

Use this Markdown file as the authoritative checklist for route-by-route API testing and full browser-to-API-to-PostgreSQL acceptance. Read it before testing. Inspect actual controllers, DTOs, authorization rules, services, repositories, migrations/schema, and existing tests. Test each listed route individually when safe prerequisites exist.

This is a **verification-first** exercise. Do not invent missing API contracts or broaden product scope to make a test pass. Make only the smallest targeted fixes for reproduced defects, add regression tests, and rerun the affected live scenario.

## Hosted authentication and workflow replay authorization — 2026-10-15

- Added a hosted Kestrel test with the actual authentication controller, MVC endpoint routing,
  ASP.NET Core authentication/authorization, login rate-limit metadata, and password-rotation
  middleware. It uses an in-memory test authentication service and no database. It verifies
  anonymous login succeeds, anonymous password change is rejected with 401, the authenticated
  subject ID is the actor supplied to password change, and correct/incorrect current-password
  results are 204/401 respectively. The rotation-required claim is present on authenticated test
  requests, proving the existing password-change exception remains usable; middleware rules were
  not changed.
- Strengthened ticket-execution denial assertions to verify no save, assignee, last-executor,
  ticket-status, work-item assignment, or event mutation occurs.
- Durable work-item completion previously returned idempotent replay before checking that the
  caller is the current assigned user. The assignment guard now precedes replay evaluation.
  Database-independent tests cover null/unassigned, mismatched, and matching actor identities.
  No persisted replay scenario was run.
- Workflow creation/activation checks current ticket permission before replay and validates
  configured workflow selection; durable approval and RBAC proposal decisions were traced for
  current approver/authority and separation-of-duties checks. Database transaction isolation,
  audit persistence, replay races, and actual provider scope remain unverified.
- Final focused database-independent selection passed **129/129**, 0 failed, 0 skipped:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~HostedAuthenticationAuthorizationTests|FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~DatabaseAuthorizationScopeTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests|FullyQualifiedName~DurableWorkflowAuthorizationTests" -p:BaseOutputPath="<unique-temp>\bin\"`.
  API build succeeded with **0 warnings and 0 errors**. Outputs were isolated and removed.
- Hosted test coverage applies to its test-only Kestrel pipeline and stub service; it is not full
  application startup, provider-backed, browser, or end-to-end acceptance. Baseline remains
  **8 PASS / 3 PARTIAL / 13 BLOCKED**.

## Authentication and execution authorization audit — 2026-10-15

- `AuthenticationController` previously placed `[AllowAnonymous]` on the whole controller while
  `ChangePassword` also declared `[Authorize]`. Controller-level anonymous metadata overrides the
  action authorization metadata in ASP.NET Core. A regression test failed against this state.
  Anonymous access is now applied only to `Login`; `ChangePassword` retains `[Authorize]` and its
  existing actor/current-password validation.
- Ticket execution previously used request `ExecutorUserId` to assign the work item and set last
  executor identity after only checking ticket mutation permission. Two service-level regressions
  failed before the fix: unauthorized executor assignment was accepted, and assignment
  authorization was not called. Execution now invokes existing scoped `EnsureCanAssignAsync`
  before mutation, retaining separate authenticated actor and executor identities.
- The final combined database-independent regression selection passed **125/125**, 0 failed, 0
  skipped:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~DatabaseAuthorizationScopeTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests" -p:BaseOutputPath="F:\PROGRAMS\Python\Temp\pa-tickets-auth-final-tests-d8a2a5e1464846ebbace513beb366596\bin\"`
- API build:
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo --verbosity:minimal -p:BaseOutputPath="F:\PROGRAMS\Python\Temp\pa-tickets-auth-final-api-ca49534beac34887a9353ce14e94739b\bin\"`
  succeeded with **0 warnings and 0 errors**. Both used unique temporary outputs.
- These tests do not establish hosted HTTP behavior, provider-backed assignment scope, database
  isolation, authenticated E2E, concurrency, or browser acceptance. Acceptance remains
  **8 PASS / 3 PARTIAL / 13 BLOCKED**; migrations 031/032 remain blocked.

## Ticket execution assignment authorization — 2026-10-15

- Authorization trace found that ticket execution accepted request `ExecutorUserId` as assignee
  and last executor after checking only the authenticated actor's ticket mutation permission.
  Unlike explicit reassignment, it did not invoke the scoped assignment authorizer.
- Added deterministic deny and allow regressions. The pre-fix focused run failed both tests: an
  unauthorized target was accepted, and an allowed target did not invoke assignment validation.
- `TicketLifecycleService.ExecuteAsync` now checks the supplied target using the existing
  `EnsureCanAssignAsync` against the persisted ticket and work-item destination before mutation.
  The request/response contract and separate authenticated event actor/executor identity remain
  unchanged.
- Focused database-independent selection passed **112/112**, 0 failed, 0 skipped:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~DatabaseAuthorizationScopeTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests" -p:BaseOutputPath="<unique-temp>\bin\"`
  API build succeeded with **0 warnings and 0 errors**:
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo --verbosity:minimal -p:BaseOutputPath="<unique-temp>\bin\"`.
- This does not prove DB-backed scope enforcement, HTTP behavior, provider isolation, or full
  lifecycle acceptance. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**; migrations 031/032
  remain blocked.

## RBAC scope-mapping regression — 2026-10-15

- Added direct deterministic tests for the shared permission-scope-class to membership-scope
  mapping used by database authorization: organization, department, department-admin, team,
  admin-group, record, and `NONE`, including fail-closed unknown/unrelated combinations.
- The mapping helper is now `internal` solely to permit a direct unit test; runtime behavior is
  unchanged. This proves the pure mapping only. It does not validate persisted membership scopes,
  EF/provider queries, client membership, or actual ticket/resource isolation.
- The first matrix run exposed a test expectation error: existing policy intentionally allows an
  organization scope to satisfy a department-class permission. The expected result was corrected;
  no production decision logic changed.
- Focused validation:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --verbosity:minimal --filter "FullyQualifiedName~DatabaseAuthorizationScopeTests|FullyQualifiedName~RoleAuthorizationPolicyTests|FullyQualifiedName~SensitiveRbacApprovalPolicyTests" -p:BaseOutputPath="F:\PROGRAMS\Python\Temp\pa-tickets-scope-tests-473defce60994a809b63cdf9ac737ea2\bin\"`
  passed **93/93** (0 failed, 0 skipped); the scope theory contributes 23 cases and the two
  existing policy suites contribute 70. API build:
  `dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo --verbosity:minimal -p:BaseOutputPath="F:\PROGRAMS\Python\Temp\pa-tickets-api-build-f503f101d3914d5295a54e668d2ce076\bin\"`
  succeeded with **0 warnings and 0 errors**. Both commands used unique temporary output paths.
- No database or live API operation is part of this evidence.
- Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**. Database-backed scope enforcement and
  031/032 migration validation remain blocked.

## Focused security and lifecycle regression continuation — 2026-10-15

- Rechecked the forced-password-rotation middleware paths, status, authentication predicate, and
  registration order; production behavior remains unchanged. `Program.cs` still registers it
  between authentication and authorization. Direct middleware tests cover authenticated deny,
  exact POST exemptions, case-insensitive matching, wrong method/trailing slash, unauthenticated
  pass-through even with the claim present, and authenticated pass-through without the claim.
- Added a domain regression asserting that resolution with evidence but an incomplete work item
  throws and leaves the ticket `InProgress`, `ResolvedAt` unset, and no resolution event emitted.
- Combined database-independent security/lifecycle selection passed **16/16**, 0 failed/skipped:
  `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~ApiSecurityConfigurationTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -p:BaseOutputPath="<unique-temp>\bin\" -v minimal`
  with unique temporary output. The tests are direct middleware/domain unit tests, not API,
  provider, concurrency, or browser acceptance.
- The acceptance baseline remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

## PostgreSQL RBAC migration repair source review — 2026-10-15

- The repository source identifies `PA_TICKETS_CLIENT.N_CLIENT_ID` as the canonical client entity
  and key, but the PostgreSQL import/ID mapping is unresolved. No client rows were inspected.
- Source review found that 031's `D_VALID_FROM NOT NULL DEFAULT CURRENT_TIMESTAMP` masks legacy
  `D_CREATED_AT` before the subsequent backfill; migration 031 also uses actor ID `0` and copies a
  membership creator onto a migration-created scope. The source does not establish those as
  truthful timestamp or actor semantics.
- Canonical audit DDL (nullable `N_ACTOR_USER_ID`, `S_ACTOR_TYPE`, user FK) conflicts with the
  prior PostgreSQL catalog report (non-null actor ID, no FK) and current EF `AuditRow` (required
  `ActorId`, no actor type). No system actor was invented. The detailed non-executable repair and
  EF contract draft is in `PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`.
- No PostgreSQL migration or EF contract was changed or executed. The 030 draft remains
  superseded; 031/032 remain blocked on client import, truthful legacy metadata/system-actor
  policy, and migration-history/baseline confirmation. The 24-scenario matrix remains
  **8 PASS / 3 PARTIAL / 13 BLOCKED**.
- Focused database-independent `ApiSecurityConfigurationTests`: **12/12 passed** using unique
  temporary output; this adds a unit assertion for unauthenticated requests carrying the rotation
  claim. The test does not exercise hosted API routing or database behavior.

## Read-only PostgreSQL catalog preflight — 2026-10-15

- The read-only profile `pa_tickets_h_verify_ro` returned database `pa_tickets_h_verify` and
  user `pa_tickets_mcp_ro` from `SELECT current_database(), current_user`. Catalog table names
  were enumerated; column, constraint, and index details were inspected for the `public`
  application schema. Application rows were not read.
- The observed schema lacks `PA_TICKETS_USER_ROLE_SCOPE`, `PA_TICKETS_ADMIN_HEAD_DELEGATION`,
  `PA_TICKETS_RBAC_PROPOSAL`, and `PA_TICKETS_CLIENT`. RBAC permission metadata and effective
  role-permission columns expected by migration 031 are absent. Migration 031 also references
  `N_CREATED_BY` on role/permission and `D_CREATED_AT`/`N_CREATED_BY` on role-permission although
  those columns are absent from the observed base tables. This is a confirmed source/catalog
  incompatibility, not an executed migration failure. Migration 032's proposal table and related
  constraints/indexes are absent. The audit actor column is non-null but has no observed FK to the
  user table. No migration history table was observed in `public`.
- No migration, DDL, DML, provisioning, API call, or application-state change occurred. No route
  or scenario was exercised by this catalog inspection. The source-level 24-scenario counts remain
  **8 PASS / 3 PARTIAL / 13 BLOCKED**, and no route or scenario is end-to-end PASS. RBAC provider
  validation remains blocked pending separate migration and synthetic-data approvals.
- Safe local validation for this continuation: focused database-independent .NET tests passed
  **75/75**, and the API build passed with **0 warnings/errors**. The aggregate Angular runner
  stalled after 3 of 12 specs; all 12 specs passed when run separately (**51/51**). The Angular
  production build passed with a 529-byte stylesheet-budget warning. None of these results is
  authenticated API or database acceptance evidence.

## Code-only security continuation — 2026-10-15

- Extracted the existing forced-password-rotation request gate into
  `PasswordRotationRequiredMiddleware` and added deterministic middleware tests: rotation-required
  authenticated requests are denied except POST login and POST password change; path matching
  remains case-insensitive and other methods/trailing-path variants are denied.
- `ApiSecurityConfigurationTests` passed **12/12** with isolated output; the API build passed with
  **0 warnings/errors**. This does not constitute hosted JWT/API or browser acceptance. The
  24-scenario matrix remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
- No migrations were changed or executed. A separate repair proposal in the remediation tracker
  calls out the unresolved PostgreSQL client entity/FK, creator/audit metadata ownership, migration
  ordering, EF model alignment, preservation risks, and required pre-approval tests.

## Continuation run record — 2026-10-14

- Repository: `G:\WSS\aw\Tickets`; branch `workstream-i-lifecycle-authority`; HEAD `0b595133286bae55b0b8fc079a631bd194cf43da`. The worktree was already dirty at inspection; no existing changes were reverted.
- Existing listeners: API `http://localhost:5018` (`PA.Tickets.Api`) and Angular `http://localhost:4200` (`node`). `/health/live` returned `200`; `/health/ready` returned `200`; Angular `/` returned `200`.
- CORS preflight for `GET /api/v1/tickets` from `http://localhost:4200` returned `204` with `Access-Control-Allow-Origin: http://localhost:4200`.
- `.env.test.local` is present and `.gitignore` lists it. Its expected fixture/configuration key names were present; no values were displayed. The environment variables were not automatically present in a fresh PowerShell process.
- A read-only PostgreSQL identity query, before and after the live login batch, returned exactly `pa_tickets_h_verify|pa_tickets_test_user`. No database mutation or database-backed test was run.
- One attempt to load the configured fixture through `POST /api/v1/auth/login` returned generic `401`; no token was received. The attempt was not repeated. A separate read-only existence check in the approved database returned false for an active matching user with a non-null password hash; identity was checked before and after. The browser sign-in did not authenticate and remained at the sign-in screen. This establishes a missing fixture-account prerequisite in the approved test database; it does not prove which database the already-running API process uses. Do not seed or fabricate an account.
- Anonymous read-only route batch: all 74 listed `GET` routes were requested individually with path placeholders replaced by non-business sentinel values; response bodies were discarded. Seventy-three returned `401`; `GET /api/v1/system/status` returned `200`. Its controller returns only a fixed service/status object and was inspected. The approved DB identity was verified before and after the batch.
- Initial verification-script invocation was blocked by the local PowerShell execution policy. Loading the test-output Npgsql assembly in Windows PowerShell also failed with a type-load error. Neither attempt connected to or modified PostgreSQL; the identity-only `psql` query was used instead and succeeded. Do not change machine execution policy to work around the script restriction.
- Safe automated checks: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~ApiSecurityConfigurationTests" -v minimal` passed 3/3. `npm.cmd run build`, run from `client`, passed. The first npm invocation from the repository root failed because there is no root `package.json`; it was rerun from the documented client directory.
- `git diff --check` returned exit code 2 on pre-existing whitespace in the already-dirty `tests/scripts/provision-postgresql-schema.ps1` (line 29 trailing whitespace and a blank line at EOF). That script was not run or changed during this continuation. The pre-existing warning was left untouched.
- Route-table status at this checkpoint (213 routes): 1 `PASS` (public system status), 73 `PARTIAL` (anonymous denial only), and 139 `BLOCKED` (including login; no authenticated actor available). The 138 other non-GET routes were not sent because no approved actor/safe disposable records were available; every row is individually marked. No positive authenticated API case, persistence workflow, or authenticated browser journey is claimed.

## RBAC implementation-preparation supplement — 2026-10-14

- No database connection, migration execution, privileged-account operation, or live API request was made in this supplement. Migration 031 remains unreviewed/unapplied, and the fixture account remains an unresolved prerequisite from the earlier run.
- Added the delegation-list route and Angular delegation list/create/end UI. The new administrative endpoints are listed individually below; each remains `BLOCKED` because there is no approved authenticated actor and the required schema migration has not been applied.
- Local validation: `dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~RoleAuthorizationPolicyTests -p:BaseOutputPath="<temp>\pa-tickets-rbac-check\bin\"` passed 12/12. `npm.cmd test` from `client` passed 40/40 across 12 files. `npm.cmd run build` from `client` passed. An earlier full Angular run exposed four stale test setup failures (permission-aware shell/ticket page mocks); after explicitly supplying permission-aware test doubles, the focused tests and complete suite passed. A final full-suite run concurrent with the production build stalled after initial specs; only that test-runner process tree was stopped, and the suite was rerun serially to a complete 40/40 pass. The initial .NET build attempted to copy outputs into a directory locked by an already-running `PA.Tickets.Api` process (PID 2116); no application process was stopped, and the isolated-output retry passed.
- Historical inventory after the batch endpoint was 226 routes: 1 `PASS`, 73 `PARTIAL`
  (anonymous denial only), and 152 `BLOCKED`. The latest source inventory is 232 routes:
  1 `PASS`, 73 `PARTIAL`, and 158 `BLOCKED`; none of the approval routes has been exercised.

## Configurable-RBAC continuation status

This records the checkpoint before the independent-approval continuation below; statements there
about the approval workflow and prior test counts are historical and are superseded by the newer
section. Route outcomes remain blocked unless individually updated in the route table.

- Database access and migrations were not attempted. PostgreSQL and Oracle migration 031 received
  source-only independent review; neither provider has been rehearsed. See
  [`PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md`](./PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md) for
  allocation, idempotency, preservation, provider differences, and recovery blockers.
- Migration scope backfill was revised to avoid assigning current user department/team scope
  retroactively to old memberships. It now creates only active/current or future effective scope
  facts and is guarded against creating a duplicate currently effective scope on rerun. This is
  **NOT RUN** on either provider.
- Backend membership removal now deactivates active scopes in the same transaction and audits the
  membership/scope before and after. A PostgreSQL audit regression assertion was added but remains
  **NOT RUN**. No route is promoted to PASS based on source inspection.
- After these code edits, the database-independent `RoleAuthorizationPolicyTests` passed **12/12**
  using an isolated build output directory. This verifies policy-unit behavior and compiles the
  focused PostgreSQL regression source; it does not execute PostgreSQL tests.
- Role permission editing now submits changes through `POST /api/v1/admin/roles/{roleId}/permissions/batch`.
  The service validates the expected effective-grant snapshot, all requested permission metadata
  and role ceilings, then commits changes and audit entries in a serializable transaction. The
  component test was updated for one-request submission and reload on rejection but did not run:
  Vitest reported `[vitest-pool]: Timeout starting forks runner` before test collection; an attempted
  single-thread override was rejected as an unknown Angular CLI option. The Angular production build
  passed. Batch behavior therefore remains **NOT TESTED**; the authenticated route remains
  **BLOCKED**.
- Sensitive permission classes (`DEPARTMENT_ADMIN`, `ORGANIZATION_ADMIN`, `SECURITY_GOVERNANCE`)
  are explicitly rejected by both direct and batch mapping service paths until independent
  approval is implemented. Admin Head delegation create/end service operations are likewise
  blocked; role-scope grants and ADMIN/ADMIN_HEAD membership grants are also denied by service
  paths. The UI exposes delegation history read-only, disables privileged membership choices,
  explains blocked scope grants, and prevents selecting sensitive permission metadata. A UI
  regression tests cover high-impact grant denial without HTTP submission and allow revoking an
  existing high-impact grant to narrow access. Latest safe
  validation passed: isolated-output .NET `RoleAuthorizationPolicyTests` 19/19, Angular
  role-administration component tests 8/8, and Angular production build. These checks do not
  verify database-backed service denial or permission-batch atomicity; those and delegation
  behavior still require approved database-backed tests.
- Existing SLA, workflow, approval, communication/notification, catalogue, and operational
  metadata APIs are documented, but their Angular administration screens remain **NOT
  IMPLEMENTED**. They must not be represented as browser acceptance passes.
- All previously recorded route outcomes remain unchanged: RBAC and other authenticated routes are
  **BLOCKED** where there is no approved authenticated fixture and/or required schema; anonymous
  denial remains **PARTIAL** only.
- Required next evidence: approved fixture and role-transition plan; reviewed permission/action/
  scope matrix; provider rehearsals and idempotency/recovery tests; individual authenticated route
  outcomes; and real browser-to-API-to-PostgreSQL journeys for all five roles plus a custom role.

## Independent RBAC approval continuation — 2026-10-14

- Implemented a persisted approval path only for sensitive permission grants where the existing
  immutable role ceiling permits the requested grant, and for permission grants to protected
  built-in roles other than `SUPER_ADMIN`. Direct single/batch grant paths remain blocked for
  those operations. `SUPER_ADMIN` grants, organization/security-governance grants, privileged
  memberships, scope increases, and Admin Head delegation changes remain blocked.
- Added six versioned API routes: `POST /api/v1/admin/roles/{roleId}/permission-proposals`,
  `GET /api/v1/admin/permission-proposals`,
  `GET /api/v1/admin/permission-proposals/{proposalId}`,
  `POST /api/v1/admin/permission-proposals/{proposalId}/approve`,
  `POST /api/v1/admin/permission-proposals/{proposalId}/reject`, and
  `POST /api/v1/admin/permission-proposals/{proposalId}/cancel`. Each is individually listed as
  **BLOCKED / NOT RUN** below because migration 032 is unapplied and no approved authenticated
  actor or safe fixture is available.
- Static code path only: a distinct, currently active Super Admin membership plus current
  `ROLE_PERMISSION_ADMIN` permission is required for proposal review. The initiator cannot decide
  their own proposal. Approval rechecks the full effective-permission snapshot, target role and
  permission metadata, immutable role ceiling, expiry, and server-computed payload digest, then
  stages the grant, audit events, and final proposal state in one service-managed serializable
  transaction. Provider atomicity is unverified. Migration drafts declare unique per-initiator
  idempotency keys and proposal-row concurrency, also unverified against providers.
- Safe tests: isolated-output .NET authorization/proposal policy tests passed **32/32** after
  disposing the failed idempotent-create transaction before querying for the concurrent winner;
  Angular role-administration component suite passed **10/10**, and the full Angular suite passed
  **44/44 across 12 files**, after correcting the stale Spectator error-message expectation.
  Angular production build succeeded with a style-budget
  warning: `role-administration.page.scss` is 529 bytes over its 4 kB budget. Problems checks on
  the touched backend/UI files found no errors. No migration, DB access, API call, account change,
  or live browser test occurred.
- PostgreSQL/Oracle migration 032 drafts are source-only and unexecuted. The source review notes
  Oracle's implicit DDL commits and need for forward-recovery rehearsal; provider behavior,
  concurrent idempotency races, transaction rollback, and the database audit actor convention
  remain unverified. At this intermediate checkpoint, the UI had not yet derived actionability
  from expiry timestamps. The later changes summarized below add that client-side check; the
  service still persists expiry/audit for at most 50 eligible rows per list call.
  Application exceptions before commit are expected to leave changes uncommitted by the service
  transaction, but provider rollback and ambiguous commit behavior are unverified; no separately
  persisted failure state/audit exists. No route is promoted to PASS. All six new route outcomes below are BLOCKED; the live
  acceptance inventory count is now 232.

## Adversarial approval checklist — 2026-10-14

**Scope:** read-only source tracing and database-independent .NET/Angular tests. `PASS` means
only the narrowly stated rule has direct source and/or deterministic-test evidence; it is not
authenticated API, database, provider, migration, or full acceptance evidence. `PARTIAL` means
source/UI behavior is present but a material part of the scenario is not evidenced. `BLOCKED`
means the decisive assertion requires provider/database/API evidence that was not run. The status
summary below is 8 PASS, 3 PARTIAL, and 13 BLOCKED; no scenario is represented as end-to-end PASS.

| # | Behavior being assessed | Implementation and exact test evidence | Evidence type | Remaining unverified | Result |
|---|---|---|---|---|---|
| 1 | A caller cannot choose another actor ID to approve their own proposal. | `AdministrationController.Actor()` derives the actor from `NameIdentifier`/`sub`; `EnsureIndependentDecisionMakerAsync` rejects the initiator. `Approval_requires_a_distinct_active_actor_with_protected_authority` tests same-actor denial. | Source inspection + policy unit test | HTTP claim binding and crafted-request behavior in a hosted API. | PASS (narrow source/policy claim only) |
| 2 | A proposal cannot supply the approver authority it needs. | `EnsureApprovalAuthorityAsync` requires current `ROLE_PERMISSION_ADMIN` and active `SUPER_ADMIN` membership; `CanCreatePermissionProposal` rejects `SUPER_ADMIN` as target. `Proposal_submission_is_limited_to_supported_targets` tests the target gate. | Source inspection + policy unit test | Persisted membership/permission behavior and API authorization. | PASS (narrow source/policy claim only) |
| 3 | Revoked/inactive approval authority is checked when the decision request executes. | `ApproveRbacProposalAsync`/`RejectRbacProposalAsync` call `EnsureApprovalAuthorityAsync` and `EnsureIndependentDecisionMakerAsync`, which query current active user, membership, role, and permission. Policy test covers inactive/missing authority flags. | Source inspection + policy unit test | Concurrent revocation relative to the request’s authorization reads; provider transaction/isolation behavior. | PARTIAL |
| 4 | Only one concurrent approval can apply a grant. | `ApproveRbacProposalAsync` checks pending status/version and applies within a serializable transaction; EF maps `RowVersion` as a concurrency token. | Source inspection + EF model metadata test | Concurrent execution, winning response, and uniqueness/row-lock behavior on PostgreSQL and Oracle. | BLOCKED |
| 5 | Concurrent create requests with one initiator/idempotency key converge safely. | Create checks an existing proposal; EF and migration drafts declare a unique `(InitiatorUserId, IdempotencyKey)` index; duplicate-insert recovery reloads a matching row after rollback. `Proposal_persistence_mapping_has_concurrency_and_retry_constraints` checks EF metadata only. | Source inspection + EF model metadata test | Unique-conflict race, different-body collision, and retry behavior against the provider. | BLOCKED |
| 6 | A stale proposal cannot apply after role, permission, target, or permission-set changes. | Approval reloads active role/permission rows, reconstructs the payload, checks the full current permission snapshot, and reruns the role ceiling. Pure snapshot/ceiling/digest tests cover predicates. | Source inspection + policy unit tests | Persisted mutation/staleness cases and transactional ordering on each provider. | BLOCKED |
| 7 | Payload-only substitution and immutable-envelope mismatches are rejected; the digest is not a keyed signature. | `DeserializeProposalPayload` recomputes SHA-256; `MapProposal` checks persisted envelope fields; approval rebuilds and compares the full payload against current role/permission rows. `Serialized_payload_digest_covers_every_security_relevant_field` checks payload-field mutations and envelope binding; `Recomputed_unkeyed_digest_accepts_a_modified_payload_and_does_not_authenticate_its_writer` demonstrates that a changed payload with a recomputed digest still matches. | Source inspection + deterministic policy unit tests | The payload uses the default `System.Text.Json` representation, not a separately specified canonical-JSON format. API users cannot directly set payload JSON/digest through the request contract; actual DB grants were not inspected. A database writer able to change both proposal payload and digest can pass the hash check; direct RBAC-table writes, audit alteration, and provider behavior are not tested. | PASS for application-level payload/envelope consistency only; no database-writer tamper protection |
| 8 | Ordinary sequential replay cannot change a terminal proposal or apply it twice. | `CanTransition`, expected-version checks, and explicit same-actor retry gates are used by decision methods. `Transition_table_contains_only_the_documented_edges`, replay, version, and retry tests cover policy predicates. | Source inspection + policy unit tests | Concurrent replay/races and persisted state under provider failures. | PASS for policy/sequential paths only |
| 9 | Single and batch direct permission endpoints cannot bypass proposal gates. | Both `SetRolePermissionAsync` and `SetRolePermissionsAsync` call `EnsureDirectPermissionGrantAllowed` for grants; helper requires a proposal for system roles and approval-class grants. `Direct_permission_grant_path_cannot_bypass_proposal_policy` tests the helper. | Source inspection + policy unit test | No service/API invocation test; deployed metadata and endpoint parity. | PASS for the inspected service gates only |
| 10 | Role ceilings reject unsupported organization/security-governance grants and protect Super Admin mappings. | `EnsureRolePermissionCeiling` and `CanCreatePermissionProposal` enforce built-in/custom-role boundaries. `Role_ceiling_rejects_forbidden_permission_combinations` and target-eligibility tests cover predicates. | Source inspection + policy unit tests | Persistence/API enforcement and all permission catalogue combinations. | PASS for tested policy combinations only |
| 11 | A non-owner without current review authority cannot read another user’s proposal. | List filters to initiator or reviewer; detail returns not-found for a non-owner without review authority. | Source inspection | Authenticated cross-user requests, claims, and live permission/membership evaluation. | BLOCKED |
| 12 | A failed audit insert cannot leave the permission grant committed alone. | Approval stages permission, proposal transitions, and audit calls inside one service transaction; `AddAuditAsync` joins the ambient transaction when present. | Source inspection only | Audit fault injection and rollback/durability on PostgreSQL and Oracle. | BLOCKED |
| 13 | A later grant/status failure cannot leave earlier audit writes durable alone. | Approval writes audit and grant before final APPLIED transition, under the same outer transaction. | Source inspection only | Provider fault injection at each save point and rollback behavior. | BLOCKED |
| 14 | An unknown commit outcome is not reported as confirmed success. | Service returns APPLIED only after `CommitAsync` returns; persisted FAILED is not implemented. UI catches request errors and refreshes. | Source inspection + Angular component test for HTTP error presentation | Commit acknowledgment loss, rollback/commit ambiguity, API response loss, and recovery against persisted state. | BLOCKED |
| 15 | State transitions and competing version updates have one legal winner. | Transition allowlist, expected version, `[ConcurrencyCheck]` row version, and serializable transactions are present; policy tests cover legal edges/version predicate. | Source inspection + policy/EF metadata tests | Provider isolation, serialization failures, retries, and race outcomes. | BLOCKED |
| 16 | Cancellation racing approval cannot produce two terminal outcomes or double-apply. | Cancel and approve each load/update proposal in a serializable transaction and increment its row version; cancellation is initiator-only in service code. | Source inspection only | Concurrent cancel/approve execution and provider transaction semantics. | BLOCKED |
| 17 | An expired proposal cannot be approved merely because it missed the expiry-list batch or still has persisted `PENDING` status. | Approve, reject, and cancel all call `ExpireProposalIfNeededAsync` before allowing a pending transition; that helper uses the shared `IsPendingAndExpired` predicate (`expiresAt <= now`). `Every_expired_pending_proposal_is_due_for_expiry_even_beyond_the_list_batch` checks 51 expired pending candidates, independently of list position. | Source inspection + deterministic policy unit test | No service/API or provider test of persisted status transition, transaction behavior, or concurrent timing. | PASS for source/policy decision guard only |
| 18 | More than 50 expired proposals remain non-actionable and reach a correct durable state. | Listing is not paginated: it reads all authorized rows, but persists expiry/audit for only the 50 oldest-expiring rows per call. Decision handlers apply the same expiry predicate to any proposal, including a row beyond that batch. The policy test checks 51 expired candidates, not service persistence or a database result set. | Source inspection + deterministic policy unit test | Rows beyond the list batch can remain persisted as `PENDING` until a later list/decision call; list reads are unbounded and performs up to 50 expiry/audit writes in a serializable transaction. No >50-row service/provider test. | PARTIAL |
| 19 | Migration DDL contains required uniqueness/concurrency constraints and is applied correctly. | EF mapping test verifies row-version token and unique idempotency index; PostgreSQL and Oracle drafts declare corresponding indexes/checks. | Source inspection + EF model metadata test | Neither DDL executed; catalog shape, repeat execution, conflicts, and provider constraint behavior. | BLOCKED |
| 20 | Oracle and PostgreSQL drafts have equivalent safe partial-failure/recovery behavior. | Source comparison identifies PostgreSQL transaction versus Oracle implicit DDL commits; Oracle ID check is weaker and name-only guards do not validate definitions. | Source inspection only | Independent Oracle review, DDL rehearsal, actual partial-install recovery; no Oracle SQL executed. | BLOCKED |
| 21 | Six proposal endpoints have correct authorization, input validation, and error handling. | Controller has `[Authorize]`, uses claim-derived `Actor()`, delegates authority checks to service, and request DTOs declare validation attributes; global error mapping is source-visible. | Source inspection only | Model binding, authorization filters, status/body mapping, and endpoint behavior in hosted API tests. | PARTIAL |
| 22 | UI accurately renders terminal states and distinguishes request failure from durable proposal state. | Angular tests cover APPROVED/APPLIED/REJECTED/CANCELLED/EXPIRED display and disabled controls, expired pending display, and failed approve HTTP request showing the refreshed PENDING state/error. There is no persisted FAILED proposal state. | Angular component unit tests | Durable backend outcome after timeout/ambiguous commit; API/browser integration. | PASS for tested UI states/error display only |
| 23 | Retrying after an ambiguous create/approval response cannot duplicate the grant. | UI keeps create idempotency key in same-tab `sessionStorage` across component recreation; policy permits same-approver APPLIED retry; EF uniqueness is declared. | Angular and policy unit tests + source inspection | Tab closure, provider uniqueness/concurrency, lost commit response, and durable single-grant proof. | BLOCKED |
| 24 | Persisted audit evidence can reconstruct initiator, approver, proposed change, decision, and actual application. | Source writes proposal-created, approved/rejected/cancelled/expired, grant, and applied events with actor, proposal/role/permission references, reason/outcome, and correlation data. `AddAuditAsync` chains and saves events. List-triggered expiry records the authenticated caller that triggered lifecycle processing; the reason explicitly says no human decision was made. | Source inspection only | Persisted event ordering/content, atomicity, audit-chain integrity, and failure recovery on provider remain unverified. The schema has an audit-user foreign key; provider compatibility and event reconstruction are not tested. | BLOCKED |

Status totals: **PASS 8, PARTIAL 3, BLOCKED 13**. These totals describe only this 24-scenario
source/unit/UI evidence table; they do not change the separate route inventory, whose six proposal
routes remain **BLOCKED / NOT RUN** for authenticated API acceptance.

### Direct write-path inventory

| Data | Direct write path | Current disposition |
|---|---|---|
| Role permissions | `PUT /roles/{roleId}/permissions/{permissionId}` ? `SetRolePermissionAsync`; `POST /roles/{roleId}/permissions/batch` ? `SetRolePermissionsAsync` | Direct grants to system roles/high-impact classes rejected; revocations remain available. Proposal apply is the only implemented grant route for eligible cases. |
| Role permissions | `POST /roles/{roleId}/permission-proposals` ? create; approve route ? `ApproveRbacProposalAsync` | Proposal creation is not a grant. Only independent approved application inserts the role-permission mapping. Super Admin permission grants remain prohibited. |
| Memberships | `POST /user-roles` ? `AssignRoleAsync`; `DELETE /user-roles/{id}` ? `RemoveRoleAsync` | ADMIN, ADMIN_HEAD, SUPER_ADMIN grants blocked; removal remains ordinary audited narrowing. No membership proposal handler exists. |
| Scopes | `POST /user-roles/{id}/scopes` ? `AssignRoleScopeAsync`; `DELETE /role-scopes/{id}` ? `RemoveRoleScopeAsync` | Scope increases rejected; scope removal remains audited narrowing. |
| Admin Head delegation | `POST /admin-head-delegations` ? `AssignAdminDelegationAsync`; `DELETE /admin-head-delegations/{id}` ? `RemoveAdminDelegationAsync` | Create/end operations remain blocked; no delegation proposal handler exists. |
| Built-in definitions/ceilings | `POST /roles`, `PUT /roles/{id}`, `POST /roles/{id}/deactivate` ? custom-role service methods | Only custom roles can be created/edited/deactivated. Protected built-in and legacy identities are rejected. |

### Migration draft comparison (source-only)

| Concern | PostgreSQL `032` | Oracle `032` | Evidence/status |
|---|---|---|---|
| DDL transaction/recovery | Explicit `BEGIN`/`COMMIT`; transactional rollback is intended | Anonymous PL/SQL block; DDL implicitly commits and can leave earlier objects installed | Source-confirmed difference; neither script executed |
| Table/idempotency guard | `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS` | `USER_TABLES`/`USER_INDEXES` name checks before DDL | Both can accept an already-existing object by name without proving its definition; preflight/catalog verification remains required |
| Payload and numeric types | `text`, `bigint`, timestamps with time zone | `CLOB`, `NUMBER`, timestamps with time zone | ORM metadata test checks PostgreSQL `text`; Oracle provider model/catalog is not run |
| Proposal identity check | Lowercase 32-hex regular expression | Length-only constraint | Oracle check is weaker than PostgreSQL; application generates lower-case `Guid.ToString("N")`, but DDL parity is not proven |
| Lifecycle/version integrity | Status, expiry-after-create, positive row version checks | Corresponding status/date/positive-version checks | Checks are present in source; deployed catalogs remain unverified |
| Uniqueness/concurrency | Unique initiator/key index and EF concurrency token | Same unique columns and mapped concurrency token | Source/model only; provider conflict/locking semantics require rehearsal |
| Rollback/recovery | Transaction rollback expected for failed migration | No blanket rollback; inspect and repair actual object state, then forward-recover | Oracle recovery runbook and rehearsal are deployment blockers |

No database migration was run as part of this continuation. Source-level `IF NOT EXISTS`/name-only
guards do not prove that an existing object has the intended columns, uniqueness, or constraint
definition. A reviewed deployment preflight must verify these definitions and reconcile any
partial/mismatched objects before the workflow is enabled.

### Changes made during adversarial continuation

- Extended serialized immutable proposal payloads to cover proposal ID, initiating actor, creation and
  expiry timestamps, reason, idempotency key, correlation ID, target role code and system-role
  identity, target permission metadata, and expected active-permission snapshot. Approval rebuilds
  the serialized representation and compares persisted immutable fields before application. SHA-256
  detects payload-only changes but is not a signature: a writer able to update both payload and
  digest can make the hash check pass. No DB privileges were inspected.
- Added deterministic database-independent tests for proposal eligibility, direct-grant gates,
  role ceilings, exhaustive state transitions, expected versions, retry actor/reason, permission
  snapshot equality, digest mutation for each payload field, and expiry boundary.
- Wrapped proposal list/detail authority checks and reads in serializable transactions. This is
  source-level mitigation only; provider isolation and stale-revocation races still require
  provider-backed validation.
- Fixed Angular decision error handling to preserve the original failure through queue refresh.
  Expired rows are displayed as expired and decision actions disabled even if they are beyond the
  service's current 50-row persisted-expiry batch.
- Decision-time expiry uses one shared pending-and-expired predicate in approve, reject, and cancel;
  a stale `PENDING` row past its expiry is transitioned to `EXPIRED` before any decision proceeds.
  List processing remains bounded to 50 writes per request and the returned list remains unpaginated.
  The list caller's authenticated actor ID is recorded as the lifecycle-processing trigger, with an
  explicit reason that no human decision was made; no synthetic user ID is used. The 51-candidate
  unit test exercises the predicate only, not the service query or provider persistence.
- Persisted the create idempotency key in session storage indexed by a SHA-256 request fingerprint,
  allowing same-tab reload retries without storing the free-text reason in the storage key. The key
  is cleared after a successful create response. The Angular test recreates the component after an
  ambiguous network failure and verifies the same key is reused and then cleared. Tab-closure and
  backend ambiguous-commit behavior still require provider/API acceptance.
- Validation results: focused .NET proposal-policy tests **48/48**; API project build **0
  warnings / 0 errors**; focused role-administration Angular suite **17/17**; full Angular suite
  **51/51 across 12 files**; Angular production build succeeded with the role-administration
  stylesheet 529 bytes over its 4 kB budget. The first focused Angular run, concurrent with .NET
  tests, failed before test collection with `[vitest-pool]: Timeout starting forks runner`; serial
  retry passed. No migration, database, API, or browser test was run. No route is promoted to PASS.

### Verification follow-up — 2026-10-15

- The focused `SensitiveRbacApprovalPolicyTests` suite passed **50/50** using isolated .NET build
  output. The two added deterministic cases cover 51 expired-pending policy candidates and show
  that a recomputed unkeyed SHA-256 digest matches modified payload bytes. This verifies policy and
  hash behavior only; it does not invoke `AdministrationService`, access a provider, or demonstrate
  persisted expiry for 51 rows.
- Source follow-up confirms the API request does not accept payload JSON or a digest; `Actor()` is
  derived from `NameIdentifier`/`sub`. The application builds payload metadata from current rows,
  and approval reconstructs and compares the full `System.Text.Json` representation; no
  standards-based canonical-JSON format is defined. A database writer with update access to both
  payload and digest can recompute a passing hash; the actual database grants were not inspected.
  The unkeyed digest is not a signature.
- List expiry now uses the authenticated list caller as the lifecycle-processing audit actor rather
  than synthetic ID `0`. The audit reason says the proposal was automatically expired and that no
  human decision was made. Listing still returns all authorized rows without pagination while
  persisting expiry/audit for at most 50 rows per call. Rows beyond the batch may remain `PENDING`
  in storage but decision-time guards prevent approve/reject/cancel after expiry.
- No database, live API, migration, account, role membership, permission, or approval record was
  accessed or changed. Provider-dependent concurrency, rollback, audit constraints, and migration
  behavior remain BLOCKED.

## Hard safety rules

- Live acceptance must use the real Angular application, real ASP.NET Core API, real authentication and PostgreSQL persistence. No mocked API/repository, fake response, stubbed auth, or fabricated success.
- Discover identities, IDs, catalogue values, scopes, and business records from authorized live responses. Do not hardcode guessed values into tests.
- Approved database identity is exactly `pa_tickets_h_verify|pa_tickets_test_user`. Run a live `SELECT current_database(), current_user;` before and after every DB-backed batch. Abort on mismatch.
- Never touch shared `pa_tickets_test`, Oracle, Oracle migrations, or `tests/scripts/provision-postgresql-schema.ps1`.
- Preserve existing acceptance tickets and audit rows. Do not delete them or clean up evidence.
- Never print passwords, signing keys, JWTs, cookies, or sensitive personal data.
- Do not commit, push, reset, clean, stash, or switch branches. Preserve the dirty worktree.
- Never weaken auth, authorization, validation, idempotency, workflow, separation-of-duties, or audit behavior to force a pass.
- Do not infer role governance, subordinate relationships, email-verification behavior, dynamic-form serialization, or external-provider success. If policy/contract is absent, report the gap.
- Do not keep repeating health checks after they pass. Move to the next unresolved criterion.
- A build, unit test, API-only test, mocked Angular test, or screenshot is not a browser E2E pass.

## Status vocabulary

- **PASS**: directly observed and evidenced.
- **FAIL**: directly observed defect/unexpected behavior.
- **BLOCKED**: safe execution requires a missing prerequisite.
- **NOT RUN**: not attempted.
- **PARTIAL**: only part of the criterion was verified.
- **NOT APPLICABLE**: justified by inspected implementation; explain why.

Never count skipped tests as passed. A route that was not attempted is not a pass.

## Phase 0 — Preflight

- [ ] Record repository path, branch, HEAD, dirty files and current running processes.
- [ ] Confirm actual API/frontend URLs and listener status.
- [ ] Record `/health/live` and `/health/ready` status codes.
- [ ] Confirm Angular uses the real API and no fixture/mock interceptor substitutes responses.
- [ ] Confirm CORS for the exact Angular origin.
- [ ] Verify intended protected signing-key configuration is loaded without displaying its value.
- [ ] Inspect `.env.test.local` loading mechanism; do not assume dotenv is loaded automatically.
- [ ] Verify `.gitignore` excludes local secrets.
- [ ] Check presence (not values) of `PA_TICKETS_AUTH_FIXTURE_LOGIN`, `PA_TICKETS_AUTH_FIXTURE_PASSWORD`, and `PA_TICKETS_AUTH_FIXTURE_SIGNING_KEY`.
- [ ] Verify DB identity exactly `pa_tickets_h_verify|pa_tickets_test_user` before DB tests and again after.
- [ ] If any prerequisite is missing, record exact blocker and resolve it safely rather than looping on service checks.

## Phase 1 — Genuine browser E2E journeys

### Authentication/session
- [ ] Real UI login with approved disposable test identity.
- [ ] Unknown and wrong credentials have the same generic failure response.
- [ ] Correct authenticated principal and permitted shell are displayed.
- [ ] Protected calls carry a real bearer token.
- [ ] Token is not persisted in local/session storage if design says in-memory only.
- [ ] Anonymous protected request is denied.
- [ ] Logout clears session and protected navigation redirects to login.
- [ ] Forced first-login rotation, password change, old-password rejection and new-password login where applicable.
- [ ] Rate limit behavior tested only within safe configured limits on the disposable environment.
- [ ] Disabled-user denial tested only with an approved test account.

### Ticket search/detail/create/lifecycle
- [ ] Search/list uses actual API data; visible rows agree with response/persisted records.
- [ ] Open an authorized record and verify public ID/number mapping.
- [ ] Cross-scope resource access is denied using real authorized actors/records.
- [ ] Service/category/subcategory/request-type selectors load live catalogue data and dependencies.
- [ ] Create a valid ticket through the UI using live discovered values and a fresh generated `Idempotency-Key`.
- [ ] Capture actual status/response/public ID; reload and verify persisted record and audit evidence.
- [ ] Verify documented idempotent replay and payload-conflict behavior when safe.
- [ ] Perform only real workflow-permitted lifecycle actions; verify API response and persisted state.
- [ ] If a UI action is absent, record a UI gap even if the API route passes separately.
- [ ] Do not manually edit DB rows or mutate unrelated tickets to manufacture a work item/completion.

### Attachments/evidence
- [ ] Upload harmless approved test content; verify metadata/version and actual downloaded bytes/hash.
- [ ] Verify cross-ticket access denial for metadata/download/review.
- [ ] Verify evidence review is persisted and audited.
- [ ] If malware scanning/provider is unavailable, verify fail-closed behavior; do not claim successful production upload.

### Workflow/approvals/communication/SLA/reporting/auditor
- [ ] Use only real configured records, identities and provider test/sandbox adapters.
- [ ] Approval requires nominated/current approver; invalid, duplicate, out-of-order and SoD-violating decisions are denied.
- [ ] Published workflow/approval/SLA/template versions obey actual immutability contract.
- [ ] Durable worker/job retries and replay are verified only when configured.
- [ ] Report exports/schedules have real persisted execution history; never fabricate export success.
- [ ] SLA operations use actual policy/deadline/calendar state.
- [ ] Auditor integrity APIs are read-only; unsupported formats fail closed.
- [ ] Do not claim an external message was delivered when only queued.

## Phase 2 — Every API route

**All routes below must have an individual row and status.** Inspect the actual contract first. For each route, test the applicable safe cases:
- Authentication: anonymous, authenticated, insufficient permission.
- Scope: in-scope allowed; out-of-scope denied; do not guess IDs.
- Reads: valid authorized record, missing record, filters/pagination, no mutation.
- Creates: valid data discovered from live sources, invalid/missing data, duplicate/conflict, persistence and audit.
- Updates/actions: valid transition, invalid/out-of-order transition, stale/concurrent write, wrong actor, idempotency/replay where supported, persisted final state and audit.
- Delete/revoke/deactivate/cancel/retire: exact semantics and authorization; use only disposable records if mutation is safe.
- Downloads: content type, version, bytes, scope and filename safety.
- Provider routes: sandbox/test configuration only; no real external send/cost without explicit authorization.
- If semantics are not established from source/docs, mark BLOCKED/NOT APPLICABLE with explanation rather than guessing.

| Method | Route | Contract inspected | Actor/permission | Actual result | Persistence/audit evidence | Negative/scope evidence | Status | Evidence reference |
|---|---|---|---|---|---|---|---|---|
| `GET` | `/api/v1/admin/users` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/users` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/users/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PATCH` | `/api/v1/admin/users/{id}/profile` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `PUT` | `/api/v1/admin/users/{id}/reporting-manager` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/users/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/users/{id}/password` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/departments` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/departments` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/departments/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/teams` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/teams` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/teams/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/roles` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/roles` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; no authenticated actor available | BLOCKED | RBAC implementation-preparation supplement |
| `PUT` | `/api/v1/admin/roles/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; no authenticated actor available | BLOCKED | RBAC implementation-preparation supplement |
| `POST` | `/api/v1/admin/roles/{id}/deactivate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; no authenticated actor available | BLOCKED | RBAC implementation-preparation supplement |
| `GET` | `/api/v1/admin/permissions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/admin/roles/{id}/permissions` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `PUT` | `/api/v1/admin/roles/{roleId}/permissions/{permissionId}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `POST` | `/api/v1/admin/roles/{roleId}/permissions/batch` | Contract/source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | Authenticated actor and migration 031 are unavailable; endpoint is not live-verified | BLOCKED | Configurable-RBAC continuation |
| `POST` | `/api/v1/admin/roles/{roleId}/permission-proposals` | Controller/service source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 032 unapplied | BLOCKED | Independent RBAC approval continuation |
| `GET` | `/api/v1/admin/permission-proposals` | Controller/service source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 032 unapplied | BLOCKED | Independent RBAC approval continuation |
| `GET` | `/api/v1/admin/permission-proposals/{proposalId}` | Controller/service source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 032 unapplied | BLOCKED | Independent RBAC approval continuation |
| `POST` | `/api/v1/admin/permission-proposals/{proposalId}/approve` | Controller/service source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | No independent authenticated approver; migration 032 unapplied | BLOCKED | Independent RBAC approval continuation |
| `POST` | `/api/v1/admin/permission-proposals/{proposalId}/reject` | Controller/service source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | No independent authenticated approver; migration 032 unapplied | BLOCKED | Independent RBAC approval continuation |
| `POST` | `/api/v1/admin/permission-proposals/{proposalId}/cancel` | Controller/service source inspected only | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated initiator; migration 032 unapplied | BLOCKED | Independent RBAC approval continuation |
| `GET` | `/api/v1/admin/users/{id}/roles` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `POST` | `/api/v1/admin/user-roles` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/user-roles/{id}/scopes` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `POST` | `/api/v1/admin/user-roles/{id}/scopes` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `DELETE` | `/api/v1/admin/role-scopes/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `GET` | `/api/v1/admin/admin-head-delegations` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No write requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `POST` | `/api/v1/admin/admin-head-delegations` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `DELETE` | `/api/v1/admin/admin-head-delegations/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No authenticated actor; migration 031 unapplied | BLOCKED | RBAC implementation-preparation supplement |
| `DELETE` | `/api/v1/admin/user-roles/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/user-clients` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `DELETE` | `/api/v1/admin/user-clients/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/approval-admin/rules` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/approval-admin/rules` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/approval-admin/rules/{ruleId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/approval-admin/rules/{ruleId}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approval-admin/rules/{ruleId}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/approval-admin/rules/{ruleId}/versions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/approval-admin/versions/{versionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/approval-admin/versions/{firstVersionId}/compare/{secondVersionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/approval-admin/tickets/{ticketId}/runtime-history` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/approval-admin/rules/{ruleId}/versions/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approval-admin/rules/{ruleId}/versions/draft` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approval-admin/versions/{versionId}/publish` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approval-admin/versions/{versionId}/retire` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approvals` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approvals/{id}/decisions` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/approvals/{id}/delegations` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/attachments` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/attachments/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/attachments/{id}/versions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/attachments/{id}/versions/{version}/download` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/auditor/reports` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/auditor/reports/{reportCode}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/auditor/reports/{reportCode}/query` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/auditor/reports/tickets/{ticketId}/integrity` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/auditor/reports/configuration/{entityType}/{entityId}/integrity` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/auth/login` | YES | Approved fixture | Generic HTTP 401; no bearer issued | No mutation observed | Not established | BLOCKED: no active password-hashed fixture account in approved DB; operator must provision through approved process | Continuation run record |
| `POST` | `/api/v1/auth/change-password` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/services` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/catalogue/services` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/services/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/catalogue/services/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/services/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/categories` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/catalogue/categories` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/categories/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/catalogue/categories/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/categories/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/sub-categories` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/catalogue/sub-categories` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/sub-categories/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/catalogue/sub-categories/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/sub-categories/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/request-types` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/catalogue/request-types` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/request-types/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/catalogue/request-types/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/request-types/{id}/publish` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/request-types/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/fields` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/catalogue/fields/{id}/options` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/catalogue/request-types/{id}/fields` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/catalogue/request-types/{id}/fields` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/changes/{number}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/changes/{number}/assess` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/schedule` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/start-implementation` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/complete-implementation` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/fail-implementation` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/rollback` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/resolve` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/close` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/reopen` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/incidents/{incidentNumber}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/changes/{number}/problems/{problemNumber}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/communication-admin/configuration` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/communication-admin/providers/{providerCode}/{channel}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `PUT` | `/api/v1/communication-admin/channels/{channel}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/communication-admin/templates` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/communication-admin/templates/versions/{versionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/communication-admin/templates/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communication-admin/templates/drafts` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communication-admin/templates/versions/{versionId}/publish` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communication-admin/templates/versions/{versionId}/retire` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/communication-admin/notification-rules` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/communication-admin/notification-rules` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/communication-admin/notification-rules/{ruleId}/versions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/communication-admin/notification-versions/{versionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/communication-admin/notification-rules/{ruleId}/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communication-admin/notification-rules/{ruleId}/drafts` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communication-admin/notification-versions/{versionId}/publish` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communication-admin/notification-versions/{versionId}/retire` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `PUT` | `/api/v1/communication-admin/notification-rules/{ruleId}/enabled` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/communication-admin/supported-event-types` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/communication-admin/audit/{entityType}/{entityId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/communications` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/communications/{publicId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/communications/by-ticket/{ticketId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/communications/{publicId}/retry` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/communications/{publicId}/cancel` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/dashboards` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/dashboards/{dashboardCode}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/dashboards/{dashboardCode}/query` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/evidence` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/evidence/{id}/review` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/kpis` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/kpis/{kpiCode}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/kpis/{kpiCode}/calculate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/operational-metadata/statuses` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/operational-metadata/statuses` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/operational-metadata/statuses/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/operational-metadata/statuses/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/operational-metadata/statuses/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/operational-metadata/statuses/audit/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/admin/operational-metadata/transitions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/operational-metadata/transitions` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/operational-metadata/priorities` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/admin/operational-metadata/priorities` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/operational-metadata/priorities/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/admin/operational-metadata/priorities/{id}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/operational-metadata/priorities/{id}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/admin/operational-metadata/priorities/{id}/default` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/admin/operational-metadata/priorities/audit/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/problems` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/problems/{number}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/problems/{number}/assign` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/start-investigation` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/root-cause` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/workaround` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/declare-known-error` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/permanent-fix` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/resolve` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/close` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/reopen` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/problems/{number}/incidents/{incidentNumber}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/reports/{reportCode}/exports` | Provider/API test exists | Authenticated PostgreSQL E2E fixture in `PostgreSqlApiE2ETests.Export_and_schedule_http_contracts_persist_and_enforce_owner` | NOT RUN in this pass | Test covers persisted export and owner download | Other actor receives 404 in test; permission-denial/no-persist still needs provider evidence | BLOCKED pending approved provider run | Existing PostgreSQL API E2E test; not executed |
| `GET` | `/api/v1/exports/{exportId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/exports/{exportId}/download` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/reports` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/reports/{reportCode}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/reports/{reportCode}/query` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/report-schedules` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/report-schedules` | Provider/API test exists | Authenticated PostgreSQL E2E fixture in `PostgreSqlApiE2ETests.Export_and_schedule_http_contracts_persist_and_enforce_owner` | NOT RUN in this pass | Test covers persisted schedule creation | Other actor receives 404 in test; permission-denial/no-persist still needs provider evidence | BLOCKED pending approved provider run | Existing PostgreSQL API E2E test; not executed |
| `GET` | `/api/v1/report-schedules/{scheduleId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/report-schedules/{scheduleId}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/report-schedules/{scheduleId}/executions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/report-schedules/{scheduleId}/pause` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/report-schedules/{scheduleId}/resume` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/report-schedules/{scheduleId}/cancel` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/sla-admin/policies` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/sla-admin/policies` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/sla-admin/policies/{policyId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/sla-admin/policies/{policyId}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/sla-admin/policies/{policyId}/versions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/sla-admin/versions/{versionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/sla-admin/versions/{firstVersionId}/compare/{secondVersionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/sla-admin/policies/{policyId}/versions/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/sla-admin/policies/{policyId}/versions/draft` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/sla-admin/versions/{versionId}/publish` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/sla-admin/versions/{versionId}/retire` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `PUT` | `/api/v1/sla-admin/versions/{versionId}/escalation` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/sla-admin/calendars` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/sla-admin/calendars` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/sla-admin/calendars/{code}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/sla-admin/calendars/{code}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/slas/{id}/operations` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/system/status` | YES: static status contract | Anonymous | HTTP 200; body discarded | No write requested | Public static status; controller inspected | PASS | Continuation Batch 2; SystemController |
| `GET` | `/api/v1/tickets` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/tickets` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/tickets/{id}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/tickets/{id}/classify` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/route` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/approval` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/approve` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/work-items/{workItemId}/execute` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/work-items/{workItemId}/reassign` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/work-items/{workItemId}/wait` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/work-items/{workItemId}/resume` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/evidence` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/work-items/{workItemId}/complete` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/resolve` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/reopen` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/cancel` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/close` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/tickets/{id}/relationships` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/workflow-admin/workflows` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/workflow-admin/workflows` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/workflow-admin/workflows/{workflowId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `PUT` | `/api/v1/workflow-admin/workflows/{workflowId}` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflow-admin/workflows/{workflowId}/active` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `GET` | `/api/v1/workflow-admin/workflows/{workflowId}/versions` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/workflow-admin/versions/{versionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/workflow-admin/versions/{firstVersionId}/compare/{secondVersionId}` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `GET` | `/api/v1/workflow-admin/tickets/{ticketId}/runtime-history` | No (anonymous check only) | Anonymous | HTTP 401; body discarded | No write requested | Anonymous request denied | PARTIAL: anonymous denial only | Continuation Batch 2 |
| `POST` | `/api/v1/workflow-admin/workflows/{workflowId}/versions/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflow-admin/workflows/{workflowId}/versions/draft` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflow-admin/versions/{versionId}/publish` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflow-admin/versions/{versionId}/retire` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflow-instances` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflow-instances/{id}/activate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/workflows/validate` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/work-items/{publicId}/complete` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |
| `POST` | `/api/v1/work-items/{publicId}/assign` | Not exercised | No authenticated fixture | NOT RUN; no request sent | No mutation requested | No valid actor or safe disposable record; unauthenticated mutation not attempted | BLOCKED | Continuation run record |

## Phase 3 — Security/authorization matrix

- [ ] Anonymous protected routes are denied.
- [ ] Authenticated actors without required permission are denied.
- [ ] Department/team/client scope is enforced for reads and mutations.
- [ ] Changing parent/child/resource/assignee IDs cannot bypass authorization.
- [ ] Super Admin profile rules enforce self/explicit-subordinate scope; team/department membership is not subordination.
- [ ] Ordinary user admins cannot edit/deactivate/reset password/change reporting manager for Super Admins where current policy denies it.
- [ ] Role changes tested only against approved role governance. Unresolved policy is BLOCKED, never inferred.
- [ ] Approval actor/delegation and separation-of-duties controls are enforced.
- [ ] Auditor access is read-only and scoped.
- [ ] Errors/logs/audit/response do not leak secrets or sensitive fields.

## Phase 4 — Automated checks

Run after live browser acceptance. Record exact commands, exit codes, test counts and skips:
- [ ] Focused API/authentication/authorization regression tests.
- [ ] Full .NET test suite.
- [ ] Angular tests (label as unit tests; not E2E).
- [ ] Angular production build.
- [ ] Existing browser E2E suite against real services and approved fixture.
- [ ] `git diff --check`.
- [ ] DB identity verified before/after every database-backed batch.
- [ ] Every skipped test documented with reason and missing configuration.
- [ ] After each fix, rerun focused tests and the affected browser scenario.

## Phase 5 — Defect handling

For each reproduced defect: record steps and actual output; identify responsible code path; make the smallest fix; add a regression test; rerun focused and relevant full tests; repeat the live browser scenario; recheck DB identity; inspect diff; record changed files and residual risks.

Do not invent missing contracts or implement unrelated features such as SSO, email verification, new role governance, or dynamic form serialization without explicit approval.

## Mandatory final report

### Summary
- Overall status: PASS / FAIL / PARTIAL / BLOCKED
- Browser E2E complete: yes/no/partial
- Route inventory counts: PASS / FAIL / BLOCKED / NOT RUN / PARTIAL (out of the supplied total)
- Automated tests: passed / failed / skipped / not run
- Production readiness: not inferred

### Environment
- API/frontend URLs and health responses
- Sanitized database identity before/after every batch
- Fixture setup status (never include values)

### API-by-API evidence
- Attach completed table. No endpoint may be claimed tested because a neighboring endpoint passed.

### Browser evidence
- Login/session, list/search/detail, catalogue, create/persistence, lifecycle, attachments/evidence, approvals/workflow, communication/provider, SLA/reporting/auditor, authorization.

### Defects and changes
- Reproduction, root cause, minimal fix, regression test, retest, changed files.

### Gaps
- Exact route/feature, status, blocker, owner/configuration/product decision required.

### Worktree/safety
- Branch and HEAD before/after
- Dirty worktree preserved
- No commit/push/reset/clean/stash/branch switch
- No shared DB/Oracle/destructive provisioner
- Existing acceptance/audit records retained

## Completion standard

This is complete only when every supplied route has an individual status and all feasible safe cases have evidence. BLOCKED/NOT RUN are honest statuses, not passes. Do not claim full API coverage if only a subset was executed. Do not claim production readiness.

**Begin with preflight and inspect the code contracts. Proceed in small auditable batches; after health checks pass, stop repeating them and execute the next unresolved test.**

### Resource-read authorization continuation

- Source inspection confirmed attachment metadata, version, and download paths are authenticated;
  the download requires scoped `TICKET_READ` on its parent ticket before version retrieval and
  storage access. The inspected API has no signed/presigned URL or unauthenticated file-delivery
  route. Communications, workflow history, approval history, ticket audit reports, and export
  downloads likewise use their owning ticket scope, specific report permission, or export-owner
  check as applicable.
- A reproduced export creation gap was fixed: report permission is now checked before writing the
  initial `RUNNING` export row. The focused denial regression passed without configuring a database
  provider. The related acceptance rows remain **PARTIAL/BLOCKED** as previously recorded; this
  unit test does not prove authenticated HTTP, persistence, or provider behavior.
- Privileged administration source paths derive actors from authenticated claims and apply
  service-level operation permissions; sensitive proposals require independent decision authority,
  while Admin Head delegation mutation remains fail-closed. Authenticated/provider-backed route
  acceptance was not run.
- A subsequent KPI source trace found assigned-ticket aggregation did not honor department/team/client
  bounds and used active client memberships without validity-window checks. The query now applies
  the same ticket-scope constraints as report retrieval and effective client membership dates.
  Database-independent predicate tests passed; they do not prove provider translation or persisted
  cross-scope behavior.
- Ticket/attachment lookups can distinguish an existing but unauthorized ID (403) from a missing
  ID (404). No approved requirement currently specifies existence concealment; the status contract
  is unchanged pending that decision. No protected data is returned on denial.
- Acceptance baseline is unchanged: **8 PASS / 3 PARTIAL / 13 BLOCKED**. No scenario was promoted.

### 2026-10-15 report-schedule permission follow-up

`ReportScheduleService.CreateAsync` now checks the selected report's declared permission before
persisting a schedule, matching the permission check at scheduled export execution. A providerless
denial test passed as part of a 26-test database-independent selection; the API build completed
with 0 warnings and 0 errors. This proves neither EF provider behavior nor scheduled execution
against persisted data. Dashboard, report, auditor, and ticket-search scope behavior was rechecked
at source level; no further independent code-only defect was confirmed. Hosted HTTP identity tests
are possible with the existing custom test host, but an inaccessible resource-ID scope test needs
the real database-backed service and an approved isolated test database. Existing ID-existence
response behavior remains unchanged pending a policy decision. Acceptance remains
**8 PASS / 3 PARTIAL / 13 BLOCKED**.

## Authorization evidence gaps — 2026-10-15

The status baseline remains unchanged. Anonymous-only `PARTIAL` rows do not establish
authenticated permission boundaries or resource scope; `BLOCKED` rows remain untested because
authenticated fixtures, approved disposable data, or unresolved migration decisions are missing.
Close rows only with the following evidence:

| Scenario group | Required evidence to close | Prerequisite/approval |
|---|---|---|
| Ticket search/list/detail, KPI, dashboards, reports, auditor ticket rows | PostgreSQL-provider allow/deny tests for department/team/client/assignee/effective-membership scope, plus hosted authenticated responses and denial-body assertions. Test list count/page results as well as detail. | Approved disposable schema, fixture users/roles/client memberships/tickets, schema and data provisioning/cleanup approval, authenticated host. Existing in-memory predicate tests do not prove translation. |
| Attachment metadata, versions, downloads/evidence | Provider-backed cross-ticket/client/department deny and in-scope allow; hosted HTTP verifies no metadata/content on denial and download only after authorization. Resolve whether missing and inaccessible IDs must both be 404. | Same DB fixtures, test-only storage root and cleanup approval, authenticated host, explicit 403/404 product policy. No signed URL route was found. |
| Export create/retrieve/download and report schedules | Denied create has no persisted export/schedule; authorized schedule create persists the authenticated owner/report/recurrence; scheduled worker exports as the owner; another user cannot retrieve/download and storage is not opened; authorized owner can. | Approved disposable PostgreSQL schema, authentication actors and storage fixture, cleanup approval. |
| Admin, workflow/approval, runtime-history endpoints | Authenticated route-level allow/deny, actor claim binding, status/error mapping, scope and no-mutation-on-denial; provider-backed proposal expiry/audit/initiator-approver persistence. | Approved DB/API fixtures; resolve and approve migrations 031/032 first where applicable. Independent approver and disposable writes must be authorized. |
| RBAC proposal scenarios 3/21 (`PARTIAL`), 4/19 and proposal routes (`BLOCKED`) | Service/provider persistence for >50 expiry outcomes, HTTP binding/status handling, and separately authorized transaction/concurrency evidence for single application. | Resolve migration repair decisions; provider/data approval. Concurrency validation requires separate approval and was not run. |
| Route-table anonymous-only `PARTIAL` GETs | Authenticated permission-specific denial and authorized response for each route group; scoped resource paths require real authorized and unauthorized resource records. | Hosted test authentication; provider-backed services and approved records wherever route behavior depends on persisted scope. |

Validation reviewed but not elevated: in-memory KPI tests exercise predicate boundaries only;
providerless export/schedule denial tests prove ordering before EF access only; the hosted auth test
exercises authentication/password-change pipeline only. Existing
`PostgreSqlApiE2ETests.Export_and_schedule_http_contracts_persist_and_enforce_owner` covers
authorized export and schedule creation, owner download, and cross-owner 404 behavior, but requires
PostgreSQL and was not run in this pass. Existing
`PostgreSqlReportScheduleExecutionTests.Worker_enqueues_only_due_active_schedules_and_retries_without_duplicate_execution`
is a database-backed worker execution test and was not run in this pass; it does not establish
worker behavior execution evidence in this pass. Schedule creation now checks the selected report's
permission before persistence; the unrun E2E covers its authorized path, while a provider-backed
denial/no-persist case remains to be added. No scenario status changed; **8 PASS / 3 PARTIAL / 13
BLOCKED** remains the baseline.


## User administration UI continuation — 2026-10-15

- Added the `USER_ADMIN`-guarded Users page for API-backed user listing, account creation,
  profile updates, activation/deactivation, and temporary-password assignment with forced rotation.
  The app shell now lets the authenticated user reach child routes based on each route's permission;
  the user page no longer depends on the unrelated parent `TICKET_READ` guard.
- Focused page tests cover API-backed loading, department/team selection, create, and profile update.
  Full Angular suite passed **54/54** and the production build succeeded (the existing role-editor
  stylesheet remains 529 bytes over budget).
- The page does not complete client membership, manager, or role-scope administration. The existing
  role editor is separately permission-gated; delegation and scope increases remain blocked pending
  approved independent-approval workflows. These UI results do not establish hosted API behavior or
  persistence. The 24-scenario acceptance baseline remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


- Super Admin authorization now requires the explicit, effective single-organization scope (`ORGANIZATION` with null organization ID) through the common permission and scope checks. Ticket search, reporting, KPI, and auditor resource filters use this scope; no account is automatically granted it. Scope assignment remains blocked pending an independent approval operation and schema deployment. Source/unit tests do not establish database-backed behavior; the end-to-end acceptance baseline remains 8 PASS / 3 PARTIAL / 13 BLOCKED.


## Incident containment and evidence boundary (2026-10-16)

The accidental full-project invocation did not establish PostgreSQL acceptance. It selected the configured `pa_tickets_h_verify` connection because PostgreSQL tests only checked for a connection string; those tests could create providers and fixtures before the schema mismatch failed. The new test guard requires an explicit opt-in token and an independent host/port/database/user target descriptor before any PostgreSQL test setup. The guard is unit-tested without a database. The acceptance baseline remains **8 PASS / 3 PARTIAL / 13 BLOCKED**; no route or scenario is promoted from source inspection or guard tests.

Superseding incident note (2026-10-16): the earlier catalog-inspection statement above is limited to that read-only transaction. A later accidentally broad test invocation did open `pa_tickets_h_verify` and inserted fixture data before failing; see the incident containment section below. No database-backed acceptance is inferred from that run, and no cleanup has been performed.

RBAC administration capability review (2026-10-16): source/UI coverage now includes role and permission catalogue reads, custom-role metadata, atomic permission updates, proposal handling, and reload/error feedback. The five-role seed mechanism remains unverified because migrations are unexecuted and the observed baseline is incompatible; no database-backed role persistence or E2E status is promoted. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

The reviewable migration set is 033-038 under database/migrations/postgresql. These files were not executed; database-backed acceptance remains unchanged at 8 PASS / 3 PARTIAL / 13 BLOCKED.

The master roadmap and static compatibility review are in PA_TICKETS_E2E_MASTER_PLAN.md. Migration 034 needs an application fail-closed compatibility release before database execution; migration 036 is limited to ROLE_PERMISSION_GRANT because scope proposals are not implemented.

Effective-date contract is now fail-closed in source: unknown role-permission starts/statuses cannot authorize. Focused provider-independent coverage passed 52 tests; provider-backed SQL/materialization and persistence remain blocked.

Static effective-grant audit found no alternate role-permission authorization path omitting ACTIVE/status/known-start checks. Cross-provider schema and provider SQL behavior remain unverified.

Final readiness review: provider selection exists for PostgreSQL and Oracle, but schema parity and provider-backed query/materialization remain unverified. Migration execution, provisioning, and E2E promotion remain blocked.

### API authorization coverage phase â€” 2026-10-16

Endpoint inventory: 232 routes across 26 controllers. Server-side auth is present on all business controllers; login and system status are intentionally public. Source-confirmed controls include all five role policy ceilings, active/effective grants, department/team/client/resource scopes, explicit organization scope, forced rotation, user administration, Admin Head delegation boundaries, spectator mutation denial, ticket assignment/execution, report/KPI/export authorization, and approval/idempotency checks. Providerless focused evidence remains 52/52 and API build 0 warnings/0 errors. Angular mocked journey coverage is documented separately and is not backend/database evidence. No acceptance status changed: 8 PASS / 3 PARTIAL / 13 BLOCKED.

## Module mapping Phase 1 verification (2026-10-16)

The operational metadata review found no implementation defect. `TICKET_READ` is used for status/transition/priority reads and `TICKET_ADMIN` for mutations, matching migration 031's explicit TICKETS configuration definition. `CONFIG_ADMIN` is not substituted without an approved requirement. No source behavior changed; no database/provider tests were run.

## Reporting/KPI/export mapping review (2026-10-16)

Static source review found no proven over-broad `TICKET_READ` use for operational reports, KPI calculations, dashboards, or export creation. Scope filtering is applied before aggregation and export generation. Export artifact status/download and report-schedule mutation permission rechecks have no approved contract; no permission or grant was changed. No database/provider tests were run.
