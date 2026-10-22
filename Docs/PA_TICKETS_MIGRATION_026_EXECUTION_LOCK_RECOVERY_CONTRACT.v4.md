# Gate T29 - Migration 026 Lock and Recovery Contract Proposal v4

Status: PROPOSAL FOR OWNER REVIEW — INDEPENDENT AUDIT REQUIRED

This documentation-only revision addresses Gate T28 findings. It does not implement the runner, select a production lock, claim PostgreSQL/Npgsql behavior, or authorize migration 026.

## 1. Scope and governance boundaries

This contract applies only to migration ID 026 and candidate SHA-256 97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468DAE49. Authorization, target identity, baseline/backup, locking, recovery, provider validation, and execution approval remain separate gates.

No state in this document authorizes a migration. Recovery completion, reconciliation, lock release, or audit repair never authorizes a new attempt. A new attempt requires fresh authorization, replay approval, independently verified target identity, exact candidate hash, non-overlap proof, baseline readiness, and execution approval.

The words MUST and MUST NOT are normative proposed rules pending owner approval. PostgreSQL/Npgsql observations are proposals requiring database-owner and provider-backed validation.

## 2. State inventory and transition contract

States are: PreflightPending, PreflightFailed, AuthorizationConsumed, AUDIT_STATE_UNCERTAIN, ConnectionOpening, ConnectionOpened, TransactionStarting, TransactionStarted, LockAcquisitionPending, LOCK_OWNERSHIP_UNCERTAIN, LockAcquired, Executing, PostconditionsChecking, PRECOMMIT_EVIDENCE_FAILURE, RollbackRequested, RollbackOutcomePending, RolledBack, RollbackUnconfirmed, CommitRequested, UNKNOWN_COMMIT, Committed, COMMITTED_EVIDENCE_PENDING, COMMITTED_RELEASE_FAILURE, ReconciliationPending, ReconciledCommitted, ReconciledRolledBack, ReconciledPartial, ReconciledIndeterminate, RecoveryDecisionPending, RecoveryExecuting, PostRecoveryVerification, RecoveryCompleted, RecoveryFailed, RecoveryIndeterminate, RecoveryRequired, Failed, and Quarantined.

Every state record MUST bind attempt ID, authorization replay tuple, migration ID/hash, signed expected target, independently observed target evidence, lock mechanism and events, transaction/session identifiers when available, timestamps, state durability, error digests, and custody references. A transition is valid only when its required evidence is durably recorded or the attempt enters AUDIT_STATE_UNCERTAIN.

