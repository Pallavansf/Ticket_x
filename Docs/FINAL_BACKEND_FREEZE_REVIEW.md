# Final Backend Freeze Review

## Verdict

**Workstream G is PARTIAL**. Regular username/password authentication, local JWT issuance/validation, and app-managed RBAC are implemented and covered by focused API/PostgreSQL tests. Angular tests and build pass, and the API starts and answers health checks using the existing Development profile. Browser-level Angular-to-live-API acceptance, first-administrator provisioning, and several supported UI flows remain incomplete. OIDC/SSO is deferred and optional; E1 dynamic forms remain excluded. This is not production readiness.

This continuation added local authentication code, Angular login wiring, and additive password-hash migrations. The PostgreSQL migration was applied only to the verified disposable database; the Oracle migration was not executed. API E2E tests used transient fixtures and no historical audit record was modified. The repository is on branch `workstream-i-lifecycle-authority` at `0b59513`; existing H/I/F1 changes remain in the worktree and are preserved. Oracle was not accessed.

## Workstream G change areas

- API/application/infrastructure: local password verification, login/JWT issuance and validation, protected password administration, and password-hash persistence.
- Database: additive PostgreSQL and Oracle migration scripts, plus initial/test PostgreSQL schema definitions. Only the disposable PostgreSQL migration was applied.
- Angular: regular-auth login/session behavior and authenticated-request interceptor coverage.
- Tests: PostgreSQL API/RBAC fixture coverage and API security configuration regressions.
- Documentation: `REMEDIATION_TRACKER.md`, `SECURITY_CONFIGURATION.md`, `API_VERSIONING.md`, this review, and the deferred OIDC contract were reconciled to the selected authentication method.

## Workstream status matrix

| Workstream | Status / claim classification | Evidence and limitations |
|---|---|---|
| A — Authorization and resource isolation | Implemented; PostgreSQL-tested; production provisioning is a documented condition | `DatabaseAuthorizationService` checks active users, effective role grants, and department/team/client scopes. Non-admin ticket search applies scope predicates; detail/mutations use ticket authorization. Tests recorded in `REMEDIATION_TRACKER.md` cover authorization/API/workflow paths. Production user/role/scope provisioning and Oracle behavior remain unverified; OIDC is optional if enabled. |
| B — Scheduled-report execution and recovery | Implemented; retry/lease tests exist; exhausted-job operations incomplete | `PersistentBackgroundWorker`, `ReportScheduleJobHandler`, `ReportScheduleService`, and `DurableBackgroundJobRepository` implement persisted schedules/executions/jobs, leases, bounded attempts/backoff, stale lease recovery, and terminal `EXHAUSTED` state. Handler failures are propagated into retry. An operator replay/recovery runbook and duplicate-side-effect semantics at every failure boundary are not established. |
| C — SLA deadlines and escalation | Worker detection implemented and PostgreSQL-regression-tested; policy semantics incomplete | With `Jobs:Enabled`, `PersistentBackgroundWorker` invokes `SlaDeadlineMonitor`; it detects response/resolution deadlines, accounts for persisted pauses/calendar rules, deduplicates breach events, and completes terminal SLAs. `BreachRules`/`NotificationRules` are not interpreted as arbitrary actions. Calendar versions are not immutable per SLA, DST behavior and Oracle execution are unverified, and the worker must be enabled. |
| D — Ticket creation and recovery | Atomic ticket/idempotency persistence implemented and tested; orchestration recovery partial | Creation validates before persistence; ticket and idempotency result share the repository transaction. PostgreSQL tests cover same-key replay, changed-payload conflict, concurrency, and retry after failure. Workflow/approval/SLA orchestration happens in separate contexts after ticket persistence; retry with the same key is caller-driven and no reconciliation sweeper is established. |
| E — Catalogue and dynamic form integrity | Partial | Server validation, request-type version/effective-path selection, and client eligibility filtering have regression evidence. The create UI does not render configured dynamic controls; the fields endpoint returns associations, not definitions/options. Several configured value types lack an unambiguous create serialization contract. |
| F — Audit transaction/hash verification | Partial | Transaction/locking and PostgreSQL regressions are recorded; verifier supports only `TICKET`, `STATUS_DEFINITION`, and `PRIORITY_DEFINITION`. Persisted `STATUS_TRANSITION` row 4258 remains an unresolved mismatch with unproven provenance; other writer formats remain excluded. Verifier is read-only and does not repair records. |

Workstream-level findings and supporting change/test history are in [REMEDIATION_TRACKER.md](./REMEDIATION_TRACKER.md), and the detailed canonicalization/support inventory is in [AUDIT_HASH_DESIGN.md](./AUDIT_HASH_DESIGN.md).

## Security and operational boundaries

