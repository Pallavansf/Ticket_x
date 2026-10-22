# PA_TICKETS Lifecycle Transition Matrix

**Branch:** `workstream-i-lifecycle-authority`  
**Evidence scope:** source review and database-independent tests only

## Domain states and entry points

| State | Domain transitions | Shared application entry points | Specialized entry points |
|---|---|---|---|
| `NEW` | `CLASSIFIED`, `PENDING_APPROVAL`, `CANCELLED`; specialized resolution is supported by the existing Incident/Problem contract | `ClassifyAsync`, `RequestApprovalAsync`, `CancelAsync` | Incident/Problem `ResolveAsync` delegates through `TransitionSpecializedStatusAsync` |
| `CLASSIFIED` | `ROUTED`, `CANCELLED` | `RouteAsync`, `CancelAsync` | none |
| `ROUTED` | `IN_PROGRESS`, `PENDING_APPROVAL`, `CANCELLED` | `ExecuteAsync`, `RequestApprovalAsync`, `CancelAsync` | none |
| `IN_PROGRESS` | `WAITING`, `RESOLVED`, `CANCELLED` | `WaitAsync`, `ResolveAsync`, `CancelAsync` | specialized resolution passes the shared policy |
| `WAITING` | `IN_PROGRESS`, `CANCELLED` | `ResumeAsync`, `CancelAsync` | none |
| `PENDING_APPROVAL` | `ROUTED`, `CANCELLED` | `ApproveAsync`, `CancelAsync` | none |
| `RESOLVED` | `CLOSED`, `REOPENED` | `CloseAsync`, `ReopenAsync` | Incident/Problem close/reopen delegates through the same lifecycle service |
| `CLOSED` | `REOPENED` | `ReopenAsync` | specialized reopen remains allowed |
| `REOPENED` | `CLASSIFIED`, `ROUTED`, `CANCELLED` | `ClassifyAsync`, `RouteAsync`, `CancelAsync` | none |
| `CANCELLED` | none | all mutating domain methods reject | specialized status changes reject |

`TicketLifecycleService.Mutate` authorizes, applies the domain action, checks
`TicketStatusTransitionPolicy`, and saves. Specialized Incident and Problem
services call `TransitionSpecializedStatusAsync` inside their transaction, so
their status writes use the same policy and repository path.

## Correction

`Ticket.TransitionSpecializedStatus` previously accepted `CLOSED` from any
state, accepted `REOPENED` from any state, and allowed changes after
`CANCELLED`. That bypassed the guards implemented by `Close`, `Reopen`, and
`EnsureMutable`. It now rejects cancelled mutation, requires `RESOLVED` before
`CLOSED`, and permits `REOPENED` only from `RESOLVED` or `CLOSED`. Existing
specialized `NEW`/`IN_PROGRESS` to `RESOLVED` behavior remains unchanged because
Incident and Problem services use that source-level contract.

## Test evidence

- `TicketLifecycleTests.Specialized_status_entry_point_rejects_invalid_closure_and_cancelled_mutation` covers denied `NEW -> CLOSED`, valid `RESOLVED -> CLOSED -> REOPENED`, and denied `CANCELLED -> RESOLVED`.
- Existing lifecycle and workflow unit tests passed in the focused run recorded in the Stage 2 transition result.

Provider persistence, configured transition-row behavior, transaction
atomicity, concurrency, audit durability, and API/browser acceptance remain
unverified.
