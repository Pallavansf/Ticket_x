# Gate T7 — Migration 026 authorization and target-identity contract specification

Status: **PROPOSED TECHNICAL SPECIFICATION — APPROVAL REQUIRED**  
Scope: the PostgreSQL migration-026 v3 candidate only. This document does not approve an authority, provide credentials or keys, identify a live target, implement a verifier, or authorize any database operation.

## Existing evidence and fixed artifact

The repository documents these requirements already: exact immutable migration hashes; an independently verified target fingerprint; a known baseline or approved adoption procedure; backup, restore, and preservation evidence; separate approval for schema execution; one transaction; rollback on confirmed pre-commit failure; and `UNKNOWN_COMMIT` for an uncertain commit. The current runner keeps execution disabled because no approved authorization verifier, trusted target verifier, or provider adapter exists.

The artifacts for this gate are fixed by evidence, not by this proposal:

| Artifact | SHA-256 |
|---|---|
| `database/migrations/026_phase8_6_reporting.sql` | `791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4` |
| `database/migrations/026_phase8_6_reporting.review.v3.sql` | `97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49` |

## Authorization contract

### Proposed authority and distinct approvals

The proposed approval authority is two independent roles:

1. A release authority approves the exact migration artifact and release context.
2. A database authority approves the exact target and the `APPLY_MIGRATION_026` operation.

The organization must define the role names, eligibility, delegation limits, separation-of-duty rules, and emergency process. No person or account is assigned by this document. The two approval subjects must have distinct, currently authorized identities; a single identity, shared account, self-delegation, or approval chain with the same effective principal is rejected.

Each identity must be authenticated by an organization-owned identity provider or signing-key registry. The registry owner must publish the approved key IDs, subject-to-role mapping, validity interval, revocation status, and rotation history. The repository currently contains none of these records; they are organizational prerequisites.

### Signed payload and canonical serialization

The proposed wire format is a detached Ed25519 signature over an RFC 8785 JSON Canonicalization Scheme (JCS) UTF-8 payload. Ed25519, the JCS profile, signature envelope, and key-registry trust anchor require security-owner approval before implementation. The payload contains exactly these fields and no secrets:

```json
{
  "schema": "pa-tickets.migration-approval.v1",
  "operation": "APPLY_MIGRATION_026",
  "migrationId": "026",
  "candidateSha256": "97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49",
  "targetFingerprint": "<canonical target value>",
  "approvalId": "<globally unique identifier>",
  "nonce": "<globally unique, never reused value>",
  "issuedAtUtc": "<RFC3339 UTC instant>",
  "expiresAtUtc": "<RFC3339 UTC instant>",
  "releaseApproverKeyId": "<registry key id>",
  "databaseApproverKeyId": "<registry key id>"
}
```

The verifier must canonicalize with JCS, reject additional or missing fields, require uppercase hexadecimal for the fixed SHA-256 value, require UTC timestamps with explicit seconds and `Z`, require `issuedAtUtc < expiresAtUtc`, and reject control characters or ambiguous values. The detached signature envelope must identify the algorithm, payload digest, both key IDs, and signature; envelope values are also canonicalized and authenticated. A free-form environment variable, command argument, or unsigned JSON document is not an authorization.

### Validity, replay, revocation, and audit

The verifier must require current trusted time, reject expired or not-yet-valid approvals, check both key IDs against the registry, and check revocation at verification time. `approvalId` and `nonce` must be atomically recorded as consumed for the exact operation, hash, and target before execution; a duplicate or changed reuse is rejected. Key rotation must preserve historical verification for already-issued, unexpired approvals while preventing use after revocation. Emergency revocation must invalidate approvals immediately.

The audit record must contain approval ID, payload digest, migration ID, candidate hash, target fingerprint (without credentials), approver key IDs, verifier key-registry version, verification time, result, and failure reason. Audit persistence or its approved durable equivalent is mandatory. If signature verification, key lookup, revocation, trusted time, replay recording, or audit persistence cannot be completed, execution is denied before a database connection is opened.

### Authorization acceptance criteria

An implementation is acceptable only when provider-independent tests demonstrate:

- Valid dual signatures pass only when both identities are distinct and authorized.
- Any changed byte, migration ID, hash, target fingerprint, operation, key ID, timestamp, nonce, or approval ID fails.
- Missing, malformed, extra, duplicated, expired, future-dated, revoked, and replayed approvals fail closed.
- One approver cannot satisfy both roles; shared or unrecognized keys fail.
- Key rotation and revocation behavior match the registry contract.
- Missing clock, registry, replay store, or audit sink prevents connection creation.
- Error output contains no signature material, tokens, credentials, or complete payloads when sensitive values are present.

Independent security review must inspect the canonicalization library/profile, signature verification, trust-anchor loading, clock source, replay atomicity, revocation freshness, and audit failure handling.

