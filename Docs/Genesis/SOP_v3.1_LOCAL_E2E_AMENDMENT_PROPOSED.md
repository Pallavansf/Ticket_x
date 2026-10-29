# Proposed Genesis SOP v3.1 Amendment â€” Sole-Developer Local PostgreSQL E2E

**Status:** PROPOSED â€” not adopted or effective
**Amends:** Genesis SOP v3, Â§Â§7.2 and 8, for the narrowly defined local E2E scope below
**Owner direction:** Mr. H, Genesis, Developer & DBA, authorized preparation of this amendment on 2026-10-28. This records direction to prepare; it is not final-text adoption, a database-operation authorization, or an independently authenticated identity claim.

## 1. Purpose and limited applicability

This amendment proposes a proportionate owner-controlled verification path for
the sole-developer PA_TICKETS local PostgreSQL E2E environment. Within this
scope, the owner may act as accountable approver, operator, and verifier. The
records must identify these roles as held by the same person; they must not
describe this as independent review.

The exact allowed target is `localhost:5432`, database
`pa_tickets_codex_e2e`, role `pa_tickets_codex`, with connections constrained
to IPv6 loopback `::1`. Any other host, address family, port, database, role,
or resolution outcome is denied. This amendment does not apply to production,
shared, customer, protected test, or any other database environment.

This amendment changes no other Genesis requirement. It does not adopt a
baseline, resolve migration history, approve grants or schema changes, or
authorize a provider test. Migration 026 remains separately prohibited under
the existing restriction; migration 038 is unaffected.

## 2. Owner-controlled local verification controls

Before an individually authorized local operation, the verifier must:

1. Confirm the exact target and operation from a signed, single-operation
   authorization. Bind the authorization to the target, operator account,
   operation name, exact artifact path and SHA-256, expected effects, and
   explicit exclusions. The only operation contemplated for initial use is
   the named S5-03 read-only catalog fingerprint. No wildcard artifacts,
   ambient PostgreSQL target overrides, or fallback targets are allowed.
2. On Windows, collect the `::1:5432` listening socket and owning PID from the
   OS; resolve the PID to process start time, executable path and SHA-256,
   Authenticode signer, command-line identity fields needed to locate the
   instance, Windows service name/PID/account, and PostgreSQL data and
   configuration paths. Redact command-line values that could contain
   credentials. Verify these facts against an owner-approved local instance
   policy before connecting.
3. Protect the owner signing key with the Windows certificate/CNG key store as
   a non-exportable key, restricted to the owner account. Pin its public
   certificate thumbprint in the local verifier policy. Use a separate,
   owner-controlled recovery/revocation key, kept unavailable to the routine
   operation process, to sign the current revocation statement; the ordinary
   evidence-signing key must not be able to declare itself unrevoked after
   compromise. Both keys remain under one owner's control, so this is not
   independent attestation. Key creation, installation, rotation, recovery,
   and thumbprint changes must be recorded as security-policy changes and must
   fail closed until the local verifier policy is updated through the
   documented owner process.
4. Require a fresh owner-signed revocation statement checked at each
   invocation. The statement must identify the authorization and peer-policy
   versions, state active/revoked, include issue/expiry UTC times, and be signed
   by the separately pinned recovery/revocation key. Missing, expired,
   malformed, mismatched, or revoked status denies the operation. A maximum
   five-minute freshness window is proposed for peer/revocation evidence; a
   single-operation authorization expires within 30 minutes. Peer evidence
   must be no more than two minutes old when the operation begins.
5. Fail closed if Windows process/socket facts, the pinned certificate, the
   signed authorization, a current revocation statement, artifact hash, or
   exact target cannot be verified. A password login or server-reported
   address is not process identity proof. Do not read credentials during
   preflight; the approved credential source remains outside logs and command
   arguments.

The PostgreSQL role must be separately evidenced as least-privilege for the
single approved operation. It must not be superuser, have `CREATEDB`,
`CREATEROLE`, `REPLICATION`, or `BYPASSRLS`, inherit a privileged role, create
objects in the database/schema, or hold INSERT/UPDATE/DELETE/TRUNCATE/DDL or
sequence-advance privileges. The operation may use CONNECT to this database,
USAGE on `public`, and SELECT only on system catalogs and the exact application
relations read by the pinned fingerprint artifact; that relation allowlist
must be derived from and bound to that artifact for each authorization. No
access to another database is included. Owner approval of this amendment does
not provision or alter database privileges. The fingerprint runs read-only.
This amendment does not permit automatic schema writes, migrations, test
writes, provisioning, seeding, sequence operations, or cleanup. Each future
write or cleanup scope requires a separate amendment/decision and
operation-specific authorization.