| From | Trigger and required evidence | Permitted next state | Retry/replay, approval, and provider gate |
|---|---|---|---|
| PreflightPending | Preflight denial with redacted durable failure record; no connection opened | PreflightFailed | No retry under attempt; replay disposition is Security/Compliance decision; verifier validation |
| PreflightPending | Atomic authorization consume plus attempt/audit record succeeds | AuthorizationConsumed | No retry under attempt; Security/Compliance policy required; replay-store validation |
| PreflightPending | Consume result or audit durability is ambiguous | AUDIT_STATE_UNCERTAIN | No connection or migration; no retry until reconciliation; Security/Compliance decision; crash-window evidence |
| AuthorizationConsumed | Connection and target identity are conclusively verified | ConnectionOpened | No replay; Target Identity approval; provider identity validation |
| AuthorizationConsumed | Connection or identity is ambiguous or lost | RecoveryRequired | No execution; reconciliation/recovery decision required; provider/network evidence |
| ConnectionOpened | Transaction start is conclusively acknowledged | TransactionStarted | Same physical connection required; provider transaction validation |
| ConnectionOpening/TransactionStarting | Cancellation, disposal, loss, or ambiguous acknowledgement | RecoveryRequired or AUDIT_STATE_UNCERTAIN | No retry; preserve transport evidence; provider fault validation |
| TransactionStarted | Approved lock request begins on same physical connection | LockAcquisitionPending | No execution; Database Owner lock approval; provider lock validation |
| LockAcquisitionPending | Mechanism-specific proof ownership was not obtained and request was denied | Failed | Ordinary failure only; no replay retry without policy; provider denial evidence required |
| LockAcquisitionPending | Verified owner token/session is held by this attempt | LockAcquired | No retry; lock owner approval; provider ownership evidence required |
| LockAcquisitionPending | Timeout, loss, termination, missing record, incomplete response, or conflicting ownership | LOCK_OWNERSHIP_UNCERTAIN | No execution or lock stealing; reconciliation and non-overlap proof required; database/recovery owner; provider evidence |
| LockAcquired | Ownership, scope, and lease/fencing evidence are durable | Executing | Lock remains held; provider lifecycle evidence required |
| Executing | Candidate returns and postcondition evidence collection begins | PostconditionsChecking | No commit yet; migration execution approval; provider execution evidence |
| Executing/PostconditionsChecking | Confirmed pre-commit error with error digest | RollbackRequested | Preserve primary error; rollback required; provider transaction evidence |
| PostconditionsChecking | Required pre-commit evidence persistence fails | PRECOMMIT_EVIDENCE_FAILURE | Commit prohibited; preserve primary and secondary failures; audit/replay policy required |
| PRECOMMIT_EVIDENCE_FAILURE | Rollback request is issued and outcome observation starts | RollbackOutcomePending | No commit; no retry; Release/Database approval; provider rollback evidence |
| RollbackOutcomePending | Rollback and lock release are both conclusive and final record durable | RolledBack | No migration retry under attempt; replay disposition audited; provider release evidence |
| RollbackOutcomePending | Rollback conclusive but evidence or release is not durable | RollbackUnconfirmed or AUDIT_STATE_UNCERTAIN | No retry; reconcile; provider evidence required |
| RollbackOutcomePending | Rollback or release is ambiguous/failed | RollbackUnconfirmed | Recovery required; preserve primary and secondary failures; recovery approval |
| PostconditionsChecking | Checks and all pre-commit evidence are durable | CommitRequested | Commit is allowed only after execution and recovery owners' prerequisites; provider commit validation |
| CommitRequested | Commit response is conclusive and success evidence durable | Committed | No retry; terminal success; provider acknowledgement evidence |
| CommitRequested | Commit response is conclusive but evidence write is absent/ambiguous | COMMITTED_EVIDENCE_PENDING | Known commit, never rerun; evidence repair/reconciliation; Security/Compliance approval |
| CommitRequested | Commit response is non-conclusive | UNKNOWN_COMMIT | Non-retryable; read-only reconciliation; recovery-owner approval |
| Committed | Commit is known but lock release fails or is ambiguous | COMMITTED_RELEASE_FAILURE | No new attempt; prove release/non-overlap or quarantine; Database/Recovery approval |
| COMMITTED_EVIDENCE_PENDING | Evidence repair links to immutable known-commit evidence | Committed | No migration rerun; audit repair only |
| COMMITTED_EVIDENCE_PENDING | Repair cannot link outcome or persistence is uncertain | AUDIT_STATE_UNCERTAIN or ReconciliationPending | No retry; independent reconciliation; Security/Recovery approval |
| UNKNOWN_COMMIT/RollbackUnconfirmed/LOCK_OWNERSHIP_UNCERTAIN/COMMITTED_RELEASE_FAILURE | Recovery process is opened with preserved evidence | ReconciliationPending | Read-only first; no execution; recovery-owner approval; provider reconciliation validation |
| ReconciliationPending | Durable historical evidence classifies outcome uniquely | ReconciledCommitted, ReconciledRolledBack, ReconciledPartial, or ReconciledIndeterminate | No retry; outcome-specific recovery decision; PostgreSQL validation |
| ReconciliationPending | Evidence store or target is unavailable/contradictory | ReconciledIndeterminate then RecoveryDecisionPending | Quarantine; no execution; recovery owner |
| AUDIT_STATE_UNCERTAIN | Independent audit/replay reconciliation establishes consumed, released, and attempt state | PreflightFailed, RecoveryRequired, or Quarantined | No execution until Security/Compliance decision and durable record |
| ReconciledCommitted | Owner-approved verification-only or approved repair plan; non-overlap proven | RecoveryDecisionPending | No rerun; recovery owner and execution authority remain separate |
| ReconciledRolledBack | Owner-approved baseline restore or no-op verification plan; non-overlap proven | RecoveryDecisionPending | No rerun; recovery owner approval |
| ReconciledPartial | Approved quarantine, restore, or forward-repair plan; non-overlap proven | RecoveryDecisionPending | No rerun; recovery and database owner approval |
| ReconciledIndeterminate | Approved quarantine or evidence-recovery plan; non-overlap proven | RecoveryDecisionPending | No rerun; recovery owner approval |
| RecoveryDecisionPending | Explicit plan, scope, executor, and approval recorded | RecoveryExecuting | Recovery only; no migration authorization; provider plan validation |
| RecoveryExecuting | Restore/repair/quarantine operation ends with durable operation evidence | PostRecoveryVerification | No migration; recovery executor authorization |
| PostRecoveryVerification | Required manifests/invariants and non-overlap evidence pass | RecoveryCompleted | No migration authorization; recovery owner acceptance |
| PostRecoveryVerification | Verification fails or is ambiguous | RecoveryFailed or RecoveryIndeterminate | Quarantine; no retry; recovery owner decision |
| RecoveryFailed/RecoveryIndeterminate | Further safe action is not established | RecoveryRequired or Quarantined | No execution; new decision and evidence required |
| RecoveryCompleted | Fresh authorization, replay decision, target verification, baseline, and execution approval later succeed | PreflightPending (new attempt ID) | Never reuse prior nonce/attempt; all gates re-run |

