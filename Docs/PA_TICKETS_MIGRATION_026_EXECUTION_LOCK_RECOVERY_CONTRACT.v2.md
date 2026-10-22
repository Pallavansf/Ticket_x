# Gate T25 — Migration 026 Lock and Recovery Contract Proposal v2

Status: **PROPOSAL FOR OWNER REVIEW — INDEPENDENT AUDIT REQUIRED**

This documentation-only revision addresses Gate T24 findings C1, C2, H1, H2, and M1–M3. It does not implement the runner, select a production lock, claim PostgreSQL behavior, or authorize migration 026.

## 1. Scope and non-goals

Scope is migration 026 and candidate SHA-256 97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468DAE49. Authorization, target identity, baseline, backup, preservation, postcondition, recovery, and execution approvals remain separate gates.

This proposal does not choose a lock mechanism, privileges, timeout, fencing model, replay-consumption policy, provider identity query, restore action, or migration approval.

## 2. State inventory and transition contract

States are: PreflightPending, PreflightFailed, AuthorizationConsumed, ConnectionOpened, TransactionStarted, LockAcquisitionPending, LOCK_OWNERSHIP_UNCERTAIN, LockAcquired, Executing, PostconditionsChecking, CommitRequested, Committed, COMMITTED_EVIDENCE_PENDING, RollbackRequested, RolledBack, RollbackUnconfirmed, UNKNOWN_COMMIT, ReconciliationPending, ReconciledCommitted, ReconciledRolledBack, ReconciledPartial, ReconciledIndeterminate, RecoveryDecisionPending, RecoveryRequired, and Failed.

Every record includes attempt ID, authorization replay tuple, migration ID/hash, expected target descriptor, verifier version, timestamps, and evidence references.

| From | Trigger and required evidence | Next state | Retry/owner condition |
|---|---|---|---|
| PreflightPending | Preflight fails before connection; redacted audit | PreflightFailed | No connection; replay disposition remains owner decision |
| PreflightPending | Atomic replay consume succeeds and audit is durable | AuthorizationConsumed | No retry under same attempt |
| AuthorizationConsumed | Approved connection opens and identity evidence is recorded | ConnectionOpened | Target policy must be approved |
| AuthorizationConsumed | Connection/open or identity result is ambiguous | RecoveryRequired | No retry until replay/recovery decision |
| ConnectionOpened | Transaction begins conclusively | TransactionStarted | Provider evidence required |
| TransactionStarted | Lock request starts | LockAcquisitionPending | Same physical connection required |
| LockAcquisitionPending | Ownership conclusively recorded | LockAcquired | Provider lock evidence required |
| LockAcquisitionPending | Conclusive no-ownership failure | Failed | No execution |
| LockAcquisitionPending | Loss, timeout, termination, or missing/conflicting ownership evidence | LOCK_OWNERSHIP_UNCERTAIN | No execution; reconcile ownership/release |
| LockAcquired | Ownership audit succeeds | Executing | Lock held through all transaction stages |
| Executing | Candidate returns without confirmed error | PostconditionsChecking | No commit until assertions pass |
| Executing/PostconditionsChecking | Confirmed pre-commit error | RollbackRequested | Preserve original error |
| PostconditionsChecking | Assertions pass | CommitRequested | Evidence must be durable |
| CommitRequested | Commit conclusive and success evidence durable | Committed | Terminal success |
| CommitRequested | Commit conclusive but evidence write fails/ambiguous | COMMITTED_EVIDENCE_PENDING | Known commit; no retry |
| CommitRequested | Commit result non-conclusive | UNKNOWN_COMMIT | Non-retryable; reconcile |
| RollbackRequested | Rollback conclusive and evidence durable | RolledBack | No success |
| RollbackRequested | Rollback/evidence ambiguous | RollbackUnconfirmed | Recovery required |
| COMMITTED_EVIDENCE_PENDING | Evidence repair succeeds and links to original commit | Committed | Never rerun |
| COMMITTED_EVIDENCE_PENDING | Evidence repair fails or state must be checked | ReconciliationPending | Never rerun |
| UNKNOWN_COMMIT/RollbackUnconfirmed | Recovery begins | ReconciliationPending | Read-only reconciliation |
| ReconciliationPending | Observations prove commit/rollback/partial/indeterminate | ReconciledCommitted, ReconciledRolledBack, ReconciledPartial, or ReconciledIndeterminate | Separate recovery decision |
| Reconciled* | Approved restore/repair/quarantine decision recorded | RecoveryDecisionPending | No migration rerun |
| RecoveryDecisionPending | Approved recovery plan exists | RecoveryRequired | Separate execution approval |
| Any pre-transaction failure | Evidence proves no migration or lock ownership | Failed | Later run requires normal approvals |

