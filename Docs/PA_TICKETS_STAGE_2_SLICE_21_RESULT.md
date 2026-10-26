# PA_TICKETS Stage 2, Slice 21 — Evidence Reconciliation Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`  
**Execution decision:** provider operation remains blocked

## Gate matrix

| Gate | Status | What repository evidence proves | Missing evidence / smallest next action |
|---|---|---|---|
| TGT | `BLOCKED` | `PostgreSqlTestEnvironment` builds a single-host descriptor and rejects protected targets; Slice 20 records the candidate `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`. | No attributable target approval, hostname/peer/TLS policy, or fresh independent server/database/role evidence. Obtain and retain an owner-attributed target decision, then perform the separate pre-connect identity check. |
| OP | `BLOCKED` | T62A.2 documents prior metadata for a superuser and the source guard requires explicit opt-in and an approved descriptor. | No least-privilege role, exact grants, scope, expiry, or attributable approval. Obtain a dedicated operator decision and grant evidence; do not treat `postgres` metadata as approval. |
| BASE | `BLOCKED` | Current working-copy bytes hash to `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`. The older `5CAA...` value is recorded in T62B.1, Sprint 6/7, the read-only authorization packet, provider-preflight illustrative tests, and RBAC repair notes. | `git log --all -- tests/scripts/postgresql-foundational-ddl.sql` shows only the 2026-10-22 baseline commit (`1bcc493d267e23e3e126b9b698fa4344ebe8588a`); no repository artifact establishes an immutable owner-adopted version or explains the byte difference. Obtain an immutable artifact/version, provenance, byte-level reconciliation, and adoption decision. |
| PRE | `BLOCKED` | `PostgreSqlProviderPreflight.Verify` fails closed for missing `OWNER_APPROVED` state, expiry, target evidence, least-privilege evidence, retention, prohibited targets, invalid baseline, and wrong operation. | No attributable PRE approval, retention boundary, expiry, or referenced TGT/OP evidence. Complete those fields and independently validate the verifier without contacting a provider. |
| OP-TEST | `PARTIAL` | The current conversation contains explicit “approved, proceed!” authorization for the named repository-persistence test; Slice 20 records it without invented identity or timestamp. Slice 19/20 describe its generated ticket, audit/history assertions, cleanup statements, and stop conditions. | Conversation approval is not independent TGT/OP/BASE/PRE evidence and has no formal attribution, expiry, or retained external reference. Preserve it as operation-scope input and obtain the missing gate evidence before execution. |

## Baseline reconciliation

The current file was hashed read-only with `Get-FileHash`:

```text
tests/scripts/postgresql-foundational-ddl.sql
21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64
```

The `5CAA6971...` value is a repeated recorded/illustrative value, not a
second immutable artifact found in the current Git history. The only history
entry for the path is commit
`1bcc493d267e23e3e126b9b698fa4344ebe8588a` (`2026-10-22`, “Publish PA Tickets
application baseline”); Git history alone does not prove owner adoption or
explain the working-tree byte difference. Neither hash is selected or approved.
No DDL, historical report, or migration was rewritten.

## Files inspected

Reviewed Slice 17–20, the master roadmap, dependency register, TGT/OP/BASE/PRE
packets, `PostgreSqlProviderPreflight.cs`, `PostgreSqlTestEnvironment.cs`,
`PostgreSqlTestEnvironmentTests.cs`, and the exact repository-persistence test
including its generated-record cleanup. No production or test source was
changed.

## Validation and preservation

`git diff --check` passed with the existing LF/CRLF conversion notices. No
PostgreSQL connection, provider-backed test, SQL, migration, DDL/DML,
provisioning, seed, cleanup, privilege change, or sequence operation occurred.
A direct PowerShell pipeline attempt to hash a historical `git show` stream was
not usable because `Get-FileHash` received line objects rather than a file;
therefore no historical byte hash is claimed beyond the documented `5CAA...`
records. It did not modify the repository.
The branch remained `workstream-i-lifecycle-authority`; all existing tracked,
untracked, ignored, and local changes were preserved. No reset, clean, stash,
branch switch, commit, or push occurred.

The next smallest safe action is owner completion of attributable TGT, OP, BASE,
and PRE evidence, including reconciliation of the two recorded hashes. Only
after those gates are consistent and independently verified may the
conversation-authorized persistence test be reconsidered for execution.