No uncertain, audit-uncertain, reconciled, or recovery state may transition directly to migration execution. RecoveryRequired is a holding state with an explicit next action: enter ReconciliationPending when read-only evidence can be collected, or Quarantined when evidence/non-overlap cannot be established.

## 3. Historical outcome certainty and reconciliation evidence

Reconciliation MUST distinguish observed current state from proven historical execution. Current catalog, rows, sequences, defaults, constraints, indexes, or postconditions that merely match the candidate are insufficient to prove that migration 026 committed or rolled back.

A definitive `ReconciledCommitted` or `ReconciledRolledBack` result requires independently trustworthy historical evidence: a durable ledger, manifest, or equivalent transaction evidence that is append-only or tamper-evident, bound to migration ID 026, exact candidate hash, canonical target identity, attempt ID/authorization tuple, transaction or session correlation when available, outcome, commit/rollback time, and persistence confirmation surviving process and connection failure. Its writer, storage integrity, retention, and read path require owner approval and provider validation. No such ledger is asserted to exist here.

Read-only evidence collection should preserve the approved target/hash, pre-migration baseline and backup references, catalog definitions, constraints/indexes, sequence/default/ownership/dependency relationships, migration-specific rows and postcondition manifests, observable transaction/session evidence, collection time, source identity, tool/provider version, and tamper-evident hashes.

| Outcome | Required evidence and conclusion | Insufficient evidence |
|---|---|---|
| ReconciledCommitted | Historical manifest/ledger or equivalent proves this attempt committed on the bound target and hash, and current state plus postconditions are consistent with it | Matching schema/rows alone, client success, connection ID, lock disappearance, catalog similarity |
| ReconciledRolledBack | Historical evidence proves this attempt rolled back or never committed, is bound to target/hash/attempt, and baseline invariants remain intact with no partial effects | Baseline match alone, missing ledger row, rollback exception, closed connection |
| ReconciledPartial | Independent observations show some migration effects or contradictory states, with enough evidence to prove inconsistency but not a complete committed outcome | One object, one row count, or one sequence value |
| ReconciledIndeterminate | Historical evidence is absent, contradictory, stale, unavailable, or cannot distinguish coincidental current state from this attempt | Any client error, lock state, timeout, or current catalog match alone |

