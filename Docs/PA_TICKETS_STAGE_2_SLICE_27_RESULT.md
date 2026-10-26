# PA_TICKETS Stage 2 Slice 27 — Owner Evidence Gate Resolution and Next Action

Branch: workstream-i-lifecycle-authority  
Stage 2 status: PARTIAL  
Provider execution: NO-GO

## Scope and outcome

This slice reconciled the existing Stage 2 provider gates and owner decision
records against repository evidence. No gate was promoted, no approval was
invented, and no source or migration change was made. The existing
IncidentService.CreateAsync transaction-boundary risk from Slice 26 remains a
named engineering follow-up and was not changed.

## Gate matrix

| Gate | Owner-selected policy | Formal repository approval | Technical evidence | Status | Missing evidence and safe next action | Blocks next operation |
|---|---|---|---|---|---|---|
| TGT | Candidate target is localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex | No attributable target/authentication approval | Static target guards exist; no fresh live identity evidence in this slice | BLOCKED | Security/database owner must approve target, hostname/peer/TLS policy, and retain independent server/database/role evidence. Then perform a fresh pre-connect identity check. | Any provider operation |
| OP | Prefer a dedicated least-privilege non-superuser; postgres metadata is not approval | No attributable role/grant approval | T62A.2 records prior superuser metadata; source verifier only requires a non-empty decision reference | BLOCKED | Database/security owner must specify role, exact database/schema/object/function privileges, scope, and expiry. | Schema or provider writes |
| BASE | No baseline selected; 21D7 and 5CAA remain unresolved | No adoption record | Current DDL is hash-verifiable; provenance/adoption is absent | BLOCKED | Release/database owner must select an immutable artifact/version, reconcile bytes, provide provenance, hash, and adoption decision. | Baseline reconciliation and any provider validation |
| PRE | Fail-closed preflight with retention and expiry is the intended contract | No attributable formal approval or retention record | PostgreSqlProviderPreflight verifies fields, expiry, operation, path, and hash; it cannot validate referenced evidence contents | BLOCKED | Formal PRE approval must bind TGT, OP, BASE, retention, expiry, and evidence custody. | Any provider operation |
| OP-TEST | Conversation-level approval exists for the named repository persistence test | No independently attributable retained approval | Slice 19/20 defines test writes, generated-record cleanup, recovery boundaries, and evidence capture; read-only operation constant remains separate | PARTIAL | Retain an attributable operation-specific approval covering exact writes, cleanup function/statements, recovery, and evidence. It cannot override TGT/OP/BASE/PRE. | The persistence test |
| D01–D03 | Policies/options are recorded in the decision packet | Formal Decision fields remain PENDING | Source reports and hash evidence are available; no owner attribution | PENDING | Owner responses and evidence for baseline, operator, and sequence allocation. | Schema request, inserts, migrations |
| D04–D08 | Canonical RBAC, history, scope, delegation, and built-in-role options are documented; D07 has an owner-proposed preference | Formal Decision fields remain PENDING; D07 is explicitly owner-proposed only | Migrations/readers/contracts identify dependencies and the D07 backfill conflict | PENDING | Security/authorization/data owners must approve each policy and resolve migration 031 scope derivation. | RBAC migrations and API acceptance |
| D09 | Unknown migration history must remain explicit and must not be fabricated | Formal Decision remains PENDING | Ledger absence and source contract are documented | PENDING | Release owner must approve representation and fingerprint/ledger treatment. | Migration 033 and history claims |
| D10 | D10 has an owner-proposed actor-type preference | Formal Decision remains PENDING; migration 038 intentional abort remains | Current AuditRow actor is non-null user ID; migration 038 and chain verifier expose compatibility gap | PENDING | Audit/compliance owner must approve actor types, nullable/FK behavior, writer/reader compatibility, and chain preservation. | Migration 038 and audit schema |
| D11/P26 | Separate migration-026 authorization chain | Decision remains PENDING | Separate hash/lock/recovery contracts exist | PENDING | Migration authority must separately approve exact artifact, target, lock/recovery, provider validation, and execution window. | Migration 026 only |

## Baseline provenance reconciliation

The current working-copy foundational DDL was read as raw bytes and hashes to:

21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64

The only Git history entry previously identified for that path is commit
1bcc493d267e23e3e126b9b698fa4344ebe8588a. Its exact blob was independently
hashed in Slice 24 as 4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19.
The repeated 5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7
value has no identifiable matching artifact in the repository history searched.

These are artifact observations only. No value is owner-adopted, and no
baseline approval can be inferred from a commit, catalog shape, report, or
test fixture.

## Resolved evidence gaps

No owner gate was resolved. The repository does provide a deterministic
hash-verification implementation and a provider-independent rejection test for
the unverified historical hash. This resolves the stale-test interpretation,
not BASE adoption. The local-only Slice 17–23 reports and owner package remain
unpublished; their presence is not formal approval evidence.

## Next authorized engineering action

No provider operation is permissible yet. The shortest safe path is one
owner-attributed evidence intake that resolves TGT, OP, BASE, PRE, and the
exact OP-TEST scope independently, followed by a fresh fail-closed preflight.
Only after those records are consistent and unexpired may the single
repository-persistence test be reconsidered.

The IncidentService.CreateAsync transaction-boundary correction remains the
next engineering follow-up after provider authorization: start the outer
boundary before lifecycle creation, make EfTicketRepository.AddAsync reuse a
caller-owned transaction, and validate commit/rollback/recovery behavior. It
must not be implemented or claimed atomic from this documentation slice.

## Validation and preservation

Validated the referenced report, decision, preflight, test, and DDL paths.
Recomputed the current DDL hash from raw file bytes. Ran git diff --check with
exit code 0; existing LF/CRLF conversion notices were emitted and no
whitespace errors were reported.

No PostgreSQL or Oracle connection, SQL, migration, DDL/DML, provisioning,
seeding, cleanup, privilege, sequence, provider test, or migration-026
operation occurred. No source, test behavior, roadmap status, environment,
configuration, or migration file was modified. The branch remains
workstream-i-lifecycle-authority; all pre-existing tracked, untracked, ignored,
and local changes were preserved. Local evidence remains distinct from the
remote publication state.
