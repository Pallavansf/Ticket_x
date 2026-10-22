# Migration 026 execution-contract proposal

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED FOR EXECUTION**  
Scope: PostgreSQL migration 026 reporting candidate only. This document defines decisions that must exist before a future execution approval; it does not grant approval, identify a live target, or authorize a database operation.

## Evidence boundary

The repository already documents that migration execution requires an independently verified target fingerprint, immutable file hashes, a trustworthy baseline or approved adoption procedure, backup and recovery evidence, preservation manifests, post-migration assertions, and separate written approval for each operation. The v3 candidate also requires one outer transaction, rollback on pre-commit failure, and `UNKNOWN_COMMIT` for an uncertain commit. These are existing documented requirements in `Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`, `tests/TESTING.md`, and the candidate comments.

The repository does not define an authorization verifier, a canonical target-fingerprint authority, or a provider-specific lock adapter. The Gate T runner therefore remains disabled by default (`NoExecutionPolicy` and the executable entry point). The contracts below are new proposals requiring approval; none is represented as an approved policy or runtime fact.

The reviewed files and hashes are:

| Artifact | SHA-256 |
|---|---|
| Original `database/migrations/026_phase8_6_reporting.sql` | `791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4` |
| Candidate `database/migrations/026_phase8_6_reporting.review.v3.sql` | `97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49` |

## Proposed authorization contract

This is a new governance proposal. The database owner and release owner must approve the authority, verifier, and retention rules before implementation.

An execution approval should be a signed, immutable approval record containing at least:

```text
approval_id
migration_id=026
candidate_sha256=97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49
target_fingerprint=<canonical value defined below>
issued_at_utc
expires_at_utc
nonce
release_owner_identity
database_owner_identity
operation=APPLY_MIGRATION_026
```

The proposed authority is two independent human approvals: the release owner approves the exact release artifact and the database owner approves the exact target and operation. Their identities must be authenticated through the organization’s approved identity provider or signing infrastructure. The repository currently records no such trust anchor, signer registry, or approval service; those are owner decisions.

The verifier must validate the signature or authenticated approval record, exact migration ID and hash, exact target fingerprint, operation value, issue and expiry times, both required approvers, and a unique nonce/approval ID. It must reject missing, malformed, expired, revoked, duplicated, or target/hash-mismatched approvals. A durable revocation/replay store and audit record are required; an environment variable or free-form string is not an approval.

If the verifier, trust anchor, revocation source, clock, or replay store is unavailable, the runner must fail closed before opening a database connection. No fallback to `.env.test.local`, a default connection string, caller-supplied text, or an unverified local setting is permitted.

**Classification:** new proposal requiring release/database-owner approval; authentication, revocation, replay, and audit behavior require implementation review and provider-independent verifier tests.

## Proposed trusted target identity contract

This is a new canonicalization proposal, not an observed target identity. The approved record should use a strict, ordered, semicolon-delimited format with ASCII keys and no secrets:

```text
v=1;provider=postgresql;host=<canonical-host>;port=<decimal-port>;database=<database>;schema=<schema>;server_version_major=<integer>;system_identifier=<provider-identity>;approved_target_id=<owner-issued-id>
```

The exact key order, allowed host representation, PostgreSQL version range, system-identifier source, and owner-issued target-ID namespace require approval. Values must be normalized before comparison; duplicate keys, unknown keys, empty values, whitespace variation, delimiter injection, invalid ports, invalid versions, and ambiguous host names must be rejected. Passwords, connection strings, tokens, and private key material must never appear in the fingerprint.

The trusted actual identity must be established by a verifier that controls the connection endpoint and reads the provider’s authenticated server/catalog identity, including the server identity/version and current database/schema. The expected value must come from the signed approval record or an independently controlled release record. A caller must not be allowed to supply both expected and purported actual values. A value returned solely from an environment variable, command argument, or untrusted connection-string descriptor is not evidence.

Comparison must occur before the migration transaction and before any migration command. Missing or inconsistent endpoint, catalog, version, schema, server identity, or approved-target data must abort. The verifier must record a redacted evidence bundle containing the canonical fingerprint, approval ID, timestamp, and verifier result without credentials.