Without durable historical evidence, a database that happens to equal the candidate's expected state MUST remain `ReconciledIndeterminate`; it must not be labeled committed or rolled back. Reconciliation itself does not create a ledger or prove historical execution.

## 4. Outcome-specific recovery restrictions

| Reconciliation result | Permitted recovery | Prohibited action |
|---|---|---|
| ReconciledCommitted | Evidence repair, verification, or separately approved corrective repair | Rerunning migration 026; declaring rollback from current state |
| ReconciledRolledBack | Verification or separately approved restore/repair if baseline is not accepted | Treating absence of effects as proof without historical evidence; automatic rerun |
| ReconciledPartial | Owner-approved quarantine, restore, or forward repair with explicit scope | Blind rerun, cleanup, sequence reset, or unapproved destructive repair |
| ReconciledIndeterminate | Quarantine and evidence recovery; restore/repair only after explicit recovery approval | Any migration rerun or assumption that lock disappearance proves safety |

Every recovery operation requires a named authorized executor, plan hash, target verification, non-overlap proof, backup/baseline evidence, immutable operation record, and independent post-recovery verification. RecoveryCompleted never authorizes migration execution.

## 5. Lock acquisition, release, fencing, and non-overlap

No production mechanism is selected. Database-owner approval and provider evidence are prerequisites.

For a transaction-scoped advisory lock, proposed evidence includes deterministic approved key derivation, same physical connection/transaction identity, conclusive acquisition response, transaction end, and provider evidence that the lock is released after commit, rollback, cancellation, disposal, or connection termination. Lock disappearance alone is insufficient.

For a session-scoped advisory lock, proposed evidence includes backend/session identity, pool checkout binding, explicit release response, connection termination, pool isolation or disabled pooling, and proof that a former session cannot continue. A pooled connection must not be reused until target and lock ownership are revalidated.

For a durable lock record, the initial implementation remains ineligible unless a separately approved fencing design is provider-tested. Atomic claim, owner identity, monotonic fencing token, trusted lease/clock rules, and every migration statement and postcondition operation must enforce the current token in the same transaction/session. Token change, expiry, connection loss, termination, or renewal failure must make obsolete-owner operations fail before each operation; a one-time check or lease expiry alone is insufficient.

Safe subsequent acquisition requires mechanism-specific proof that the former owner cannot execute, the prior transaction/session ended or was fenced, release is conclusive, and the new owner has a fresh token/lock. Missing records, timeout, incomplete response, connection loss, process termination, or ambiguous release are never conclusive denial and never permit lock stealing.

## 6. Phase-specific interruption and failure matrix

