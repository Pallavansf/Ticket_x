# Gate T17 — PostgreSQL Target Identity Contract Proposal v2

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED**

This revision addresses Gate T16 findings C1, H1, H2, H3, and H4. It defines proposed canonicalization and endpoint-binding rules without selecting a production policy or claiming provider evidence.

## 1. Gate T16 traceability

| Finding | Revision | Status |
|---|---|---|
| C1: field-order contradiction | §2 selects strict canonical-order input; reordered serialized descriptors reject consistently. | Resolved as proposal |
| H1: IPv6 ambiguity | §3 defines an RFC 5952-style profile, tie-breaking, mapped/compatible forms, and zone rejection. | Owner/parser approval required |
| H2: hostname-to-observed-endpoint ambiguity | §4 separates signed host, configured endpoint, DNS answers, peer endpoint, and authenticated server identity. | Owner/network/provider approval required |
| H3: database/schema normalization incomplete | §5 defines a proposed PostgreSQL identifier profile and rejection rules. | PostgreSQL/security approval required |
| H4: system-identity source unselected | §6 compares candidate authenticated identity sources without selecting one. | PostgreSQL-owner decision required |

## 2. Canonical descriptor and strict input model

The descriptor is one UTF-8, ASCII-serialized key/value record with exactly this field order:

v=1;provider=postgresql;endpoint_host=<value>;endpoint_port=<value>;database=<value>;schema=<value>;server_version_major=<value>;system_identifier=<value>;approved_target_id=<value>

This revision chooses the **strict canonical-order model**:

- A verifier accepts only this exact field order and one occurrence of every field.
- Reordered input, duplicate fields, unknown fields, missing fields, empty values, unescaped semicolon/equal characters, invalid UTF-8, malformed percent escapes, and trailing data reject.
- The verifier does not sort or repair a noncanonical descriptor.
- The exact serialized UTF-8 bytes above, including field order and encoded values, are the bytes compared and, when included in the signed authorization payload, authenticated by the payload JCS digest.
- Parsing and field normalization occur before serialization. Serialization occurs once; no second normalization is permitted.

Values are encoded as UTF-8 bytes. ASCII unreserved bytes ALPHA, DIGIT, hyphen, period, underscore, and tilde remain literal. Every other byte is percent-encoded with uppercase percent and two uppercase hexadecimal digits. NUL, controls, invalid UTF-8, and alternate encodings reject. The descriptor is limited to 4096 UTF-8 bytes; field limits and parser resource limits remain OWNER DECISION REQUIRED.

Consistent examples:

- Canonical input in the exact order above: accepted if every field validates.
- Same fields with schema before database: rejected as noncanonical input.
- Duplicate schema or unknown region field: rejected.
- A semantically equivalent descriptor must be regenerated in canonical order before it is signed; it is not accepted merely because it parses.

## 3. IPv4 and IPv6 canonicalization

The proposed parser profile is RFC 5952-style textual IPv6 plus strict dotted-decimal IPv4:

- DNS and hexadecimal host input are classified before IP parsing.
- IPv4 is four decimal octets, each 0–255, with no leading alternate integer, hexadecimal, or octal form.
- IPv6 is eight 16-bit fields, accepts uppercase or lowercase input, and serializes lowercase.
- Leading zeroes in a field are removed.
- The longest run of consecutive zero fields is compressed to ::. If equal-length runs exist, the leftmost run is compressed. A single zero field is not compressed.
- IPv4-compatible and IPv4-mapped forms remain IPv6 values and are serialized as IPv6. They are never equivalent to an IPv4 descriptor.
- IPv4-mapped values are not converted to IPv4; the address-family marker is retained in comparison evidence.
- Zone identifiers and scoped addresses are rejected because they are interface-local and not stable target identity.
- Invalid field counts, multiple compression markers, malformed embedded IPv4, mixed invalid forms, and ambiguous leading-zero forms reject.

Illustrative vectors:

| Input | Canonical result | Result |
|---|---|---|
| 2001:0DB8:0:0:0:0:2:1 | 2001:db8::2:1 | Accept |
| 2001:db8:0:0:1:0:0:1 | 2001:db8::1:0:0:1 | Accept |
| 2001:db8:0:0:1:0:0:1 with an equal run on the left | Leftmost longest run is compressed | Accept under selected tie-break |
| ::ffff:192.0.2.1 | ::ffff:c000:201 | Accept as IPv6 only |
| 192.0.2.1 | 192.0.2.1 | Accept as IPv4 only |
| 2001:db8:::1 | — | Reject |
| 2001:db8::1%eth0 | — | Reject |
| 001.002.003.004 | — | Reject |
| ::ffff:192.0.2.1 represented as 192.0.2.1 | — | Reject as family substitution |

