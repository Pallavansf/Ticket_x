# PA_TICKETS — Export Lifecycle Owner Approval Checklist

**Review date:** 2026-10-16  
**Status:** decision-ready; no owner approvals recorded  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

No item is marked APPROVED because the repository contains no explicit owner approval record.

| Decision ID | Decision | Recommended default | Alternatives and consequences | Required owner/approval | Dependencies | Status |
|---|---|---|---|---|---|---|
| AUTH-1 | Historical download semantics | Keep downloads fail-closed until a strategy is implemented and verified | Regenerate changes original bytes and repeat-download fidelity; snapshot preserves bytes but needs persistence | Product + Security + Data | Row-level evidence or regeneration contract | BLOCKED |
| AUTH-2 | Authorization strategy | Prefer an approved authorization-context snapshot only if it proves every row/field; otherwise choose regeneration if product accepts changed output | Snapshot needs schema/versioning; regeneration repeats queries and may time out | Product + Security + Provider owner | AUTH-1, AUTH-3 | PENDING |
| AUTH-3 | Minimum artifact evidence | Exact included rows/fields or a demonstrably complete authorization proof, plus requester, normalized filters, report/field versions, effective scopes, grant context, hash, and generation time | Scope-only or signed metadata records provenance but cannot prove row authorization | Security + Data | Definitions and scope/version sources | BLOCKED |
| AUTH-4 | Invalidation | Revoke access on permission, scope, account, report-definition, and field-policy changes; legacy artifacts without proof remain denied | Permanent owner-bound access is incompatible with current fail-closed policy | Security + Data | AUTH-3, persistence contract | BLOCKED |
| AUD-1 | Lifecycle event catalogue | Request, success, failure, download, denial, expiry, deletion, and cleanup-failure events using existing audit chain conventions | Separate operational logging loses established chain integrity unless explicitly approved | Audit/Security owner | Actor and transaction decisions | PENDING |
| AUD-2 | Actors | Authenticated numeric user for user events; explicit approved system identity for worker events | Substituting an administrator is prohibited; nullable/legacy representation requires schema approval | Security + Data + Provider owner | Non-null ActorId/FK contract | BLOCKED |
| AUD-3 | Transaction ownership | Persist export audit with the owning transaction where one context can safely own both; otherwise approve durable retry/outbox semantics | Cross-context TicketsDbContext/ExecutionDbContext/storage atomicity is unproven | Architecture + Provider owner | AUD-1, AUD-2 | BLOCKED |
| AUD-4 | Audit failure | Denials remain fail-closed; creation/download/deletion behavior follows an explicit outage policy rather than an implicit global rule | Blocking protects accountability but harms availability; retry permits temporary audit gaps | Security + Product + Operations | AUD-3 and event criticality | PENDING |
| AUD-5 | Retry/duplicates | Stable event identity and idempotent interpretation across worker retries | Duplicate rows may remain but must be marked/interpretable if exact dedupe is unavailable | Audit + Operations | Job correlation/dedupe | PENDING |
| CLEAN-1 | Expiry versus deletion | ExpiresAt immediately blocks access; physical deletion is asynchronous and never restores access | GET-time deletion risks writes and races; retaining files increases storage exposure | Security + Operations | AUTH-1, retention decision | PENDING |
| CLEAN-2 | Worker behavior | Use existing durable lease, retry, cancellation, and exhausted/dead-letter capabilities | Process-local locks do not coordinate multiple instances | Operations + Architecture | BackgroundJobRepository and handler contract | PENDING |
| CLEAN-3 | Race semantics | Approve a download/cleanup linearization point and storage lease/atomic-read guarantee | LocalFileStorage alone does not prove distributed safety | Architecture + Storage owner | CLEAN-2, provider/storage evidence | BLOCKED |
| CLEAN-4 | Failure handling | Missing files are explicit idempotent outcomes; delete/persistence/audit failures remain retryable or alertable; crashes never restore access | Best-effort deletion without durable outcome loses evidence | Operations + Audit owner | CLEAN-2, AUD-3 | PENDING |
| RET-1 | Retention | Treat seven days as provisional pending explicit confirmation | Different retention affects storage cost, privacy, and product expectations | Data-retention + Product + Operations | Cleanup eligibility and monitoring | PENDING |
| REL-1 | Restore downloads | Require approved strategy, authorization tests, non-disclosure tests, API build, and legacy-artifact policy | No restoration until current authorization is provable | Security + Product + Release owner | AUTH-1..4 | BLOCKED |
| REL-2 | Enable cleanup | Require lease/race contract, retry/dead-letter tests, storage guarantees, monitoring, and audit policy | Manual cleanup leaves operational exposure; uncoordinated automation risks deletion races | Operations + Architecture | CLEAN-1..4, RET-1 | BLOCKED |
| REL-3 | Audit completeness | Require event coverage, actor contract, redaction, chain verification, transaction/retry evidence, and provider tests | Source-only calls do not prove persistence | Audit + Security + Provider owner | AUD-1..5 | BLOCKED |
| REL-4 | Database/provider execution | Require target fingerprint, migration baseline, hashes, backup, rollback, preservation checks, and exact-operation approval | No migration or provider claim from static inspection | Database owner + Release owner | All schema-dependent decisions | BLOCKED |
| REL-5 | Full E2E readiness | Require approved PostgreSQL/Oracle evidence, hosted API tests, Angular/browser journeys, and archived results | Mocked/provider-independent tests remain limited evidence | QA + Release owner | REL-1..4 | BLOCKED |

