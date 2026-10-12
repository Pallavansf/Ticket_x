# Workstream H — critical contract corrections

## Scope

This report follows the actual repository state and separates source inspection from executed local tests. Workstream H remained within safe local validation: no Oracle connection, no shared persistent PostgreSQL validation, and no destructive shared-database actions.

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

### Tests present in the repository but not executed against the shared PostgreSQL environment

`tests/PA.Tickets.Domain.Tests/PostgreSqlTicketCreationIdempotencyTests.cs` contains the following lifecycle/idempotency coverage, which was inspected but not run locally because it targets the shared persistent PostgreSQL configuration:

- `First_create_persists_ticket_and_completes_idempotency()`
  - Asserts the initial TICKET_CREATE idempotency row is created and completed with the created ticket ID.
- `Same_key_retry_replays_exact_ticket_without_duplicate()`
  - Asserts the same key replays the same ticket and does not create a duplicate ticket row.
- `Same_key_different_payload_returns_conflict()`
  - Asserts a re-used key with a different payload throws a conflict-style InvalidOperationException.
- `Concurrent_same_key_creates_exactly_one_logical_ticket()`
  - Asserts concurrent callers with the same key yield only one ticket row.
- `Failed_create_then_retry_recovers_and_creates_ticket()`
  - Asserts a failed prior attempt creates a FAILED idempotency row, then a retry with the same key succeeds once the request type is valid again.
- `Caller_isolation_allows_independent_keys_and_prevents_leakage_or_replay()`
  - Asserts two different actors using the same key receive distinct ticket IDs and separate idempotency records.
- `Ambiguous_expired_processing_record_without_ticket_fails_safely()`
  - Asserts ambiguous timed-out processing rows fail safely rather than replaying without evidence.
- `Serialization_transient_failure_retries_transparently()`
  - Asserts transient serialization failures are retried and do not silently break the request flow.

These tests were inspected for contract coverage but not executed in this session due the explicit prohibition on shared persistent PostgreSQL validation.

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

Status: Controller guard verified locally; broader PostgreSQL idempotency coverage was inspected but not executed.

The local unit test verifies the blank/whitespace guard at the API boundary; the PostgreSQL suite in `PostgreSqlTicketCreationIdempotencyTests.cs` was inspected for replay, payload-conflict, concurrency, and recovery semantics but not executed because it targets the shared persistent PostgreSQL configuration.

## H3 — Real communication-provider validation

Status: Provider resolver and processor validation are now covered with safe isolated tests.

The new resolver test exercises the actual `CommunicationProviderResolver` and production-mode guard rather than a fake resolver that always succeeds:

- Missing channel config prevents sending via `CommunicationDeliveryProcessor` because the processor fails the `IsActive(...)` gate before provider resolution.
- Missing provider config prevents sending because `providerConfig` is null and `IsActive(...)` fails.
- Disabled or out-of-window configuration prevents sending because the configuration is inactive.
- An unknown provider cannot be selected because the resolver throws `InvalidOperationException` when no matching provider exists.
- A non-production-ready provider is rejected in production mode via `ValidateConfiguration()` / `Resolve(...)` guard.
- A valid production-ready provider is accepted when the channel/provider configuration is enabled and effective.

This follows the actual production fail-closed behavior of `CommunicationProviderResolver` and `CommunicationDeliveryProcessor` without requiring external credentials or network calls.

## H4 — Documentation and remaining limitations

Status: Updated to separate inspection, unexecuted PostgreSQL tests, executed local tests, blocked Angular validation, and residual limitations.

### Commands executed successfully

1. `dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter WorkstreamHRegressionTests --nologo -v minimal`
   - Result: Passed
   - Counts: 5 passed, 0 failed, 0 skipped
   - Exit code: 0

2. `dotnet build PA.Tickets.sln --nologo -v minimal`
   - Result: Build succeeded
   - Counts: 0 warnings, 0 errors
   - Exit code: 0

### Environment-blocked commands

1. `node .\node_modules\@angular\cli\bin\ng test --watch=false --include src/app/features/tickets/ticket-detail.page.spec.ts --browsers=ChromeHeadless`
   - Result: failed before executing specs
   - Exit code: 1
   - Reason: missing `@vitest/browser-*` packages required by the Angular test runner in this environment

2. `npx ng build --configuration production`
   - Result: blocked before Angular executed
   - Exit code: 1
   - Reason: local PowerShell script execution policy prevented `npx` from running

### Remaining limitations

- Angular browser validation remains blocked in this environment and is not reported as passed.
- Replay, conflict, concurrency, and downstream-recovery semantics were inspected in the PostgreSQL-backed test suite but not executed because those tests target the shared persistent database configuration.
- Workstream I was not started.