The exact parser/library and RFC 5952 conformance suite are OWNER DECISION REQUIRED. Until approved, no implementation may claim these vectors establish provider behavior.

## 4. Hostname-to-observed-endpoint binding

The verifier maintains separate records:

1. Signed expected host/address and port from the verified authorization payload.
2. Configured connection endpoint and options supplied to request a connection.
3. DNS resolution result, resolver identity, resolution time, freshness, alias registry record, and approved address set.
4. Actual connected peer endpoint, if the provider/driver exposes it reliably.
5. Authenticated PostgreSQL identity: current database, schema, server major version, and provider system identity.
6. Final comparison result, failure code, timestamps, and redacted evidence references.

A caller cannot provide the expected and observed records. DNS equality, configured endpoint equality, or a caller fingerprint alone is not server identity.

Proposed comparison rules:

- The configured endpoint must be permitted by the signed expected host/address and owner target registry.
- If the expected host is a DNS name, every selected address must be within the approved resolver/alias/address policy. A DNS answer alone does not prove the server.
- If the expected value is an IP literal, the connected peer address must match the approved family and canonical address when peer evidence is available. If peer evidence is unavailable, the selected network policy must explicitly deny or provide an approved authenticated substitute.
- A changed DNS answer, stale resolution, rebinding, unexpected address family, alias not in the target registry, proxy redirect, or backend change denies.
- The authenticated PostgreSQL identity must match database, schema, server-version policy, system identity, and approved target registry. Endpoint equality cannot override an identity mismatch.
- Comparison completes before BEGIN and before any migration command. Missing, stale, unavailable, or conflicting layers deny.

Candidate policies remain unselected:

| Layer | Candidate | Trust assumption | Failure | Evidence |
|---|---|---|---|---|
| DNS | Fresh resolver plus owner alias/address registry | Resolver and registry are trusted | Rebinding, stale answer, ambiguous alias, or changed address denies | Resolver logs, timestamps, alias vectors |
| TLS | Mandatory TLS, certificate chain/CA or pin, hostname and SNI validation | CA/pin registry and driver validation are trusted | Chain, SNI, hostname, expiry, revocation, or wrong-server failure denies | TLS handshake and rotation evidence |
| Proxy | Explicit approved proxy with backend identity propagation | Proxy cannot substitute backend identity | Missing propagation or backend change denies | Proxy/redirect/backend-switch evidence |
| Pool | Revalidate target on checkout and reconnect | Pool cannot cross target boundaries | Reuse without revalidation or target change denies | Pool/reconnect/cross-target evidence |
| Peer endpoint | Provider exposes authenticated peer address | Driver value is reliable and bound to connection | Unavailable or inconsistent peer evidence denies unless policy approves another proof | Provider-specific endpoint evidence |

## 5. Database and schema identifier profile

Proposed profile, pending PostgreSQL-owner and security approval:

- Input is a PostgreSQL identifier token, not arbitrary SQL text.
- Unquoted identifiers are ASCII-folded to lowercase before comparison. Non-ASCII unquoted names are rejected under this conservative profile.
- Quoted identifiers preserve case and Unicode after UTF-8 NFC normalization. A quoted identifier cannot contain NUL or controls.
- The normalized identifier length is at most 63 UTF-8 bytes, matching the proposed PostgreSQL compatibility limit. Longer values reject.
- Empty quoted identifiers, unmatched quotes, embedded NUL, controls, invalid UTF-8, and ambiguous escape forms reject.
- Database and schema values are compared as separate normalized identifiers; schema is never inferred from search_path or connection defaults.
- The observed database and schema must come from the authenticated PostgreSQL session/provider, not solely from connection-string text.
- PostgreSQL versions with incompatible identifier behavior are outside the approved version range and deny.

Illustrative examples:

| Input | Normalized descriptor value | Result |
|---|---|---|
| public | public | Accept |
| Public (unquoted) | public | Accept under proposed fold |
| "Public" | Public | Accept as distinct from public |
| "" | — | Reject |
| identifier longer than 63 UTF-8 bytes | — | Reject |
| unquoted café | — | Reject under conservative profile |
| quoted NFC-equivalent name | NFC form | Accept if within limit |
| schema omitted and search_path supplied | — | Reject |

The exact PostgreSQL identifier grammar, Unicode profile, byte limit, and supported-version range are OWNER DECISION REQUIRED. The proposed profile is not an approved provider fact.