| Phase | Interruption/failure | Required state and evidence | Success/retry rule |
|---|---|---|---|
| Preflight | Cancellation, verifier/audit failure | PreflightFailed or AUDIT_STATE_UNCERTAIN with redacted failure and replay evidence | No connection; no retry until policy resolves |
| Authorization consume | Crash or store ambiguity | AUDIT_STATE_UNCERTAIN with crash window and idempotency key | No new attempt until replay reconciliation |
| Connection/identity | Loss, redirect, stale/ambiguous identity | RecoveryRequired with network/identity evidence | No transaction or retry |
| Transaction start | Cancellation, disposal, lost/ambiguous acknowledgement | RecoveryRequired or AUDIT_STATE_UNCERTAIN | No execution; provider evidence required |
| Lock acquisition | Denial with proof of no ownership | Failed | Ordinary failure only; no lock stealing |
| Lock acquisition | Timeout, loss, termination, missing/conflicting ownership | LOCK_OWNERSHIP_UNCERTAIN | No execution; reconcile non-overlap |
| Migration execution | Confirmed error | RollbackRequested then RollbackOutcomePending | No commit; preserve primary failure |
| Migration execution | Cancellation, disposal, process termination, connection loss | RollbackOutcomePending or RollbackUnconfirmed | No success; reconcile if rollback ambiguous |
| Postconditions | Check failure or interruption | RollbackRequested or RollbackOutcomePending | No commit; preserve evidence |
| Pre-commit evidence | Persistence failure | PRECOMMIT_EVIDENCE_FAILURE | Prohibit commit, request rollback, preserve both failures |
| Rollback/release | Rollback failure or ambiguous release | RollbackUnconfirmed, AUDIT_STATE_UNCERTAIN, or RecoveryRequired | No retry; recovery required |
| Commit in flight | Cancellation, disposal, connection loss, timeout | UNKNOWN_COMMIT | Non-retryable; read-only reconciliation |
| Confirmed commit | Success response but release fails/ambiguous | COMMITTED_RELEASE_FAILURE | Do not claim clean completion; prove non-overlap or quarantine |
| Post-commit evidence | Evidence write failure/ambiguity | COMMITTED_EVIDENCE_PENDING or AUDIT_STATE_UNCERTAIN | Never rerun; repair/reconcile |
| Reconciliation | Target/evidence unavailable or contradictory | ReconciledIndeterminate and RecoveryDecisionPending, or Quarantined | No recovery mutation without approval |
| Recovery | Cancellation, termination, or operation result ambiguous | RecoveryFailed or RecoveryIndeterminate | Quarantine; no migration attempt |
| Post-recovery verification | Manifest/invariant failure | RecoveryFailed or RecoveryIndeterminate | No future attempt |

Original migration errors, rollback errors, release errors, evidence errors, and transport errors MUST be retained as separate redacted diagnostics with cryptographic integrity references. No exception may be converted into success.

## 7. Mandatory replay and audit durability

Before implementation, Security/Compliance MUST approve one of these designs:

1. An atomic mechanism that couples authorization consumption, attempt-state transition, and audit evidence in one trusted durability boundary; or
2. A durable `AUDIT_STATE_UNCERTAIN` protocol with an idempotency key, immutable append records, crash-window markers, independent replay-store reconciliation, and a rule that every uncertain tuple blocks new attempts.

Cross-system atomicity MUST NOT be claimed without a concrete reviewed design. At minimum, each attempt record stores authorization ID, nonce/replay tuple, migration hash, target descriptor/version, state sequence, lock/fencing events, transaction outcome, evidence references, error digests, custody references, and timestamps. Duplicate records are reconciled by attempt ID and idempotency key; conflicting records quarantine the attempt.

Replay consumption versus release remains a Security/Compliance owner decision. The selected policy MUST be explicit before implementation and MUST never release a nonce for UNKNOWN_COMMIT, ownership uncertainty, partial state, indeterminate outcome, or unresolved audit state. A preflight denial may be released only under the approved policy and with durable evidence.

## 8. Acceptance criteria and provider validation