- Authenticated API calls are not sufficient authorization by themselves: application permission and resource/scope checks remain part of the service path. Non-admin searches require an explicit filter and are narrowed to the actor's department/team or authorized client/self-assignment scope. Ticket detail and mutation paths use resource-aware checks.
- Auditor integrity routes enforce `AUDITOR_VIEW`. Ticket integrity checks the actor's authorized ticket set and conceals inaccessible tickets. Operational metadata verification remains limited to the two supported configuration streams. Responses expose integrity status/count/failure metadata, not audit payloads.
- Audit verification is strictly read-only and unsupported formats fail closed. It does not rewrite hashes, restore rows, or repair history. Hash chains provide tamper evidence only: they are not immutable storage, do not independently detect tail deletion, and do not establish ISO certification.
- Idempotency is operation-specific. Ticket create accepts an optional API header (the Angular client sends it); some durable work-item and configuration operations require keys. No universal idempotency guarantee is claimed.
- The durable worker persists lease/attempt/failure state, bounded retry, and exhausted state. Schedules and SLA polling require `Jobs:Enabled`. Communication delivery has a separate worker/provider boundary. Exhausted jobs require explicit operator inspection; eventual success is not guaranteed.
- Current local authentication uses app-managed username/password and a locally signed JWT; production signing-key provisioning/rotation and first-administrator provisioning remain open. OIDC issuer/authority settings are only needed when the optional OIDC scheme is enabled. Production also requires explicit HTTPS CORS origins, a trusted proxy allowlist where forwarded headers are enabled, and a production malware scanner. The deterministic scanner is Development/Testing only, while the default unconfigured scanner fails closed. Communication delivery requires a real configured provider. Oracle 19c persistence, migration, transaction and concurrency behavior remain unverified.
- No fake-success API behavior or invented route was introduced. The only direct schema change was the additive password-hash column migration on the verified disposable PostgreSQL database; transient integration fixtures were cleaned by the tests. No audit history was rewritten, and no destructive provisioner was run.

## Contracts available to the current Angular client

The Angular `TicketsApiService` currently uses real backend contracts:

| Use | Existing contract |
|---|---|
| Ticket search/list | `GET /api/v1/tickets` with page/pageSize and supported ticket filters |
| Ticket detail | `GET /api/v1/tickets/{id}` |
| Ticket creation | `POST /api/v1/tickets` with the current DTO and `Idempotency-Key` |
| Catalogue selectors | `GET /api/v1/admin/catalogue/services`, `/categories`, `/sub-categories`, and `/request-types` with hierarchy filters |
| Work-item completion | `POST /api/v1/work-items/{publicId}/complete` with required `Idempotency-Key` |

The ticket controller also has real lifecycle routes for classify, route, approval request/decision, execute/reassign/wait/resume/complete, evidence, resolve/reopen/cancel/close, and relationships. Their existence does not mean the current Angular client implements all of those interactions or that backend-only endpoints should be used without their established DTOs, permission checks, and tests.

The API map identifies additional backend-only groups: attachment operations; approval and workflow runtime/configuration; reports, exports, schedules; SLA; auditor reports; and administration/catalogue authoring. PDF export is explicitly unsupported. Do not fabricate missing routes or payload shapes.

## Unsupported UI flows and missing contracts

- Dynamic request-type form rendering: no endpoint returns associated field definitions and selectable options; value serialization is not defined for all configured field types.
- Full ticket detail/lifecycle interface: only a subset of the real transition routes is wired into the current Angular service/UI.
- Attachment upload, version history/download, and review interaction.
- Approval/workflow authoring and runtime interaction.
- Auditor reports/integrity UI, report/export/schedule UI, SLA UI, and administration UI.
- PDF export is absent from the backend API.

These are scope exclusions for local acceptance, not claims that corresponding backend contracts do not exist. Dynamic-field-configured ticket creation is excluded until the read/serialization contract and UI are complete.

## Findings by severity

### Critical

None established by this source/test review.

### High

1. **Dynamic ticket fields remain incomplete (E partial).** Configured required fields cannot be rendered end-to-end by the Angular create UI. A deployment relying on those fields must not treat that create path as accepted.
2. **Historical audit formats remain unverifiable (F partial).** `STATUS_TRANSITION` row 4258 has an unresolved `HASH_MISMATCH`; no writer provenance is established. Unsupported persisted formats must remain excluded.
3. **Production security/provider requirements are deployment gates.** Local tests do not demonstrate live identity integration, production malware scanning, trusted proxy/CORS configuration, communication delivery, or Oracle compatibility.

### Medium

