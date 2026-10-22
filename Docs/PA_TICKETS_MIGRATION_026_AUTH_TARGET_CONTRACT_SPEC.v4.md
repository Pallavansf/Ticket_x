# Gate T13 — Migration 026 Signature Envelope and Authentication Boundary v4

Status: **PROPOSED CONTRACT — SECURITY APPROVAL REQUIRED**

This document resolves the Gate T12 C1 and C2 documentation gaps. It does not implement a verifier, create keys, identify approvers, select trust anchors, connect to PostgreSQL, or authorize migration 026.

## 1. Scope and governing status

The contract applies only to migration ID 026, operation APPLY_MIGRATION_026, and candidate SHA-256:

97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49

The original migration hash remains:

791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4

The envelope schema discriminator is pa-tickets.migration-approval-envelope.v1. The payload schema discriminator is pa-tickets.migration-approval.v1. Any other schema, version, migration, operation, or candidate hash is unsupported and rejected. There is no forward-compatibility mode: a future schema requires a separately approved contract.

## 2. Exact envelope schema

The envelope is a single UTF-8 JSON object. It must contain exactly these three properties, each exactly once:

| Property | Type | Required and limits |
|---|---|---|
| schema | string | Required; exact value pa-tickets.migration-approval-envelope.v1; ASCII; maximum 64 bytes |
| payload | object | Required; maximum 8192 UTF-8 bytes when JCS serialized |
| signatures | array | Required; exactly two entries; maximum 16 KiB serialized |

Unknown properties, duplicate JSON property names, nulls, numbers, booleans, arrays in place of objects, trailing JSON, invalid UTF-8, and input beyond owner-approved parser limits are rejected before signature verification. JSON member ordering is irrelevant to parsing; it has no semantic effect.

Each signature entry is an object with exactly these five properties:

| Property | Type | Required and limits |
|---|---|---|
| role | string | Required; exactly release or database |
| keyId | string | Required; ASCII registry identifier, 1–128 bytes |
| algorithm | string | Required; exact value Ed25519 |
| payloadDigestSha256 | string | Required; exactly 64 uppercase hexadecimal characters |
| signature | string | Required; unpadded base64url decoding to exactly 64 bytes |

Unknown or duplicate entry properties, nulls, alternate algorithms, standard-base64 padding, whitespace, line breaks, malformed key IDs, or invalid signature lengths are rejected.

The two entries must contain exactly one role release and exactly one role database. Their keyId values must be distinct. The registry lookup for each keyId must report the same role as the entry role, the corresponding payload field, and a distinct effective principal. Duplicate principals, shared accounts, self-approval, a key registered for both roles, revoked keys, expired keys, and unauthorized delegation are rejected.

The payload object must contain exactly these twelve properties:

| Property | Type and validation |
|---|---|
| schema | Exact string pa-tickets.migration-approval.v1 |
| operation | Exact string APPLY_MIGRATION_026 |
| migrationId | Exact ASCII string 026 |
| candidateSha256 | Exactly 64 uppercase hexadecimal characters |
| targetFingerprint | Canonical target record, 1–4096 UTF-8 bytes |
| approvalId | Lowercase canonical UUIDv4, 36 characters |
| nonce | Unpadded base64url decoding to exactly 32 bytes |
| issuedAt | RFC3339 UTC with Z and exactly three fractional digits |
| expiresAt | Same format; strictly later than issuedAt |
| releaseApproverKeyId | ASCII registry identifier, 1–128 bytes |
| databaseApproverKeyId | ASCII registry identifier, 1–128 bytes; distinct from release key |
| payloadVersion | Exact string 1 |

No payload property may be null, empty, whitespace-padded, control-containing, or coerced from another JSON type. The verifier must reject duplicate names even if a JSON parser would otherwise retain the last value.

## 3. Exact authenticated representation

### 3.1 Signed projection

Only the payload object is signed. The exact signed message is:

1. Parse the envelope under §2.
2. Validate every payload field and reject unknown/duplicate members.
3. Require payload.schema, operation, migrationId, candidateSha256, and payloadVersion to equal the fixed values above.
4. Serialize the validated payload object using RFC 8785 JSON Canonicalization Scheme (JCS), with the approved implementation and version recorded in verifier evidence.
5. Encode the JCS result as UTF-8 without BOM or trailing bytes.
6. Hash those exact bytes with SHA-256.
7. Verify both Ed25519 signatures over the 32 raw digest bytes.

