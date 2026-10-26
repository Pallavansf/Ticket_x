# PA_TICKETS PostgreSQL Read-Only Validation Authorization Packet

**Repository:** `G:\\WSS\\aw\\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Operation:** PostgreSQL catalog metadata reads only

This packet is a request for accountable owner decisions. It is not approval,
provider authorization, baseline adoption, or migration execution authority.

## Authorization record

| Field | Required value/evidence | Current status |
|---|---|---|
| Database/release owner | Named accountable role and verifiable approval reference | **PENDING** — owner identity: ______; approval reference: ______ |
| Approval status | Explicit `OWNER_APPROVED`, signed or otherwise verifiable | **PENDING** |
| Approval date / expiry | UTC issue and expiry; revalidate on target, artifact, policy, or privilege change | **PENDING** — issued: ______; expires: ______ |
| Revocation | Owner-controlled revocation contact and evidence source | **PENDING** — contact: ______ |
| Exact target | Historical candidate: host `localhost`; port: `5432`; database: `pa_tickets_e2e_verify`; role: `postgres` | **PENDING / TARGET CONFLICT** — later bring-up evidence names `pa_tickets_codex_e2e` / `pa_tickets_codex`; owner selection and current TGT attestation are absent |
| Target identity evidence | Independent server/database/user/port evidence plus approved hostname, DNS, peer, TLS, and SNI policy | **PENDING** — evidence reference: ______ |
| Operation scope | Read-only catalog metadata: identity, tables, columns, constraints, indexes, and sequence metadata | **PENDING** — scope owner: ______ |
| Baseline artifact | `tests/scripts/postgresql-foundational-ddl.sql` | **EVIDENCE READY; APPROVAL PENDING** |
| Baseline SHA-256 | `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7` | **VERIFIED BY SOURCE HASH; provenance/adoption PENDING** |
| Baseline provenance | Source-control provenance, exact-definition comparison, and owner adoption/rejection | **PENDING** |
| Access boundary | Dedicated restricted read-only role with catalog permissions sufficient for the fixed queries | **PENDING** — prior `postgres` superuser observation is not approval |
| Least privilege | Security/database owner decision and grant evidence | **PENDING** — decision reference: ______ |
| Evidence retention | Repository or approved evidence store, permitted contents, custody, and retention period | **PENDING** — location: ______; period: ______ |
| Protected targets | Reject `pa_tickets_test`, `pa_tickets_h_verify`, and `pa_tickets_test_user` | **NORMATIVE GUARD; owner acknowledgement PENDING** |
| Separate approvals | Schema changes, migrations, persistence tests, cleanup, and migration 026 require separate approvals | **PENDING** |

Owner identity: ____________________  
Decision date (UTC): ____________________  
Scope/environment: ____________________  
Signature or approval reference: ____________________

## User-supplied target evidence (not independently executed by Codex)

The user-selected target is localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex. The supplied SQL result reports server ::1, PostgreSQL 18.6, recovery false, transaction read-only off, and SSL false. Non-TLS IPv6 loopback is accepted for local read-only identity verification. This evidence is not a live query performed by Codex. The packet still requires attributable owner identity/role, UTC issue date, fixed expiry, approval reference, rationale, and evidence custody; its status remains PENDING / TARGET CONFLICT until those fields are supplied.

## Identity-policy questions requiring approval

A caller-supplied hostname and port do not authenticate the connected
PostgreSQL server. Before a provider run, the target-identity owner must
approve how DNS aliases, rebinding, resolved IPv4/IPv6 peer addresses, TLS
certificate/SNI identity, proxies, and pooling are checked and evidenced. The
current source verifier does not claim to establish those facts.

## Preconditions and stop conditions

The provider run must stop if any field is missing, contradictory, expired,
stale, revoked, or cannot be independently evidenced. A matching artifact hash
proves only current file-byte identity; it does not prove provenance, owner
approval, baseline adoption, or migration execution. No migration history may
be inferred from catalog shape. Migration 026 remains under its separate
authorization chain.

No credentials are recorded in this packet. No database operation is authorized
by this document.

