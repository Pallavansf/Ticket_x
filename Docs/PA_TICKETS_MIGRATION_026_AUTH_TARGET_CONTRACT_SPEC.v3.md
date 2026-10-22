# Gate T11 — Migration 026 Protocol Decision Proposal v3

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED**

This documentation-only proposal closes the material gaps reported by Gate T10. It does not implement a verifier, select a production lock, establish a trusted target, authorize a database connection, or authorize migration 026.

## Scope and evidence boundary

Scope is PostgreSQL migration 026 using only:

- Candidate: database/migrations/026_phase8_6_reporting.review.v3.sql
- Candidate SHA-256: 97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49
- Migration ID: 026
- Operation: APPLY_MIGRATION_026

The original migration SHA-256 is 791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4 and is not an executable candidate. Existing requirements for exact hashes, target verification, baseline/adoption, backup/restore, preservation manifests, postconditions, separate approval, one outer transaction, rollback, and UNKNOWN_COMMIT remain in force.

## Gate T10 finding-to-decision traceability

| Finding | Proposed closure | Status |
|---|---|---|
| Envelope/signing boundary ambiguous | §1 defines exact envelope, payload, JCS bytes, digest, signature entries, and states that execution-affecting values must be in the signed payload. | Proposal; security approval required |
| Signature roles and delegation ambiguous | §1.3 binds keys to release/database roles through a registry and requires distinct principals. | Proposal; registry and tests required |
| Target identity source and network policy unselected | §2 separates signed expected identity, configured endpoint, and observed authenticated identity; candidate mechanisms are compared. | Owner decision required |
| DNS, IP, TLS/SNI, aliases, redirection unclear | §2.2–2.3 defines normalization candidates, fail-closed behavior, and provider evidence. | Owner decision/provider evidence required |
| Lock and key derivation unselected | §3 compares advisory-lock and row-lock designs without selecting production. | Database-owner decision required |
| Replay, audit, evidence, recovery incomplete | §4 defines state transitions, required evidence, and ambiguous-failure behavior without inventing retention values. | Compliance/release decisions required |
| Interfaces mistaken for implementation | §5 records missing concrete verifier, target, lock, replay, audit, and recovery adapters. | Implementation remains blocked |

## 1. Signing boundary

### 1.1 Envelope and payload

The UTF-8 JSON envelope has exactly schema, payload, and signatures members. Schema is the literal pa-tickets.migration-approval-envelope.v1. Payload is an object. Signatures is an array of exactly two objects, one release and one database; array order is irrelevant after validation. Unknown or duplicate names, nulls, control characters, trailing data, floating-point values, and oversized input are rejected.

The payload has exactly: schema (pa-tickets.migration-approval.v1), operation (APPLY_MIGRATION_026), migrationId (026), candidateSha256 (64 uppercase hexadecimal characters), targetFingerprint (canonical target record), approvalId (lowercase UUIDv4), nonce (unpadded base64url decoding to 32 bytes), issuedAt and expiresAt (UTC RFC3339 with Z and exactly three fractional digits), releaseApproverKeyId, databaseApproverKeyId, and payloadVersion (1). Missing, empty, whitespace-only, malformed, duplicate, unknown, or coerced values deny.

### 1.2 Signed bytes

The signed message is exactly the UTF-8 bytes of RFC 8785 JCS applied to the validated payload object, with no BOM or trailing bytes. SHA-256 of those bytes is represented as 64 uppercase hexadecimal characters and must match payloadDigestSha256 in both signature entries. Each Ed25519 signature is over the 32 raw digest bytes and is unpadded base64url decoding to exactly 64 bytes.

The envelope is not a second signed message. Its execution-affecting metadata must be derivable from the signed payload; any conflicting top-level value, detached caller payload, or separately supplied digest denies. This is a proposed security profile and requires owner approval.

### 1.3 Roles, keys, delegation, and time

Each key ID resolves in an approved registry to one role, effective principal, public key, validity interval, and revocation state. Signature role must match the registry role and payload key ID. Release and database keys must be different and resolve to distinct principals. Shared principals, self-approval, one key serving both roles, revoked keys, cycles, and expired delegation deny. Delegation is valid only through a non-expired registry record scoped to this operation.

