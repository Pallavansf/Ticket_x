# PA_TICKETS Gate T50 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** one provider-independent positive authorization acceptance test over hosted HTTP  
**Database/provider activity:** none

## Selected positive boundary

The selected route is:

`GET /api/v1/tickets/9001`

The existing T45 hosted case proved that the same route denies a user whose department scope is 20 while the ticket belongs to department 10. T50 adds the complementary acceptance case: user `4107` has an active `User` grant for `TICKET_READ`, a valid effective window, and a department scope matching the ticket's department (10).

This was the highest-value uncovered positive boundary identified by T48. The prior hosted evidence was denial-focused; no existing hosted test demonstrated that a correctly scoped user could reach the protected ticket response through the real application pipeline.

## Production authorization path exercised

`TicketsController` is class-level `[Authorize]` and maps the route to `TicketLifecycleService.GetAsync`. `Program` runs authentication, password-rotation middleware, authorization, and controller mapping before the action. `GetAsync` loads the ticket and calls the production `ITicketAccessAuthorizer`; the registered `DatabaseAuthorizationService` reads `IAuthorizationFactsReader`, validates effective grant status/time, applies `RoleAuthorizationPolicy`, and evaluates the department scope through the production scope policy. Only the fact reader and ticket repository are substituted with deterministic test implementations; the authorization decision is not mocked.

## Test and assertions

Added to `tests/PA.Tickets.Domain.Tests/HostedCrossScopeAuthorizationTests.cs`:

`Authenticated_user_is_allowed_for_ticket_within_department_scope`

The test uses `WebApplicationFactory<Program>` and an HTTP request with the repository's testing authentication headers. It asserts:

- HTTP `200 OK`.
- The expected ticket subject is present in the response body.
- The Spectator filter is not consulted for this safe read.
- The authorization facts reader is called exactly once.
- The repository is reached exactly once after authorization succeeds.
- No repository write occurs.

The existing T45 out-of-scope denial remains unchanged and continues to use the default department-20 fact reader, so the positive and negative cases now form a focused scope pair.

## Focused validation

Executed only the directly relevant hosted and scope-policy tests:

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t50-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedCrossScopeAuthorizationTests|FullyQualifiedName~ApplicationScopeAuthorizationPolicyTests|FullyQualifiedName~ScopeAuthorizationPolicyTests' --results-directory $results --no-restore --verbosity minimal
```

Result: **14 passed, 0 failed, 0 skipped**. No full suite or provider-backed test was run.

## Worktree preservation

The branch remained `workstream-i-lifecycle-authority`. The pre-existing worktree contained 24,161 status entries; all modified, untracked, and ignored work was preserved. The T50 changes are limited to the focused hosted test update and this result document. No file was staged or committed, and no reset, clean, stash, branch switch, or push was performed.

## Remaining limitations

This proves provider-independent HTTP control flow and the department-scope decision with synthetic facts. It does not prove PostgreSQL/Npgsql query translation, persisted grant/scope materialization, transaction or audit atomicity, concurrency, revocation races, or full 232-route coverage. Those remain separate provider and broader hosted-matrix concerns. No database, SQL, migration, provisioning, seeding, cleanup, Oracle, or protected-target operation occurred.

