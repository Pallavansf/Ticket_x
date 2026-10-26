# PA_TICKETS Stage 2, Slice 17 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`  
**Scope:** provider-validation authorization readiness; no provider operation

## Authorization evidence matrix

| Gate | Evidence present | Missing or inconsistent | Effect |
|---|---|---|---|
| **TGT** | `PostgreSqlTestEnvironment` has a fail-closed single-host descriptor, rejects protected databases/roles, and requires an independently supplied approved descriptor. Sprint 6/7 reports document the verifier design. | No attributable owner approval for the exact host, port, database, role, hostname/peer/TLS policy, or independently authenticated server evidence is present in the repository. A caller-supplied descriptor alone is not server identity proof. | Blocks any provider connection. |
| **OP** | T62A.2 records that the observed `postgres` role was a superuser and documents minimum schema privileges. The test guard requires explicit opt-in. | No approved least-privilege operator identity, grant scope, expiry, or operation authorization is recorded. The prior superuser observation is not approval. | Blocks provider execution. |
| **BASE** | The selected artifact can be explicitly supplied to `PostgreSqlProviderPreflight`; the verifier checks repository locality and exact bytes. | Current working-copy hash is `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`. Sprint 6/7 reports recorded `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`. The recorded hash does not match the current bytes, and no provenance/adoption decision is attributable. | Blocks baseline approval until the artifact/version and provenance are reconciled. |
| **PRE** | `PostgreSqlProviderPreflight.Verify` fails closed for missing approval state, expiry, target evidence, least-privilege evidence, operation, prohibited target, invalid baseline, and hash mismatch. | No attributable formal PRE approval or provider evidence exists. The verifier is a source-level guard, not proof that a provider identity or catalog query succeeded. | Blocks provider execution. |
| **Operation authorization** | The repository persistence test is narrowly identifiable and uses generated ticket IDs, reloads in a fresh scope, verifies one audit row/history, and performs test-owned cleanup in `finally`. | No operation-specific approval authorizes writes, the exact target, cleanup SQL/function use, or retention/evidence handling. The existing preflight's operation constant only permits `READ_ONLY_CATALOG_RECONCILIATION`, not this write test. | Blocks the persistence test. |

Owner-selected policy recommendations in the dependency register are inputs for
review, not formal approvals. No owner identity, signature, approval reference,
or technical provider validation was invented or inferred.

## Baseline hash reproduction

`Get-FileHash tests/scripts/postgresql-foundational-ddl.sql -Algorithm SHA256`
was run read-only against the working copy. It returned:

```text
21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64
```

The earlier `5CAA...` value remains recorded in Sprint 6/7 reports, but it is
not the hash of the current working-copy bytes. This is an evidence conflict,
not an approval or evidence that the file was changed by a migration. BASE must
select one immutable artifact version, reconcile the difference, record
provenance, and obtain attributable adoption approval before any provider run.

## Narrow candidate operation

The smallest useful later operation is the existing
`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`
test in `PostgreSqlTicketPersistenceTests`. It creates one generated ticket,
checks fresh-context reload plus its `TICKET_CREATED` audit/history records,
and in `finally` deletes only rows keyed by that generated ticket ID (including
assignment, work-item, event, audit-maintenance cleanup, and ticket rows).
This source review does not establish that the cleanup function, permissions,
or transaction behavior are safe on a live target; those require explicit
operation approval and provider evidence.

Before that test may run, the owner package must supply: (1) attributable TGT
identity and server-authentication evidence, (2) an OP decision for the exact
write account and scope, (3) a reconciled BASE artifact/hash/provenance and
adoption decision, (4) an operation-specific authorization naming this one
test and its test-owned cleanup, (5) evidence-retention and recovery boundaries,
and (6) any required PRE/security approvals. Missing or contradictory evidence
must fail closed.

## Validation and preservation

Reviewed the master roadmap, dependency register, Slice 7, Slice 8, Slice 16,
RBAC reconciliation, Sprint 6/7 authorization/preflight artifacts, the current
preflight verifier, test guard, and repository persistence test. No tests were
run because this gate is authorization preparation only. `git diff --check`
passed with existing LF/CRLF conversion notices.

No PostgreSQL or Oracle connection, SQL execution, migration, DDL/DML,
provisioning, seeding, cleanup, privilege change, or sequence operation
occurred. The branch remained `workstream-i-lifecycle-authority`; all existing
tracked, untracked, ignored, and local worktree changes were preserved. No
reset, clean, stash, branch switch, commit, or push occurred.

The next action is human review of the TGT/OP/BASE/PRE and operation-specific
approval fields. Until those fields contain attributable evidence and the
current baseline-hash conflict is resolved, no provider operation is
authorized.
