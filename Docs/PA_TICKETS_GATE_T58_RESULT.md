# PA_TICKETS Gate T58 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Review mode:** focused read-only regression and seam review  
**Database/provider activity:** none

## Scope and preservation

Reviewed `Docs/PA_TICKETS_GATE_T55_RESULT.md`, `Docs/PA_TICKETS_GATE_T56_RESULT.md`, `Docs/PA_TICKETS_GATE_T57_RESULT.md`, the complete T57 seam diff, the attachment controller/service/reader/contracts, dependency-injection registration, the production authorization path, and existing hosted/provider-gated attachment tests.

The branch was unchanged. Before this result document, `git status --short --ignored` reported 198 entries; after creating this document it reported 199 entries. Existing modified, untracked, and ignored work was preserved. No file was staged, reset, cleaned, stashed, committed, pushed, or otherwise rewritten.

## Source call trace

`AttachmentsController.Download` maps `GET /api/v1/attachments/{id}/versions/{version}/download` and remains under class-level `[Authorize]` (`src/PA.Tickets.Api/Controllers/AttachmentsController.cs:8,22-25`). The normal application pipeline still performs authentication, authorization, and the existing exception mapping before the action (`src/PA.Tickets.Api/Program.cs:27-78`).

The current download path is:

1. `AttachmentService.DownloadAsync` calls `IAttachmentDownloadReader.ReadOwnerAsync(id)` (`src/PA.Tickets.Infrastructure/Foundations/AttachmentService.cs:251-254`).
2. It invokes the existing `ITicketAccessAuthorizer.EnsureCanAccessAsync` with the returned parent ticket (`line 255`). This remains the production authorization decision; the hosted tests do not replace it.
3. Only after authorization it calls `ReadVersionAsync(id, version)` (`line 256`).
4. It rejects mismatched attachment/version identity or missing storage/hash metadata before storage (`lines 257-258`).
5. It opens the existing `IFileStorage` stream, computes SHA-256, disposes and fails on mismatch, then returns the verified stream (`line 259`).
6. The controller sets content type, content length, content-disposition, and returns the file result (`AttachmentsController.cs:23`).

Production DI registers `EfAttachmentDownloadReader` and `AttachmentService` (`src/PA.Tickets.Infrastructure/DependencyInjection.cs:95-98`). The EF adapter performs the same two-stage owner and version lookups, reconstructing the parent `Ticket` (`src/PA.Tickets.Infrastructure/Foundations/EfAttachmentDownloadReader.cs:8-42`).

## Original versus current behavior

The pre-T57 implementation (recoverable from `HEAD`) directly queried attachment and parent-ticket rows, authorized the reconstructed ticket, queried the requested version, then opened and checksum-verified storage. T57 replaces only those download metadata queries with the two-method reader. Authorization, query ordering, checksum verification, response mapping, exception types/messages, and storage boundary remain in the service/controller path. The new identity checks add fail-closed rejection for a reader returning an attachment or version different from the requested identifiers.

## Security-invariant findings

| Invariant | Evidence and result |
|---|---|
| Authorization before protected version/storage access | **PASS for application behavior.** Service line 255 precedes lines 256 and 259. The out-of-scope and missing-actor hosted tests assert 403, one owner read, zero version reads, and zero storage reads. |
| Metadata lookup does not authorize | **PASS.** `EnsureCanAccessAsync` remains an explicit call using the parent ticket; the reader only supplies facts. `DatabaseAuthorizationService` and scope/effective-grant policy remain active in the hosted factory. |
| Ownership and parent-ticket scope | **PASS for the production query shape, limited by provider evidence.** `EfAttachmentDownloadReader` reads the attachment and then its `TicketId` parent (`lines 12-18`); the service authorizes that parent. Hosted fakes use deterministic identifiers and do not silently repair mismatches. Persisted relationship integrity remains provider-gated. |
| Version binding | **PASS for service boundary.** EF queries by both attachment ID and version (`EfAttachmentDownloadReader.cs:21-25`); service rejects returned ID/version mismatches (`AttachmentService.cs:257-258`). The mismatch test receives 404 and records zero storage reads. |
| Authorized content behavior | **PASS for the tested contract.** The in-scope test exercises the real route and service, receives 200, and receives the harmless checksum-verified `safe attachment` body. Controller source establishes content type, length, and attachment filename behavior (`AttachmentsController.cs:23`). The T57 tests do not independently assert every response header. |
| Denial/error semantics and disclosure | **PASS at reviewed boundary.** Denials are 403; missing/mismatched facts are mapped through the existing exception middleware and storage is not opened. No storage credentials, object keys, or bytes are placed in the response by the reviewed path. Provider-specific exception mapping and storage error disclosure remain unverified. |
| Dependency injection | **PASS.** Production registers the EF reader; hosted tests replace only `IAttachmentDownloadReader`, authorization fact retrieval, spectator facts, and `IFileStorage`. They retain the real `Program`, controller, `AttachmentService`, authorization service, middleware, and route. No test-only registration is in production. |

No concrete production defect or changed authorization semantics was established. Therefore no source or test change beyond this result document was justified. The header behavior is source-established and unchanged from pre-T57; the hosted test evidence is intentionally limited to status/body and protected-read ordering.

## Tests independently rerun

The following commands were run with unique temporary result directories and `--no-restore`; no database/provider operation was performed:

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t58-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedAttachmentDownloadAuthorizationTests' --results-directory $results --no-restore --verbosity minimal
```

Result: **4 passed, 0 failed, 0 skipped**.

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t58-focused-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedAttachmentDownloadAuthorizationTests|FullyQualifiedName~HostedCrossScopeAuthorizationTests|FullyQualifiedName~HostedAuthorizationDenialTests|FullyQualifiedName~HostedRbacAuthorizationTests|FullyQualifiedName~HostedDelegationAuthorizationTests|FullyQualifiedName~HostedSuperAdminScopeAuthorizationTests|FullyQualifiedName~ApplicationScopeAuthorizationPolicyTests|FullyQualifiedName~EffectiveGrantAuthorizationPolicyTests|FullyQualifiedName~TicketLifecycleTests' --results-directory $results --no-restore --verbosity minimal
```

Result: **29 passed, 0 failed, 0 skipped**.

The existing `PostgreSqlAttachmentEvidenceTests` remains provider-gated and was not run. No full suite was run. The T57 `git diff --check` result was already clean; no unrelated diff was changed during this review. A final repository-wide `git diff --check` reports pre-existing whitespace/EOF findings in unrelated dirty files (`AdministrationService.cs`, `DatabaseAuthorizationService.cs`, `AdministrationRows.cs`, reporting files, `PostgreSqlTestEnvironment.cs`, and the already-modified provisioning script); none is in the T58 result document or T57 seam files.

## Remaining limitations

Hosted fakes do not prove EF/Npgsql translation or persisted attachment-to-ticket integrity, storage-provider ACLs/object-key confidentiality, signed-URL behavior, transaction/audit atomicity, concurrent replacement, cancellation/connection-loss recovery, or TOCTOU safety. The reader contract also does not itself establish a database snapshot across owner authorization and version retrieval; those properties require separately approved provider-backed tests. These limitations do not show a T57 regression.

## Safety confirmation

No PostgreSQL or Oracle connection, SQL, migration, provisioning, seeding, cleanup, sequence reset, protected-target access, environment/configuration change, permission change, or sandbox bypass occurred.

**GATE T58 PASS — ATTACHMENT AUTHORIZATION AND DOWNLOAD REGRESSIONS VERIFIED**
