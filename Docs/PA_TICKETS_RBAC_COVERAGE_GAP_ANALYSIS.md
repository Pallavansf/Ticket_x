# PA_TICKETS RBAC Coverage and Evidence Gap Analysis (Gate T48)

**Repository:** `G:\WSS\aw\Tickets`  
**Branch observed:** `workstream-i-lifecycle-authority`  
**Review mode:** documentation-only, read-only inventory. No database, provider, SQL, migration, provisioning, or database-backed test operation was performed.

## Purpose and evidence vocabulary

This document compares the authoritative route inventory with the authorization code and the hosted/policy evidence already present in the repository. It is an evidence map, not an approval to change authorization or to execute migration 026.

Evidence labels:

- **VERIFIED** — the cited provider-independent test exercises the named behavior through the real ASP.NET Core `Program`/pipeline and asserts the result.
- **PARTIAL** — a representative route or one side of a rule is covered; the family or all relevant paths are not covered.
- **UNVERIFIED** — the source describes a rule or a policy/unit test exists, but the required hosted boundary or failure path is not demonstrated.
- **BLOCKED** — evidence requires provider/runtime access that this gate forbids or that has not been approved.

The authoritative inventory is `Docs/PA_TICKETS_API_ENDPOINT_INVENTORY.md`: 26 controllers declare 232 HTTP routes. Its route and authorization statements are source-level expectations; it explicitly says hosted persistence/provider behavior remains unverified.

## A. Boundary and policy evidence matrix

| Boundary or policy | Existing evidence | Classification | Remaining limitation |
|---|---|---|---|
| Login, password rotation, and anonymous boundary | `HostedAuthenticationAuthorizationTests` and the auth middleware/rotation middleware; login is public, change-password is authenticated, and rotation is exempt only for change-password | PARTIAL | The hosted cases are focused, not a complete auth matrix; persisted users and provider behavior are unverified. |
| Protected endpoint anonymous result (401) | Real `Program` pipeline is used by T43–T47 fixtures; inventory defines protected-controller behavior | PARTIAL | The cited T43–T47 cases are authenticated denials. A route-by-route anonymous matrix was not run for this gate. |
| Global Spectator mutation filter | `HostedRbacAuthorizationTests.Actual_program_denies_active_spectator_before_mutating_authentication_service`; `SpectatorMutationPolicyTests` | VERIFIED | One mutating path and one role are exercised; every unsafe verb, alternate route, and positive read path still need matrix evidence. |
| Effective grant and role policy | T44 `Authenticated_user_without_effective_permission_is_denied_before_report_query`; effective-grant and role policy tests | VERIFIED | The hosted case uses deterministic facts. SQL translation, materialization, persisted role IDs, expiry, and provider failure remain unverified. |
| Ticket resource/cross-scope authorization | T45 `Authenticated_user_is_denied_for_ticket_outside_department_scope`; real ticket service and authorization path; repository save/search is not reached | VERIFIED | One department mismatch is covered. Team, client, record, organization, in-scope positive, and other ticket methods are not hosted-matrix evidence. |
| Admin Head delegation | T46 `Admin_head_without_active_delegation_is_denied_for_target_memberships`; delegation policy tests | VERIFIED | The absent-delegation denial is covered. Expired/revoked/positive delegation and other admin routes are not. |
| Super Admin explicit organization scope | T47 `Super_admin_without_explicit_organization_scope_is_denied_ticket_search`; scope policy tests | VERIFIED | Missing organization scope is denied. Positive organization scope and other resource families are not hosted evidence. |
| Admin/catalogue and operational metadata scopes | Inventory and service/policy source checks; no representative hosted route in T43–T47 | UNVERIFIED | The declared permission/resource scope rules need hosted allow/deny evidence. |
| Approval-admin and approval execution/initiator separation | Policy and service tests, including approval policy tests; no hosted approval route in this gate | PARTIAL | Persistence, idempotency, initiator/approver separation, and route coverage are unverified. |
| Reports, report query, and export authorization | T44 hosted report-query denial; existing report policy/query-denial tests | PARTIAL | Export, schedule, dashboard, KPI, and positive authorized paths are not represented. |
| Attachments, evidence, changes, problems, work-items | Inventory and service authorization tests | UNVERIFIED | No hosted representative route demonstrates the combined permission and resource-scope boundary. |
| Workflow, SLA, communication, and administration families | Inventory plus focused policy/service tests | UNVERIFIED | Hosted endpoint/method coverage is absent; alternate routes and verbs are not proven. |
| No mutation after denial / no audit side effect | T43/T45 denial-before-service assertions and repository `SaveCalls == 0` spies; T44/T46/T47 stop before the protected operation | PARTIAL | Provider transaction/audit durability and all denial paths are not established by fakes. |
| Missing or unavailable authorization facts fail closed | Policy tests cover invalid/missing facts and unknown values | UNVERIFIED | No hosted test demonstrates reader exception, unavailable provider, or startup failure behavior. |
| Provider persistence and runtime parity | Provider-backed tests exist in the repository but were not run | BLOCKED | This gate forbids provider operations; Npgsql/SQL translation, persistence, transactions, and cross-user isolation remain open. |

