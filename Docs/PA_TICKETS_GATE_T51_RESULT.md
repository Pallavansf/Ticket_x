# PA_TICKETS Gate T51 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** one provider-independent fail-closed authorization boundary over hosted HTTP  
**Database/provider activity:** none

## Selected scenario

The selected scenario is an authenticated request for a protected ticket when the authorization-facts reader returns no actor identity (`AuthorizationFacts.Actor == null`). This is a concrete missing-fact condition in the production authorization contract. T44 covered an unsupported effective grant on the report-query route, but no prior T43–T50 hosted test exercised the `DatabaseAuthorizationService` null-actor fail-closed branch against a protected resource.

Route and method:

`GET /api/v1/tickets/9001`

The expected result is HTTP `403 Forbidden`; the ticket subject must not be returned and no repository write may occur.

## Production path exercised

The test uses `WebApplicationFactory<Program>` and the real authentication, password-rotation, authorization, route mapping, `TicketsController`, `TicketLifecycleService`, `ITicketAccessAuthorizer`, and `DatabaseAuthorizationService` pipeline. Only `IAuthorizationFactsReader`, `ISpectatorMembershipReader`, and `ITicketRepository` are replaced with deterministic provider-independent fakes.

`DatabaseAuthorizationService.HasPermissionAsync` receives the facts and fails closed when `facts.Actor` is null before evaluating the otherwise valid permission, grant, or matching department scope. The final decision is therefore produced by production authorization code, not by a fake that directly returns denial. The ticket repository is necessarily read before the resource-scoped check because `TicketLifecycleService.GetAsync` loads the ticket first; the test asserts that no ticket content is serialized and no write occurs.

## Test and assertions

Added to `tests/PA.Tickets.Domain.Tests/HostedCrossScopeAuthorizationTests.cs`:

`Missing_authorization_actor_fact_is_denied_before_ticket_content_is_returned`

Assertions:

- HTTP status is `403 Forbidden`.
- The protected ticket subject is absent from the response body.
- The Spectator reader is not consulted for this safe read.
- The authorization-facts reader is invoked exactly once.
- The repository is read once as required by the existing resource-loading path.
- No repository write occurs.

The fact reader supplies a null actor while retaining synthetic permission, active grant, and matching department-scope values. This isolates the missing identity fact without simulating a provider exception or replacing the authorization decision.

## Focused validation

Executed only the new hosted test, directly related hosted authorization tests, and relevant policy regressions:

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t51-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedCrossScopeAuthorizationTests|FullyQualifiedName~HostedAuthorizationDenialTests|FullyQualifiedName~HostedRbacAuthorizationTests|FullyQualifiedName~HostedDelegationAuthorizationTests|FullyQualifiedName~HostedSuperAdminScopeAuthorizationTests|FullyQualifiedName~ApplicationScopeAuthorizationPolicyTests|FullyQualifiedName~EffectiveGrantAuthorizationPolicyTests' --results-directory $results --no-restore --verbosity minimal
```

Result: **19 passed, 0 failed, 0 skipped**. No full suite or provider-backed test was run.

## Worktree preservation

The branch remained `workstream-i-lifecycle-authority`. Before T51, the worktree status contained 24,162 entries; all existing modified, untracked, and ignored work was preserved. The focused hosted test file was already an untracked Gate T50 artifact and was extended only with this T51 case; this result document is the new T51 documentation artifact. Nothing was staged or committed, and no reset, clean, stash, branch switch, or push was performed.

## Remaining limitations

This verifies the provider-independent null-actor fail-closed branch over HTTP. It does not establish behavior when PostgreSQL/Npgsql fact retrieval throws, times out, returns malformed persisted rows, or loses connectivity; those require separately approved provider/runtime evidence. It also does not establish transaction/audit atomicity, concurrency, revocation races, or complete 232-route coverage. No database, SQL, migration, provisioning, seeding, cleanup, Oracle, or protected-target operation occurred.