## 6. Authenticated PostgreSQL system identity candidates

No source is selected by this revision.

| Candidate | Proves | Does not prove | Assumptions/privileges | Failure and evidence |
|---|---|---|---|---|
| Provider-supported authenticated system identifier | Identity value reported by the authenticated server | It does not by itself prove DNS or TLS endpoint binding | Approved driver, identity query, and least-privilege access | Missing/ambiguous value denies; provider identity tests required |
| TLS certificate/SNI identity | Transport endpoint identity under approved CA/pin policy | It does not prove database/schema or provider system identity | Mandatory TLS and trusted CA/pin registry | Certificate/SNI failure denies; TLS tests required |
| Owner-approved proxy propagation | Backend identity attested by an approved proxy | It does not prove an unapproved backend | Trusted proxy and signed propagation contract | Missing propagation denies; proxy tests required |
| Server version plus database/schema | Session context and compatibility | It is not a unique server identity | Authenticated session and approved query source | Mismatch/unavailable values deny; provider tests required |
| Server start time or connection metadata | Diagnostic evidence only | It is not a stable unique identity | Provider exposes values | Never sufficient alone; no authorization use |

The PostgreSQL owner must select the system-identity source and document query/privilege behavior. The verifier must record source, timestamp, connection identity, and redacted result. Caller-supplied system identifiers, DNS names, connection strings, or environment values are never accepted as observed identity.

## 7. Acceptance matrix

| Requirement | Positive criterion | Negative criterion | Evidence | Classification | Owner/block |
|---|---|---|---|---|---|
| Strict field order | Exact ordered record parses and compares | Reordered, duplicate, unknown, missing, or trailing input rejects | Parser vectors | Unit | Platform/security; canonicalizer |
| Encoding | UTF-8 percent encoding round-trips exact bytes | Invalid UTF-8, alternate escapes, delimiter injection rejects | Byte vectors | Unit | Security; canonicalizer |
| IPv6 | RFC 5952 vectors serialize deterministically | Tie ambiguity, zone, malformed, mapped-family substitution rejects | IPv6 suite | Unit plus parser review | Platform; canonicalizer |
| Host/alias | Approved alias and address policy compares consistently | Unapproved alias, stale/rebound address, family change rejects | DNS/registry vectors | Provider-backed for resolution | Platform/database; target adapter |
| TLS/SNI | Approved certificate/SNI path matches expected policy | Wrong cert, SNI, hostname, expiry, revocation rejects | TLS traces | Provider-backed | Security/database; target adapter |
| Peer endpoint | Reliable peer address matches approved policy | Unavailable/mismatched peer denies | Driver evidence | Provider-backed | PostgreSQL/platform; target adapter |
| Proxy/redirect | Approved backend propagation matches identity | Unexpected proxy/backend or missing propagation denies | Redirect traces | Provider-backed | Platform/database; target adapter |
| Pooling | Checkout/reconnect revalidates identity | Cross-target reuse or unavailable revalidation denies | Pool traces | Provider-backed | Platform/database; provider validation |
| Database/schema | Approved identifier profile and authenticated session values match | Case, quote, Unicode, length, search_path, or session mismatch denies | Identifier vectors and provider evidence | Unit plus provider-backed | PostgreSQL/security; canonicalizer |
| Version | Supported authenticated version passes | Unsupported, future, unavailable, malformed version denies | Version matrix | Provider-backed | PostgreSQL owner; target adapter |
| System identity | Selected provider source matches registry | Missing, stale, substituted, or mismatched identity denies | Identity query evidence | Provider-backed | PostgreSQL owner; target adapter |

## 8. Governance and runner compatibility

Security/key-registry approval, target-identity policy approval, PostgreSQL system-identity approval, TLS/network approval, lock selection, recovery policy, provider validation, and migration-execution authorization remain separate gates.

Runner.cs currently accepts caller-supplied ApprovedTargetFingerprint and opaque Authorization, delegates identity to IExecutionPolicy, and exposes no canonicalizer, DNS/TLS adapter, peer-endpoint evidence, provider identity retrieval, registry lookup, pooling/proxy revalidation, or evidence writer. Program.cs remains disabled through NoExecutionPolicy. Injected interfaces and unit fakes do not establish actual server identity.

This revision resolves the documentation contradiction and supplies candidate rules and vectors. It does not select owner policies, implement the verifier, establish a target, run provider tests, or authorize migration execution.

**GATE T17 COMPLETE — TARGET IDENTITY CONTRACT REVISION PREPARED, OWNER APPROVAL REQUIRED**

