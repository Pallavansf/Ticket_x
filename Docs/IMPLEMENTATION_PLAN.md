# Implementation plan

## Completed
- [x] Inspected supplied Oracle baseline schema.
- [x] Created .NET 8 Clean Architecture solution and initial test project.
- [x] Created Angular standalone shell with lazy-loaded dashboard and tickets feature entry points.
- [x] Added API health checks, Swagger, CORS, exception boundary, and DI extension points.
- [x] Created database deployment layout and preserved the baseline schema.
- [x] Performed static schema review; Oracle deployment validation remains pending a target database.
- [x] Documented architecture and significant decisions.

- [x] Added Oracle 19c EF Core provider, mappings, sequence identity generator, transactional repository, and authenticated lifecycle endpoints.
- [x] Added domain lifecycle/SOD/evidence/versioning tests (4 passing).
- [x] Added hardening migrations 002/003 and audit hash design.
- [x] Verified API host health/status endpoints without Oracle.
- [x] Extended the core engine with waiting/resume, reassignment, reopen/cancel, relationships, ownership projection, and paginated search.
- [x] Added communication, provider-neutral authentication/MFA, and versioned SLA foundations with migration 005.
- [x] Added Phase 3 core operations, relationship policy, paginated search, and minimal Angular ticket create/list UI.
- [x] Added Phase 4 workflow validation/runtime, approval/SOD/delegation, deterministic SLA clocks, migration 006 concurrency, and durable job contracts.
- [x] Added durable workflow configuration/runtime, approval, SLA, communication idempotency, background job repositories, migration 007, and administrator workflow draft/publish APIs.

## Next phases
- [x] Selected Oracle 19c / `Oracle.EntityFrameworkCore` 8.23.40; secret-managed connection and target validation remain pending.
- [x] Implemented the first ticket aggregate lifecycle, authorization boundary, transaction contract, event, and audit persistence.
- [ ] Validate configuration-based workflow, approval, SLA, and security rules with business owners.
- [x] Added first-slice REST resources; API integration tests and Oracle-backed tests await a 19c instance.
- [ ] Build ticket UI, administration, reporting, and auditor read-only capability.








