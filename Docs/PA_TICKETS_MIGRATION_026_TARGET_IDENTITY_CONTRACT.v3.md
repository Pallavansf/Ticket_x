# Gate T19 — PostgreSQL Target Identity Contract Proposal v3

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED**

This revision addresses Gate T18 findings H1, H2, and M1. It defines lexical IPv6 handling and final descriptor serialization without selecting production DNS, TLS, proxy, pooling, or PostgreSQL identity policy.

## 1. Gate T18 traceability

| Finding | Revision | Status |
|---|---|---|
| H1: IPv6 lexical and canonical ambiguity | §3 defines accepted lexical forms, parsed 128-bit representation, embedded IPv4 conversion, RFC 5950-style output, tie-breaking, mapped/compatible handling, and zone rejection. | Proposed; parser/profile approval required |
| H2: hostname-to-observed-endpoint binding incomplete | §5 defines expected host, configured endpoint, DNS result, peer endpoint, authenticated server identity, and comparison evidence as separate records. | Proposed; network/provider policy approval required |
| M1: incomplete equal-run vector | §3 includes a concrete equal-length zero-run input and output. | Resolved as proposal |

## 2. Descriptor processing pipeline

The descriptor has exactly this field order:

v=1;provider=postgresql;endpoint_host=<encoded-value>;endpoint_port=<encoded-value>;database=<encoded-value>;schema=<encoded-value>;server_version_major=<encoded-value>;system_identifier=<encoded-value>;approved_target_id=<encoded-value>

The verifier performs these steps in order:

1. Strictly parse one descriptor record and require the exact field order, one occurrence of every field, no unknown fields, no missing fields, no trailing bytes, and no unescaped semicolon or equals inside a value.
2. Decode percent escapes exactly once. Reject malformed escapes, alternate encodings, invalid UTF-8, controls, NUL, and delimiter injection.
3. Validate and normalize each decoded field with its field-specific grammar.
4. Produce intermediate canonical field values. For an IP host, this includes a parsed address family and 128-bit value; for a DNS host, this includes the canonical A-label.
5. Encode each intermediate value as UTF-8 bytes: leave only ASCII ALPHA, DIGIT, hyphen, period, underscore, and tilde literal; percent-encode every other byte as uppercase percent plus two uppercase hexadecimal digits.
6. Serialize fields in the exact order above with ASCII semicolon and equals separators.
7. Compute any descriptor evidence digest as uppercase hexadecimal SHA-256 over the exact final serialized UTF-8 bytes. When the descriptor is inside the authorization payload, the authorization payload's JCS digest remains the authority.

A noncanonical input is rejected; the verifier does not sort or repair it. The intermediate IPv6 value may be 2001:db8::1, while the final descriptor field is endpoint_host=2001%3Adb8%3A%3A1.

The complete final descriptor is limited to 4096 UTF-8 bytes. Field limits and parser resource limits are OWNER DECISION REQUIRED.

## 3. IPv6 lexical and canonical profile

### 3.1 Lexical input

Accepted IPv6 input is either:

- Eight hexadecimal 16-bit fields separated by seven colons; or
- A compressed form containing at most one double-colon, representing one or more omitted zero fields; or
- One of the above with the final 32 bits written as dotted-decimal IPv4, consisting of exactly four decimal octets 0–255.

Hexadecimal digits may be uppercase or lowercase. Leading zeroes in a field are accepted lexically and removed during canonicalization. Dotted IPv4 is accepted only in the final two 16-bit fields. No other mixed form is accepted.

Rejected lexical forms include multiple double-colons, too few or too many fields after expansion, an embedded IPv4 component outside the final 32 bits, invalid octets, octal/hexadecimal IPv4, leading-zero IPv4 octets, zone identifiers, scope identifiers, empty labels, and non-hex characters.

### 3.2 Parsed address and embedded IPv4

After lexical validation, the address is a 128-bit value with an explicit IPv6 family marker.

For dotted IPv4 X1.X2.X3.X4:

- The first embedded 16-bit field is (X1 << 8) | X2.
- The second embedded 16-bit field is (X3 << 8) | X4.
- The dotted form is never retained in canonical output.

IPv4-mapped addresses (prefix ::ffff:) and IPv4-compatible addresses (prefix ::) remain IPv6 values. They are never equal to an IPv4 descriptor, even when the low 32 bits match. The family marker is part of comparison evidence.

### 3.3 Canonical output

Canonical IPv6 output is lowercase hexadecimal with:

- No leading zeroes in a 16-bit field.
- The longest run of two or more zero fields compressed to ::.
- If multiple longest runs have equal length, the leftmost run compressed.
- A single zero field left uncompressed.
- No dotted IPv4 output.
- No zone or scope suffix.

The exact parser/library and conformance profile require PostgreSQL/platform owner approval. The rules above are proposed defaults, not verified provider behavior.

