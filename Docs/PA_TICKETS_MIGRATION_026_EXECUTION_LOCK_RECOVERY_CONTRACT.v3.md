# Gate T27 — Migration 026 Lock and Recovery Contract Proposal v3

Status: PROPOSAL FOR OWNER REVIEW — INDEPENDENT AUDIT REQUIRED

This documentation-only revision addresses Gate T26 findings. It does not implement the runner, select a production lock, claim PostgreSQL behavior, or authorize migration 026.

## 1. Scope and governance

Scope is migration 026 and candidate SHA-256 97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468DAE49. Security, authorization, target identity, database-owner lock, recovery, provider validation, and migration-execution approvals remain independent.

Recovery completion never authorizes a new migration. A new attempt requires fresh authorization, replay decision, independently verified target identity, exact candidate hash, baseline/backup readiness, and separate execution approval.

## 2. Complete state inventory

States:

PreflightPending, PreflightFailed, AuthorizationConsumed, ConnectionOpened, TransactionStarted, LockAcquisitionPending, LOCK_OWNERSHIP_UNCERTAIN, LockAcquired, Executing, PostconditionsChecking, PRECOMMIT_EVIDENCE_FAILURE, CommitRequested, Committed, COMMITTED_EVIDENCE_PENDING, RollbackRequested, RolledBack, RollbackUnconfirmed, UNKNOWN_COMMIT, ReconciliationPending, ReconciledCommitted, ReconciledRolledBack, ReconciledPartial, ReconciledIndeterminate, RecoveryDecisionPending, RecoveryExecuting, PostRecoveryVerification, RecoveryCompleted, RecoveryFailed, RecoveryIndeterminate, RecoveryRequired, and Failed.

Every record binds attempt ID, authorization replay tuple, migration ID/hash, expected target, target-verification evidence, lock events, state timestamps, evidence durability state, and custody references.

| From | Trigger/evidence | Next state | Retry and approvals |
|---|---|---|---|
| PreflightPending | Preflight fails before connection; redacted audit | PreflightFailed | No connection; replay disposition remains owner decision |
| PreflightPending | Atomic replay consume and audit succeed | AuthorizationConsumed | No retry in attempt |
| AuthorizationConsumed | Approved connection and identity evidence | ConnectionOpened | Target policy must be approved |
| AuthorizationConsumed | Ambiguous connection/identity outcome | RecoveryRequired | No retry until replay/recovery decision |
| ConnectionOpened | Transaction begins conclusively | TransactionStarted | Provider evidence required |
| TransactionStarted | Lock request starts | LockAcquisitionPending | Same physical connection |
| LockAcquisitionPending | Ownership conclusively recorded | LockAcquired | Provider evidence required |
| LockAcquisitionPending | No ownership conclusively proven | Failed | No execution |
| LockAcquisitionPending | Loss, termination, timeout, or conflicting ownership | LOCK_OWNERSHIP_UNCERTAIN | No execution; reconcile |
| LockAcquired | Ownership evidence durable | Executing | Lock held throughout |
| Executing | Candidate returns without confirmed error | PostconditionsChecking | No commit until checks/evidence |
| Executing/PostconditionsChecking | Confirmed pre-commit error | RollbackRequested | Preserve original error |
| PostconditionsChecking | Checks pass but required evidence write fails before commit | PRECOMMIT_EVIDENCE_FAILURE | Do not request commit |
| PRECOMMIT_EVIDENCE_FAILURE | Evidence repaired and rollback/lock outcome conclusive | RollbackRequested | No commit; replay disposition audited |
| PRECOMMIT_EVIDENCE_FAILURE | Rollback or release ambiguous | RollbackUnconfirmed | Recovery required |
| PostconditionsChecking | Checks and evidence durable | CommitRequested | Commit only with durable record |
| CommitRequested | Commit conclusive and success evidence durable | Committed | Terminal success |
| CommitRequested | Commit conclusive, evidence missing/ambiguous | COMMITTED_EVIDENCE_PENDING | Known commit; never rerun |
| CommitRequested | Commit non-conclusive | UNKNOWN_COMMIT | Non-retryable; reconcile |
| RollbackRequested | Rollback conclusive and evidence durable | RolledBack | No success |
| RollbackRequested | Rollback/release/evidence ambiguous | RollbackUnconfirmed | Recovery required |
| COMMITTED_EVIDENCE_PENDING | Evidence repair links to known commit | Committed | No migration rerun |
| COMMITTED_EVIDENCE_PENDING | Repair fails or state must be checked | ReconciliationPending | No migration rerun |
| UNKNOWN_COMMIT/RollbackUnconfirmed | Recovery begins | ReconciliationPending | Read-only reconciliation |
| ReconciliationPending | Read-only evidence classifies outcome | ReconciledCommitted/RolledBack/Partial/Indeterminate | Separate recovery decision |
| Reconciled* | Authorized restore/forward-repair/quarantine plan | RecoveryDecisionPending | Recovery authorization required |
| RecoveryDecisionPending | Approved plan begins | RecoveryExecuting | Recovery executor separately authorized |
| RecoveryExecuting | Restore/repair/quarantine operation ends | PostRecoveryVerification | No migration execution |
| PostRecoveryVerification | Required manifests/invariants pass | RecoveryCompleted | Does not authorize migration |
| PostRecoveryVerification | Verification fails or is ambiguous | RecoveryFailed or RecoveryIndeterminate | Quarantine; owner decision |
| RecoveryFailed/Indeterminate | No safe conclusion | RecoveryRequired | No new attempt |
| RecoveryCompleted | Fresh approvals and identity checks later obtained | PreflightPending for a new attempt | New replay/approval required |