## Target-identity contract

### Canonical fingerprint proposal

The proposed fingerprint is an ordered, semicolon-delimited ASCII record. The field order is fixed and duplicate or unknown fields are invalid:

```text
v=1;provider=postgresql;endpoint_host=<canonical-host>;endpoint_port=<decimal-port>;database=<database>;schema=<schema>;server_version_major=<integer>;system_identifier=<provider-identity>;approved_target_id=<owner-issued-id>
```

Values are UTF-8, percent-encoded for `;`, `=`, `%`, control characters, and non-ASCII bytes. The percent-encoding profile, maximum lengths, PostgreSQL version range, system-identifier source, and approved-target-ID namespace require owner approval. No password, connection string, token, private key, or secret-bearing diagnostic may be a field.

`endpoint_host` is either an IDNA A-label DNS name lowercased with one approved trailing-dot policy, or a canonical textual IP address. IPv4 and IPv6 forms are normalized by a single approved parser; IPv4-mapped IPv6 values are not silently treated as equivalent. DNS aliases are not interchangeable unless the approval records the complete approved endpoint set and the verifier proves which endpoint was used. Default ports are never inferred; the port must be explicit.

The expected fingerprint is supplied only by the signed approval payload or a separately controlled release record. The caller cannot supply both expected and actual identity values.

### Independent actual-identity verification

The provider adapter must establish actual identity from two controlled sources:

1. The connection endpoint selected by the verifier, including normalized host, port, TLS/SNI or equivalent endpoint-authentication evidence, and the resolved address set under an approved DNS policy.
2. Authenticated PostgreSQL server/catalog evidence for current database, schema, server major version, and the approved server system identity. The exact catalog/API privilege and system-identifier mechanism must be selected and tested by the PostgreSQL owner; this repository does not claim one is available.

The verifier compares both sources to the signed expected fingerprint before beginning the migration transaction. It rejects connection-string substitution, unexpected redirects or proxies, TLS identity mismatch, stale DNS/address evidence, database/schema mismatch, server-version mismatch, unavailable system identity, and any disagreement between endpoint and catalog evidence. A caller-provided descriptor or `current_database()` result alone is insufficient proof of the approved target.

### Target-identity acceptance criteria

Provider-independent tests must cover strict parsing, field order, duplicate/unknown/missing fields, escaping, case and port rules, DNS aliases, IPv4/IPv6 normalization, stale or expired expected records, and mismatches. Provider-backed PostgreSQL tests on a separately approved disposable target must prove that the verifier observes the actual server/database/schema/version/system identity, rejects a redirected or substituted endpoint, and never logs credentials. A failed or unavailable identity check must occur before transaction creation and before migration command execution.

## Decision and evidence matrix

| Area | Existing documented requirement | Proposed technical contract | Organizational approval | Runtime/provider evidence |
|---|---|---|---|---|
| Artifact scope/hash | Exact immutable hash required | Fixed ID `026`, fixed v3 path/hash | Release authority | Independent file hash immediately before execution |
| Approval authority | Separate operation approval required | Distinct release and database approvers | Security, release, database owners | Verifier tests and audit inspection |
| Signature | No repository implementation | Ed25519 + JCS detached signature | Security owner/key-registry owner | Key rotation, revocation, replay tests |
| Target expected value | Independent fingerprint required | Signed canonical fingerprint record | Database owner/release owner | PostgreSQL identity verification |
| Target actual value | Must not trust caller text | Endpoint plus authenticated catalog/server evidence | PostgreSQL/platform owner | Disposable-target tests |
| Lock | One migration worker required | Provider adapter with reviewed uniqueness and lifetime | Database owner | Concurrency, timeout, release tests |
| Transaction | One outer transaction; unknown commit is `UNKNOWN_COMMIT` | Exact state machine and no-retry behavior | Database/release owners | Connection-loss/cancellation/rollback tests |
| Recovery | Backup, baseline, manifests, restore, postconditions | Evidence schema and sign-off checkpoints | Database/release owners | Restore rehearsal and preservation comparison |
| Audit | Evidence and approval history required | Durable redacted verification/execution record | Security/compliance owner | Audit outage and retention tests |

## Dependencies, blockers, and next gate

Implementation must not begin until the security owner approves the signature and key-registry choices, the release and database owners approve the distinct-authority model, and the PostgreSQL/platform owner selects the actual identity and lock mechanisms. The verifier then requires provider-independent tests, followed by provider-backed tests on a separately approved disposable target. Execution approval additionally requires the baseline/adoption decision, backup/restore evidence, preservation manifests, exact artifact hashes, postconditions, and independent sign-off.

The current runner remains correctly disabled while these dependencies are absent. This specification does not convert any proposal into approved policy and does not authorize migration 026.