1. **Ticket orchestration recovery is caller-driven.** A post-persistence workflow/approval/SLA failure requires retry using the same idempotency key; no automatic reconciler is established.
2. **Exhausted durable jobs need an operational recovery procedure.** Persisted retry/dead-letter state exists, but the reviewed docs do not establish operator replay/recovery and alerting end-to-end.
3. **SLA policy/calendar semantics remain partial.** Rule JSON interpretation, immutable calendar-version history, DST behavior, and Oracle execution are open.

### Low

1. `tests/POSTGRESQL_INTEGRATION.md` incorrectly stated that the SLA runtime did not automatically detect response/resolution deadlines. This review corrected it to reflect the worker-driven monitor and its actual `Jobs:Enabled`/policy limitations.
2. The Phase 8.7 simulation report is historical evidence and predates the A–F continuation; it is not a fresh Workstream G validation result.

## Workstream G acceptance results

| Acceptance item | Result | Evidence / limitation |
|---|---|---|
| Angular build and unit suite | PASS | Production build completed; 10 test files and 29 client tests passed. Tests use mocked HTTP. |
| API persistence/contracts | PASS, scoped | Filtered PostgreSQL API E2E plus administration authorization batch passed 15/15 using `pa_tickets_h_verify` / `pa_tickets_test_user`; this is not a browser-to-live-API session. |
| Local API health | PASS | The existing `http` launch profile binds port 5018. Starting it with process-scoped PostgreSQL provider configuration and the approved disposable connection returned HTTP 200 for both `/health/live` and `/health/ready`; the prior failure was because no API process was listening. |
| Regular username/password authentication | PASS, scoped | Password-hash login, generic failed-login response, local HMAC JWT, and database RBAC/scope checks passed with a disposable fixture. First-administrator provisioning and live Development-profile login remain unverified. |
| OIDC / SSO | DEFERRED | Future optional add-on; it is not a prerequisite for local acceptance. |
| Client/tenant selection | PARTIAL | Search API accepts `clientId`, but the Angular list has no client/tenant selector. |
| Supported ticket flows | PASS, scoped | Search/list, detail, base ticket create, catalogue selectors, and work-item completion are covered by current client tests/API contract evidence. Broader lifecycle routes are not all implemented in the UI. |
| Authorization/error states | PASS, scoped / PARTIAL | Anonymous ticket search returned 401; local-auth integration accepted an in-scope department/team search and denied a foreign department and non-admin password reset with 403. Angular interceptor tests cover bearer attachment with mocked HTTP; live browser attachment remains unverified. |
| E1 dynamic forms | EXCLUDED | Dynamic field definitions/options and full typed-value serialization are unsupported; dynamic-field-configured creation is not accepted. |

Fresh commands and results:

```text
client> npm.cmd test -- --watch=false
Exit code: 0; 10 test files passed; 29 passed, 0 failed, 0 skipped.

client> npm.cmd run build
Exit code: 0; Angular production build completed.

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~Local_password_login_issues_a_valid_bearer_and_preserves_database_rbac|FullyQualifiedName~ApiSecurityConfigurationTests"
Exit code: 0; 4 passed, 0 failed, 0 skipped.

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests"
Exit code: 0; 15 passed, 0 failed, 0 skipped (API E2E plus administration authorization).

dotnet build PA.Tickets.sln --no-restore --nologo --verbosity:minimal
Exit code: 0; 0 warnings, 0 errors.
```

The PostgreSQL identity was freshly queried before each DB-backed test batch and live API readiness/auth probes, and after both DB test batches. Every result was exactly `pa_tickets_h_verify|pa_tickets_test_user`. The shared `pa_tickets_test` database and Oracle were not accessed; no destructive schema provisioner was run. `dotnet test PA.Tickets.sln --no-restore --nologo` was not run, and no full-suite-clean claim is made.

For API startup and health checks, `dotnet run --project src/PA.Tickets.Api/PA.Tickets.Api.csproj --no-build --launch-profile http` ran with process-only PostgreSQL and local JWT configuration. `/health/live` and `/health/ready` returned 200; anonymous ticket search returned 401. The API was stopped after verification. Its initial absence from the configured port was the cause of the earlier health failure. The successful password-login/RBAC test used a separate API subprocess in the `Testing` environment and a transient fixture; it did not use the Development process or a browser session.

The first npm invocation through PowerShell's `npm.ps1` shim was blocked by the active execution policy. Invoking the existing `npm.cmd` succeeded; the execution policy was not changed.

## G disposition and next step

Keep Workstream G **PARTIAL** until browser-level login and ticket search/detail/create/catalogue/work-item completion run against the live API with a provisioned account. Client/tenant selection, first-administrator bootstrap, password rate limiting/lockout, and broader lifecycle UI remain gaps. OIDC/SSO is deferred; E1 dynamic fields are excluded. These checks do not establish production readiness, Oracle compatibility, or full-suite status. Preserve I1 as CLOSED for its scoped backend remediation and F1 as PARTIAL.