No uncertain state can transition to migration execution without independent reconciliation, explicit replay handling, and a new approved decision.

## 3. Lock mechanisms and lifecycle

No production mechanism is selected.

| Mechanism | Required lifecycle | Risks requiring proof |
|---|---|---|
| Transaction-scoped advisory lock | Scope target+026; acquire in transaction; hold through execution/assertions/commit/rollback; prove release | Collision, privileges, timeout, provider semantics |
| Session-scoped advisory lock | Bind session to one target; explicit release; disable or revalidate pooling | Broken-session leaks and reuse |
| Durable lock record | Atomic claim, lease renewal, fencing token, owner identity, expiry, release, stale reconciliation | Clock skew, stale owners, former owner continuing |

All mechanisms require a versioned bounded key derived from canonical target/provider/migration with collision analysis. Acquisition has an owner-approved bounded timeout; contention, privilege failure, unavailable server, cancellation, or uncertainty denies. Ownership is tied to the same physical connection and attempt. Missing ownership evidence does not prove the lock is free. Stale ownership is never force-cleared without an approved provider procedure.

For durable records, claim and renewal are atomic; every lease has an owner/fencing token; a former owner is rejected after token change; expiry requires a trusted-clock policy; stale reconciliation proves no active holder before takeover. These remain database-owner/provider decisions.

## 4. Failure and interruption matrix

| Timing/failure | State | Evidence and retry rule |
|---|---|---|
| Preflight failure | PreflightFailed | Redacted audit; no connection |
| Replay/audit outage | PreflightFailed | Durable failure if possible; no connection |
| Connection loss before transaction | Failed or RecoveryRequired if ambiguous | No retry until replay/recovery resolved |
| Lock contention with no ownership | Failed | Contention evidence; later approved run only |
| Lock acquisition uncertainty | LOCK_OWNERSHIP_UNCERTAIN | Provider ownership/release reconciliation; no execution |
| Migration error before commit | RollbackRequested -> RolledBack or RollbackUnconfirmed | Preserve both diagnostics |
| Cancellation during execution/postconditions | RollbackRequested | Ambiguity requires recovery |
| Commit acknowledgment loss | UNKNOWN_COMMIT | No retry; reconciliation |
| Confirmed commit, evidence failure | COMMITTED_EVIDENCE_PENDING | Repair evidence only; never rollback claim |
| Release failure/ambiguity | LOCK_OWNERSHIP_UNCERTAIN or RecoveryRequired | No stale stealing |
| Process termination | RecoveryRequired if transaction/lock may exist | Reconcile before any future attempt |

Cancellation, disposal, connection loss, and termination never produce success without conclusive provider evidence.

## 5. UNKNOWN_COMMIT reconciliation

UNKNOWN_COMMIT means commit initiation occurred but the client cannot establish the result. It is terminal and non-retryable.

The evidence bundle contains authorization ID/nonce, migration ID/hash, target descriptor, verifier and attempt IDs, connection/transaction identifiers when safe, lock scope/token/events, commit timestamp and transport outcome, baseline/backup references, migration object definitions, sequence/default/constraint/index state, postcondition manifests, evidence hashes, custody, and recovery decisions.

Independent read-only reconciliation must classify ReconciledCommitted, ReconciledRolledBack, ReconciledPartial, or ReconciledIndeterminate. Connection closure, lock disappearance, a client exception, absence of a ledger row, or a current connection alone cannot prove commit or rollback. Provider-specific observations must define which catalog/data/manifests establish each classification.

Decision path: quarantine; reconcile read-only; classify outcome; obtain separate restore/forward-repair/quarantine approval; verify post-recovery manifests; preserve evidence. No blind rerun, cleanup, sequence reset, or ledger fabrication.

