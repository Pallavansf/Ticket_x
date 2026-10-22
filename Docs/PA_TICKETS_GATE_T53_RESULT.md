# PA_TICKETS Gate T53 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** authorization concurrency and revocation analysis  
**Database/provider activity:** none

## Authorization lifecycle traced

For a ticket mutation such as `POST /api/v1/tickets/{id}/close`, the request passes through authentication, password-rotation middleware, authorization, and controller routing. `TicketsController.Close` calls `TicketLifecycleService.CloseAsync`, which enters `Mutate`: it loads the ticket, calls `ITicketAccessAuthorizer.EnsureCanMutateAsync`, applies the domain transition, and then calls `ITicketRepository.SaveAsync` (`src/PA.Tickets.Api/Controllers/TicketsController.cs:3,23`; `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs:113,116`).

The registered `DatabaseTicketAccessAuthorizer` requests `TICKET_UPDATE` with the ticket resource (`src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs:221-224`). `DatabaseAuthorizationService` invokes `IAuthorizationFactsReader` once for that permission, evaluates actor identity, permission metadata, active roles, effective grants, and scopes, and invokes the delegation reader only for the relevant Admin Head resource path. No authorization decision cache or cross-request fact reuse was found.

`DatabaseAuthorizationFactsReader` performs separate `AsNoTracking` queries for actor, permission, roles, grants, and scopes (`src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationFactsReader.cs:7-75`). `DatabaseDelegationFactsReader` similarly performs separate queries for target privilege, target role codes, and active delegation (`src/PA.Tickets.Infrastructure/Foundations/DatabaseDelegationFactsReader.cs:7-63`). `EfTicketRepository.SaveAsync` begins the persistence/event/audit work only after authorization has returned (`src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs:9-24`).

## Candidate risk investigated

The strongest candidate is a revocation or scope change after facts are retrieved but before mutation persistence:

1. Request A reads an active grant and matching scope.
2. Request B revokes that grant, delegation, or scope and commits.
3. Request A continues with its already-computed authorization decision and calls `SaveAsync`.

The same ordering applies to Admin Head delegation because the delegation reader result is consumed before the mutation reaches persistence. The source therefore does not provide a single transaction or lock that atomically binds the authorization read to the protected write. This is a **potential TOCTOU consistency risk**, not a confirmed vulnerability: actual behavior depends on PostgreSQL isolation, transaction boundaries, connection timing, and the revocation operation's commit ordering.

The separate `AsNoTracking` fact queries also leave a possible intra-read consistency question if role, grant, or scope rows change while facts are being assembled. No cache was found that would create a longer-lived stale decision, and each request performs its own authorization evaluation.

## Existing safeguards and evidence

- Effective grants require active status, `ACTIVE` effective status, a valid start, and a valid end window.
- Scope comparison and explicit organization-scope rules are evaluated in production policy code.
- Admin Head delegated-target checks require active target/delegation facts.
- Mutations recheck authorization in `Mutate` immediately before applying the domain action and before `SaveAsync`.
- T52 proves an out-of-scope close request is denied over hosted HTTP with no `SaveAsync` call.
- T50 proves an in-scope ticket read succeeds; T51 proves a missing actor fact denies access; T43–T47 cover representative denial boundaries.

These safeguards establish request-level fail-closed behavior. They do not prove that a concurrent revocation cannot commit between the final fact read and the protected write.

## Test decision

No provider-independent concurrency test was added. A deterministic in-memory barrier could show that two fakes return values in a chosen order, but it cannot establish PostgreSQL snapshot visibility, row-lock behavior, transaction isolation, commit ordering, or whether a revocation and ticket save can overlap safely. Adding such a test would risk presenting a scheduler scenario as provider evidence.

No production code was changed.

## Required provider-backed follow-up

A separately authorized PostgreSQL concurrency gate is required to establish:

- The transaction boundary and isolation used for grant, scope, and delegation reads.
- Whether authorization facts and the mutation share a transaction or require a lock/fencing protocol.
- Revocation-before-save, revocation-after-facts, and concurrent commit outcomes.
- Intra-read consistency of the multiple facts queries.
- Audit/event atomicity when revocation and mutation race.
- Recovery and terminal behavior after serialization, deadlock, timeout, or connection failure.

That gate must use an independently approved target and provider test environment. It must remain separate from migration execution authorization.

## Validation and worktree preservation

No T53 test command was run because no provider-independent test could truthfully establish the investigated guarantee. The branch remained `workstream-i-lifecycle-authority`. Before T53, the worktree status contained 24,164 entries; all existing modified, untracked, and ignored work was preserved. Only this result document was added by T53; no source, test, migration, environment, or configuration file was changed. No files were staged or committed, and no reset, clean, stash, branch switch, or push was performed.

No database, SQL, migration, provisioning, seeding, cleanup, Oracle, or protected-target operation occurred.