The five Gate T43–T47 hosted scenarios are genuine application-pipeline evidence, but they are representative denial cases rather than 232 independent route proofs.

## B. Route-family inventory and coverage

All inventory declarations were classified at family level: **232/232 routes** are assigned to a documented authorization family. This is classification completeness, not equivalent hosted authorization coverage.

| Route family (inventory) | Routes | Current evidence |
|---|---:|---|
| auth | 2 | Focused hosted auth/rotation; PARTIAL |
| system | 1 | Public status classification; hosted auth evidence does not cover all status behavior |
| admin | 38 | T46 is one representative route; PARTIAL |
| admin/catalogue | 25 | Source/policy only; UNVERIFIED hosted |
| admin/operational-metadata | 15 | Source/policy only; UNVERIFIED hosted |
| approval-admin | 13 | Policy/service only; PARTIAL |
| approvals | 3 | Service/policy only; PARTIAL |
| attachments | 4 | Service/policy only; UNVERIFIED hosted |
| evidence | 2 | Service/policy only; UNVERIFIED hosted |
| auditor/reports | 5 | Source/policy only; UNVERIFIED hosted |
| changes | 14 | Service/policy only; UNVERIFIED hosted |
| problems | 12 | Service/policy only; UNVERIFIED hosted |
| tickets | 18 | T45 and T47 are representative; PARTIAL |
| work-items | 2 | Service/policy only; UNVERIFIED hosted |
| workflow-admin | 13 | Service/policy only; UNVERIFIED hosted |
| workflow-instances | 2 | Service/policy only; UNVERIFIED hosted |
| workflows | 1 | Service/policy only; UNVERIFIED hosted |
| sla-admin | 16 | Service/policy only; UNVERIFIED hosted |
| slas | 1 | Service/policy only; UNVERIFIED hosted |
| communication-admin | 20 | Service/policy only; UNVERIFIED hosted |
| communications | 5 | Service/policy only; UNVERIFIED hosted |
| reports | 3 | T44 covers one query denial; PARTIAL |
| report-exports (`api/v1/reports/.../export`) | 3 | Inventory/source only; UNVERIFIED hosted |
| report-schedules | 8 | Inventory/source only; UNVERIFIED hosted |
| dashboards | 3 | Inventory/source only; UNVERIFIED hosted |
| kpis | 3 | Inventory/source only; UNVERIFIED hosted |
| **Total** | **232** | Family classification complete; hosted/provider evidence incomplete |

The inventory records class-level `[Authorize]` protection for business controllers, the global Spectator unsafe-verb filter, effective-grant validity (`Active`, `ACTIVE`, valid-from/window), and explicit organization scope for organization-wide access. Those are implementation requirements and source evidence; they do not establish persisted-provider behavior.