### 3.4 Vectors

| Lexical input | Parsed intermediate value | Canonical IPv6 output | Final descriptor host value | Result |
|---|---|---|---|---|
| 2001:db8:0:0:1:0:0:1 | IPv6 fields 2001,db8,0,0,1,0,0,1 | 2001:db8::1:0:0:1 | 2001%3Adb8%3A%3A1%3A0%3A0%3A1 | Accept |
| 2001:0db8:0:0:1:0:0:1 | Same 128-bit value | 2001:db8::1:0:0:1 | 2001%3Adb8%3A%3A1%3A0%3A0%3A1 | Accept |
| ::192.0.2.1 | fields 0,0,0,0,0,0,c000,0201 | ::c000:201 | %3A%3Ac000%3A201 | Accept as IPv6 |
| ::ffff:192.0.2.1 | fields 0,0,0,0,0,ffff,c000,0201 | ::ffff:c000:201 | %3A%3Affff%3Ac000%3A201 | Accept as IPv6-mapped |
| 0:0:0:0:0:ffff:c000:0201 | Same mapped 128-bit value | ::ffff:c000:201 | %3A%3Affff%3Ac000%3A201 | Accept as same IPv6 |
| 192.0.2.1 | IPv4 octets 192,0,2,1 | 192.0.2.1 | 192.0.2.1 | Accept as IPv4 only |
| 2001:db8:0:0:1:0:0:1 with equal runs | Two zero runs of length two; leftmost selected | 2001:db8::1:0:0:1 | Encoded form above | Accept |
| 2001:db8:::1 | — | — | — | Reject: multiple compression markers |
| ::ffff:192.0.2.999 | — | — | — | Reject: octet out of range |
| 2001:db8:1:2:3:4:5:6:7 | — | — | — | Reject: excess fields |
| 2001:db8::1%eth0 | — | — | — | Reject: zone identifier |
| ::ffff:c000:201 represented as IPv4 | IPv6 family differs | — | — | Reject: family substitution |
| 001.002.003.004 | — | — | — | Reject: noncanonical IPv4 lexical form |

All accepted vectors are pure parser/unit evidence. Their use as actual endpoint identity still requires provider/network evidence.

## 4. Complete descriptor examples and digest

### 4.1 IPv4 descriptor

v=1;provider=postgresql;endpoint_host=192.0.2.1;endpoint_port=5432;database=illustrative;schema=public;server_version_major=16;system_identifier=example-system;approved_target_id=example-target

### 4.2 IPv6 descriptor

v=1;provider=postgresql;endpoint_host=2001%3Adb8%3A%3A1;endpoint_port=5432;database=illustrative;schema=public;server_version_major=16;system_identifier=example-system;approved_target_id=example-target

The exact UTF-8 byte length of the IPv6 descriptor above is 201 bytes. Its uppercase SHA-256 digest, calculated over those exact bytes without a BOM or trailing newline, is:

F2E1DF16CC11B1C2CFE20A0208C06AB1E0241C92EB2C310917112AF843289FDB

This is an illustrative digest, not a target identity or authorization. The signed authorization payload's JCS digest remains authoritative, as specified by the v4 authorization contract.

## 5. Endpoint-to-server binding

The verifier records these distinct layers:

1. Signed expected host/address, port, database, schema, version policy, system-identity requirement, and approved target ID.
2. Configured endpoint and connection options used to request a connection.
3. DNS answer set, resolver identity, resolution timestamp, freshness result, alias registry record, and selected address.
4. Actual connected peer endpoint, only where the provider/driver exposes a value reliably bound to that connection.
5. TLS certificate chain, hostname, and SNI result where TLS is selected.
6. Authenticated PostgreSQL database, schema, server major version, and provider system identity.
7. Final comparison result, failure code, timestamps, and redacted evidence references.

The caller cannot supply the observed layers. DNS equality, configured endpoint equality, TLS name equality, or a caller fingerprint alone is not proof of server identity.

The comparison must occur before BEGIN and before any migration command:

- A configured endpoint must be permitted by the signed descriptor and owner target registry.
- A DNS hostname may resolve only within the approved alias/address policy; a DNS answer alone is not server identity.
- An IP literal must match the approved family and canonical address when reliable peer evidence exists.
- If peer evidence is unavailable, the selected policy must deny or provide an explicitly approved authenticated substitute.
- Any stale/rebound DNS answer, unregistered alias, changed address family, proxy redirect, backend switch, pool cross-target reuse, certificate/SNI failure, database/schema mismatch, unsupported version, or system-identity mismatch denies.
- Missing or unauthenticated evidence at any layer denies.

DNS freshness, aliases, address pinning, TLS, certificate trust, SNI, proxy, redirect, pooling, and peer-endpoint policies remain unselected owner decisions.

## 6. Database/schema and identity-source governance