## Recommended Approval Order

1. Approve original-artifact versus regenerated-output semantics.
2. Approve row-level authorization evidence, invalidation, and legacy-artifact behavior.
3. Approve audit event names, actors, transaction ownership, failure policy, and retry identity.
4. Confirm retention, cleanup eligibility, worker lease, cancellation, race, and storage guarantees.
5. Add provider-independent contract tests and implement only the approved authorization path.
6. Approve provider/schema changes with baseline, backup, rollback, and preservation evidence.
7. Run provider-backed persistence and transaction tests.
8. Run hosted API and Angular E2E, then evaluate production-readiness gates.

## Release Gates

### Authorization

- Current permission, active-account, and applicable scope checks are verified.
- The selected strategy proves every returned row and field or regenerates under an approved contract.
- Legacy artifacts, revocation, scope reduction, deactivation, definition changes, and field-policy changes are tested.
- Non-owner requests remain non-disclosing.
- Downloads remain fail-closed until this gate passes.

### Auditing

- All required lifecycle events are emitted through the approved existing audit architecture.
- User and worker actors satisfy the approved non-null actor contract.
- Correlation and idempotency keys are stable.
- Sensitive data is redacted.
- Transaction, retry, duplicate, and audit-outage semantics are tested.
- Hash-chain integrity is verified in an approved provider phase.

### Cleanup

- ExpiresAt blocks access independently of file deletion.
- Durable leasing, idempotent deletion, cancellation, retry, dead-letter, crash recovery, and missing-file behavior are tested.
- Download/cleanup concurrency and multi-instance storage guarantees are approved.
- Monitoring and alerting are operational.
- Seven-day retention is explicitly confirmed or replaced.

### Provider verification

- PostgreSQL and Oracle targets, migration baseline, immutable file hashes, backup, rollback, and preservation assertions are approved.
- Provider-specific transaction, locking, actor-FK, and storage persistence tests pass.
- No provider claim is based solely on checked-in schema or source.

### E2E

- Hosted API tests verify authorization, expiry, non-disclosure, audit visibility, cleanup, and no unintended mutation.
- Angular/browser journeys verify login, report/export flows, denied/expired states, and error handling.
- Results are archived with commands, environment fingerprints, and evidence boundaries.
- Mocked UI tests are not counted as database-to-UI proof.

## Current status

All decisions remain PENDING or BLOCKED. No application behavior, schema, migration, permission, grant, scope, audit, or cleanup implementation is approved by this checklist. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


## Decision briefing cross-reference (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_LIFECYCLE_DECISION_BRIEFING.md` for the consolidated questions, recommended defaults, alternatives, dependencies, joint-approval groups, and release evidence. Decision statuses remain unchanged.
