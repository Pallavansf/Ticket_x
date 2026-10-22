# Gate T23 — Migration 026 Lock and Recovery Contract Proposal v1

Status: **PROPOSAL FOR OWNER REVIEW — NOT APPROVED**

This document defines concurrency, transaction, interruption, rollback, and unknown-commit contracts for migration 026. It does not select a production lock, implement the runner, connect to PostgreSQL, or authorize migration execution.

## 1. Scope and prerequisites

The contract applies only to migration 026 and candidate SHA-256 97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468DAE49. Authorization, target identity, baseline, backup, preservation, and postcondition contracts remain separate prerequisites.

Security approval, target-identity approval, database-owner lock approval, recovery approval, provider validation, and migration-execution authorization are independent gates.

## 2. Lock candidates

No production mechanism is selected.

| Candidate | Scope/lifetime | Benefits | Risks and required proof |
|---|---|---|---|
| Transaction-scoped advisory lock | Target descriptor plus migration 026; released by transaction completion | No persistent row; naturally bounded to transaction | Collision-resistant key derivation, provider semantics, privilege, timeout, crash/release behavior |
| Session-scoped advisory lock | Target descriptor plus migration 026; session lifetime | Can span transaction boundaries | Pooling and broken-session leaks; explicit release and reconnect proof |
| Durable lock record | Unique target/migration row with owner and lease metadata | Auditable ownership and explicit recovery | Requires schema, uniqueness, lease clock, stale-owner recovery, and cleanup policy |

The database owner must select one mechanism, approve its SQL/driver shape, privileges, collision analysis, timeout, ownership model, and recovery procedure. Until then, the runner must remain disabled.

### 2.1 Generic lock requirements

The lock key or row identity must be derived from the canonical target descriptor, provider, and migration ID 026 using a versioned, deterministic, bounded algorithm. The derivation must have a documented collision analysis. A caller cannot choose an alternate key.

Acquisition must occur after the transaction is started and before migration execution. Contention, timeout, missing privilege, malformed scope, unavailable server, cancellation, connection loss, or uncertain ownership denies. The timeout is explicit and owner-approved; no indefinite wait is allowed.

Ownership must be tied to the same physical connection that executes the candidate. A pooled connection must be revalidated before use. The lock remains held through candidate execution, assertions, postconditions, commit, or rollback. Release must be proven on commit, rollback, cancellation, disposal, connection loss, and process termination. Stale ownership is never force-cleared without an approved recovery procedure.

## 3. Transaction state machine

The execution record uses these states:

- PreflightPending
- PreflightFailed
- AuthorizationConsumed
- ConnectionOpened
- TransactionStarted
- LockAcquired
- Executing
- PostconditionsChecking
- CommitRequested
- Committed
- RollbackRequested
- RolledBack
- RollbackUnconfirmed
- UnknownCommit
- Failed
- RecoveryRequired

Permitted transitions:

1. PreflightPending -> PreflightFailed when authorization, candidate hash, target identity, replay, audit, or evidence preflight fails.
2. PreflightPending -> AuthorizationConsumed only after the approved replay operation succeeds.
3. AuthorizationConsumed -> ConnectionOpened only with the approved endpoint and trusted identity policy.
4. ConnectionOpened -> TransactionStarted only after connection and identity checks complete.
5. TransactionStarted -> LockAcquired only after the approved lock is acquired.
6. LockAcquired -> Executing after lock ownership is recorded.
7. Executing -> PostconditionsChecking after candidate execution returns without a confirmed execution error.
8. PostconditionsChecking -> CommitRequested after required assertions pass.
9. CommitRequested -> Committed only after the provider gives a conclusive commit result.
10. Executing or PostconditionsChecking -> RollbackRequested on a confirmed pre-commit failure.
11. RollbackRequested -> RolledBack only after rollback is conclusively confirmed.
12. RollbackRequested -> RollbackUnconfirmed when rollback fails or its result is ambiguous.
13. CommitRequested -> UnknownCommit on connection loss, cancellation, timeout, disposal, or any non-conclusive result.
14. RollbackUnconfirmed or UnknownCommit -> RecoveryRequired.
15. Any terminal failure before transaction start -> Failed only when no migration execution or lock ownership could have occurred.

No transition from UnknownCommit, RollbackUnconfirmed, or RecoveryRequired to execution is permitted without independent reconciliation and a new approved recovery decision. No automatic retry is permitted.

## 4. Transaction and interruption contract

The generic sequence is:

1. Validate signed authorization, exact candidate path/bytes/hash, target identity, baseline/adoption, backup readiness, and audit/replay availability.
2. Consume replay authorization according to the separately approved policy.
3. Open the approved connection and verify the authenticated target.
4. Begin one transaction.
5. Acquire and record the approved migration-026 lock.
6. Execute only the retained candidate bytes.
7. Check required postconditions while the lock remains held.
8. Request commit.
9. Mark Committed only after a conclusive provider result and evidence write.

Cancellation and connection loss behavior:

| Timing | Required outcome |
|---|---|
| Before authorization consumption | Deny; no connection |
| During preflight or identity verification | Deny; no transaction |
| After consumption but before connection | Failed or RecoveryRequired according to replay policy; no retry without approved decision |
| During lock acquisition | Deny; release/ownership outcome must be recorded |
| During migration execution | Request rollback; if rollback is not conclusive, RollbackUnconfirmed/RecoveryRequired |
| During postconditions | Request rollback; inconclusive result requires recovery |
| After commit request begins | UnknownCommit unless provider conclusively reports outcome |
| During disposal or connection close | Preserve the last non-conclusive state; never claim success |
| After process termination | RecoveryRequired until independent reconciliation |

A cancellation token or exception must never convert an unknown result into success.

## 5. Rollback and release behavior

On confirmed pre-commit migration failure, the runner requests rollback and records both migration and rollback diagnostics. A confirmed rollback yields RolledBack and no success claim.

If rollback fails, times out, loses the connection, or returns an ambiguous result, the record is RollbackUnconfirmed and then RecoveryRequired. The runner must preserve the original migration error and rollback error/outcome. It must not retry, delete data, reset sequences, or force-clear a lock.

A lock-release failure or ambiguous release is an operational failure. It cannot be reported as successful cleanup. For transaction-scoped locks, provider-backed evidence must prove release on both commit and rollback. For session or durable locks, explicit release and stale-owner recovery evidence are mandatory.

## 6. UNKNOWN_COMMIT and recovery

UNKNOWN_COMMIT means the runner cannot prove whether commit occurred after commit initiation. It is terminal for the current attempt and non-retryable.

Required evidence includes:

- Authorization ID, nonce, candidate hash, target descriptor, and verifier version
- Connection and transaction identifiers available without credentials
- Lock scope, acquisition, and release evidence
- Commit request timestamp and exact exception/transport outcome
- Migration and postcondition evidence observed before disconnect
- Backup/baseline manifest references
- Reconciliation and recovery decision references

Recovery requires:

1. Stop all automatic retries and further migration attempts.
2. Preserve logs, evidence hashes, and the original authorization binding.
3. Perform an independent, read-only reconciliation against the approved target.
4. Determine whether the migration committed, rolled back, or left an indeterminate partial state.
5. Obtain a separately approved restore or forward-repair decision.
6. Verify post-recovery manifests and preserve the recovery evidence.

A reconciliation that cannot establish outcome remains RecoveryRequired. No blind rerun, destructive cleanup, sequence reset, or migration-ledger fabrication is permitted.

## 7. Failure matrix

| Failure | Execution record | Replay/authorization effect | Required evidence | Blocking stage |
|---|---|---|---|---|
| Authorization or target preflight failure | PreflightFailed | No connection; consume/release per approved replay policy | Redacted failure audit | Verifier |
| Replay-store outage or ambiguity | PreflightFailed | Deny; no connection | Store availability evidence | Verifier |
| Lock contention/timeout | Failed or RecoveryRequired if ownership uncertain | No retry under same attempt | Lock contention/ownership record | Lock adapter/provider |
| Privilege failure | Failed before execution | Deny; no success | Provider error without secrets | Lock adapter/provider |
| Migration error, confirmed rollback | RolledBack | Authorization remains consumed or released per policy; no retry automatically | Both migration and rollback outcomes | Provider validation |
| Migration error, rollback failed | RollbackUnconfirmed -> RecoveryRequired | No retry; recovery approval required | Both errors, lock/release evidence | Recovery |
| Commit acknowledgment lost | UnknownCommit -> RecoveryRequired | Replay remains non-retryable | Commit request and reconciliation evidence | Recovery |
| Connection loss after commit request | UnknownCommit | No retry | Transport and provider reconciliation | Recovery |
| Failed lock release | RecoveryRequired if ownership uncertain | No new execution | Release/stale-owner evidence | Lock/recovery |
| Audit/evidence write failure | Failed or RecoveryRequired | Deny success; preserve in-memory outcome only if durable state is proven | Storage failure evidence | Evidence/approval |
| Process termination | RecoveryRequired if transaction/lock may exist | No automatic retry | Process and target reconciliation | Recovery |

