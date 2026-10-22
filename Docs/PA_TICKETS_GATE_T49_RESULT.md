# PA_TICKETS Gate T49 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Review type:** read-only alternate-route and HTTP-method authorization verification  
**Database/provider activity:** none

## Baseline and scope

Before inspection, `git status --short --untracked-files=all --ignored` reported 24,160 entries. The Gate T48 report and `Docs/PA_TICKETS_API_ENDPOINT_INVENTORY.md` were read first, followed by the controller mappings, `Program.cs`, the authorization service boundary, and the existing T43–T47 hosted tests. No full test suite, database-backed test, SQL, provider operation, migration, provisioning, or protected-target access was performed.

The inventory contains 232 routes. T49 required one evidence-backed alternate path only if source tracing demonstrated a meaningful authorization gap. The source did not demonstrate one, so no test or production code was added.

## Candidate 1: administration profile versus membership route

**Routes examined**

- `GET /api/v1/admin/users/{id}` (`AdministrationController.GetUser`)
- `GET /api/v1/admin/users/{id}/roles` (`AdministrationController.UserRoles`)

Both actions are under `[Authorize]` on `AdministrationController` (`src/PA.Tickets.Api/Controllers/AdministrationController.cs:9-14,82-83`). The application pipeline runs authentication, password-rotation middleware, authorization, and controller mapping before either action (`src/PA.Tickets.Api/Program.cs:95-100`). The actor is taken from the authenticated subject claim and invalid subjects throw (`AdministrationController.cs:112-115`).

The two handlers are intentionally different operations. `UserRoles` reads an active target through `IAdministrationTargetReader`, then invokes `authorization.EnsurePermissionAsync` with `ROLE_ADMIN` and the target department/team/user resource before querying memberships (`AdministrationService.cs:860-870`). This is the path covered by T46; the missing-delegation Admin Head case returns 403 before membership data is returned.

`GetUserAsync` is a profile operation. It loads the target, permits an authenticated user to read their own active profile, and for a different target invokes `EnsureUserAdministrationAsync`; it rejects privileged target profiles for ordinary administration (`AdministrationService.cs:50-70`). The self-profile branch is an explicit product rule and does not grant role, scope, delegation, or membership data. The non-self branch still passes through the production authorization service. A fake reader cannot safely turn this profile route into a provider-independent hosted test because the target profile is loaded by the real EF context before the authorization decision.

**Conclusion:** no alternate privilege path was shown. The routes expose different resources, and the role-membership route has the stronger, delegation-aware resource check. A new test would either duplicate T46 or replace the profile persistence boundary with a test-only substitute, which would not prove the production path.

## Candidate 2: report export creation, status, and download

**Routes examined**

- `POST /api/v1/reports/{reportCode}/exports`
- `GET /api/v1/exports/{exportId}`
- `GET /api/v1/exports/{exportId}/download`

All three actions are in `ReportExportsController`, whose class-level `[Authorize]` applies to every method (`src/PA.Tickets.Api/Controllers/ReportExportsController.cs:9-23`). Each action derives the actor from the authenticated subject claim (`ReportExportsController.cs:26-30`).

The service does not rely on the class attribute alone. Export creation obtains the report definition and calls `authorization.EnsurePermissionAsync` before creating the export row (`src/PA.Tickets.Infrastructure/Reporting/ReportExportService.cs:44-52`). Status and download both call `Authorized`, which restricts the row to the requesting actor, then call `EnsureCurrentReportPermissionAsync` before returning status or considering file access (`ReportExportService.cs:123-143`). Download additionally rejects expired, incomplete, or storage-less rows and currently fails closed with `UnauthorizedAccessException` when artifact context cannot be authorized (`ReportExportService.cs:130-137`).

**Conclusion:** the download route is a higher-risk alternate data path, but source evidence shows owner binding plus a current permission check and fail-closed artifact handling. There is no source-supported bypass to test. A provider-independent hosted test would need an in-memory export persistence/storage implementation; that would not establish the production owner query or EF behavior and would duplicate service-level tests already present.

## Existing versus new coverage

Existing evidence includes T44 hosted denial through the real report-query pipeline, T46 hosted denial through the real administration membership route, policy/service tests for report export authorization, and the T48 route-family inventory. These tests are representative and do not constitute a 232-route matrix, but they cover the relevant authorization boundaries without changing production policy.

No T49 test was added because:

1. The source trace did not reveal an equivalent handler missing authentication, authorization, scope, ownership, or permission enforcement.
2. The only plausible candidates either have distinct, intentional resource semantics (profile versus memberships) or already enforce owner and current-permission checks (export status/download).
3. A new test would require provider-backed persistence or a substitute that would bypass the production boundary, contrary to T49's evidence requirements.

## Validation and worktree evidence

No test command was run for T49. No files other than this result document were changed by this gate. The final branch remained `workstream-i-lifecycle-authority`; final status reported 24,161 entries, reflecting the single new untracked result document. All pre-existing modified, staged, untracked, and ignored work was preserved. No database/provider operation occurred.

## Remaining limitations

The route inventory still needs an inventory-driven hosted matrix covering anonymous 401, authenticated 403, authorized success, alternate HTTP methods, and no-side-effect assertions. Provider-backed evidence remains necessary for EF/Npgsql translation, persisted export ownership and permissions, transaction/audit behavior, and concurrent revocation. Those are separate follow-up concerns and are not established by this read-only source review.