## 3. Audit evidence and assurance limits

For each attempt, retain a sanitized owner-controlled record containing the
authorization and artifact hashes, exact target, Windows listener/process and
service facts, signer thumbprint, verifier version, UTC decision time,
revocation evidence identifier and freshness, allow/deny result, and sanitized
operation output. Exclude passwords, tokens, unrestricted command lines, and
unrelated catalog data. Protect the evidence directory with owner-only write
access; export and hash the evidence record at completion. Record denied and
uncertain attempts. On any uncertain outcome, stop, do not retry, mark the
result `UNKNOWN`, and require a new read-only reconciliation authorization.

This model makes the owner's verification attributable and auditable; it does
not provide separation of duties or tamper resistance against the local
administrator. The owner can control the OS, local trust store, verifier, key
policy, and evidence custody. It does not prove host/kernel integrity or
independent server provenance. If the threat model later includes a malicious
or compromised local administrator, this amendment is insufficient and the
operation must remain blocked pending a separately governed managed-device or
external-attestation control.

## 4. Adoption and operation gates

This is a proposed versioned amendment only. SOP v3 does not define a complete
amendment workflow. The canonical owner decision pack's existing proposed
change path calls for an attributable project-owner decision, database/security
review, consistency review against the FRD, SLICES, and roadmap, and adoption
with an approval reference and effective date. This proposal does not silently
waive those recorded steps. The owner must explicitly adopt the exact final
text and scope, supply the required attributable approval reference and
effective date, and record any owner decision that changes the prior review
path. The independent-review requirement is proposed to be replaced only for
this exact environment; no independent reviewer is asserted or invented.

Even after amendment adoption, the first S5-03 operation requires a separate
owner-signed authorization naming the exact query artifact/hash, target,
operator, least-privilege evidence, validity, revocation, recovery/uncertain
outcome handling, and evidence custodian. This amendment itself never enables
runner Execute mode. Execute mode remains disabled until the verifier is
implemented and tested against the adopted policy and the separate S5-01
authorization is complete.

## 5. Adoption record (intentionally incomplete)

- Final adopted version and exact-text hash: **Outstanding**
- Owner final adoption decision and attributable approval reference/signature: **Outstanding**
- Effective date/time UTC: **Outstanding**
- Consistency review against FRD/SLICES/roadmap and technical security controls: **Outstanding**
- Owner-controlled certificate thumbprint, key custody, local policy and revocation mechanism: **Not provisioned or verified**
- S5-01 read-only operation authorization: **Not authorized by this proposal**

## Owner-approved simplified local read-only policy direction — addendum (2026-10-28)

This addendum records the owner's later simplification direction without
rewriting the previously reviewed amendment body or its historical approved
SHA-256 (`B78980670B1E0E8E79C333A36D7B93F46633C02B2172088C8B33469BED28A18E`).
The owner-provided policy direction permits exactly two read-only operations
against `::1:5432`, database `pa_tickets_codex_e2e`, role `pa_tickets_codex`,
using the existing `PA_TICKETS_E2E_DB_PASSWORD` loader and the two raw-byte
artifact hashes recorded in the canonical owner decision pack: privilege
preflight `C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C`
and S5-03 fingerprint
`2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8`.

For these operations only, the direction replaces CNG keys, signed approval
and revocation envelopes, ProgramData trust/custody provisioning, and repeated
per-operation owner forms. It retains strict target/artifact allowlists,
same-connection read-only transaction and target checks before catalog SQL,
credential secrecy, bounded timeouts, sanitized evidence under the project
`test-results/evidence` path, and stop/no-retry handling. It does not verify the
Windows PostgreSQL process/executable. Writes, schema changes, migrations,
provisioning, grants, seeds, provider-test writes and cleanup remain separately
controlled. All other environments retain their existing independent identity
and authorization requirements.

The canonical decision pack records the supplied owner direction and actual
record-entry timestamp. No owner-message timestamp, digital signature, live
privilege fact, or database execution is asserted by this addendum.

