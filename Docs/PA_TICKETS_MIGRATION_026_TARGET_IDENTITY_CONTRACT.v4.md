# Gate T21 — PostgreSQL Identity and Network Policy Decision Proposal v4

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED**

This revision addresses Gate T20 findings and develops explicit, reviewable alternatives for PostgreSQL server identity and network evidence. It does not select an approved production policy, implement an adapter, connect to PostgreSQL, or authorize migration 026.

## 1. Gate T20 traceability

| Finding | Revision | Status |
|---|---|---|
| RFC reference inconsistency | All normative references in this document use RFC 5952. | Corrected in proposal |
| Equal-run vector wording | The vector is represented by a valid input string and a separate explanatory note. | Corrected in proposal |
| IPv6 parser approval | §3 compares parser profiles and proposes a default without claiming approval. | Owner decision required |
| Hostname-to-peer binding | §§4–6 define endpoint layers, candidate policies, comparison, and evidence. | Policy decision/provider evidence required |
| Provider identity source | §5 compares authenticated identity mechanisms, privileges, proof, and failure. | PostgreSQL-owner decision required |
| Acceptance coverage | §7 supplies positive/negative criteria, evidence, classification, owners, and blockers. | Implementation/provider evidence required |

## 2. Fixed descriptor and corrected IPv6 profile

The descriptor remains the strict, ordered record from v3:

v=1;provider=postgresql;endpoint_host=<encoded-value>;endpoint_port=<encoded-value>;database=<encoded-value>;schema=<encoded-value>;server_version_major=<encoded-value>;system_identifier=<encoded-value>;approved_target_id=<encoded-value>

Input order is mandatory. Unknown, duplicate, missing, malformed, trailing, or noncanonical records reject. Values are decoded once, normalized by field grammar, UTF-8 encoded, percent-encoded with uppercase hexadecimal for every non-ASCII-unreserved byte, then serialized in the fixed order. Descriptor evidence hashing is uppercase SHA-256 over the exact final UTF-8 bytes. The authorization payload JCS digest remains authoritative.

The proposed IPv6 profile is RFC 5952-style:

- Full or compressed hexadecimal forms are accepted.
- A final dotted IPv4 component is accepted only in the final 32 bits.
- Embedded IPv4 octets map to two 16-bit fields and never remain dotted in output.
- Lowercase hexadecimal, longest zero-run compression, and leftmost tie-breaking are used.
- Single zero fields are not compressed.
- IPv4-mapped and IPv4-compatible values remain IPv6 and never equal IPv4.
- Zone and scope identifiers, invalid octets, multiple compression markers, excess fields, and mixed invalid forms reject.

The exact parser/library and conformance suite require PostgreSQL/platform approval.

## 3. Candidate PostgreSQL server-identity mechanisms

No candidate is selected by this proposal.

| Candidate | What it proves | What it does not prove | Privileges/assumptions | Failure and evidence |
|---|---|---|---|---|
| Provider-reported system identifier through an authenticated PostgreSQL mechanism | A value returned by the authenticated server for its system identity | It does not alone prove DNS, TLS, or peer endpoint | Approved driver, query/function availability, least privilege, stable value semantics | Missing, ambiguous, stale, or changed value denies; provider query and identity evidence required |
| TLS certificate and SNI identity | Transport endpoint matches approved certificate/hostname/SNI policy | It does not prove database/schema or unique PostgreSQL system identity | Mandatory TLS, trusted CA or pin registry, hostname verification, controlled driver options | Chain, SNI, hostname, expiry, revocation, or trust failure denies; TLS traces and rotation evidence required |
| Approved proxy-attested backend identity | A trusted proxy reports the backend identity | It does not prove an unapproved backend or direct endpoint | Signed/secured propagation contract, trusted proxy, backend binding | Missing propagation or backend change denies; proxy and backend-switch evidence required |
| Database/schema/server version | Authenticated session context and compatibility | It is not a unique server identity | Authenticated session and approved query source | Mismatch or unavailable value denies; provider session evidence required |
| Server start time or connection metadata | Diagnostic correlation | It is not stable unique identity | Driver exposes values | Never sufficient for authorization; no acceptance as identity proof |
| TLS plus provider identity combination | Stronger endpoint and server binding | Depends on both policies and sources being trustworthy | All requirements of both mechanisms | Any missing layer denies; combined provider/TLS evidence required |

