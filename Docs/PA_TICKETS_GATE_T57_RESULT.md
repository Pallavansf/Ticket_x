# PA_TICKETS Gate T57 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** attachment download reader seam and focused hosted validation  
**Database/provider activity:** none

## Original and implemented call sequence

The route remains `GET /api/v1/attachments/{id}/versions/{version}/download`. The controller and authentication pipeline are unchanged. `AttachmentService.DownloadAsync` now performs the same logical sequence through the new reader:

1. `IAttachmentDownloadReader.ReadOwnerAsync(id)` retrieves attachment identity and the parent-ticket domain snapshot.
2. The existing `ITicketAccessAuthorizer.EnsureCanAccessAsync(actor, parentTicket)` makes the production authorization decision.
3. `IAttachmentDownloadReader.ReadVersionAsync(id, version)` retrieves only the requested version metadata.
4. The service validates attachment/version identity and non-empty storage/hash metadata.
5. Existing `IFileStorage.OpenReadAsync` reads content, followed by the existing SHA-256 verification and response stream.

Version and storage access remain after authorization. Missing owner/version facts retain not-found behavior, and mismatched metadata fails closed before storage access.

## Files changed

- `src/PA.Tickets.Application/Foundations/AttachmentContracts.cs` — added `AttachmentDownloadOwnerFact`, `AttachmentDownloadVersionFact`, and the narrow `IAttachmentDownloadReader` contract. It exposes domain metadata only; no EF, connection, credential, or storage-content types.
- `src/PA.Tickets.Infrastructure/Foundations/EfAttachmentDownloadReader.cs` — added the production EF adapter for the two-stage reads and parent-ticket reconstruction. It preserves attachment/ticket/version not-found errors and rejects missing storage references or hashes.
- `src/PA.Tickets.Infrastructure/Foundations/AttachmentService.cs` — replaced only the download method's direct EF queries with the reader, retained `ITicketAccessAuthorizer`, checksum validation, storage call, and response semantics, and added attachment/version identity validation.
- `src/PA.Tickets.Infrastructure/DependencyInjection.cs` — registered `IAttachmentDownloadReader` to `EfAttachmentDownloadReader`. Other pre-existing DI changes in this dirty file were preserved.
- `tests/PA.Tickets.Domain.Tests/HostedAttachmentDownloadAuthorizationTests.cs` — added four provider-independent hosted tests using deterministic reader/facts/storage substitutions while retaining the real `Program`, `AttachmentService`, authorization service, middleware, and route.
- `Docs/PA_TICKETS_GATE_T57_RESULT.md` — this result.

## Focused tests and assertions

The new hosted tests prove:

- Out-of-scope caller receives 403; version reader and storage are not called.
- In-scope caller receives 200 and the checksum-verified harmless content; owner/version/storage calls occur in order.
- Mismatched version attachment identity returns 404 and does not read storage.
- Missing authorization actor fact returns 403 before version/storage access.

No authorization decision was mocked. `DatabaseAuthorizationService` and the existing scope/effective-grant policies remained active; only facts, metadata, and storage were deterministic substitutions.

## Validation

New seam tests:

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t57-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedAttachmentDownloadAuthorizationTests' --results-directory $results --no-restore --verbosity minimal
```

Result: **4 passed, 0 failed, 0 skipped**.

Focused regression set:

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t57-focused-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedAttachmentDownloadAuthorizationTests|FullyQualifiedName~HostedCrossScopeAuthorizationTests|FullyQualifiedName~HostedAuthorizationDenialTests|FullyQualifiedName~HostedRbacAuthorizationTests|FullyQualifiedName~HostedDelegationAuthorizationTests|FullyQualifiedName~HostedSuperAdminScopeAuthorizationTests|FullyQualifiedName~ApplicationScopeAuthorizationPolicyTests|FullyQualifiedName~EffectiveGrantAuthorizationPolicyTests|FullyQualifiedName~TicketLifecycleTests' --results-directory $results --no-restore --verbosity minimal
```

Result: **29 passed, 0 failed, 0 skipped**. `git diff --check` passed for the tracked seam changes. No full suite or provider-backed attachment test was run.

## Remaining limitations

The seam proves application authorization ordering and storage-read suppression with synthetic facts and harmless in-memory content. It does not prove EF/Npgsql translation/materialization, persisted ownership, storage ACLs or signed URLs, transaction/audit atomicity, concurrent attachment replacement, TOCTOU behavior, or provider error recovery. Those remain separately approved provider-validation work. The seam does not add caching or claim to solve concurrency.

## Worktree preservation and safety

Before T57, the worktree status contained 24,169 entries on `workstream-i-lifecycle-authority`; final status contained 24,174 entries, reflecting the new adapter, hosted test, and result document alongside the existing dirty work. All modified, staged, untracked, and ignored files were preserved. No reset, clean, stash, branch switch, stage, commit, or push was performed. No database, SQL, migration, provisioning, seeding, cleanup, Oracle, protected-target, or provider operation occurred.