| Requirement | Positive criterion | Negative criterion | Evidence/classification | Owner/block |
|---|---|---|---|---|
| Historical certainty | Definitive outcome requires bound durable historical evidence | Current-state match alone yields committed/rolled-back | Manifest/ledger integrity and fault tests; unit plus provider | PostgreSQL/recovery |
| State transitions | Every listed transition records evidence and owner/replay result | RecoveryRequired or uncertainty loops without next action | State-machine vectors; unit | Security/recovery |
| Pre-commit evidence | Evidence failure prevents commit, then rollback is classified | Commit proceeds or failures are collapsed | Fault fixtures/provider transaction traces | Release/database |
| Audit durability | Atomic design works or AUDIT_STATE_UNCERTAIN blocks attempts | Consume/audit divergence permits replay | Crash-window/idempotency evidence; implementation/provider | Security/compliance |
| Lock classification | Only proved denial enters Failed | Timeout/loss/missing record treated as free | Mechanism-specific traces; provider | Database owner |
| Non-overlap | New acquisition requires former-owner inactivity/fencing and release proof | Lock disappearance or lease expiry alone permits acquisition | Crash/pool/fencing traces; provider | Database/recovery |
| Recovery restrictions | Each outcome accepts only authorized, scoped recovery | Indeterminate/partial outcome permits rerun | Recovery rehearsal and approval record; provider | Recovery owner |
| Interruptions | Each phase reaches non-misleading state with retained diagnostics | Cancellation/disposal/termination reports success | Fault-injection traces; provider | Provider/recovery |
| Release failure | Known commit plus release failure is COMMITTED_RELEASE_FAILURE | Release error is labeled rollback or clean success | Release traces; provider | Database/recovery |
| UNKNOWN_COMMIT | Reconciliation is read-only, non-retryable, and evidence-bound | Automatic retry or success claim | Commit fault/reconciliation evidence; provider | Recovery |

Unit fakes cannot establish PostgreSQL/Npgsql lock, pooling, transaction acknowledgement, release, or recovery properties. Provider validation must use an approved disposable target and never imply production approval.

## 9. Runner capability gap

`Runner.cs:7` exposes only five runtime states and has no states for ownership uncertainty, evidence uncertainty, reconciliation, recovery, or committed release failure. `Runner.cs:13-23` provides injected authorization/target interfaces, transaction start, one lock call, and byte execution, but no replay store, audit durability, fencing, release, postcondition, reconciliation, or recovery contract. `Runner.cs:82-89` handles commit ambiguity and rollback exceptions but does not implement this state machine. `Program.cs:1-2` remains disabled through `NoExecutionPolicy`.

The runner therefore does not implement this proposal. Interfaces and fakes are not provider evidence.

## 10. Owner decision register and blockers

| Decision | Proposed default | Accountable owner | Required evidence | Blocking stage |
|---|---|---|---|---|
| Lock mechanism/key/timeout/privileges | No selection until database-owner review; transaction-scoped remains a candidate | Database owner | Collision, contention, crash, release, privilege tests | Lock design |
| Durable fencing | Exclude durable lock from initial implementation | Database owner/provider | Per-operation obsolete-token rejection | Provider validation |
| Historical ledger/manifest | Require independently trusted bound evidence; otherwise indeterminate | PostgreSQL/recovery owner | Integrity, retention, target/hash/attempt binding | Reconciliation |
| Replay disposition | Security/Compliance-selected consume/release policy with no unsafe release | Security/compliance | Crash/race/idempotency evidence | Authorization |
| Audit atomicity | Atomic trusted boundary or mandatory AUDIT_STATE_UNCERTAIN | Security/compliance/release | Durability and custody audit | Implementation |
| Recovery actions | Outcome-specific, scoped, separately approved plan | Recovery owner | Restore/repair/quarantine rehearsal | Recovery |
| Non-overlap proof | Mechanism-specific former-owner inactivity/fencing and release evidence | Database/recovery owner | Termination/pooling/race traces | Subsequent execution |
| Provider behavior | No claims before approved Npgsql/PostgreSQL validation | PostgreSQL/provider owner | Provider-backed fault and lifecycle evidence | Provider validation |
| Migration execution | Separate explicit approval after all gates | Execution authority | Complete evidence package | Execution |

This proposal creates no approvals, credentials, target identities, ledger, fencing mechanism, or provider evidence. It does not authorize implementation, provider testing, database access, or migration execution.

Gate T28 disposition: historical certainty, recovery transitions, pre-commit ordering, replay durability, lock classification, outcome-specific recovery, interruption paths, and mechanism-specific evidence are revised above. Independent audit remains required.

**GATE T29 COMPLETE — CONTRACT V4 DRAFTED, INDEPENDENT AUDIT REQUIRED**