The maximum approval lifetime and clock-skew policy are owner decisions. Missing or stale trust registry, revocation data, or trusted time denies. Required negative tests cover altered payload/metadata, duplicate or unknown fields, duplicate roles, role substitution, invalid encodings, digest mismatch, expiry, revocation, missing delegation, and unavailable trust sources. Every negative case must cause no connection attempt.

Replay key: (approvalId, nonce, migrationId, candidateSha256, targetFingerprint). Atomic compare-and-insert occurs before opening a database connection. Duplicate, conflicting, unavailable, or ambiguous replay results deny. Whether failed preflight consumes the approval permanently is a security/compliance decision.

## 2. Trusted PostgreSQL target identity

### 2.1 Separate values

The signed expected fingerprint comes only from the verified approval. The configured connection endpoint is untrusted input subject to policy. The observed actual identity is constructed by the verifier from the authenticated PostgreSQL connection and approved network/TLS controls. A caller cannot supply both expected and purported actual identity.

### 2.2 Canonical record

Proposed fixed serialization:

v=1;provider=postgresql;endpoint_host=<value>;endpoint_port=<decimal>;database=<value>;schema=<value>;server_version_major=<integer>;system_identifier=<value>;approved_target_id=<value>

One approved UTF-8 percent-encoding profile, normalization order, host trailing-dot rule, identifier grammar, maximum lengths, supported version range, system-identifier grammar, and approved-target-ID namespace must be selected by owners. Unknown, missing, duplicate, empty, malformed, overlong, or ambiguously normalized fields deny.

### 2.3 Candidate mechanisms

| Candidate | Prerequisites and trust assumptions | Failure and evidence |
|---|---|---|
| Provider-authenticated system/catalog identity | Approved driver and least-privilege identity queries; authenticated connection is to intended server | Missing/inconsistent identity denies; PostgreSQL tests required |
| TLS certificate and SNI binding | Mandatory TLS, approved CA/pinning, SNI and hostname policy | Chain, SNI, hostname, expiry, rotation, or redirect mismatch denies; TLS tests required |
| Canonical DNS/IP policy | A-label and address policy, resolver freshness, IPv4/IPv6 rules | Alias ambiguity, stale DNS, changed address, or mapped-address mismatch denies; DNS tests required |
| Approved proxy/pool path | Explicit proxy identity and end-to-end backend identity propagation | Missing propagation or changed backend denies; proxy/pool/reconnect tests required |

No mechanism is selected here. TLS requirement, SNI, DNS aliases, address pinning, proxy allowance, system-identity source, and target registry are owner decisions. DNS name, connection string, current_database alone, or caller fingerprint alone is insufficient. Comparison occurs before BEGIN and before candidate execution.

## 3. Lock and execution recovery

Candidate designs are transaction-scoped advisory lock, dedicated lock row, and session advisory lock. The database owner must select one and approve its provider contract. No production mechanism may be claimed before that decision.

Generic requirements: scope is canonical target plus migration 026; key derivation is versioned, deterministic, bounded, and collision-reviewed; acquisition has an explicit timeout; contention, timeout, privilege failure, lost connection, cancellation, or uncertain ownership denies. The same physical connection and transaction hold the lock through execution, assertions, postconditions, commit, or rollback. Release must be proven after commit, rollback, cancellation, disposal, connection loss, and process termination. Stale ownership is never force-cleared without an approved recovery procedure.

Provider tests must prove mutual exclusion, timeout, privileges, crash/process termination, pooled-connection reuse, release after both outcomes, and safe subsequent recovery. Fakes cannot prove these properties.

Transaction sequence: validate approval/hash/identity; connect and begin; acquire lock; execute retained bytes; on confirmed pre-commit failure attempt rollback while preserving both outcomes; on uncertain commit, cancellation, timeout, disposal, or connection loss report UNKNOWN_COMMIT; never retry automatically; reconcile independently before forward repair or restore. No destructive cleanup or sequence reset is permitted.

