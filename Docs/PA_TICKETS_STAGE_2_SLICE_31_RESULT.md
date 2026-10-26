# PA_TICKETS Stage 2 Slice 31 — Candidate Owner Decisions and Policy Ambiguity

Branch: workstream-i-lifecycle-authority  
Stage 2 status: PARTIAL  
Provider execution: NO-GO

## Outcome

Added a clearly labelled candidate-policy section to the existing owner
decision packet. D01-D10 remain formally PENDING; the candidate choices are
not approvals and do not authorize schema changes, migrations, grants, or
provider tests. D07 and D10 retain their existing OWNER-PROPOSED —
PENDING FORMAL APPROVAL status.

## Candidate decisions prepared

The packet now records conservative owner-review choices for:

- D01 immutable baseline adoption only after raw-byte hash, provenance, and
  expected/observed definition review.
- D02 dedicated least-privilege non-superuser operation.
- D03 preserved sequence state with no blind resets or advances.
- D04 canonical permission codes with explicit alias/duplicate handling.
- D05 preservation of unknown grant dates/provenance and fail-closed access.
- D06 validated authenticated client/tenant relationships.
- D07 explicit lifecycle-managed scope/delegation records, preserving explicit
  rows and failing closed on conflicts.
- D08 separate approval of five built-in roles and their grant matrix.
- D09 explicit unknown migration-history representation without fabricated
  ledger entries.
- D10 explicit user/service/system actors with compatible nullable/FK and
  hash-chain semantics.

Each candidate includes rationale, dependencies, acceptance evidence, and
prohibited operations. Existing owner attribution fields remain the required
formal response mechanism.

## Baseline ambiguity

The current foundational DDL raw-byte hash remains:

21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64

The identifiable historical Git blob remains:

4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19

The repeated 5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7
reference remains untraceable to an artifact. No baseline was selected. The
owner must review the exact artifact definition and provenance, then record
one immutable adoption decision.

## TGT, OP, PRE, and OP-TEST templates

The candidate intake requirements remain:

- TGT: proposed target is localhost:5432 / pa_tickets_codex_e2e /
  pa_tickets_codex; fresh independent server, database, role, hostname/peer,
  and TLS evidence is still missing.
- OP: approve a dedicated least-privilege role with exact database, schema,
  object, maintenance-function actions, scope, and expiry; no role or grant
  change was made.
- PRE: bind attributable TGT, OP, BASE, and operation references with
  retention/custody and expiry; no formal approval is recorded.
- OP-TEST: authorize only
  Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts,
  its generated records, exact cleanup function/statements, recovery boundary,
  and retained evidence. READ_ONLY_CATALOG_RECONCILIATION does not authorize
  this write test.

TGT, OP, BASE, and PRE remain BLOCKED. OP-TEST remains partial because the
conversation-level scope input lacks independent attribution and cannot
override the other gates.

## Validation and preservation

Inspected the roadmap, Slices 27–30, owner decision and approval packets,
preflight contracts, DDL/hash records, and transaction artifacts. No database
or provider operation, SQL, migration, schema/RBAC change, provisioning,
seeding, cleanup, privilege, sequence, or migration-026 operation occurred.

Ran git diff --check. It reported existing trailing-whitespace findings in
pre-existing modified lines of the decision packet and LF/CRLF conversion
notices; these were preserved rather than rewritten. The newly added candidate
section and this result report introduce no intentional trailing whitespace.
The decision packet and this result report are local worktree changes; no
commit or push occurred.
All pre-existing tracked, untracked, ignored, and local changes were
preserved. Stage 2 remains PARTIAL.
