# PA_TICKETS Stage 2, Slice 23 — Owner Evidence Workflow Audit

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`  
**Provider decision:** `NO-GO`

## Workflow findings

### TGT

`PostgreSqlTestEnvironment.RequireConnectionString` and
`BuildTargetDescriptor` require a connection string, explicit opt-in, and an
exact single-host approved descriptor while rejecting protected databases and
roles. `PostgreSqlProviderPreflight.ParseDescriptor` rejects incomplete
descriptors, prohibited databases/roles, and multi-host values. These checks do
not authenticate the server, validate TLS/SNI/peer identity, or consume an
owner-attributed target-policy record. The owner can provide string references
through the authorization record, but independent identity evidence remains an
external prerequisite.

### OP

`PostgreSqlProviderValidationAuthorization` carries a least-privilege decision
reference, and the verifier requires it to be non-empty. Neither verifier
validates the referenced role's actual grants, scope, expiry, or whether
cleanup-function privileges are included. Existing superuser metadata is not
operator approval. A security/database owner must supply and retain the grant
decision; no safe source-only correction can infer it.

### BASE

The verifier requires an explicit repository-local artifact and exact 64-digit
SHA-256. It does not infer a baseline. The current DDL hash is
`21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`, while
the success test and several historical documents still use `5CAA...`. The
success test therefore fails closed, correctly exposing unresolved baseline
provenance rather than authorizing either version.

### PRE

`PostgreSqlProviderPreflight.Verify` rejects null/incomplete records, non-
`OWNER_APPROVED` status, expired records, missing retention, missing target
evidence, missing least-privilege evidence, wrong operation, prohibited target,
invalid baseline paths, and hash mismatches. It does not verify the contents or
attribution of referenced evidence, bind a read-only record to the separate
write-test operation, or perform server identity verification. Those are
deliberate owner/provider gates, not source claims.

### OP-TEST

The current conversation authorizes the named persistence test only at the
operation-scope level. The test's generated ticket write, audit/history
assertions, generated-ID cleanup, maintenance audit-delete function, and
uncertain-outcome stop conditions are documented in Slice 22. The verifier's
only accepted operation is `READ_ONLY_CATALOG_RECONCILIATION`; it cannot
authorize the write test without a separately approved operation contract.

## Concrete workflow defect and disposition

`PostgreSqlTestEnvironmentTests.Provider_preflight_verifies_explicit_baseline_hash_without_provider_access`
still supplies the historical `5CAA...` value for the current DDL path. The
provider-independent preflight selection returned **8 passed, 1 failed, 0
skipped**; the failure was the expected hash-mismatch exception. This is a
stale test fixture/evidence conflict, but changing it to `21D7...` would select
a baseline without an attributable BASE adoption decision. Computing the
expected hash from the file would weaken the exact-artifact check. No source or
test change is therefore safe in this slice; BASE owner evidence must resolve
the fixture first.

No other missing fail-closed branch was established. Duplicate descriptor keys
throw during `ToDictionary`, malformed ports and paths reject, expiry is
checked, and prohibited targets are rejected before a provider connection.

## Remaining owner actions

1. Provide attributable TGT approval and independent server identity policy/evidence.
2. Provide OP approval for a dedicated least-privilege role, exact grants,
   scope, and expiry.
3. Resolve BASE by selecting an immutable artifact/version, reconciling
   `21D7...` versus `5CAA...`, and recording provenance/adoption.
4. Provide PRE approval with valid retention, expiry, and referenced TGT/OP
   evidence.
5. Provide operation-specific approval for the persistence test and its writes,
   cleanup, recovery, and evidence retention.

Until all five are attributable and consistent, provider execution remains
`NO-GO`; the read-only preflight scope must not be widened implicitly.

## Validation and preservation

Ran the provider-independent filter:

```text
dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~Provider_preflight|FullyQualifiedName~PostgreSQL_tests_require|FullyQualifiedName~Target_descriptor|FullyQualifiedName~Schema_reconciliation" -v minimal
```

Result: **8 passed, 1 failed, 0 skipped**; the single failure was the stale
historical hash fixture described above. No PostgreSQL/Oracle connection,
provider-backed test, SQL, migration, DDL/DML, provisioning, seed, cleanup,
privilege, or sequence operation occurred. `git diff --check` passed with
existing LF/CRLF notices. All pre-existing tracked, untracked, ignored, and
local worktree changes were preserved; no reset, clean, stash, branch switch,
commit, or push occurred.
