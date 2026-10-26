# PA_TICKETS Stage 2, Slice 19 — Owner Approval Submission Record

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Status:** `PENDING` — submission-ready only; no approval or provider execution

## Owner decision request

Please review [Slice 18](PA_TICKETS_STAGE_2_SLICE_18_RESULT.md) and this
record, then submit attributable decisions through an approved external
channel selected by the accountable owner. This record authorizes no database
operation. The exact target candidate is:

`localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`

The repository records two competing hashes for
`tests/scripts/postgresql-foundational-ddl.sql`:

- current working-copy bytes: `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`;
- earlier recorded value: `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`.

Neither hash is approved. BASE approval must select an immutable artifact and
version, reconcile the byte difference, establish provenance, and record an
attributable adoption decision.

## Compact approval checklist

| ID | Decision required | Approver role | Evidence reference | Expiry | Status |
|---|---|---|---|---|---|
| TGT | Approve the exact target and server-authentication policy, including hostname/peer/TLS/SNI and independent identity evidence | Security/database/release authority | ____________________ | __________ UTC | `PENDING` |
| OP | Approve a least-privilege non-superuser role and exact database/schema/object/function privileges for this test and cleanup | Security/database authority | ____________________ | __________ UTC | `PENDING` |
| BASE | Adopt one immutable foundational DDL artifact/version and reconciled SHA-256 with provenance and expected-definition review | Database/release authority | ____________________ | __________ UTC | `PENDING` |
| PRE | Approve fail-closed preflight requirements, evidence retention, expiry, and target/operator references | Application architecture/transaction and security owners | ____________________ | __________ UTC | `PENDING` |
| OP-TEST | Authorize only the repository persistence test and its reviewed writes, cleanup, recovery, and evidence capture | Database/release authority and test owner | ____________________ | __________ UTC | `PENDING` |

For each row, the owner must complete:

```text
Decision: APPROVED / REJECTED / DEFERRED
Owner identity: ____________________
Decision date (UTC): ____________________
Scope/environment: ____________________
Approval reference or signature: ____________________
Rationale and evidence references: ____________________
```

## Operation-specific scope

The only requested operation is:

`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`

The test generates a unique ticket ID and `PERSIST-*` number, writes one ticket
through `ITicketRepository.AddAsync`, reloads it in a fresh scope, verifies
ticket fields plus one `TICKET_CREATED` audit row and history entry, and then
uses its `finally` block to:

1. delete `PA_TICKETS_ASSIGNMENT` rows for that ticket ID;
2. delete `PA_TICKETS_WORK_ITEM` rows for that ticket ID;
3. delete `PA_TICKETS_EVENT` rows for that ticket ID;
4. call `pa_tickets_maintenance_delete_audit_for_ticket(ticketId)`; and
5. delete the `PA_TICKETS` row for that ticket ID.

Approval must explicitly cover these writes and cleanup effects, the
maintenance function, recovery if commit or cleanup is uncertain, and retained
evidence. The test must stop on target mismatch, uncertain transaction outcome,
or uncertain cleanup; no manual cleanup is included.

The existing preflight operation constant
`READ_ONLY_CATALOG_RECONCILIATION` does not authorize this write test. It must
not be bypassed or widened implicitly; operation-specific authorization is
required.

## Submission and execution boundary

The repository contains no configured owner contact, ticket queue, or approved
submission integration. The accountable owner must select the approved external
channel and preserve the resulting attributable record. No request is claimed
to have been submitted, and no identity, signature, timestamp, approval ID, or
provider evidence is invented here.

Provider execution remains blocked until TGT, OP, BASE, PRE, and OP-TEST are
all attributable, mutually consistent, unexpired, and independently verified.
After that, the next technical action is a fresh target-guard and identity
check followed by this one test only.

## Validation and preservation

Read Slice 18 and the existing TGT/OP/BASE/PRE artifacts. `git diff --check`
passed with existing LF/CRLF conversion notices. No PostgreSQL or Oracle
connection, SQL, migration, DDL/DML, provisioning, cleanup, privilege change,
or sequence operation occurred. Stage 2 remains `PARTIAL`; all existing
tracked, untracked, ignored, and local worktree changes were preserved.