No uncertain state can transition directly to migration execution. RecoveryRequired means unresolved recovery, not completed recovery or a safe target.

## 3. Proposed PostgreSQL reconciliation procedure

This is a proposal requiring PostgreSQL/database-owner review and provider validation, not an approved runbook.

Before reconciliation, independently verify target identity and candidate hash. Preserve pre-migration baseline, backup references, evidence timestamps, collection method, source connection identity, and integrity hashes.

Read-only observations must include required migration-specific tables, columns, types, nullability, defaults, constraints, indexes, ownership/dependency relationships, sequence existence/ownership/current state, default dependencies, migration-specific rows, expected presence/absence, postcondition manifests, baseline comparison, observable transaction/session evidence where exposed, collection time, tool/provider version, source identity, and tamper-evident hashes.

| Outcome | Sufficient evidence proposal | Insufficient alone |
|---|---|---|
| ReconciledCommitted | Required objects/definitions, sequence/default relationships, rows, and postconditions match the approved candidate with no contradictory partial state | Connection ID, lock disappearance, client success, catalog similarity |
| ReconciledRolledBack | Pre-migration baseline remains intact, candidate changes are absent as specified, and no partial changes exist | Missing ledger row, rollback exception, closed connection |
| ReconciledPartial | Some candidate changes exist while definitions/rows/postconditions or baseline invariants conflict | One object or one row count |
| ReconciledIndeterminate | Evidence conflicts, is incomplete, unavailable, or cannot distinguish outcomes | Any client-error or lock-state assumption |

If migration 026 has no durable ledger or manifest, current-state comparison cannot prove historical execution by itself. Any outcome not uniquely established remains indeterminate. Restore, forward repair, or quarantine requires separate explicit approval.

## 4. Lock mechanisms, fencing, and safe subsequent attempts

No production mechanism is selected.

- Transaction advisory lock: safe subsequent execution requires proof the original transaction ended and lock released; lock disappearance alone is insufficient.
- Session advisory lock: require session identity, explicit release, pool disablement/revalidation, and proof old session cannot continue.
- Durable lock record: ineligible for initial implementation unless provider-backed fencing enforcement is demonstrated.

For durable design, atomic claim creates owner and fencing token; every migration statement and postcondition operation executes under a provider-enforced token check tied to the same transaction/session. Renewal/expiry require trusted clock policy. Token change causes old-owner operations to fail before each operation or equivalent database guard. Loss, termination, token mismatch, or renewal failure denies and enters ownership uncertainty/recovery. Lease expiry alone never proves former-owner inactivity.

