# Remediation baseline

## Repository and environment

- Repository root: `g:\WSS\aw\Tickets`
- Solution: `PA.Tickets.sln`
- Frontend workspace: `client/`
- Git branch metadata: not available in this environment; the working directory is not a git repository.
- Git status/diff cannot be collected because this directory is not a Git repository. This remediation pass changed the authorization, scheduled-report worker, regression-test, PostgreSQL test-fixture, and tracker files listed in `REMEDIATION_TRACKER.md`; no unrelated client styling was revisited.

## Prior baseline results

### .NET solution

Command executed:

```text
cd g:\WSS\aw\Tickets
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
 dotnet test PA.Tickets.sln --nologo
```

Result:

- Passed: 162
- Failed: 0
- Skipped: 0
- Duration: 1m 15s

### Angular client

Command executed:

```text
cd g:\WSS\aw\Tickets\client
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
npm test -- --no-watch --no-progress
npm run build
```

Result:

- Angular tests: 10 files passed, 26 tests passed, 0 failed
- Production build: passed, 0 warnings, 0 errors

## Current remediation verification

### Authorization regression batch

Command executed:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~PostgreSqlAdministrationAuthorizationTests|FullyQualifiedName~PostgreSqlWorkflowExecutionE2ETests|FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~AuditorReportingTests|FullyQualifiedName~PostgreSqlChangeTests|FullyQualifiedName~PostgreSqlIncidentTests|FullyQualifiedName~PostgreSqlProblemTests|FullyQualifiedName~PostgreSqlAttachmentEvidenceTests" --nologo
```

Result: exit code 0; 55 passed, 0 failed, 0 skipped.

### Scheduled-report regression

Command executed:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~PostgreSqlReportScheduleExecutionTests" --nologo
```

Result: exit code 0; 1 passed, 0 failed, 0 skipped.

### SLA deadline regression batch

Command executed:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~Sla_deadline_monitor|FullyQualifiedName~Background_worker_detects_both_sla|FullyQualifiedName~Sla_clock_uses_business_calendar|FullyQualifiedName~Sla_administration_persists_version" --nologo
```

Result: exit code 0; 4 passed, 0 failed, 0 skipped. Coverage includes response and resolution deadlines, business-calendar pause extension, terminal completion, worker reprocessing, and escalation.

### Full .NET solution after A–C changes

Command executed:

```text
dotnet test PA.Tickets.sln --no-restore --nologo
```

Result: exit code 0; 169 passed, 0 failed, 0 skipped; duration 1m 18s.

### .NET solution build after A–C changes

Command executed:

```text
dotnet build PA.Tickets.sln --no-restore --nologo
```

Result: exit code 0; build succeeded with 0 warnings and 0 errors.

## Environment limitations and scope guardrails

- Oracle connectivity and Oracle migration validation were intentionally not executed.
- Live OIDC / SSO integration is explicitly deferred and not part of the current local acceptance gate.
- PostgreSQL-backed tests were not assumed to prove Oracle compatibility. The repository test classification documents this distinction explicitly.
- No production secrets, connection strings, or signing keys were introduced or exposed in source or logs.

## Audit evidence reviewed

The supplied end-to-end remediation guide described multiple workstreams and gate checks, but there was no repository-local `PA_TICKETS_END_TO_END_AUDIT` report or comparable audit document in `Docs/` at the start of this remediation pass. Therefore, the audit findings were treated as hypotheses to verify against the actual code and the existing retained test suite rather than accepted as facts.

## Current verified state

Authorization workstream A and scheduled-report execution workstream B each had confirmed defects reproduced and fixed with PostgreSQL regressions. SLA deadline detection now has a worker-driven persisted path and regression coverage for response/resolution deadlines, pauses, terminal states, repeated runs, and escalation. Policy breach/notification JSON semantics and immutable calendar-version history remain unresolved as documented in `REMEDIATION_TRACKER.md`. PostgreSQL tests do not certify Oracle behavior; Oracle access/migrations, production identity-provider integration, and deployment checks were not performed.
