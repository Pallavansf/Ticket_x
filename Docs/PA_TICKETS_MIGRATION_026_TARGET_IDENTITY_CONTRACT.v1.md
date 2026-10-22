# Gate T15 — PostgreSQL Target Identity Contract Proposal v1

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED**

This document defines a deterministic target descriptor and a future verifier boundary for PostgreSQL migration 026. It does not identify a real target, select a production network policy, implement an adapter, connect to PostgreSQL, or authorize migration execution.

## 1. Scope and traceability

The proposal applies only to migration 026 and the signed candidate hash:

97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49

It addresses:

- Gate T10 target identity and DNS/TLS/redirection findings.
- Gate T12 target canonicalization and independently observed identity findings.
- Gate T14's confirmation that target identity remains an owner/provider gate.
- The v3 and v4 distinction between expected target data and current runner interfaces.

No target fingerprint, server identity, credential, trust anchor, DNS result, or provider guarantee is supplied.

## 2. Canonical target descriptor

### 2.1 Descriptor fields

The canonical descriptor is a UTF-8 ASCII key/value record with exactly this version and order:

v=1;provider=postgresql;endpoint_host=<value>;endpoint_port=<value>;database=<value>;schema=<value>;server_version_major=<value>;system_identifier=<value>;approved_target_id=<value>

The descriptor contains exactly these fields:

| Field | Type and rules |
|---|---|
| v | Decimal string, exact value 1 |
| provider | ASCII string, exact value postgresql |
| endpoint_host | Canonical DNS A-label or canonical IP literal; no empty value |
| endpoint_port | ASCII decimal integer 1–65535; no default |
| database | PostgreSQL database identifier in the approved identifier grammar |
| schema | PostgreSQL schema identifier in the approved identifier grammar |
| server_version_major | ASCII decimal major version in the approved supported range |
| system_identifier | Authenticated provider system identity in the approved grammar; no secret |
| approved_target_id | Owner-issued target registry identifier in the approved grammar; no secret |

Unknown, missing, duplicate, empty, overlong, malformed, or reordered fields are rejected. Keys are case-sensitive and fixed. Values cannot contain unescaped semicolon or equals characters.

The complete serialized descriptor is limited to 4096 UTF-8 bytes. Every field and its maximum length require a security/platform owner decision before implementation.

### 2.2 Value encoding

Values are normalized before serialization. The proposed encoding is:

- Normalize only according to the field-specific rules below.
- Encode the normalized value as UTF-8 bytes.
- Leave only ASCII unreserved bytes ALPHA, DIGIT, hyphen, period, underscore, and tilde unchanged.
- Percent-encode every other byte as uppercase percent followed by two uppercase hexadecimal digits.
- Do not decode, trim, case-fold, or Unicode-normalize after serialization.
- Reject malformed percent sequences, duplicate encodings, NUL, controls, and invalid UTF-8.

The encoding profile, maximum field lengths, Unicode normalization policy for database/schema identifiers, and parser resource limits are OWNER DECISION REQUIRED. No implementation may silently choose alternatives.

### 2.3 Hostnames, aliases, and IP literals

Proposed default:

- DNS names are converted to lowercase IDNA A-label form using one approved IDNA implementation and profile.
- A trailing dot is removed before canonicalization. Names that differ only by trailing dot are therefore equivalent under this proposed default.
- An alias is not equivalent to its canonical service name unless the owner-controlled target registry explicitly lists the alias for that target and the verifier records which alias was used.
- Wildcard names, empty labels, invalid IDNA, ambiguous aliases, and names containing a percent-encoded delimiter are rejected.
- IPv4 is dotted decimal with each octet 0–255 and no alternate integer or hexadecimal form.
- IPv6 uses the approved parser's lowercase compressed representation with the shortest permitted zero run and no zone identifier.
- IPv4-mapped IPv6 is not equivalent to IPv4. The descriptor must preserve the parsed address family.

The DNS alias set, trailing-dot equivalence, IDNA profile, resolver policy, and address-family policy remain OWNER DECISION REQUIRED.

### 2.4 Port, database, schema, and version

Ports have no default and must be decimal 1–65535. Database and schema values use an approved PostgreSQL identifier grammar with explicit handling for quoted identifiers, case folding, length limits, and Unicode. The verifier must compare the normalized database and schema values returned by the authenticated provider, not merely connection-string text.

server_version_major is the provider-reported major version parsed from an approved server-version source. The supported-version range and behavior for a future/unknown version are OWNER DECISION REQUIRED; unsupported, unavailable, malformed, or inconsistent versions deny.

