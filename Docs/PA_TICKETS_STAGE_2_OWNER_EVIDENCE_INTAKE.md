# PA_TICKETS Stage 2 — Owner Evidence Intake

This checklist is a submission aid. It records required evidence; it does not
grant approval and does not authorize provider execution.

## Gate evidence

| Gate | Required decision/evidence | Minimum attribution fields | Acceptance criteria | Prohibited until accepted |
|---|---|---|---|---|
| TGT | Approve localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex and its server-authentication policy | Owner role and identity, UTC decision date, scope, expiry, approval reference, independent identity record | Fresh read-only evidence independently identifies server, database, role, host/port, recovery/read-only state, and approved hostname/peer/TLS expectations | Any provider connection or operation |
| OP | Approve a dedicated least-privilege operator and exact actions, or a scoped exception | Role name, database/schema/object/function grants, allowed operation, scope, expiry, owner attribution and reference | Grants are sufficient for the named operation and cleanup, limited to the target, and do not rely on superuser inference | Schema changes, writes, cleanup, privilege changes |
| BASE | Adopt one immutable foundational artifact/version | Repository or artifact URI/version, raw-byte SHA-256, provenance, definition review, owner identity/date/scope/reference | Hash is independently reproducible from the immutable bytes and the owner explicitly adopts that artifact | Baseline reconciliation or migration claims |
| PRE | Approve the fail-closed preflight contract bound to TGT, OP, BASE and operation | References to all three gates, retention/custody location, expiry, owner attribution | Evidence references are present, mutually consistent, unexpired, and the verifier rejects missing/mismatched records | Any provider execution |
| OP-TEST | Authorize only Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts | Exact test name, target, writes, cleanup statements/function, recovery boundary, evidence retention, owner identity/date/reference/expiry | Generated identifiers are unique; writes and cleanup are test-owned; uncertain commit or cleanup stops execution; no unrelated tests run | All other provider tests, migrations, DDL, RBAC changes |

The existing PostgreSqlProviderPreflight operation
READ_ONLY_CATALOG_RECONCILIATION does not authorize OP-TEST and must not be
widened implicitly.


### Target decision still unresolved

Two repository records declare different candidates: the historical read-only authorization packet names localhost:5432 / pa_tickets_e2e_verify / postgres, while the later bring-up result names localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex. Neither record contains the current attributable TGT attestation required by this intake. The owner must select exactly one target and record its authentication/hostname/peer/TLS policy, scope, expiry, and approval reference. Independent server/database/role evidence must then be captured immediately before the authorized operation. No target is selected by this checklist.


### User-supplied TGT input (formal evidence incomplete)

The owner selected the codex E2E candidate and supplied a SQL result showing database `pa_tickets_codex_e2e`, role `pa_tickets_codex`, server `::1`, port `5432`, PostgreSQL 18.6, recovery `false`, transaction read-only `off`, and SSL `false`. Non-TLS IPv6 loopback is accepted as the stated local read-only policy. This result was supplied by the user and was not independently queried by Codex.

The repository requires attributable identity/role, verifiable approval reference, UTC issue date, and fixed expiry; an until-revoked policy is not the recorded convention. The labels `Mr.H & SE` and `Mr.H` are retained as supplied but do not establish those requirements. TGT remains `PENDING`.

## D01–D10 owner decisions

Use Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md as the authoritative
decision record. Every formal decision remains PENDING unless the owner
supplies an attributable response in its existing fields.

- D01: adopt or reject the exact foundational baseline after definition/hash review.
- D02: approve a dedicated least-privilege operator or a scoped exception.
- D03: approve sequence/key reconciliation without unsafe reset or advance.
- D04: approve canonical role/permission codes and alias handling.
- D05: approve historical grant effective-date/backfill and fail-closed policy.
- D06: approve client identity and scope foreign-key semantics.
- D07: approve explicit authored versus derived scope/delegation records; the existing owner-proposed preference is not formal approval.
- D08: approve five built-in roles separately from any grant matrix.
- D09: approve explicit unknown migration-history treatment without fabricated ledger rows.
- D10: approve actor types, nullable/FK behavior, writer/reader compatibility, and hash-chain preservation; the existing owner-proposed preference is not formal approval.

For each decision, supply owner identity, UTC date, scope, response
(APPROVE/REJECT/DEFER), rationale, evidence references, and an approval or
signature reference. Until then, the decision's documented downstream
migrations and API/provider acceptance remain blocked.

## D11/P26

Migration 026 remains a separate approval chain. Its exact artifact,
target/identity, lock and recovery contract, provider validation, execution
window, and evidence retention must be approved independently. Baseline or
Stage 2 approval does not authorize migration 026.

## Submission and custody

The repository contains no configured owner contact or approved submission
integration. The accountable owner must select an approved external channel
and retain the attributable result. No identity, signature, date, expiry,
approval ID, or external submission is supplied by this document.
