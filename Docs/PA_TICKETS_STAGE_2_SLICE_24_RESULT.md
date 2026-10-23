# PA_TICKETS Stage 2 Slice 24 — Baseline Provenance and Test Contract

Branch: workstream-i-lifecycle-authority  
Stage 2 status: PARTIAL  
Provider decision: NO-GO

## Result

The historical value 5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7 is not reproducible from an identifiable repository artifact. It must not be treated as an adopted baseline. The provider-independent preflight test was corrected so that this value is explicitly exercised as a rejected, unverified historical hash rather than presented as a successful baseline verification.

No owner adoption, provider authorization, or baseline selection was inferred.

## Historical hash occurrence inventory

Exact repository search found the value in:

- tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironmentTests.cs — the preflight fixture and a fail-closed pending-authorization fixture.
- Docs/reconciliation/T62B_1_EXACT_POSTGRESQL_DEFINITION_MATRIX.md — a recorded definition-matrix value.
- Docs/PA_TICKETS_IMPLEMENTATION_SPRINT_6_RESULT.md and Docs/PA_TICKETS_IMPLEMENTATION_SPRINT_7_RESULT.md — historical report references.
- Docs/PA_TICKETS_POSTGRESQL_READ_ONLY_VALIDATION_AUTHORIZATION.md — a recorded authorization-template value.
- Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md — historical decision-note references.
- Slice 8 and Slice 17–23 reports and the Slice 22 owner-evidence package — references classified by those documents as illustrative, historical, or unresolved.

These occurrences are documentation or test inputs; none identifies an immutable byte source or an attributable baseline-adoption decision. Repetition in reports does not establish provenance or approval.

## Exact-byte verification

The current working copy of tests/scripts/postgresql-foundational-ddl.sql, read as raw bytes, hashes to:

21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64

The only Git history entry found for that path was commit 1bcc493d267e23e3e126b9b698fa4344ebe8588a (2026-10-22). Its exact blob bytes were obtained with git cat-file, written to a temporary file outside the repository, and hashed independently:

4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19

Neither byte stream hashes to 5CAA.... No line-oriented shell output was hashed, and no temporary file was retained in the repository.

Classification:

| Value | Classification | Evidence |
|---|---|---|
| 21D7... | Actual hash of the current working-copy artifact | Direct raw-byte SHA-256 |
| 4F4E... | Historical hash of an identifiable Git artifact | Exact blob from commit 1bcc493d... |
| 5CAA... | Unverified recorded/illustrative reference | No matching artifact or attributable provenance found |

Neither 21D7... nor 4F4E... is thereby owner-adopted.

## Preflight contract review

PostgreSqlProviderPreflight.Verify requires an explicit repository-local artifact and an independently supplied 64-character SHA-256, then hashes the artifact bytes and rejects a mismatch. It does not infer a baseline. It also fails closed for missing references, non-OWNER_APPROVED status, expiry, prohibited targets, invalid descriptors, and the wrong operation (READ_ONLY_CATALOG_RECONCILIATION).

The original test name and assertions claimed that 5CAA... successfully verified the current DDL path. That claim was false because the bytes do not match. Selecting 21D7... would instead have silently selected an unapproved baseline. The safe correction was therefore to rename the test to Provider_preflight_rejects_unverified_historical_baseline_hash_without_provider_access, assert the hash-mismatch exception, and retain the independent expected value. The expected hash is not computed dynamically from the file under test.

The related pending-authorization test continues to prove fail-closed behavior for missing approval state and evidence references. No authorization policy or runtime provider behavior was changed.

## Files changed

- tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironmentTests.cs — stale success fixture converted to an explicit mismatch/rejection test.
- Docs/PA_TICKETS_STAGE_2_SLICE_24_RESULT.md — this evidence report.

## Validation

Focused provider-independent command:

    dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~Provider_preflight|FullyQualifiedName~PostgreSQL_tests_require|FullyQualifiedName~Target_descriptor|FullyQualifiedName~Schema_reconciliation" -v minimal

Result: 9 passed, 0 failed, 0 skipped.

git diff --check was run after the edit; no new whitespace errors were reported (the repository may still emit its existing line-ending notices).

No PostgreSQL or Oracle connection, provider-backed test, SQL, migration, DDL, DML, provisioning, seed, cleanup, privilege, or sequence operation occurred. The protected databases and roles were not accessed.

## Remaining decision and next action

BASE remains blocked. An accountable owner must select an immutable baseline artifact/version, provide its provenance and exact-byte SHA-256, and record an attributable adoption decision. The selected artifact must then be reconciled with the preflight input without changing the verifier to infer or dynamically derive the expected hash.

TGT, OP, PRE, and operation-specific authorization remain independently unresolved as applicable. The READ_ONLY_CATALOG_RECONCILIATION operation must not be widened implicitly to authorize the persistence write test. Provider execution remains NO-GO; Stage 2 remains PARTIAL.

The branch and all pre-existing tracked, untracked, ignored, and local worktree changes were preserved. No reset, clean, stash, branch switch, commit, or push was performed.