system_identifier is obtained from an approved authenticated PostgreSQL mechanism. It is never caller-supplied and is never inferred from hostname, port, database name, or connection-string text.

approved_target_id is an owner-controlled registry identifier. It is not derived from DNS or from a caller-provided fingerprint and does not prove actual identity by itself.

### 2.5 Descriptor digest and examples

When a descriptor is included in a signed authorization payload, the payload's JCS digest authenticates the exact descriptor string. A separate descriptor digest may be recorded for evidence as uppercase hexadecimal SHA-256 over the descriptor's exact UTF-8 bytes. It must not replace the signed payload binding.

Illustrative, non-operational positive descriptor:

v=1;provider=postgresql;endpoint_host=example.invalid;endpoint_port=5432;database=illustrative;schema=public;server_version_major=16;system_identifier=example-system;approved_target_id=example-target

This is not a real target identity.

Negative examples:

- endpoint_host=Example.invalid: rejected if not normalized before comparison; accepted only after the defined normalization step.
- endpoint_port=5432.0: rejected.
- endpoint_port omitted: rejected.
- endpoint_host=2001:db8::1 without encoding: rejected because delimiters are not encoded.
- endpoint_host=::ffff:192.0.2.1 represented as IPv4: rejected as an address-family substitution.
- duplicate schema fields: rejected.
- unknown field region=prod: rejected.
- database containing an unapproved quoted-identifier form: rejected.
- server_version_major outside the approved range or unavailable: rejected.
- system_identifier copied from a caller argument: rejected.
- approved_target_id changed while all other fields match: rejected.

## 3. Expected, configured, and observed identity

The verifier must keep four records separate:

1. Signed expected descriptor: taken only from the verified authorization payload and owner target registry.
2. Configured endpoint: the host, port, database, and connection options used to request a connection; treated as untrusted input until policy checks complete.
3. Observed actual identity: collected after authentication from the connected PostgreSQL server and approved TLS/network evidence.
4. Comparison result and evidence: a redacted record containing expected descriptor, observed descriptor, selected endpoint, evidence timestamps, comparison outcome, and failure code.

A caller cannot supply both the expected descriptor and purported observed descriptor. Endpoint equality is insufficient because DNS can resolve to a different address, aliases can conceal substitution, proxies can redirect, pooled connections can be reused, and a server can report a different database, schema, version, or system identity. A caller-supplied fingerprint is only expected configuration; it is not proof of actual server identity.

Comparison must finish before BEGIN and before any migration command. Missing or conflicting evidence denies.

## 4. Authentication and network-policy candidates

No production policy is selected by this proposal.

| Candidate | Trust assumptions and prerequisites | Failure behavior | Required evidence |
|---|---|---|---|
| TLS mandatory with certificate and SNI validation | Approved CA or pin registry, hostname/SNI policy, trusted certificate validation, controlled driver options | Chain, hostname, SNI, expiry, revocation, or policy mismatch denies | TLS rotation, alias, SNI, expiry, and wrong-server tests |
| TLS optional with provider identity only | Provider system identity cannot be substituted; network path is trusted | Any unavailable system identity or unexpected transport policy denies; optional TLS carries greater substitution risk | Owner threat-model approval and provider identity tests |
| Canonical DNS and address policy | Approved resolver, freshness rule, alias registry, IPv4/IPv6 policy | Rebinding, changed address, ambiguous alias, stale result, or address-family mismatch denies | DNS alias, rebinding, IPv4/IPv6, trailing-dot, and reconnect tests |
| Approved proxy or redirect path | Proxy identity and end-to-end backend identity propagation are trusted | Missing propagation, unexpected backend, or changed identity denies | Proxy, redirection, and backend-switch tests |
| Connection pooling | Pool is bound to target descriptor and revalidates identity on checkout/reconnect | Pool reuse with different endpoint/backend or unavailable revalidation denies | Pool reuse, expiration, reconnect, and cross-target tests |
| Provider-authenticated system identity | Approved PostgreSQL driver and least-privilege identity retrieval | Missing, malformed, stale, or mismatched system identity denies | PostgreSQL version/system-identity tests on an approved disposable target |

The final TLS requirement, certificate policy, DNS/address policy, proxy allowance, pool policy, system-identity source, and target registry are separate owner decisions. No candidate is treated as approved because it is listed here.

## 5. Acceptance criteria and evidence

### 5.1 Provider-independent tests

Positive criteria:

- Reordering descriptor input fields produces one canonical serialization.
- Equivalent approved hostname case and trailing-dot forms normalize identically under the selected policy.
- An approved IPv4 or IPv6 literal round-trips without changing address family.
- A valid port, database, schema, version, system-identity grammar, and target-ID grammar parse successfully.
- The same descriptor bytes always produce the same uppercase SHA-256 digest.
- Expected and observed descriptors compare equal only when every canonical field matches.

Negative criteria:

- Unknown, duplicate, missing, empty, malformed, overlong, or reordered fields reject.
- Invalid IDNA, wildcard host, malformed IPv4/IPv6, zone identifier, mapped-address substitution, invalid port, delimiter injection, and malformed percent encoding reject.
- Database/schema grammar violations, unsupported version, empty system identity, or invalid target ID reject.
- A changed host, address family, port, database, schema, version, system identity, or approved target ID rejects.

Evidence: parser conformance vectors, normalization vectors, boundary-length tests, independent digest calculation, and security review of the canonicalization implementation.

### 5.2 Provider-backed tests

Positive criteria:

- The adapter obtains current database, schema, server major version, and authenticated system identity from the connected PostgreSQL server.
- The observed descriptor matches the signed expected descriptor on an approved disposable target.
- Approved TLS/SNI, DNS alias, IPv4/IPv6, and pool policies behave as specified.

Negative criteria:

- Wrong certificate, SNI, hostname, alias, resolved address, backend, database, schema, version, or system identity denies before BEGIN.
- DNS rebinding, stale target registration, proxy redirection, pool substitution, connection loss during identity retrieval, or unavailable identity denies.
- A connection string that claims the expected target while the authenticated server reports another target denies.

Evidence: separately approved disposable PostgreSQL target, provider-specific logs without credentials, identity query results, TLS/DNS/pool traces, and proof that no migration command was sent.

No unit or provider test has been run for this proposal.

## 6. Decision register

| Decision | Proposed default | Alternative/trade-off | Owner role | Evidence | Blocking stage |
|---|---|---|---|---|---|
| Target registry authority | Security/platform-controlled registry | Database-owned registry | Security/platform owner | Registry governance and change audit | Target verifier |
| Host/IDNA policy | Lowercase A-label, remove trailing dot | Preserve trailing dot or pin aliases | Platform owner | IDNA and alias vectors | Canonicalizer |
| DNS/address policy | Fresh resolution with approved alias/address policy | Pin addresses or require a proxy | Platform/database owner | Rebinding and address tests | Target adapter |
| TLS | Mandatory TLS with approved CA/SNI policy | Provider identity without mandatory TLS | Security/database owner | TLS/SNI and rotation tests | Target adapter |
| IPv4/IPv6 | Preserve family; mapped IPv6 not IPv4-equivalent | Explicit mapped-address equivalence | Platform owner | Address-family vectors | Canonicalizer |
| Database/schema grammar | Approved PostgreSQL identifier profile | Restricted ASCII-only profile | PostgreSQL owner | Identifier vectors | Canonicalizer |
| Supported versions | Owner-set allowlist; unknown denies | Compatibility range | PostgreSQL owner | Version matrix | Target adapter |
| System identity source | Authenticated provider mechanism | Approved proxy-propagated identity | PostgreSQL owner | Provider identity evidence | Target adapter |
| Pool/proxy behavior | Revalidate identity on checkout/reconnect | Disallow pooling/proxies | Platform/database owner | Pool/proxy tests | Provider validation |
| Evidence retention/custody | Approved audited evidence store | Central compliance archive | Release/compliance owner | Storage and access audit | Execution approval |

Security/key-registry approval, target-identity approval, PostgreSQL-owner approval, lock approval, provider validation, recovery approval, and migration-execution approval are separate gates.

## 7. Runner compatibility and blockers

Runner.cs currently accepts caller-supplied ApprovedTargetFingerprint and opaque Authorization, then delegates authorization and actual identity to IExecutionPolicy. It has no canonical descriptor parser, DNS/TLS policy adapter, provider identity retrieval, registry lookup, pool/proxy binding, or comparison evidence writer. ITargetConnection exposes only connection, transaction, lock, and execution methods; it does not expose identity evidence or revalidation.

Program.cs remains disabled through NoExecutionPolicy. The runner's existing fail-closed behavior is not evidence that the target contract is implemented.

Implementation is blocked until the owner decisions above are approved, a concrete target adapter is designed, and provider-backed identity/network evidence is completed. This proposal does not authorize implementation, provider testing, database access, or migration execution.

**GATE T15 COMPLETE — TARGET IDENTITY CONTRACT PROPOSED, OWNER APPROVAL REQUIRED**