## 8. Acceptance criteria and evidence

| Requirement | Positive criterion | Negative criterion | Evidence | Classification | Owner/block |
|---|---|---|---|---|---|
| Lock scope/derivation | Same target+026 maps to same approved scope | Alternate/colliding/malformed scope rejects | Derivation vectors and collision review | Unit plus provider | Database owner; lock adapter |
| Mutual exclusion | Two workers cannot execute concurrently | Contention does not permit second execution | Concurrency traces | Provider-backed | Database owner; provider validation |
| Timeout/privilege | Approved timeout and privilege behavior are deterministic | Indefinite wait, missing privilege, or unavailable server denies | Provider logs | Provider-backed | Database owner |
| Lifetime/release | Lock remains through assertions and releases conclusively | Connection loss, cancellation, disposal, crash, or stale owner creates no false success | Crash/release evidence | Provider-backed | Database owner/recovery |
| State transitions | Valid sequence reaches Committed or RolledBack only with conclusive evidence | Invalid transition or retry from UnknownCommit is rejected | State-machine tests | Unit | Security/release; verifier |
| Rollback diagnostics | Migration and rollback outcomes are both retained | Rollback failure is not collapsed into ordinary failure | Exception/evidence fixtures | Unit plus provider | Release/database |
| Unknown commit | Ambiguous commit enters non-retryable UnknownCommit | Automatic retry or success claim is impossible | Fault-injection/reconciliation evidence | Provider-backed | Recovery owner |
| Cancellation/connection loss | Timing-specific state and evidence are preserved | Cancellation or disposal produces false success | Interruption traces | Provider-backed | Provider/recovery |
| Recovery | Reconciliation precedes restore/forward repair | Blind retry, cleanup, or sequence reset is rejected | Recovery rehearsal | Provider-backed | Recovery owner |
| Evidence/audit | Complete redacted record persists | Outage, ambiguity, or secret leakage denies | Storage/access audit | Implementation/provider | Compliance/release |

Unit fakes cannot establish provider locking, transaction, connection-loss, or recovery properties.

## 9. Decision register

| Decision | Proposed default | Alternative | Owner role | Evidence | Blocking stage |
|---|---|---|---|---|---|
| Production lock | Database-owner-selected candidate above; transaction-scoped is preferred for bounded scope | Durable row or session lock | Database owner | Concurrency, crash, privilege, release tests | Lock adapter |
| Key derivation | Versioned deterministic target+026 derivation with collision analysis | Provider-native key namespace | Database/security owner | Collision and interoperability review | Lock design |
| Timeout | Explicit bounded owner-approved timeout | Queueing with separate lease | Database owner | Contention/timeout evidence | Provider validation |
| Replay after failed preflight | Owner-selected consume or release policy | Opposite trade-off | Security/compliance owner | Replay crash/race evidence | Verifier |
| Rollback uncertainty | RecoveryRequired, never success | Provider-specific reconciliation | Recovery owner | Rollback fault evidence | Recovery |
| Commit ambiguity | UnknownCommit, no retry | None permitted without reconciliation | Release/database owners | Reconciliation/restore rehearsal | Recovery |
| Release failure | RecoveryRequired unless provider proves release | Manual approved stale-owner procedure | Database/recovery owner | Release/crash evidence | Lock/recovery |
| Evidence custody | Audited immutable store | Central archive | Release/compliance owner | Storage and access evidence | Execution approval |

## 10. Runner compatibility

Runner.cs currently:

- Calls an injected lock method after transaction start but has no concrete lock scope, timeout, ownership, or release contract.
- Executes retained candidate bytes and handles confirmed rollback and UnknownCommit state.
- Does not expose postcondition checks, replay consumption, audit persistence, backup evidence, reconciliation, or recovery decisions.
- Treats injected IExecutionPolicy and ITargetConnection as boundaries, not verified implementations.

Program.cs remains disabled through NoExecutionPolicy. The current code cannot establish provider-specific lock or recovery guarantees.

This proposal preserves fail-closed behavior and documents design requirements only. No lock, recovery policy, provider behavior, or migration execution is approved.

**GATE T23 COMPLETE — LOCK AND RECOVERY CONTRACT PROPOSED, OWNER APPROVAL REQUIRED**

