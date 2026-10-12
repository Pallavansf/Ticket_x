# Phase 8.7 backend simulation and readiness report

Date: 2026-10-09 (Asia/Calcutta)

## Database identity and isolation

The simulation verified `current_database() = pa_tickets_test` and `current_user = pa_tickets_test_user` before any write. The harness refuses `names_trainer`, production-like database names, and any database other than `pa_tickets_test`. `tests/scripts/phase87-simulation.ps1` runs the existing PostgreSQL provisioner, applies the Phase 8.6 migration, and then runs one serial test process. No concurrent test processes are permitted against the mutable database. Oracle was not connected.

The provisioner performs a repeatable dedicated-test-database reset, reapplies reference/schema slices, and runs commit/rollback smoke checks. Existing API E2E tests use generated identifiers and cleanup paths rather than depending on unrelated shared rows.

## Baseline and final verification

| Gate | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Phase 8.6 focused export/schedule tests | 8 | 0 | 0 |
| PostgreSQL integration tests (final) | 77 | 0 | 0 |
| Phase 8.7 HTTP simulation set | 17 | 0 | 0 |
| Relevant security/auth regression subset | 12 | 0 | 0 |
| Full solution (final) | 162 | 0 | 0 |
| Solution build | 0 warnings | 0 errors | — |

The pre-change baseline was 8 focused tests, 76 PostgreSQL tests, 161 full-suite tests, and a clean build. The additional PostgreSQL test is the new export/schedule HTTP scenario.

## Synthetic data and scenarios

The simulation uses existing application services and the real API host. The HTTP set contains nine PostgreSQL API scenarios covering authentication and unsupported versions, health/readiness, ticket/workflow/idempotency/concurrency, approvals/delegation/separation of duties, SLA operations, reports, auditor access, and the new export/schedule owner-boundary flow. Formatter and recurrence tests add eight deterministic Phase 8.6 checks. Generated records use test identities and are cleaned up by the existing trackers.

Repository PostgreSQL tests additionally cover evidence hashing/versioning and scanner outcomes, communication queue/retry/terminal failure behavior, SLA persistence and durable jobs, workflow joins, authorization, audit history, and provider-specific persistence. This is a repeatable test fixture set, not a permanent business dataset.

## Defect found and fixed

The first real HTTP export request failed with PostgreSQL SQLSTATE `42804`: `S_FILTERS_JSON` is `jsonb` but the EF parameter was sent as `text`. The export service failed while persisting its durable row, so no file was published. The same issue applied to scheduled report filters.

`ExecutionDbContext` now maps export and schedule filter properties to PostgreSQL `jsonb` only when the active provider is PostgreSQL. Oracle mappings are unchanged. The new HTTP simulation passes export creation, download authorization, schedule persistence, owner isolation, pause, resume, and cancel behavior after the fix. The final simulation harness run completed against the verified dedicated database.

## Readiness findings

- **BLOCKER fixed:** PostgreSQL export/schedule persistence could not create durable rows until the provider-specific JSON mapping was added.
- **HIGH:** none reproduced after the fix. Serial PostgreSQL and full-suite runs are clean.
- **DEFERRED:** PDF export remains unsupported; no placeholder PDF is returned. Local file storage is suitable only for the current single-instance development setup. External communication providers and Oracle deployment behavior require environment-specific validation.
- **INFORMATIONAL:** enum request values currently follow the existing numeric JSON contract (`Format: 0`, `Frequency: 0`); the API map records the controller contracts. Angular is not included in this phase.

The following are intentionally not claimed as production deployment approval: shared multi-instance export storage, organizational retention policy, live external delivery, and Oracle compatibility.

## API contract deliverable

[API_V1_SCREEN_CONTRACT_MAP.md](API_V1_SCREEN_CONTRACT_MAP.md) inventories the implemented `/api/v1` controller routes, groups them by future screen, records shared authorization/error/pagination behavior, and identifies the known format/storage limitations.

## Freeze decision

The backend test and build gates pass, the PostgreSQL provider is verified, the HTTP simulation is repeatable, and no unresolved BLOCKER or HIGH finding remains. The documented deferred items are deployment or future-provider limitations and do not change the current versioned API contracts.

**BACKEND READY FOR ANGULAR**