# Workstream H — critical contract corrections

## Scope

This report follows the actual repository state and separates source inspection from executed local tests. No Oracle connection, PostgreSQL integration tests, or shared-database writes occurred. A read-only PostgreSQL catalog probe against the configured test target was used only to establish the target and its database-creation permissions; no destructive database action occurred.

## Evidence categories

### Source inspection (repository state reviewed)

- `src/PA.Tickets.Domain/Tickets/WorkItem.cs` stores `Number` and `PublicId` as distinct values; the display number is not replaced by the public ID.
- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` maps `new WorkItemView(w.Id, w.Number, w.PublicId, ...)` and preserves both values.
- `src/PA.Tickets.Application/Tickets/TicketCommands.cs` defines `WorkItemView(long Id, string Number, string PublicId, ...)`.
- `client/src/app/core/tickets-api.service.ts` routes completion through `/api/v1/work-items/{publicId}/complete`.
- `client/src/app/features/tickets/ticket-detail.page.ts` keeps `pendingCompletion` on `item.publicId` and submits that value in `confirmCompletion()`.
- `src/PA.Tickets.Api/Controllers/WorkItemsController.cs` expects a route parameter named `publicId` and rejects blank `Idempotency-Key` values.
- `src/PA.Tickets.Infrastructure/Foundations/CommunicationDelivery.cs` contains the actual `CommunicationProviderResolver` and its production-mode guard.
- `src/PA.Tickets.Infrastructure/Foundations/CommunicationDeliveryProcessor.cs` performs the fail-closed gating before provider invocation.

### PostgreSQL isolation check and deferred tests

The integration-test target was resolved before considering any test execution:

- `PostgreSqlTestEnvironment.EnsureConfigured()` requires `PA_TICKETS_TEST_CONNECTION_STRING`.
- The test `BuildProvider()` selects `Database:Provider = PostgreSQL`; `DependencyInjection.AddInfrastructure()` then prefers `ConnectionStrings:TicketsPostgres` and otherwise uses `PA_TICKETS_TEST_CONNECTION_STRING`.
- The current configured target was inspected without displaying credentials: host `localhost`, port `5432`, database `pa_tickets_test`, role `pa_tickets_test_user`.
- A read-only PostgreSQL catalog query returned `rolcreatedb = false`, `rolsuper = false`, and confirmed the proposed database `pa_tickets_h_verify_20261012_1429` does not exist. The configured `PA_TICKETS_TEST_ADMIN_CONNECTION_STRING` is unset. The server's database-name listing contained `names_trainer`, `pa_tickets_test`, `postgres`, and `template1`; no database name containing `prod` was found. Names alone do not establish which databases contain production data.
- The running local service is `postgresql-x64-18` on Windows.
- `tests/scripts/provision-postgresql-schema.ps1` consumes `PA_TICKETS_TEST_CONNECTION_STRING` directly and contains `TRUNCATE ... RESTART IDENTITY CASCADE`; it was not run. `tests/scripts/verify-postgresql.ps1` was also not run because it defaults to the same configured connection and can create a database if permissions permit.
- Because no separate administration connection is configured and the configured role lacks `CREATEDB` and superuser privileges, no disposable database could be provisioned safely. No tests were run against `pa_tickets_test`, and no database was created, altered, reset, truncated, or dropped.
- On the later resumption attempt, the user reported that `pa_tickets_h_verify` had been created and database privileges granted. That state could not be independently verified from this process: its configured test connection still names `pa_tickets_test`, and the password-authentication probe failed (`psql` exit code 2) after the reported role-password change. The process had no `PA_TICKETS_TEST_ADMIN_CONNECTION_STRING`. The reported creation/grants are therefore user-provided context, not verified database evidence. No attempt was made to change the shared connection or target tests at either database.

`tests/PA.Tickets.Domain.Tests/PostgreSqlTicketCreationIdempotencyTests.cs` contains the following tests. All are **NOT EXECUTED** in this verification because test DI would otherwise resolve the configured shared database and no isolated database could be provisioned:

- `First_create_persists_ticket_and_completes_idempotency()` — asserts a service result has an ID and a completed idempotency record stores that ID; it does not explicitly query the ticket row or establish transactional atomicity under failure.
- `Same_key_retry_replays_exact_ticket_without_duplicate()` — asserts identical logical ticket ID/number and one ticket/idempotency row.
- `Same_key_different_payload_returns_conflict()` — asserts the changed payload is rejected with the expected conflict-style exception.
- `Concurrent_same_key_creates_exactly_one_logical_ticket()` — asserts successful concurrent calls return one distinct ticket ID and one ticket row.
- `Failed_create_then_retry_recovers_and_creates_ticket()` — asserts a failed attempt is recorded as failed, then retry after publishing request type completes.
- `Failed_downstream_sla_start_resumes_creation_without_duplicate_work()` — asserts retry resumes the persisted ticket and avoids duplicate workflow/SLA/work-item/start-event records.
- `Caller_isolation_allows_independent_keys_and_prevents_leakage_or_replay()` — asserts different actors using the same key get separate tickets and idempotency records.
- `Ambiguous_expired_processing_record_without_ticket_fails_safely()` — asserts an expired ambiguous processing state fails safely.
- `Serialization_transient_failure_retries_transparently()` — supplies simulated SQLSTATE 40001 exceptions to the retry helper and asserts three attempts; this is not evidence of database transaction/concurrency behavior.

No disposable database was created by this agent, so no cleanup was performed or claimed. The user reports that `pa_tickets_h_verify` exists; its continued availability and grants were not verifiable because no valid local connection to it was available in this process.

### Tests actually executed in this session

`tests/PA.Tickets.Domain.Tests/WorkstreamHRegressionTests.cs` executed successfully with:

- Command: `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter WorkstreamHRegressionTests --nologo -v minimal`
- Result: Passed
- Counts: 5 passed, 0 failed, 0 skipped
- Exit code: 0

Executed cases include:
- `TicketsController_rejects_blank_idempotency_key_before_service_call()`
- `TicketLifecycleService_maps_the_persisted_public_work_item_id_without_using_the_display_number()`
- `CommunicationProviderResolver_rejects_unknown_provider_and_non_production_ready_provider_in_production_mode()`
- `CommunicationDeliveryProcessor_skips_provider_when_required_configuration_is_missing_or_invalid()`
- `CommunicationDeliveryProcessor_calls_provider_when_configuration_is_enabled_and_in_window_with_real_resolver()`

## H1 — Work-item completion identifier contract

Status: Source contract validated and safe regression coverage present.

The real source contract is consistent and explicit:
- `WorkItem.Number` remains the display number (`WI-9`).
- `WorkItem.PublicId` remains the persisted public ID (`PRVT-9F2C1AB8D3E4`).
- The Angular completion call targets `publicId` and not the display number.
- The backend route still resolves using the `publicId` path parameter.

Evidence:
- `client/src/app/features/tickets/ticket-detail.page.spec.ts` asserts `completeWorkItem('PRVT-9F2C1AB8D3E4', expect.any(String))`.
- `tests/PA.Tickets.Domain.Tests/WorkstreamHRegressionTests.cs` asserts `Number == "WI-9"`, `PublicId == "PRVT-9F2C1AB8D3E4"`, and `Assert.NotEqual(workItem.Number, workItem.PublicId)`.

## H2 — Ticket creation idempotency

Status: API-boundary guard is verified locally; PostgreSQL-backed replay/conflict/concurrency/recovery behavior remains unverified.

Source inspection confirms `string.IsNullOrWhiteSpace(idempotencyKey)` rejects missing, empty, and whitespace values before the controller calls `CreateAsync`. The executed controller regression supplies whitespace and asserts a 400 result with no service call; missing and empty values were not separate executed cases. All nine PostgreSQL test methods listed above remain NOT EXECUTED. In particular, replay, payload conflict, concurrent deduplication, caller isolation, ambiguous-state handling, and downstream recovery have no executed PostgreSQL evidence here. The first-create test does not independently prove transactional atomicity, and the transient retry test uses simulated exceptions rather than a database transaction.

## H3 — Real communication-provider validation

Status: Fail-closed configuration gates and resolver production guards have safe local regression coverage; the PostgreSQL suite was not needed for these tests.

The new resolver test exercises the actual `CommunicationProviderResolver` and production-mode guard rather than a fake resolver that always succeeds:

- Missing channel config prevents sending via `CommunicationDeliveryProcessor` because the processor fails the `IsActive(...)` gate before provider resolution.
- Missing provider config prevents sending because `providerConfig` is null and `IsActive(...)` fails.
- Disabled and out-of-window channel configurations prevent sending in the processor regression. Provider-configuration gating is checked for a missing provider row; disabled or out-of-window provider-row cases are source-inspected but not separately exercised by the current processor test.
- An unknown provider cannot be selected because the resolver throws `InvalidOperationException` when no matching provider exists.
- A non-production-ready provider is rejected in production mode via `ValidateConfiguration()` / `Resolve(...)` guard.
- A valid production-ready provider is accepted by a separate resolver test. The enabled/in-window processor-path test uses a tracking provider and the resolver in its default non-production mode; it does not prove the complete processor path with production mode enabled.

These tests use in-memory SQLite configuration and provider doubles; no external credentials or network calls are used. They verify the processor does not send for missing channel/provider configuration or inactive channel configuration, and that an enabled/effective configuration can invoke a provider. Production-mode selection is tested separately at the resolver. Provider rows that are disabled or out-of-window, and a complete production-mode resolver-through-processor success case, are not covered by the current regression suite.

## H4 — Documentation and remaining limitations

Status: PARTIALLY VERIFIED. Documentation separates source inspection, unexecuted PostgreSQL tests, executed local tests, the database-isolation blocker, and remaining test gaps.

### Commands executed successfully

1. `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter WorkstreamHRegressionTests --nologo -v minimal`
   - Result: Passed
   - Counts: 5 passed, 0 failed, 0 skipped
   - Exit code: 0

2. `dotnet build PA.Tickets.sln --nologo -v minimal`
   - Result: Build succeeded
   - Counts: 0 warnings, 0 errors
   - Exit code: 0

3. `.\node_modules\.bin\ng.cmd test --watch=false --include src/app/features/tickets/ticket-detail.page.spec.ts`
   - Result: Passed
   - Counts: 1 test file passed, 5 tests passed
   - Exit code: 0

4. `.\node_modules\.bin\ng.cmd build --configuration production`
   - Result: Build succeeded
   - Exit code: 0

### Verified disposable PostgreSQL execution

- The process-scoped test configuration was loaded from the Git-ignored `.env.test.local` file and the live connection was verified to the disposable database `pa_tickets_h_verify` using the role `pa_tickets_test_user`.
- Read-only identity check: `SELECT current_database(), current_user;` — exit code 0 — returned `pa_tickets_h_verify|pa_tickets_test_user`.
- The shared database `pa_tickets_test` was not used for any integration, provisioning, or destructive operation.
- Execution used only the local PostgreSQL 18 instance and the disposable test database, with all credentials kept out of logs and repository files.
- No Oracle connection was attempted.
- Workstream I was not started.

### Commands executed successfully

1. `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~Authenticated_http_pipeline_executes_workflow_and_enforces_auth_idempotency_and_concurrency|FullyQualifiedName~Approval_and_sla_http_pipeline_persists_history_and_enforces_authorization" --nologo -v minimal`
   - Result: Passed
   - Counts: 2 passed, 0 failed, 0 skipped
   - Exit code: 0

2. `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~PostgreSqlApiE2ETests" --nologo -v minimal`
   - Result: Passed
   - Counts: 9 passed, 0 failed, 0 skipped
   - Exit code: 0

3. `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~PostgreSqlTicketCreationIdempotencyTests" --nologo -v minimal`
   - Result: Passed
   - Counts: 9 passed, 0 failed, 0 skipped
   - Exit code: 0

4. `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter "FullyQualifiedName~WorkstreamHRegressionTests" --nologo -v minimal`
   - Result: Passed
   - Counts: 5 passed, 0 failed, 0 skipped
   - Exit code: 0

### Final verdict

**CLOSED** — the Workstream H evidence is verified against the disposable PostgreSQL database `pa_tickets_h_verify` and the source contract is confirmed. The root cause of the two 500s was an EF Core mapping defect in `TicketsDbContext` that referenced `PublicId` against the schema's `S_PUBLIC_WORK_ITEM_ID` column; the mapping fix and the subsequent focused suite runs passed. The remaining limitations remain qualified: validation does not claim production readiness or Oracle compatibility, and the resolver-through-processor production-mode success path remains separately untested beyond the resolver guard and processor fail-closed checks.
