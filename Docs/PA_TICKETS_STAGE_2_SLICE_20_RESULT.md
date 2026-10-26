# PA_TICKETS Stage 2, Slice 20 — Owner Approval and Gate Revalidation

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`  
**Execution decision:** **BLOCKED before connection**

## Approval recorded

The current user instruction states: **“approved, proceed!”** in response to
the Slice 19 owner-approval package. This is recorded as an explicit
conversation-level authorization for the operation-specific candidate test:

`Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts`

The source is the current conversation. No reliable owner identity, timestamp,
signature, approval ID, external submission reference, expiry, or evidence
retention record was supplied, so none is invented. This statement does not
establish TGT, OP, BASE, or PRE approval and does not authorize a connection by
itself.

## Gate revalidation

| Gate | Result | Evidence and remaining requirement |
|---|---|---|
| **TGT** | `BLOCKED` | The candidate remains `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, but no attributable target approval, hostname/peer/TLS policy, or fresh independent server/database/role evidence is present. |
| **OP** | `BLOCKED` | No approved least-privilege non-superuser role, exact grants, scope, expiry, or grant evidence exists. Prior `postgres` superuser metadata is not approval. |
| **BASE** | `BLOCKED` | Current DDL bytes hash to `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`; repository records also contain `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7`. No attributable immutable artifact/version, provenance, reconciliation, or adoption decision selects either value. |
| **PRE** | `BLOCKED` | The fail-closed verifier exists, but no attributable formal approval, retention boundary, expiry, target-evidence reference, or least-privilege reference is recorded. Its operation constant remains `READ_ONLY_CATALOG_RECONCILIATION`. |
| **OP-TEST** | `CONVERSATION-AUTHORIZED / FORMAL EVIDENCE PENDING` | The user statement authorizes the named test scope in this conversation. Formal attribution, target binding, write/cleanup/recovery boundaries, and retained evidence are still absent. The read-only preflight constant does not authorize this write test and was not bypassed. |

Because TGT, OP, BASE, and PRE are missing or contradictory, the required
fail-closed decision is to stop before opening a database connection. The
operation-specific approval cannot override those independent gates.

## Evidence still required

Before any provider operation, an attributable record must provide:

1. exact target approval and independent identity/authentication evidence;
2. least-privilege operator role and exact database/schema/object/function
   grants with scope and expiry;
3. one immutable baseline artifact/version, reconciled SHA-256, provenance, and
   adoption decision;
4. formal preflight approval with retention boundary, expiry, and target/OP
   evidence references; and
5. operation-specific authorization covering the generated ticket writes,
   audit/history assertions, all `finally` cleanup statements and
   `pa_tickets_maintenance_delete_audit_for_ticket`, recovery boundaries, and
   evidence capture.

No migration, DDL, schema/RBAC change, seed, privilege change, sequence
operation, or manual cleanup is authorized. The persistence test was not run.

## Validation and preservation

Read Slice 19, the provider preflight and target guard, and the persistence
test. The current working-copy baseline hash was independently rechecked with
`Get-FileHash` and remains `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`.
No database/provider connection or SQL operation occurred. `git diff --check`
passed with existing line-ending notices. All tracked, untracked, ignored, and
local worktree changes were preserved; no reset, clean, stash, branch switch,
commit, or push occurred.

The next action is to obtain attributable TGT/OP/BASE/PRE evidence and reconcile
the baseline hash. Only then may the separately approved test be reconsidered
for a fresh target guard and execution.