## 4. Replay, audit, and evidence durability

Replay storage atomically records the binding tuple, first-seen time, verifier version, result, and audit reference. Before-write failure, write failure, response loss, outage, or ambiguous result denies unless durable prior state is conclusively known. Permanent consumption versus release on failed preflight remains an owner decision.

Audit is mandatory for approval, identity, replay, lock, transaction, and recovery events. Audit outage, ambiguous persistence, incomplete records, credentials, private keys, connection strings, or secrets deny. Evidence includes signed-envelope digest, approval identifiers, exact candidate hash/path, expected and observed identity, endpoint/TLS evidence, baseline/adoption decision, deterministic manifests, backup identifier, restore rehearsal, reviewers, lock result, transaction outcome, postconditions, and UNKNOWN_COMMIT reconciliation.

Artifacts require integrity hashes, approved immutable storage, custody owner, access control, retention, deletion/export rules, and access audit. No duration or storage guarantee is invented; compliance/release owners must decide them. Missing, incomplete, interrupted, or unverifiable evidence blocks execution. Manifest comparisons are deterministic, versioned, hashed, and zero-tolerance for identity, key, constraint, sequence, and row-preservation fields unless an approved exception is recorded.

## 5. Decision and acceptance register

| ID | Exact question | Safe proposed default | Owner role | Evidence | Blocking stage | Negative criterion |
|---|---|---|---|---|---|---|
| T11-A1 | What bytes are signed? | JCS payload digest with detached Ed25519 | Security/key-registry | Canonical test vectors | Specification/verifier | Execution-affecting unsigned metadata denies |
| T11-A2 | Which keys/roles are trusted? | Registry with distinct release/database principals | Security/key-registry | Rotation/revocation/delegation tests | Verifier | Missing/stale/revoked registry denies |
| T11-A3 | Lifetime and clock? | Bounded owner-set lifetime and trusted UTC | Security/release | Boundary and clock-failure tests | Approval issuance | Unavailable/ambiguous time denies |
| T11-A4 | Replay timing? | Atomic consume before connection | Security/compliance | Isolation/crash tests | Verifier | Duplicate or ambiguous consume denies |
| T11-A5 | Target grammar? | Fixed ordered record and one encoding profile | Platform/database | Parser/normalization tests | Target adapter | Any ambiguity or duplicate field denies |
| T11-A6 | Actual identity source? | Provider identity plus approved TLS/network policy | PostgreSQL/platform | Identity, TLS, DNS, redirect tests | Target adapter/provider | Missing actual identity denies |
| T11-A7 | Production lock? | Owner-selected candidate in §3 | Database | Concurrency/crash/release tests | Lock adapter/provider | Contention or uncertain release denies |
| T11-A8 | Evidence custody/retention? | Approved audited immutable store | Release/compliance | Outage/access/restore evidence | Execution approval | Missing/incomplete evidence denies |
| T11-A9 | Unknown commit recovery? | Reconcile before restore/forward repair | Database/release | Reconciliation and restore rehearsal | Provider/execution | Retry or cleanup without state proof denies |

## 6. Actual implementation boundary and readiness

Runner.cs currently provides candidate path checks, exact retained-byte hashing, strict UTF-8 decoding, injected policy/identity boundaries, lock invocation, rollback diagnostics, and UNKNOWN_COMMIT state. Program.cs remains disabled through NoExecutionPolicy. The repository has no concrete envelope verifier, registry, replay store, PostgreSQL identity/TLS adapter, selected provider lock, durable audit/evidence store, backup/baseline/restore adapter, or postcondition adapter.

This proposal establishes documentation completeness only. It does not establish implementation correctness, provider behavior, execution readiness, or migration authorization. Implementation is blocked by owner decisions T11-A1 through T11-A9 and the provider-backed evidence listed above. Anything requiring a trusted external registry, actual server identity, provider lock semantics, storage guarantees, retention, or recovery evidence cannot safely be resolved by documentation alone.

**GATE T11 COMPLETE — DECISION PROPOSAL PREPARED, OWNER APPROVALS REQUIRED**

