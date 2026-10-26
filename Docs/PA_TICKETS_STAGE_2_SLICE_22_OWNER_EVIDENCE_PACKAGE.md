# PA_TICKETS Stage 2, Slice 22 — Owner Evidence Package

**Branch:** `workstream-i-lifecycle-authority`  
**Status:** owner completion required; no provider operation authorized

This package consolidates the unresolved gates from [Slice 21](PA_TICKETS_STAGE_2_SLICE_21_RESULT.md).
Each gate requires an attributable decision and retained evidence. A policy
recommendation, source guard, or conversation-level instruction is not a
substitute for the independent gates below.

## TGT — Target identity approval

**Decision:** approve the exact target and the method used to authenticate the
connected PostgreSQL server.

Owner must supply:

- host, port, database, and intended database role;
- hostname/DNS, peer-address, TLS certificate/SNI, proxy, and connection
  security policy;
- owner identity, decision date, scope, expiry, approval reference, and
  evidence-retention location; and
- independent server/database/role identity evidence captured immediately
  before any provider operation.

The candidate under review is `localhost:5432 / pa_tickets_codex_e2e /
pa_tickets_codex`; it is not approved by this package. No identity check is
performed here.

## OP — Least-privilege operator approval

**Decision:** approve a dedicated non-superuser role and exact permissions for
the one repository test and its generated-record cleanup.

Owner must supply the role identity, permitted database/schema/object/function
privileges, whether test writes and cleanup are included, prohibited
privileges/targets, scope, expiry, owner attribution, approval reference, and
retained grant evidence. A superuser observation is not a least-privilege
decision. No role or grant is created, changed, or inspected live here.

## BASE — Immutable foundational DDL adoption

**Artifact path:** `tests/scripts/postgresql-foundational-ddl.sql`

Recorded values, neither selected:

- current working-copy hash: `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`;
- historical recorded/illustrative value: `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`.

Owner must identify one immutable artifact/version and byte source, provide
source revision/provenance and a byte-level explanation of the difference, then
explicitly adopt the verified artifact/hash with an effective date and retained
approval reference. Neither recency nor working-tree presence selects a
baseline. The DDL and history remain unchanged.

## PRE — Fail-closed preflight approval

**Decision:** approve the preflight evidence package for one exact operation.

Required fields are target evidence reference, least-privilege grant evidence
reference, adopted baseline path/hash, explicit operation identifier, owner
attribution, issue/expiry, retention boundary, and evidence location. The
owner must confirm that the existing verifier accepts the exact operation
without bypassing or weakening checks. The verifier's current operation
constant remains `READ_ONLY_CATALOG_RECONCILIATION`; this package does not
modify it.

## Operation-specific authorization (OP-TEST)

The current conversation contains explicit authorization for:

`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`

That authorization is recorded as `PARTIAL`; it does not supply TGT, OP, BASE,
or PRE evidence. The future test would generate one unique ticket, write it
through `ITicketRepository.AddAsync`, reload it in a fresh scope, verify ticket
fields plus one `TICKET_CREATED` audit row and history entry, then run its
generated-ID cleanup:

1. delete `PA_TICKETS_ASSIGNMENT` by ticket ID;
2. delete `PA_TICKETS_WORK_ITEM` by ticket ID;
3. delete `PA_TICKETS_EVENT` by ticket ID;
4. call `pa_tickets_maintenance_delete_audit_for_ticket(ticketId)`; and
5. delete `PA_TICKETS` by ticket ID.

The owner must approve this exact write, assertion, cleanup-function, recovery,
and evidence-retention scope through an attributable record. Stop on target
mismatch, uncertain transaction outcome, uncertain cleanup, or missing evidence.
The read-only preflight operation must not be widened or bypassed implicitly.

## Go/no-go gate

Before opening any provider connection, verify all of the following:

- TGT is approved and independently verifiable.
- OP has approved least-privilege grants and scope.
- BASE is immutable, reconciled, and explicitly adopted.
- PRE is complete, valid, unexpired, and tied to the exact target/operator.
- OP-TEST covers every write, assertion, cleanup, and recovery boundary.
- Fresh target guard and independent identity checks pass.
- No conflicting, expired, or unverifiable evidence remains.

Any missing or contradictory item is **NO-GO**. Do not open a connection.

Owner identity: ____________________  Decision date (UTC): ____________________  
Scope/environment: ____________________  Approval reference/signature: ____________________