**Classification:** new proposal requiring owner approval and provider-backed PostgreSQL verification. The current repository has no independently trusted target or live fingerprint evidence.

## Proposed PostgreSQL lock and transaction contract

The candidate and repository require one outer transaction and one migration worker, but the provider adapter is not yet implemented. A PostgreSQL implementation may use a transaction-scoped advisory lock derived from a reviewed, collision-resistant migration-026 lock name; the key derivation, collision analysis, privileges, and SQL shape require PostgreSQL owner approval. The runner must not claim this mechanism is available until the adapter and its tests exist.

The lock must be acquired after beginning the transaction and before executing the candidate. It must remain held through migration execution, assertions, and commit/rollback. It must be released by the provider’s transaction completion semantics on both commit and rollback; release failure or uncertain release must be reported as an operational failure and must not be treated as successful cleanup. A second worker must block or fail closed rather than execute concurrently.

The transaction sequence is:

1. Validate approval, canonical candidate path, exact bytes/hash, encoding, and trusted target identity.
2. Open the approved provider connection and begin one transaction.
3. Acquire and verify the migration-026 worker lock.
4. Execute only the retained candidate bytes through that transaction.
5. On a confirmed pre-commit error, attempt rollback and preserve both the migration and rollback outcomes.
6. If commit initiation returns a connection loss, cancellation, timeout, disposal error, or any other uncertain result, report `UNKNOWN_COMMIT`; do not retry or claim rollback.
7. Mark success only after a conclusive commit result and required postconditions.

Cancellation before execution should abort without a migration. Cancellation during execution should attempt rollback. Cancellation or process termination after commit initiation is inherently ambiguous unless the provider conclusively reports the outcome; it therefore remains `UNKNOWN_COMMIT`. Disposal exceptions must never convert an uncertain outcome into success.

**Classification:** transaction and rollback behavior are existing documented requirements; lock implementation, release guarantees, interruption behavior, and PostgreSQL semantics require provider-backed tests and owner approval.

## Recovery and execution-readiness contract

Before approval, the database and release owners must provide:

- A disposable, exact target identity and read-only preflight evidence covering endpoint, database, schema, user, PostgreSQL version, and server identity.
- A trustworthy deployed baseline or an explicitly approved adoption procedure. Object existence alone must not infer migration history.
- A target-consistent backup or snapshot, restore owner, tested restore path, recovery time objective, and forward-repair decision.
- Immutable hashes for the exact candidate and every prerequisite artifact.
- Preservation manifests and counts for users, roles, permissions, role-permission pairs, memberships, client/SLA references, tickets, exports, audits, reporting objects, sequences, constraints, and indexes.
- An explicit decision on existing reporting objects, sequence ownership/state, strict constraint equivalence, and whether adoption or empty-target creation is allowed.

Pre-execution approval checkpoints should be recorded separately for: target preflight, baseline/adoption, backup/restore readiness, migration execution, provider-backed tests, and post-execution verification. No provisioning, seeding, cleanup, sequence reset, unrelated migration, or migration-ledger backfill is included in the 026 operation.

Post-execution verification must prove the exact tables, columns, defaults, constraints, indexes, and sequences; stable pre-existing IDs and row counts; no unapproved rows or grants; and the candidate’s expected reporting objects. If any assertion is inconclusive, the release remains blocked and recovery follows the approved restore/forward-repair decision. Rollback must not delete data to simulate recovery after committed rows exist.

Independent review should include a release reviewer, a database owner, and a security reviewer who did not author the migration or runner. Each reviewer must record the evidence they checked and the approval ID; no reviewer may substitute a unit-test result for provider evidence.

**Classification:** existing repository requirements organized into an execution-readiness proposal; backup, baseline, preservation, restore, and provider evidence remain unresolved prerequisites.

## Current implementation boundary

The current runner enforces the canonical candidate path, exact retained bytes, strict UTF-8 decoding, injected policy and target-verification boundaries, mandatory lock acquisition, rollback diagnostics, and `UNKNOWN_COMMIT` state. Its executable entry point remains disabled because the authorization verifier and trusted target-fingerprint implementation are absent. The isolated tests use fakes only and do not prove PostgreSQL behavior.

This proposal does not approve migration 026, establish a target, provide credentials, define an authorization token, or authorize any database operation.