The PostgreSQL owner must approve the selected source, query/function, privileges, stability assumptions, version range, and evidence format. Caller-provided identity, connection-string text, environment values, DNS names, or endpoint equality are never observed identity.

## 4. Proposed TLS policy alternatives

| Policy | Proposed behavior | Benefit | Risk/trade-off | Owner/evidence |
|---|---|---|---|---|
| Mandatory TLS with CA validation | Require TLS, validate chain to approved CA, hostname and SNI, expiry and revocation policy | Strong transport and endpoint assurance | Requires CA lifecycle and driver configuration | Security/database owner; handshake, rotation, wrong-server tests |
| Mandatory TLS with certificate pinning | Require approved leaf/intermediate pin set plus SNI | Limits CA substitution | Rotation and emergency recovery complexity | Security owner; pin rotation and expiry tests |
| TLS plus provider identity | Require TLS and authenticated PostgreSQL identity | Binds transport and server layers | More provider dependencies | Security/PostgreSQL owner; combined evidence |
| Optional TLS | Allow provider identity without mandatory TLS | Compatibility | Weaker network substitution resistance | Only with security threat-model approval; otherwise deny |

Recommended default for review: mandatory TLS with approved CA/SNI validation plus provider identity. This is a proposal, not an approval. Missing TLS, certificate, SNI, revocation, or provider identity evidence denies.

## 5. DNS, address, peer, proxy, and pooling policy alternatives

### 5.1 DNS and addresses

Recommended default for review: resolve the signed hostname through an approved resolver, record resolver identity/time/answers, require freshness, and restrict answers to an owner-controlled alias/address policy. Rebinding, stale answers, unregistered aliases, changed address family, and ambiguous answers deny.

Alternatives are pinned addresses or a trusted proxy. DNS resolution alone never proves server identity.

IPv4 and IPv6 selection must preserve address family. IPv4-mapped IPv6 is not equivalent to IPv4. A selected address must match reliable peer evidence or an explicitly approved substitute.

### 5.2 Peer endpoint

If the driver exposes a peer address reliably bound to the authenticated connection, record it and compare it with the approved address policy. If unavailable, deny unless the approved policy supplies authenticated TLS/proxy evidence with equivalent assurance. A local socket descriptor or caller-reported endpoint is not sufficient.

### 5.3 Proxies and redirection

Default proposal: disallow unapproved proxies and redirects. An approved proxy must propagate backend identity through a documented, authenticated contract and must be recorded in evidence. Unexpected backend, redirect, or missing propagation denies.

### 5.4 Connection pooling

Default proposal: revalidate target identity at pool checkout and after reconnect, bind pools to one target descriptor, and deny cross-target reuse. An implementation that cannot prove pool binding must disable pooling for this operation. Required evidence includes checkout, expiration, reconnect, and backend-switch tests.

All defaults above require platform/security/database owner approval.

## 6. Expected-versus-observed comparison

The verifier records:

1. Signed expected descriptor and target registry version.
2. Configured endpoint and connection options.
3. DNS answers, aliases, resolver freshness, and selected address.
4. Peer endpoint evidence.
5. TLS certificate, hostname, and SNI evidence.
6. Authenticated PostgreSQL database, schema, major version, and system identity.
7. Final comparison result, failure code, timestamps, and redacted evidence references.

Comparison occurs before BEGIN and any migration command. Every required field and selected evidence layer must match the approved policy. Missing, stale, ambiguous, contradictory, or unauthenticated evidence denies. No caller-supplied value can fill an observed field.

## 7. Acceptance matrix

