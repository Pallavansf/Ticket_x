# Gate T9 — Migration 026 precise authorization and target contract (v2)

Status: **PROPOSED CONTRACT — OWNER DECISION REQUIRED**  
This version resolves serialization and validation ambiguities identified by Gate T8. It remains a proposal: no key, approver, endpoint, recovery record, or provider capability is approved or present in the repository.

## Fixed scope and evidence

The contract applies only to migration ID `026` and the canonical candidate `database/migrations/026_phase8_6_reporting.review.v3.sql`. Independently calculated hashes are:

| File | SHA-256 |
|---|---|
| `database/migrations/026_phase8_6_reporting.sql` | `791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4` |
| `database/migrations/026_phase8_6_reporting.review.v3.sql` | `97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49` |

The existing runner must remain fail-closed until the contracts below are approved and implemented. This document does not authorize SQL, a connection, or execution.

## A. Signed authorization envelope

### Proposed envelope and payload

The proposed envelope is UTF-8 JSON with exactly these top-level fields: `schema`, `payload`, and `signatures`. No top-level field may be null; unknown or duplicate fields are rejected.

`payload` is an object with exactly these fields and types:

| Field | Type and constraints |
|---|---|
| `schema` | string, exact value `pa-tickets.migration-approval.v1` |
| `operation` | string, exact value `APPLY_MIGRATION_026` |
| `migrationId` | string, exact ASCII value `026` |
| `candidateSha256` | string, exactly 64 uppercase hexadecimal characters |
| `targetFingerprint` | string, canonical target record defined in section B, 1–4096 UTF-8 bytes |
| `approvalId` | lowercase UUIDv4 string, 36 characters |
| `nonce` | base64url without padding, 32 decoded random bytes |
| `issuedAtUtc` | RFC 3339 UTC string with `Z`, seconds and exactly three fractional digits |
| `expiresAtUtc` | same format; strictly later than `issuedAtUtc` |
| `releaseApproverKeyId` | ASCII registry identifier, 1–128 characters |
| `databaseApproverKeyId` | ASCII registry identifier, 1–128 characters; distinct from release key ID |
| `payloadVersion` | string, exact value `1` |

Nulls, empty strings, whitespace padding, control characters, non-ASCII field names, numeric coercion, alternate timestamp forms, and duplicate fields are rejected. The verifier requires `expiresAtUtc - issuedAtUtc` to be within the owner-approved maximum validity window. The maximum window is an owner decision; a deployment must not silently choose one.

The signed bytes are the UTF-8 bytes of the RFC 8785 JCS serialization of the `payload` object, with no BOM and no trailing bytes. The JCS implementation and version must be pinned and recorded in verifier evidence. The payload digest is SHA-256 over those exact signed bytes, represented as 64 uppercase hexadecimal characters.

Each `signatures` entry has exactly: `algorithm`, `keyId`, `payloadDigestSha256`, and `signature`. `algorithm` must be `Ed25519`; `keyId` must equal the corresponding payload approver key ID; `payloadDigestSha256` must equal the digest of the signed JCS bytes; and `signature` must be standard base64url without padding encoding exactly 64 Ed25519 signature bytes. Unknown algorithms, alternate encodings, padding, line breaks, digest mismatches, duplicate key IDs, or signatures not covering the exact payload fail closed. The signatures array must contain exactly two entries, one for each required role.

Ed25519, JCS, registry trust anchors, key-ID grammar, maximum validity window, and emergency process are proposed choices requiring security, release, and database-owner approval. No key or approver identity is supplied here.

### Authority, distinctness, and registry

The release authority approves the artifact; the database authority approves the target and operation. The organization must define eligibility and delegation. Delegates may sign only when the registry records a non-expired delegation from the role owner; delegation chains, self-delegation, shared accounts, and one effective principal occupying both roles are rejected.