## 6. Replay and audit binding

Every terminal or recovery record binds attempt ID, authorization replay tuple (approvalId, nonce, migrationId, candidateSha256, targetFingerprint), replay disposition, lock events/token, state transitions, final outcome, reconciliation ID, evidence hashes, and custody references.

Whether failed preflight, confirmed rollback, or recovery-required outcomes release or permanently consume authorization remains a security/compliance decision. Each policy must be audited. Ambiguous attempts cannot be silently retried.

Known commit plus evidence failure is COMMITTED_EVIDENCE_PENDING, never RolledBack or ordinary retryable Failed. Evidence repair links to the original commit and cannot authorize a new execution.

## 7. Acceptance criteria

| Requirement | Positive | Negative | Evidence | Classification | Owner/block |
|---|---|---|---|---|---|
| State machine | Valid transitions reach only conclusive terminal states | Invalid transition or uncertain-state retry rejects | State vectors | Unit | Security/release; verifier |
| Lock scope | Same target+026 yields same approved scope | Alternate/colliding/malformed scope rejects | Collision review | Unit/provider | Database; lock adapter |
| Exclusion | Two workers cannot overlap | Contention/timeout permits no second execution | Concurrency traces | Provider | Database owner |
| Ownership uncertainty | Uncertain acquisition enters LOCK_OWNERSHIP_UNCERTAIN | Missing record treated as free or migration proceeds | Crash/timeout evidence | Provider | Database/recovery |
| Release/fencing | Commit/rollback/crash release is proven | Ambiguous release or stale stealing succeeds | Release/fencing traces | Provider | Database owner |
| Commit evidence | Known commit with evidence reaches Committed | Evidence outage becomes rollback/retry | Failure fixtures | Unit/provider | Release/compliance |
| UNKNOWN_COMMIT | Ambiguous commit is non-retryable | Automatic rerun/success claim impossible | Fault/reconciliation rehearsal | Provider | Recovery owner |
| Reconciliation | Read-only evidence classifies or remains indeterminate | Client error/lock disappearance alone classifies | Provider evidence bundle | Provider | PostgreSQL/recovery |
| Replay/audit | Every state binds tuple and evidence | Ambiguous attempt disappears or retries | Audit/access evidence | Implementation/provider | Security/compliance |

Fakes cannot establish provider lock, transaction, crash, connection-loss, or reconciliation behavior.

## 8. Generic versus provider-specific boundary

Generic requirements are state safety, no retry from uncertain states, evidence correlation, replay binding, and fail-closed behavior. PostgreSQL/Npgsql evidence is required for advisory-lock semantics, durable-row concurrency, release, transaction acknowledgement, cancellation, connection loss, pooling, catalog observations, and reconciliation. No such evidence exists here.

## 9. Decision register and runner boundary

| Decision | Proposed default | Alternative | Owner | Evidence | Block |
|---|---|---|---|---|---|
| Production lock | Database-selected; transaction-scoped preferred | Durable row/session | Database owner | Concurrency/crash/release/fencing | Lock adapter |
| Key derivation | Versioned target+026 with collision analysis | Provider namespace | Database/security | Collision review | Lock design |
| Timeout/privileges | Explicit bounded values | Other approved values | Database owner | Contention/privilege | Provider |
| Durable lease/fencing | Atomic token and trusted expiry | Disallow durable record | Database/recovery | Lease/crash tests | Lock adapter |
| Replay disposition | Owner-selected consume/release | Opposite trade-off | Security/compliance | Replay races | Verifier |
| Recovery action | Reconcile then separately approved restore/repair/quarantine | Provider runbook | Recovery owner | Reconciliation rehearsal | Recovery |
| Evidence custody | Audited immutable store | Central archive | Release/compliance | Storage/access | Execution approval |

Runner.cs has only an injected lock call, retained candidate execution, transaction commit/rollback, and a narrow UnknownCommit state. It lacks ownership-uncertain, evidence-pending, postcondition, replay, audit, reconciliation, recovery, fencing, and release-proof implementations. Program.cs remains disabled through NoExecutionPolicy. Interfaces and fakes are not verified guarantees.

**GATE T25 COMPLETE — CONTRACT V2 DRAFTED, INDEPENDENT AUDIT REQUIRED**