A later attempt requires mechanism-specific evidence proving prior owner cannot execute and lock is safely available, plus fresh authorization and target verification. Otherwise quarantine continues.

## 5. Evidence failure and replay durability

PRECOMMIT_EVIDENCE_FAILURE is distinct from COMMITTED_EVIDENCE_PENDING:

- Before commit request: do not request commit; preserve original and evidence-write failures; request rollback; record release ambiguity; replay disposition remains the security/compliance owner decision.
- After known commit: enter COMMITTED_EVIDENCE_PENDING; never claim rollback or ordinary retry; repair evidence or reconcile.

Every record binds attempt ID, authorization replay tuple, migration hash, target identity, lock events/token, transaction state, final/reconciled outcome, evidence durability state, and custody references.

Replay/audit storage should be transactionally coupled where possible. If atomicity cannot be guaranteed, record the crash window and require reconciliation before any future attempt. Security/compliance owners decide consume versus release after preflight failure, rollback, ownership uncertainty, unknown commit, and recovery; each disposition is audited and cannot silently authorize duplicate execution.

## 6. Acceptance and provider evidence

| Requirement | Positive | Negative | Evidence/classification | Owner/block |
|---|---|---|---|---|
| Recovery lifecycle | Approved recovery reaches PostRecoveryVerification then RecoveryCompleted with verified manifests | RecoveryRequired treated as safe or execution-enabled | State vectors; provider rehearsal | Recovery owner |
| Reconciliation | Evidence classifies committed/rolled back/partial/indeterminate | Client error, missing ledger, lock disappearance alone classifies | Read-only PostgreSQL bundle | PostgreSQL/recovery; provider |
| Precommit evidence | Failure prevents commit and preserves both failures | Evidence failure silently commits or becomes retryable | Failure fixtures/provider transaction evidence | Release/database |
| Lock uncertainty | Uncertain acquisition prevents execution | Missing record treated as free | Crash/timeout/release evidence | Database owner |
| Fencing | Former durable owner cannot execute after token change | One-time token check permits continuation | Per-operation fencing tests | Database/provider |
| Release | Commit/rollback/crash release proven | Stale stealing or ambiguous release succeeds | Lifecycle traces | Database/recovery |
| UNKNOWN_COMMIT | Non-retryable and reconciled independently | Automatic rerun or success claim | Fault/reconciliation rehearsal | Recovery |
| Replay/audit | Every state binds tuple/outcome/evidence | Ambiguous attempt disappears or retries | Audit/access evidence | Security/compliance |

Provider-backed evidence is required for PostgreSQL/Npgsql locking, transaction acknowledgement/disposal, cancellation, connection loss, termination, advisory/session release, durable fencing, catalog reconciliation, and pooling. No such evidence exists here.

## 7. Runner capability gap

Runner.cs exposes only an injected lock call, retained candidate execution, transaction commit/rollback, and UnknownCommit. It has no ownership-uncertain, precommit-evidence, evidence-pending, reconciliation, recovery, fencing, replay, audit, or postcondition implementation. ITargetConnection has no release, identity-evidence, reconciliation, or recovery contract. Program.cs remains disabled through NoExecutionPolicy.

Interfaces and fakes are not verified provider guarantees.

## 8. Decision register and gates

Database-owner approval is required for lock mechanism, key derivation, timeout, privileges, fencing, lease/expiry, release, and stale-owner recovery. PostgreSQL-owner approval is required for reconciliation observations and provider behavior. Security/compliance approval is required for replay disposition and audit atomicity. Release/compliance approval is required for evidence custody. Recovery-owner approval is required for restore, forward repair, quarantine, and post-recovery acceptance.

Target identity, authorization/key registry, locking, recovery, provider validation, and migration-execution approval remain independent.

This proposal does not authorize implementation, provider testing, database access, or migration execution.

**GATE T27 COMPLETE — CONTRACT V3 DRAFTED, INDEPENDENT AUDIT REQUIRED**