The organization-owned key registry is the source of truth for key material, role, subject, algorithm, activation interval, revocation state, and rotation predecessor. Trust-anchor distribution, registry ownership, cache freshness, and key-rotation retention require owner decisions. A registry, trust anchor, trusted clock, revocation source, replay store, or audit sink that is unavailable or stale denies execution before opening a database connection.

`approvalId` and `nonce` are consumed in one atomic operation keyed by `(approvalId, nonce, migrationId, candidateSha256, targetFingerprint)`. A prior consumption with any changed field is a replay or tampering failure. Consumption must be durable before the execution boundary is crossed; whether a failed preflight releases or permanently consumes the approval is an owner decision and must be explicit.

The redacted audit record contains approval ID, payload digest, migration/hash/target, key IDs, registry version, verification time, result, failure code, and verifier version. It never stores signatures, private material, credentials, or complete connection strings. Audit failure denies execution.

### Authorization acceptance criteria

Provider-independent tests must prove exact-byte JCS signing, schema/type/null rejection, digest and signature encoding rejection, distinct-role enforcement, delegation expiry, key rotation and revocation, clock failure, registry failure, replay races, atomic nonce consumption, audit failure, expiry, future issuance, validity-window limits, and secret-free diagnostics. Independent security review must inspect the canonicalization library, Ed25519 implementation, registry trust path, replay atomicity, and audit failure behavior.

## B. Trusted target identity

### Deterministic canonical record

The proposed record is an ordered ASCII key/value sequence with exactly these fields:

```text
v=1;provider=postgresql;endpoint_host=<host>;endpoint_port=<port>;database=<database>;schema=<schema>;server_version_major=<major>;system_identifier=<identifier>;approved_target_id=<target-id>
```

Values use UTF-8 percent-encoding for `%`, `;`, `=`, controls, and non-ASCII bytes. Keys and order are fixed; unknown, duplicate, missing, empty, oversized, or malformed values are rejected. The complete encoded record is limited to 4096 bytes.

`endpoint_host` is either a lowercase IDNA A-label DNS name with exactly one approved trailing-dot policy, or a normalized textual IP. IPv4 uses dotted decimal; IPv6 uses the approved compressed parser; IPv4-mapped IPv6 is not equivalent to IPv4. `endpoint_port` is decimal `1–65535` with no defaulting. `database` and `schema` are PostgreSQL identifiers normalized by the approved identifier grammar and are case-sensitive after normalization. `server_version_major` is decimal and must be within the owner-approved supported range. `system_identifier` and `approved_target_id` have owner-approved grammars and maximum lengths; neither contains secrets.

The DNS alias set, trailing-dot policy, supported version range, system-identifier source, target-ID registry, and endpoint address policy are owner decisions. No target is approved by this document.

### Expected versus actual identity

The expected record comes only from the signed authorization payload and the owner-controlled target registry. The caller cannot provide both expected and actual identity values. The verifier records the connection endpoint it selected, DNS resolution and freshness evidence, TLS certificate/SNI identity where TLS is required, and the authenticated PostgreSQL server evidence separately.

The provider adapter must obtain current database/schema, server major version, and an authenticated server system identity through an approved PostgreSQL mechanism. A `current_database()` value or connection-string descriptor alone is insufficient. The adapter must compare endpoint host/port, resolved address policy, TLS/SNI identity, database, schema, version, system identity, and approved target ID. Redirection, unexpected proxying, stale DNS, certificate mismatch, changed port/database/schema, unavailable system identity, connection-pool reuse with a different target, or any disagreement aborts before transaction creation.

Provider-backed acceptance must use a separately approved disposable PostgreSQL target and prove endpoint binding, DNS/IPv4/IPv6 behavior, TLS identity, server identity/version, database/schema comparison, pool reuse handling, stale registry handling, and failure before migration execution. No such evidence currently exists.

## C. PostgreSQL single-worker lock contract

No mechanism is selected by this proposal. The database owner must choose one of these candidates and document the decision:

| Candidate | Benefit | Risk/decision required |
|---|---|---|
| Transaction-scoped advisory lock | Releases automatically with the transaction; no persistent row | Collision-free key derivation, privilege, timeout, and provider semantics must be proven |
| Dedicated lock row with a unique target/migration key | Observable ownership and audit trail | Requires schema, stale-owner recovery, and cleanup policy |
| Session advisory lock | Survives transaction boundaries | Higher risk of leaked locks after broken sessions; harder release verification |

The approved mechanism must derive scope from the canonical target identity and migration ID `026`, with a documented collision analysis. Acquisition must have an explicit timeout; contention, timeout, unavailable privilege, and lost connection deny execution. Ownership must be bound to the same connection and transaction that executes the candidate. The lock remains held through assertions and commit/rollback. Release must be proven on commit, rollback, cancellation, connection loss, and process termination; stale ownership must not be force-cleared without an approved recovery procedure.

Provider-backed concurrency tests must show that two workers cannot execute concurrently, that lock contention fails safely, that a broken process cannot leave a false success state, and that a subsequent approved run can recover only after the provider confirms lock release. Unit-test fakes cannot establish these properties.

## D. Replay, audit, recovery, and evidence

The replay store must expose an atomic compare-and-insert operation and retain the approval ID, nonce, binding tuple, first-seen time, verifier version, and result. Retention, encryption, access control, deletion, and audit export are compliance-owner decisions. A store outage or write ambiguity denies execution.

The execution evidence bundle must contain the signed envelope digest, exact candidate hash, canonical target record, preflight timestamp, baseline/adoption decision, backup identifier, reviewer approvals, lock result, transaction outcome, postcondition result, and any `UNKNOWN_COMMIT` reconciliation. It must exclude credentials and private key material. Evidence files require integrity hashes, immutable storage, named custody owner, retention period, and access audit; these values require owner approval.

Baseline and preservation manifests must be deterministic, versioned, and hashed. They must enumerate reporting tables, columns/defaults, constraints, indexes, sequences/ownership/state, existing export rows, and the repository-required user/role/permission/membership/client/SLA/ticket/audit counts. Comparison tolerances must be zero for identity, key, constraint, sequence, and row-preservation fields unless an owner-approved exception is recorded.

Recovery acceptance requires a restore rehearsal, a documented restore owner and decision time, pre/post manifest comparison, and an explicit path for confirmed pre-commit rollback versus `UNKNOWN_COMMIT`. After an uncertain commit, no blind retry or destructive cleanup is allowed; a read-only reconciliation and approved forward-repair or restore decision are required.

## Decision register and dependency order

| Decision | Proposed default | Alternatives/trade-off | Accountable role | Blocks |
|---|---|---|---|---|
| Signature/envelope | Ed25519, JCS, base64url-no-padding | Other approved modern signature profile; interoperability versus complexity | Security/key-registry owner | Verifier implementation |
| Validity window | Owner-set bounded lifetime | Shorter reduces replay; longer improves scheduling | Release/security owners | Approval issuance |
| Replay consumption | Durable atomic consume before connection | Consume after preflight risks races; before preflight may burn approvals | Security/compliance owner | Verifier implementation |
| Host/DNS policy | One canonical A-label/IP parser and explicit port | Alias set or pinned addresses; flexibility versus substitution risk | Platform/database owner | Target verifier |
| Server identity | Authenticated provider system identity | Alternative provider-supported identity; availability versus assurance | PostgreSQL owner | Provider verifier |
| Lock mechanism | Database-owner-selected candidate above | Advisory versus row lock trade-offs | Database owner | Lock adapter |
| Evidence custody | Hashed immutable store with access audit | Central compliance archive; cost versus availability | Release/compliance owner | Execution approval |

The dependency order is: approve governance and cryptographic profile; approve target registry and provider identity source; select and approve the lock; implement verifier and adapters; run provider-independent tests; run provider-backed identity/lock/transaction tests; complete baseline, backup, preservation, and restore evidence; obtain independent review; then issue separate migration execution approval.

All rows marked owner decision remain unresolved. The runner must remain fail-closed until every implementation and provider-evidence gate is closed.