## C. Prioritized coverage and safety gaps

1. **High — representative hosted route/method matrix.** Add provider-independent hosted positive and negative cases for each high-risk family, including alternate HTTP verbs, report exports, attachments/evidence, workflow execution, approval execution, and administrative mutation routes. Include anonymous 401, authenticated 403, authorized success, and no-side-effect assertions.
2. **High — persisted grant, scope, delegation, and role translation.** A separately approved PostgreSQL evidence gate is needed for active/expired/revoked rows, organization scope, client/team/department/record scope, Admin Head delegation, and cross-user isolation. Fakes cannot establish SQL translation or materialization.
3. **High — fail-closed unavailable-facts behavior.** Exercise reader exceptions, unavailable provider, malformed facts, and startup/runtime failure at the hosted boundary. The expected result is a deterministic denial/error with no protected operation or audit success claim.
4. **High — positive controls.** Current evidence is denial-heavy. Add in-scope ticket access, valid Admin Head delegation, explicit Super Admin organization scope, valid approval initiator/approver separation, and authorized mutation cases.
5. **Medium — alternate route and verb bypasses.** Build an inventory-driven method matrix proving the global Spectator filter and service authorization cannot be bypassed through another action, route, controller, or HTTP verb.
6. **Medium — audit and transaction evidence.** Provider-backed testing must establish that denied requests do not create misleading audit records and that authorization facts, mutation, and audit outcomes have the intended transaction/atomicity behavior.
7. **Medium — concurrency and revocation.** Runtime evidence is still required for concurrent requests, grant revocation/expiry during a request, delegation changes, and cross-scope isolation.

## D. Provider and runtime limitations

No PostgreSQL or Oracle connection, SQL statement, migration, provisioning script, seed, cleanup, sequence reset, or database-backed test was run. The protected databases were not accessed. Existing provider-backed tests are only noted as repository artifacts and were intentionally not rerun.

The hosted tests use the real ASP.NET Core pipeline with `TestingAuthenticationHandler`, deterministic fact readers, sentinel connection strings (`127.0.0.1:1`), and spies. They prove control flow and denial ordering. They do not prove Npgsql/SQL translation, schema/materialization, persisted role and grant identifiers, transaction/audit atomicity, provider error handling, concurrent revocation, or real 401/403 behavior backed by production persistence.

## E. Evidence-driven follow-up gates (maximum three)

1. **Hosted route/method coverage gate:** expand provider-independent tests from representative denials to an inventory-driven matrix with positive controls, alternate methods/routes, anonymous 401, authenticated 403, authorized success, no-side-effect assertions, and missing-facts failure cases.
2. **Approved PostgreSQL persistence gate:** under separately approved target, identity, security, and database-owner controls, verify persisted grants/scopes/delegations, SQL translation/materialization, cross-user isolation, transaction/audit behavior, and recovery evidence. This gate must not be inferred from fakes.
3. **Runtime concurrency/revocation gate:** under provider approval, verify concurrent authorization, revocation/expiry races, delegation changes, and audit durability/atomicity. Keep execution authorization for migration 026 separate.

## F. Validation performed for Gate T48

Read-only inspection covered the Gate T48 request, `Docs/PA_TICKETS_API_ENDPOINT_INVENTORY.md`, the authorization/facts/scope/delegation implementations, `Program.cs`, the T43–T47 hosted tests, and related policy tests. The branch and complete status were captured before and after inspection. No T48 test command was run. The T43–T47 focused result reported by their respective gates was 28 passed and 0 failed; that result is historical evidence and was not independently rerun here.

## G. Worktree preservation

Before this document was created, `git status --short --untracked-files=all --ignored` reported 24,159 entries on `workstream-i-lifecycle-authority`, including extensive pre-existing modified, untracked, and ignored work. Those entries were preserved. The only file attributable to Gate T48 is this new documentation proposal; no source, test, migration, environment, configuration, or existing document was changed.

