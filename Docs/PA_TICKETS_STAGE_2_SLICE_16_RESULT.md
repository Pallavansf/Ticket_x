# PA_TICKETS Stage 2, Slice 16 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Bounded lifecycle coverage matrix

The current source was re-read in `TicketLifecycleService.cs`, `Ticket.cs`,
`WorkItem.cs`, `IncidentService.cs`, and `ProblemService.cs`. The generic
status-changing methods all use the shared `Mutate` path. `Mutate` performs
authorization, then the optional target-status policy check, then the domain
action, and finally `SaveAsync`; the target-status branch is now used for every
generic method with a known target.

| Entry point | Source target | Preflight location | Denial-preservation evidence |
|---|---|---|---|
| `ClassifyAsync` | `NEW -> CLASSIFIED` | `Mutate(expectedStatus: Classified)` | `Configured_denied_transition_is_rejected_before_save`; fields/events/status/save assertions |
| `RequestApprovalAsync` | `NEW -> PENDING_APPROVAL` | `Mutate(expectedStatus: PendingApproval)` | `Denied_approval_transition_preserves_state_fields_events_and_persistence` |
| `ApproveAsync` | `PENDING_APPROVAL -> ROUTED` | `Mutate(expectedStatus: Routed)` | `Denied_approval_completion_preserves_state_fields_events_and_persistence` |
| `RouteAsync` | `CLASSIFIED -> ROUTED` | assignment authorization, then `Mutate(expectedStatus: Routed)` | `Denied_route_transition_preserves_routing_state_events_and_persistence` |
| `ExecuteAsync` | `ROUTED -> IN_PROGRESS` | assignment authorization, then `Mutate(expectedStatus: InProgress)` | `Denied_execution_transition_preserves_assignment_state_events_and_persistence` |
| `WaitAsync` | `IN_PROGRESS -> WAITING` | waiting-reason request validation, then `Mutate(expectedStatus: Waiting)` | `Denied_waiting_transition_preserves_waiting_state_events_and_persistence` |
| `ResumeAsync` | `WAITING -> IN_PROGRESS` | `Mutate(expectedStatus: InProgress)` | `Denied_resume_transition_preserves_waiting_state_events_and_persistence` |
| `ResolveAsync` | `IN_PROGRESS -> RESOLVED` | `Mutate(expectedStatus: Resolved)` | `Denied_resolution_transition_preserves_resolution_state_events_and_persistence` |
| `ReopenAsync` | resolved/closed -> `REOPENED` | `Mutate(expectedStatus: Reopened)` | `Denied_reopen_transition_preserves_state_fields_events_and_persistence` |
| `CancelAsync` | current legal state -> `CANCELLED` | `Mutate(expectedStatus: Cancelled)` | `Denied_cancellation_preserves_state_fields_events_and_persistence` |
| `CloseAsync` | `RESOLVED -> CLOSED` | `Mutate(expectedStatus: Closed)` | `Denied_close_transition_preserves_state_fields_events_and_persistence` |

The tests currently run in `TicketLifecycleConfiguredTransitionTests`; they
assert status and relevant aggregate fields, unchanged uncommitted events, and
zero repository saves for denied operations. The execution, waiting, resume,
and resolution tests additionally assert their work-item or resolution fields.

`AddEvidenceAsync`, `CompleteWorkAsync`, and `ReassignAsync` were also checked.
They perform authorization and domain validation before persistence but do not
introduce a ticket status target in their service action. Their domain methods
do not change ticket status, so no additional target-status preflight is
justified by this audit.

## Specialized Incident and Problem paths

`IncidentService.ResolveAsync`, `ReopenAsync`, and `CloseAsync`, and
`ProblemService.ResolveAsync`, `CloseAsync`, and `ReopenAsync` call
`EnsureSpecializedStatusTransitionAllowedAsync` before changing the subtype row.
Their shared mutation paths then call
`TransitionSpecializedStatusAsync` inside the transaction after subtype
mutation staging. This preserves the owner-selected read-only preflight seam
and the in-transaction transition check. The source also shows explicit
authorization before those preflight calls. Non-status Incident and Problem
operations use the shared transaction/audit helper and have no lifecycle target.

Creation remains a distinct path: the ticket lifecycle service creates the
ticket, and the subtype service starts its own transaction after that creation
before writing subtype/event/audit rows. The existing source reports identify
provider atomicity and uncertain commit behavior as unverified; this audit does
not repeat or promote that evidence.

No additional generic pre-mutation defect was found after Slice 15. The
bounded lifecycle audit is therefore closed without another production change.

## Stage 2 exit reconciliation

Source-level transition ordering and denied-mutation coverage are evidenced for
the generic lifecycle methods above. The roadmap's Stage 2 exit criterion also
requires provider-backed persistence tests, failure-injection results,
audit-chain evidence, and lifecycle API acceptance for each lifecycle. Those
criteria remain unverified. In particular, the dependency register still lists
TGT/OP/BASE and formal PRE evidence as pending, and the RBAC metadata contract
continues to block authorized lifecycle provider acceptance. No source or fake
test result proves PostgreSQL rollback, commit ambiguity handling, audit
durability, concurrency, or API-to-database behavior.

The next actionable Stage 2 slice is provider-validation preparation and, once
formal target/operator/baseline authorization is attributable, the narrowly
approved provider transaction/audit validation described by the roadmap. Until
then, no database operation is authorized by this result.

## Evidence and preservation

Reviewed the master roadmap, dependency register, Slice 1–15 reports, current
generic lifecycle/domain source, and Incident/Problem services directly. No
database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
provisioning, seed, cleanup, privilege change, or sequence operation occurred.

The branch remained `workstream-i-lifecycle-authority`. All pre-existing
tracked, untracked, ignored, and local worktree changes were preserved. No
reset, clean, stash, branch switch, commit, or push occurred.
