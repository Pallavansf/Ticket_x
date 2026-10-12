# Final Backend Freeze Review

## Verdict

**READY WITH EXPLICIT LIMITATIONS** for local Angular integration and acceptance testing against the implemented API contracts listed below. This is not production readiness. Workstreams E and F remain partial; integration acceptance must exclude the corresponding unsupported flows.

This review audited the current workspace documentation, API/controller surfaces, selected implementation paths, and associated test evidence. It did not authorize a broad rewrite. No backend behavior, API route, schema, migration, or persisted database record was changed. Git status/diff could not be obtained: `G:\WSS\aw\Tickets` is not a Git repository. Oracle was not accessed.

## Files changed in this review

- `Docs/FINAL_BACKEND_FREEZE_REVIEW.md` — this evidence-based freeze decision.
- `Docs/REMEDIATION_TRACKER.md` — Workstream G status, evidence, limitations, validation, and next phase.
- `tests/POSTGRESQL_INTEGRATION.md` — corrected the stale statement about worker-driven SLA deadline detection.

## Workstream status matrix

| Workstream | Status / claim classification | Evidence and limitations |
|---|---|---|
| A — Authorization and resource isolation | Implemented; PostgreSQL-tested; identity/deployment integration is a documented condition | `DatabaseAuthorizationService` checks active users, effective role grants, and department/team/client scopes. Non-admin ticket search applies scope predicates; detail/mutations use ticket authorization. Tests recorded in `REMEDIATION_TRACKER.md` cover authorization/API/workflow paths. Production IdP and organizational scope integration, and Oracle behavior, remain unverified. |
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
- Production deployment requires real OIDC/JWT issuer, audience and authority configuration; explicit HTTPS CORS origins; a trusted proxy allowlist where forwarded headers are enabled; and a production malware scanner. The deterministic scanner is Development/Testing only, while the default unconfigured scanner fails closed. Communication delivery requires a real configured provider. Oracle 19c persistence, migration, transaction and concurrency behavior remain unverified.
- No fake-success API behavior or invented route was introduced by this review. No database write, reset, truncate, or provisioning script was run.

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

## Validation and safety

Fresh commands and results:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditChainVerifierTests"
Exit code: 0; passed 8, failed 0, skipped 0.
```

These are in-memory verifier unit tests and do not establish PostgreSQL behavior or historical writer compatibility.

```text
dotnet build PA.Tickets.sln --no-restore --nologo
Exit code: 0; build succeeded; 0 warnings, 0 errors.
```

The full suite command was **not run**:

```text
dotnet test PA.Tickets.sln --no-restore --nologo
```

The configured target is the persistent `pa_tickets_test` database. The current test role cannot create a database; PostgreSQL integration tests mutate shared data and include explicit deletes; `tests/scripts/provision-postgresql-schema.ps1` truncates multiple tables. No disposable isolated database was established, so running the full suite would risk shared state. Historical results (including a recorded 180/180 run) are not fresh results for this review. Angular tests and production build were also not rerun in this backend-only pass.

No Oracle connection/script was used. No database records were read or written during this review's fresh validation commands.

## Freeze decision and next approved phase

Proceed with local Angular integration only against the real contracts listed above. Limit acceptance to the current list/detail/create/catalogue-selector/work-item-completion scope; exclude dynamic-field-configured creation and backend-only screens/actions. Before full PostgreSQL integration testing, provision and verify a disposable isolated test database that cannot affect shared records.

The next approved activity is the local Angular integration and acceptance phase within those bounds—not Workstream H. Production release approval requires separate Oracle 19c validation, production identity/scanner/proxy/CORS/provider configuration, explicit durable-job exhausted-state operations, and resolution or formal risk acceptance for E/F and SLA limitations. No workstream is represented as production-ready by this verdict.
