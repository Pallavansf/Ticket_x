# PA_TICKETS Stage 2 — Migration 014 Owner Approval Request

**Status:** REQUEST FOR OWNER COMPLETION — no approval is recorded by this
document.

## Exact operation

Approve or reject one first application of the immutable PostgreSQL artifact:

- Path: `database/migrations/014_incident_foundation.sql`
- Provenance commit: `0b595133286bae55b0b8fc079a631bd194cf43da`
- Git blob: `2943a7bf977772ce3619b3ae5eda473cf030547f`
- Raw Git-blob SHA-256: `08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3`
- Target candidate: `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`

The target's last recorded preflight found a compatible `PA_TICKETS` key,
absent Incident objects, and no migration ledger. Migration history is therefore
**UNKNOWN**; absence of the ledger does not prove that migration 014 was never
applied.

## User-confirmed conditional input (Slice 58; not formal approval)

The user explicitly confirmed the recommended `APPROVE` selections for D01 and
D09 for owner review, subject to exact baseline verification and compatibility,
and conditionally authorized migration 014 in principle. The conditional scope
requires every TGT, OP, PRE, recovery, and evidence-retention gate below to pass
before execution. The execution window and expiry remain unset. No owner
identity, role, signature, approval reference, UTC date, or expiry was supplied;
this input does not authorize immediate execution and does not change any formal
decision status.

## Explicit owner-supplied decision (Slice 67; conditional, not fully cleared)

Owner identity: `Mr. H`; role: `SE`. The owner explicitly approves the
recommended D01/BASE and D09 decisions, conditionally approves the proposed
target `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, and
conditionally approves read-only preflight and one migration-014 execution,
subject to all safeguards and remaining gates. Migration 026 is expressly
excluded. No signature or approval reference, UTC date, expiry, operator grants,
evidence-custody location, or independent verification was supplied; those
fields remain pending and this statement does not authorize immediate work.

## Decisions for the accountable owner

### D01 / BASE — foundational baseline

Adopt or reject the immutable foundational baseline and state how unknown target
history is handled. The existing decision pack records D01 as `PENDING`.

Response: `APPROVE / REJECT / DEFER`: ____________________  
Owner identity and role: ____________________  
UTC decision date: ____________________  
Scope and expiry: ____________________  
Approval reference/signature: ____________________  
Rationale and evidence references: ____________________

### D09 — unknown migration history and recovery

Approve recording history as `UNKNOWN` without fabricating ledger rows, and
authorize a reviewed first-application procedure only after a fresh read-only
preflight confirms the ticket dependency and absence of partial Incident
objects. If any object is partial or contradictory, stop for a separate recovery
decision.

Response: `APPROVE / REJECT / DEFER`: ____________________  
Owner identity and role: ____________________  
UTC decision date: ____________________  
Scope and expiry: ____________________  
Approval reference/signature: ____________________  
Rationale and evidence references: ____________________

### Migration 014 operation authorization

Authorize or reject exactly one execution of the artifact above on the named
target. This authorization must identify the operator, permitted effects,
execution window, expiry, revocation method, recovery boundary, and evidence
custody. It authorizes no other migration, schema change, seed, grant,
privilege, sequence reset, cleanup, or provider test.

Response: `AUTHORIZE / REJECT / DEFER`: ____________________  
Authorized operator identity/role: ____________________  
Exact privileges and schema scope: ____________________  
Execution window (UTC): ____________________  
Expiry/revocation: ____________________  
Approval reference/signature: ____________________  
Rationale/evidence references: ____________________

### TGT / OP / PRE

Confirm the exact target, authentication/hostname/peer/TLS expectations,
least-privilege operator, read-only preflight scope, evidence retention, and
fixed UTC validity. These gates remain separate from D01, D09, and migration
execution.

Target confirmation: ____________________  
Operator/grants confirmation: ____________________  
Preflight approval/reference: ____________________  
Evidence location and retention period: ____________________  
Issue date/expiry/revocation: ____________________

## Permitted and prohibited effects

If all decisions are approved, the permitted operation is limited to creating
the sequence, Incident table, and three indexes contained in migration 014.
No data seed, role/grant change, migration-ledger insert, migration 015/026,
Phase 5 provisioning, cleanup, or unrelated object change is permitted.

The operator must use one transaction and preserve the exact artifact bytes.
Before execution, perform fresh identity and catalog checks. After confirmed
commit, capture the artifact hash, target identity, operator, timestamp,
transaction outcome, and full object definitions. Retain evidence at:

Evidence custody location: ____________________  
Retention period: ____________________

## Failure and recovery contract

- A pre-commit statement failure preserves the primary exception; attempt one
  rollback and verify the catalog read-only. If rollback fails, classify the
  outcome as `UNKNOWN`.
- A commit, connection, or disposal failure with uncertain outcome is
  `UNKNOWN`; do not retry automatically. Perform independent read-only catalog
  reconciliation and await owner direction.
- Do not create synthetic migration-history rows, assume absence means never
  applied, drop partial objects, or perform speculative cleanup.

## Acceptance gate for Incident atomicity tests

The next provider test operation may proceed only when:

1. D01, D09, migration-014, TGT, OP, and PRE responses are attributable,
   current, consistent, and unexpired.
2. A fresh target/catalog preflight matches the approved target and exact
   migration artifact, with no partial Incident objects and a documented
   unknown-history decision.
3. Migration 014 execution has a confirmed transaction outcome and a
   post-apply catalog comparison matching the artifact and EF mapping.
4. Test scope explicitly authorizes `PostgreSqlIncidentAtomicityTests`, its
   generated-record cleanup, recovery boundary, and evidence retention.

Until all four conditions are evidenced, the correct status is **NO-GO** and
Stage 2 remains `PARTIAL`.
