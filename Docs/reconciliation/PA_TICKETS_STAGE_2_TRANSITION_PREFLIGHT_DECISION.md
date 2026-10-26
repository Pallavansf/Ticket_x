# Incident/Problem Transition Preflight Decision Record

**Status:** `OWNER DECISION REQUIRED`  
**Scope:** source-level transition ordering only  
**Migration/database authorization:** none

## Decision question

May Incident and Problem status operations perform a read-only lifecycle
transition preflight against the parent ticket before mutating their tracked
subtype row, while retaining the existing authorization, domain-invariant,
transaction, event, audit, and commit boundaries?

The preflight must validate the requested target (`Resolved`, `Closed`, or
`Reopened`) against the same configured transition policy used by
`TicketLifecycleService.TransitionSpecializedStatusAsync`. A denied or
invalid target must fail before subtype-row mutation, event creation, audit
writing, or persistence. An allowed preflight must not itself mutate, save,
write an audit row, consume a domain event, or authorize a retry. The actual
status mutation remains responsible for repeating the check immediately before
mutation so the preflight is advisory with respect to races.

## Alternatives and effects

1. **Approve a shared preflight contract (recommended):** expose a read-only
   application contract implemented by `TicketLifecycleService`; Incident and
   Problem call it before subtype mutation and retain the existing lifecycle
   call inside their transaction. This closes the source-level ordering gap,
   but provider-backed tests must still establish transaction and concurrent
   policy behavior.
2. **Rely on the existing local provider transaction:** make no source change
   and rely on rollback after a denied lifecycle call. This leaves tracked
   state and rollback semantics provider-dependent and does not prove the
   ordering invariant.
3. **Move subtype mutation after the lifecycle call without a preflight
   contract:** this risks changing audit/event ordering and transaction
   ownership and is not approved by existing source evidence.

## Required owner response

**Accountable role:** Application architecture / database transaction owner,
with security/RBAC and audit owners reviewing the unchanged authorization and
audit semantics.

**Response:** `APPROVE` / `REJECT` / `NEEDS DISCUSSION`  
**Owner identity:** ____________________  
**Decision date:** ____________________  
**Scope/version:** ____________________  
**Approval reference:** ____________________  
**Rationale/evidence:** ____________________

Approval of this record does not authorize database access, migrations,
provider tests, or migration 026.

## Evidence required before implementation

- Focused tests proving denied and allowed Resolved/Closed/Reopened paths do
  not mutate subtype state before validation and preserve event/audit ordering.
- A provider-backed test plan for rollback, commit failure, and concurrent
  policy changes; fake tests cannot establish those properties.
- Confirmation that the implementation does not alter public error semantics,
  authorization ordering, transaction ownership, or audit-chain behavior.