The digest is represented as uppercase hexadecimal in each entry. Both entries must contain the same digest, equal to the computed digest. Signatures do not cover the raw envelope byte ordering or signature-array ordering.

The envelope is linked to the signed payload by strict structural validation: the only envelope values that can affect execution are schema, the validated payload, and the two signature entries. Top-level schema must equal the fixed discriminator. Payload values used by execution are taken only from the verified signed payload; no caller-supplied payload, hash, target, expiry, role, or digest may override it. The role/key relationship is authenticated by the signed payload key IDs plus the independent registry lookup and by each signature entry's explicit role.

Changing JSON member order alone must not change acceptance. Changing any payload byte, field value, digest, signature entry role, key ID, algorithm, or signature must cause rejection. A replacement payload with copied signatures fails because its JCS digest changes.

### 3.2 Validation order and fail-closed behavior

Validation order is mandatory:

1. Bound input size and decode strict UTF-8.
2. Parse one JSON value and reject duplicate names, unknown names, trailing data, and invalid types.
3. Validate envelope and payload schema discriminators and fixed scope.
4. Validate field encodings, timestamps, target syntax, key IDs, and distinct-role structure.
5. Canonicalize the payload with JCS and compute the digest.
6. Compare both declared digests to the computed digest.
7. Load the approved key registry, trust anchors, revocation state, delegation state, and trusted time.
8. Verify both Ed25519 signatures and role/principal bindings.
9. Validate expiry, validity window, and replay state.
10. Only after all checks succeed may the future runner proceed to target connection and identity verification.

Any failure, unavailable registry, unavailable revocation source, stale trust data, unavailable trusted time, ambiguous replay result, or audit-write failure rejects before opening a database connection.

### 3.3 Owner decisions

**OWNER DECISION REQUIRED:** security ownership of the key registry and trust anchors; approved JCS implementation/version; Ed25519 library/version; registry freshness and revocation service; approval lifetime and clock-skew allowance; delegation governance; replay-consumption behavior after failed preflight; and emergency/revocation procedures. This proposal supplies no keys, identities, approvals, or trust anchors.

## 4. Canonical illustrative example

The following values are fictitious, non-operational, and intentionally do not identify a real target or key. The shown payload is already in the exact compact JCS form because all names and values are simple ASCII strings and the properties are in canonical lexicographic order.

Exact canonical payload UTF-8 text and bytes:

{"approvalId":"00000000-0000-4000-8000-000000000001","candidateSha256":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","databaseApproverKeyId":"db-example-key","expiresAt":"2030-01-01T01:00:00.000Z","issuedAt":"2030-01-01T00:00:00.000Z","migrationId":"026","nonce":"AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE","operation":"APPLY_MIGRATION_026","payloadVersion":"1","releaseApproverKeyId":"release-example-key","schema":"pa-tickets.migration-approval.v1","targetFingerprint":"v=1;provider=postgresql;endpoint_host=example.invalid;endpoint_port=5432;database=illustrative;schema=public;server_version_major=16;system_identifier=example-system;approved_target_id=example-target"}

The SHA-256 digest of exactly those UTF-8 bytes is:

6B0D663CF30F1238181DD4669230C5542DBE6D7FB033FEAE3AA9998DD2744BAF

Illustrative envelope shape:

{
  "schema": "pa-tickets.migration-approval-envelope.v1",
  "payload": { "the exact payload object above" },
  "signatures": [
    {
      "role": "release",
      "keyId": "release-example-key",
      "algorithm": "Ed25519",
      "payloadDigestSha256": "6B0D663CF30F1238181DD4669230C5542DBE6D7FB033FEAE3AA9998DD2744BAF",
      "signature": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    },
    {
      "role": "database",
      "keyId": "db-example-key",
      "algorithm": "Ed25519",
      "payloadDigestSha256": "6B0D663CF30F1238181DD4669230C5542DBE6D7FB033FEAE3AA9998DD2744BAF",
      "signature": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    }
  ]
}

The all-zero illustrative signature strings are correctly shaped unpadded base64url for 64 bytes but are not valid signatures and must not verify. No fictional signature, key, approver, or approval is claimed.

## 5. Acceptance criteria and evidence

| Property | Positive criterion | Negative criterion | Required evidence |
|---|---|---|---|
| Deterministic parsing | Same object with any member ordering produces same validated payload | Unknown/duplicate member, trailing data, invalid UTF-8/type, or oversized input rejects | Parser conformance vectors and duplicate-name tests |
| Canonicalization | Exact payload produces the documented JCS bytes and digest | Any changed byte or non-canonical numeric/string representation changes digest or rejects | JCS test vectors and independent digest calculation |
| Role binding | Exactly one release and database entry maps to matching registry roles | Role substitution, duplicate role/key/principal, shared principal, or unauthorized delegation rejects | Registry fixtures and security review |
| Signature validation | Two approved Ed25519 signatures over the computed digest verify | Altered signature, algorithm, key, digest, or payload rejects | Independent cryptographic test vectors |
| Envelope consistency | Fixed envelope schema and payload scope are accepted only when payload is verified | Top-level schema mismatch, detached payload/hash/target, or payload substitution rejects | Envelope mutation tests |
| Time and expiry | Valid trusted time and bounded issued/expires interval pass | Expired, future-invalid, excessive-window, unavailable-clock, or revoked-key input rejects | Boundary, clock-failure, revocation tests |
| Unsupported schema | Exact v1 discriminators are accepted | Any other schema/version is rejected without compatibility fallback | Version-negotiation tests |
| Replay/audit | Atomic consume and audit complete before connection | Replay collision, ambiguous store result, unavailable store, or audit failure rejects | Isolation/crash/outage evidence |

## 6. Current runner boundary

The current Runner.cs still accepts an opaque Authorization value and caller-supplied ApprovedTargetFingerprint (lines 10, 69–78), delegates authorization and actual identity to interfaces (lines 13–23), and has no envelope parser, key registry, replay store, audit sink, or concrete provider verifier. Its candidate hash/path, strict UTF-8, lock invocation, rollback, and UNKNOWN_COMMIT behavior remain as previously documented. Program.cs remains disabled through NoExecutionPolicy.

This proposal defines the contract needed for a future verifier design; it does not claim that the runner implements it.

## 7. Decision register

| Decision | Proposed default | Alternative/trade-off | Owner role | Required evidence | Blocking stage |
|---|---|---|---|---|---|
| Authenticated projection | JCS payload only; envelope linked by strict structural validation | Sign whole envelope; tighter coupling but different interoperability | Security/key-registry owner | Canonical and mutation vectors | Specification/verifier |
| Signature profile | Ed25519 over raw SHA-256 digest bytes | Sign JCS payload directly; different protocol profile | Security owner | Independent cryptographic vectors | Specification/verifier |
| Registry/trust anchors | Organization-owned registry with pinned trust anchors | External approval service | Security/key-registry owner | Rotation, revocation, outage tests | Verifier |
| Delegation | Explicit, scoped, non-expired registry record | No delegation | Security/release owners | Delegation and separation tests | Verifier |
| Approval lifetime/clock | Bounded owner-set lifetime and trusted UTC | Shorter/longer window | Security/release owners | Time-boundary evidence | Approval issuance |
| Replay after failed preflight | Explicit owner-selected consume or release policy | Opposite trade-off | Security/compliance owner | Crash and replay-race evidence | Verifier |
| Parser limits | Conservative bounded limits approved by security | Larger limits increase resource risk | Security owner | Resource-exhaustion tests | Verifier |

## 8. Status and restrictions

This is a documentation proposal only. No implementation, provider test, database access, SQL execution, migration execution, provisioning, seeding, cleanup, sequence reset, or approval has occurred. Security governance must approve the decisions above before implementation design is treated as an approved protocol. Database-owner lock selection, trusted PostgreSQL identity policy, provider evidence, recovery evidence, and separate migration execution approval remain independent gates.

**GATE T13 COMPLETE — SIGNATURE CONTRACT PROPOSED, SECURITY APPROVAL REQUIRED**