The v2 proposed identifier profile remains provisional: unquoted identifiers lowercased under a conservative ASCII rule; quoted identifiers preserve case after NFC; maximum 63 UTF-8 bytes; invalid, empty, control, or ambiguous forms reject. PostgreSQL owner and security approval are required.

The PostgreSQL owner must select an authenticated system-identity source. Candidates are provider-reported system identity, TLS/SNI identity, approved proxy propagation, or a combination. Server version and database/schema prove session context but not unique server identity. Server start time and connection metadata are diagnostic only. Missing, stale, substituted, or unavailable identity denies.

No DNS, TLS, provider identity, proxy, pool, lock, recovery, or migration-execution policy is approved by this document.

## 7. Acceptance matrix

| Requirement | Positive criterion | Negative criterion | Evidence | Classification | Owner/block |
|---|---|---|---|---|---|
| Strict descriptor order | Exact ordered bytes parse and serialize identically | Reordered/duplicate/unknown/missing/trailing input rejects | Parser and byte vectors | Unit | Security/platform; canonicalizer |
| Percent encoding | IPv6 colons and all reserved bytes encode as uppercase percent bytes | Raw delimiters, alternate escapes, invalid UTF-8 reject | Exact-byte fixtures | Unit | Security; canonicalizer |
| IPv6 | All accepted vectors map to stated 128-bit value and canonical output | Multiple compression, invalid octets/fields, zone, family substitution reject | IPv6 conformance suite | Unit plus parser review | Platform; canonicalizer |
| Host/DNS binding | Approved alias/address policy and fresh answer compare consistently | Rebinding, stale answer, unregistered alias/address mismatch denies | Resolver/registry evidence | Provider-backed | Platform/database; target adapter |
| TLS/SNI | Approved certificate, SNI, and hostname match | Wrong certificate, SNI, hostname, expiry, revocation denies | TLS traces | Provider-backed | Security/database; target adapter |
| Peer endpoint | Reliable peer endpoint matches expected policy | Unavailable/mismatched endpoint denies unless approved substitute exists | Driver endpoint evidence | Provider-backed | PostgreSQL/platform; target adapter |
| Proxy/pooling | Approved backend propagation and checkout revalidation succeed | Redirect, backend switch, or cross-target reuse denies | Proxy/pool traces | Provider-backed | Platform/database; provider validation |
| Identifier profile | Approved database/schema values match authenticated session | Case, quote, Unicode, length, search_path, or session mismatch denies | Identifier vectors and provider evidence | Unit plus provider-backed | PostgreSQL/security; canonicalizer |
| Version/system identity | Supported authenticated values match registry | Unsupported, stale, missing, substituted, or mismatched values deny | Provider identity/version evidence | Provider-backed | PostgreSQL owner; target adapter |

## 8. Decision register and runner boundary

| Decision | Proposed default | Alternative | Owner role | Evidence | Blocking stage |
|---|---|---|---|---|---|
| IPv6 parser/profile | RFC 5952-style, leftmost tie-break, no zones | Approved library profile | PostgreSQL/platform owner | Conformance vectors | Canonicalizer |
| Descriptor limits | 4096 bytes and conservative parser bounds | Other approved limits | Security owner | Resource tests | Canonicalizer |
| DNS/address policy | Fresh resolver plus target registry | Pin addresses or approved proxy | Platform/database owner | Rebinding/address evidence | Target adapter |
| TLS/SNI | Mandatory TLS with approved CA/SNI policy | Provider identity with approved threat model | Security/database owner | TLS tests | Target adapter |
| Identifier grammar | Conservative profile in §6 | PostgreSQL-native profile | PostgreSQL/security owner | Identifier/version matrix | Canonicalizer |
| System identity | Authenticated provider mechanism | Approved proxy propagation | PostgreSQL owner | Provider identity tests | Target adapter |
| Peer/proxy/pool | Revalidate or deny on unavailable evidence | Disallow these paths | Platform/database owner | Peer/pool/proxy evidence | Provider validation |
| Evidence custody | Approved audited store | Central compliance archive | Release/compliance owner | Storage/access evidence | Execution approval |

Security/key-registry, target-identity, PostgreSQL identity, TLS/network, lock, recovery, provider-validation, and migration-execution approvals remain separate.

Runner.cs still accepts caller-supplied ApprovedTargetFingerprint and opaque Authorization, delegates identity to IExecutionPolicy, and has no canonicalizer, network adapter, peer evidence, provider identity retrieval, registry lookup, pool/proxy revalidation, or evidence writer. Program.cs remains disabled through NoExecutionPolicy. Injected interfaces and unit fakes do not establish actual server identity.

This revision resolves the documented IPv6 and descriptor-serialization ambiguities as a proposal. It does not implement the contract, select owner policies, establish a target, run tests, or authorize migration execution.

**GATE T19 COMPLETE — CANONICALIZATION REVISION PREPARED, OWNER APPROVAL REQUIRED**

