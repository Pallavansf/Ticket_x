# PA_TICKETS Gate T55 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** attachment/evidence authorization over hosted HTTP  
**Database/provider activity:** none

## Routes and source trace

The highest-value candidate is the protected attachment download route:

`GET /api/v1/attachments/{id}/versions/{version}/download`

`AttachmentsController` is class-level `[Authorize]` and maps this route to `IAttachmentService.DownloadAsync` (`src/PA.Tickets.Api/Controllers/AttachmentsController.cs:8,22`). The service loads attachment metadata and its parent ticket, then calls the production `ITicketAccessAuthorizer.EnsureCanAccessAsync` before loading the attachment version or calling `IFileStorage.OpenReadAsync` (`src/PA.Tickets.Infrastructure/Foundations/AttachmentService.cs:251-257`). This ordering is the required protected-content boundary: metadata may be loaded before authorization, but the version row and storage stream must not be read after denial.

The related metadata/version routes (`GET /api/v1/attachments/{id}` and `GET /api/v1/attachments/{id}/versions`) use the same parent-ticket authorization helper (`AttachmentService.cs:240-249`). Evidence creation and review are protected mutation routes; they load ticket/evidence rows, call `EnsureCanMutateAsync`, and only then write events/audit rows (`AttachmentService.cs:259-278,291`).

## Existing coverage and gap

The repository contains provider-gated `PostgreSqlAttachmentEvidenceTests`, including `Attachment_and_evidence_access_uses_ticket_authorization_for_metadata_versions_download_and_review`. That test verifies authorized content, unauthorized download, unauthorized evidence review, and related ownership behavior at the service layer, but it is intentionally not runnable in this gate and is not hosted HTTP evidence.

No T43–T54 hosted test exercises an attachment or evidence controller route. The gap is therefore real: HTTP authentication, middleware, controller mapping, parent-ticket authorization, and the storage-read boundary have not been demonstrated together.

## Safe hosted-test assessment

The current production `AttachmentService` is constructed directly with `TicketsDbContext`, `IFileStorage`, scanner, retry, audit locks, and `ITicketAccessAuthorizer`. Its download path performs EF queries for the attachment and ticket before the authorization decision. A provider-independent `WebApplicationFactory<Program>` test cannot substitute only a narrow repository or metadata reader: replacing `IAttachmentService` would bypass the production service authorization and storage ordering, while retaining it attempts the forbidden provider connection. A storage fake alone is insufficient because EF metadata access still requires PostgreSQL.

Adding a controller test with a fake `IAttachmentService` would prove only class-level authentication and a test-only service decision, not the production parent-ticket authorization or the guarantee that `OpenReadAsync` is unreachable after denial. No safe, truthful hosted test can therefore be added without first introducing a separately reviewed abstraction/refactor or using an approved provider target.

No production code or test was changed, and no artificial authorization bypass was introduced.

## Required future evidence

The next authorized provider/API gate should use a disposable, independently verified PostgreSQL target and a harmless storage fake to verify:

- Anonymous callers receive 401 and authenticated out-of-scope callers receive the documented 403/404 behavior.
- Parent-ticket scope is checked before attachment-version metadata beyond the required lookup and before `IFileStorage.OpenReadAsync`.
- Authorized callers receive only the current, checksum-verified version.
- Evidence review and creation deny without state/event/audit writes.
- Storage cleanup and audit transaction behavior remain correct on denial, scanner failure, and connection loss.

Those tests must preserve the PostgreSQL integration guard and remain separate from migration-026 authorization and execution gates.

## Validation and preservation

Read-only inspection used `git branch --show-current`, `git status --short --untracked-files=all --ignored`, `Get-Content`, and `rg`. No test command was run because the only existing attachment authorization test is provider-gated and a hosted substitute would not exercise the production service honestly. The branch remained `workstream-i-lifecycle-authority`; before T55 the worktree status contained 24,166 entries, and all modified, staged, untracked, and ignored work was preserved. Only this result document was added. No files were staged or committed, and no reset, clean, stash, branch switch, or push was performed.

No database, SQL, migration, provisioning, seeding, cleanup, Oracle, or protected-target operation occurred.