| Requirement | Positive criterion | Negative criterion | Evidence | Classification | Owner/block |
|---|---|---|---|---|---|
| RFC 5952 parser | Full/compressed vectors and equal-run tie-break produce one canonical output | Multiple compression, invalid octet, zone, family substitution rejects | Conformance vectors | Unit | Platform/PostgreSQL; canonicalizer |
| Descriptor serialization | IPv6 colons become uppercase percent escapes and exact bytes hash consistently | Raw delimiters, alternate escapes, reordered fields reject | Byte fixtures and digest | Unit | Security; canonicalizer |
| Provider identity | Selected mechanism returns stable identity on authenticated connection | Missing, changed, stale, unavailable, or substituted identity denies | Provider identity trace | Provider-backed | PostgreSQL owner; target adapter |
| TLS/SNI | Approved chain, hostname, SNI, expiry, and revocation pass | Wrong chain, SNI, hostname, expiry, revocation, or absent TLS denies | TLS handshake/rotation evidence | Provider-backed | Security/database; target adapter |
| DNS | Fresh approved answer and alias policy match | Rebinding, stale answer, unregistered alias, changed address denies | Resolver evidence | Provider-backed | Platform/database; target adapter |
| Peer endpoint | Reliable peer address matches approved address policy | Unavailable or mismatch denies unless approved substitute exists | Driver endpoint evidence | Provider-backed | PostgreSQL/platform; target adapter |
| Proxy/redirect | Approved backend identity propagates and matches | Unexpected redirect/backend or missing propagation denies | Proxy/backend traces | Provider-backed | Platform/database; validation |
| Pooling | Checkout/reconnect revalidation prevents cross-target reuse | Pool substitution or unavailable revalidation denies | Pool/reconnect traces | Provider-backed | Platform/database; validation |
| Version/database/schema | Authenticated values are supported and match expected descriptor | Unsupported, malformed, changed, or unavailable values deny | Provider query/version evidence | Unit plus provider-backed | PostgreSQL owner; adapter |
| Evidence | Redacted comparison record is complete and auditable | Missing, ambiguous, or failed evidence write denies | Evidence artifact and access audit | Implementation/provider | Release/compliance; execution |

## 8. Decision register

| Decision | Recommended default | Alternative | Owner role | Required evidence | Blocking stage |
|---|---|---|---|---|---|
| System identity source | Authenticated provider identity plus mandatory TLS | Approved proxy-attested identity | PostgreSQL owner | Query, privilege, stability, mismatch tests | Target adapter |
| TLS | Mandatory CA/SNI validation | Pinning or approved threat-model exception | Security/database owner | TLS and rotation tests | Target adapter |
| DNS/address | Fresh resolver plus target registry and family preservation | Pinned addresses/proxy | Platform/database owner | Rebinding/address evidence | Target adapter |
| Peer endpoint | Require reliable peer evidence or approved equivalent | Disable peer-dependent paths | PostgreSQL/platform owner | Driver evidence | Provider validation |
| Proxy/redirect | Disallow unless authenticated propagation approved | Dedicated trusted proxy | Platform/database owner | Backend-switch tests | Provider validation |
| Pooling | Revalidate checkout/reconnect or disable pooling | Alternative pool binding | Platform/database owner | Pool tests | Provider validation |
| Identifier/version profile | v3 conservative profile and owner allowlist | PostgreSQL-native profile | PostgreSQL/security owner | Identifier/version matrix | Canonicalizer |
| Evidence custody | Approved audited store | Compliance archive | Release/compliance owner | Storage/access evidence | Execution approval |

Security/key-registry, target/network, PostgreSQL identity, locking, recovery, provider validation, and migration-execution approvals remain independent.

## 9. Runner compatibility and status

Runner.cs still accepts caller-supplied ApprovedTargetFingerprint and opaque Authorization, delegates identity to IExecutionPolicy, and has no concrete identity, TLS, DNS, peer, proxy, pool, registry, or evidence adapters. Program.cs remains disabled through NoExecutionPolicy. Interfaces and unit fakes do not prove server identity.

This proposal corrects the RFC reference and equal-run wording in a new version only. It does not modify v3, implement policy, establish a target, run tests, claim provider behavior, or authorize migration execution.

**GATE T21 COMPLETE — IDENTITY POLICY PROPOSAL PREPARED, OWNER APPROVAL REQUIRED**

