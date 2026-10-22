# Gate T33 - Migration 026 Lock and Recovery Contract Proposal v6

Status: PROPOSAL FOR OWNER REVIEW - FINAL INDEPENDENT AUDIT REQUIRED

This documentation-only revision addresses Gate T32 findings. It does not implement the runner, select a production lock or audit store, claim PostgreSQL/Npgsql behavior, or authorize migration 026.

## 1. Scope and governance boundaries

The contract applies only to migration 026 and candidate SHA-256 97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49. Authorization, target identity, baseline/backup, locking, recovery, provider validation, and migration execution remain separate approvals.

The rules below are proposed requirements pending owner approval. No state, reconciliation result, lock release, audit repair, or recovery completion authorizes a migration. A new attempt requires a new attempt ID and nonce, approved replay disposition, exact candidate hash, independent target verification, non-overlap proof, baseline readiness, and separate execution authorization.

## 2. State inventory and complete reachable transition contract

States are: PreflightPending, PreflightFailed, AuthorizationConsumed, AUDIT_STATE_UNCERTAIN, ConnectionOpening, ConnectionOpened, TransactionStarting, TransactionStarted, LockAcquisitionPending, LOCK_OWNERSHIP_UNCERTAIN, LockAcquired, Executing, PostconditionsChecking, PRECOMMIT_EVIDENCE_FAILURE, RollbackRequested, RollbackOutcomePending, RolledBack, PRECOMMIT_RELEASE_FAILURE, RollbackUnconfirmed, CommitRequested, UNKNOWN_COMMIT, Committed, COMMITTED_EVIDENCE_PENDING, COMMITTED_RELEASE_FAILURE, ReconciliationPending, ReconciledCommitted, ReconciledRolledBack, ReconciledNeverCommitted, ReconciledPartial, ReconciledIndeterminate, RecoveryDecisionPending, RecoveryExecuting, PostRecoveryVerification, RecoveryCompleted, RecoveryFailed, RecoveryIndeterminate, RecoveryRequired, Failed, and Quarantined.

Every state record MUST bind attempt ID, replay tuple, migration ID/hash, signed expected target, observed target evidence, lock mechanism/events, transaction/session identifiers when available, timestamps, state sequence, error digests, and custody references. If a record cannot be durably acknowledged, the prescribed next state is AUDIT_STATE_UNCERTAIN and section 7 applies.

The table is exhaustive. Each row has one exact next state. A predicate is satisfied only when every listed fact is durably evidenced; otherwise the row for insufficient or conflicting evidence applies.

| From | Exact trigger and evidence predicate | Next state | Replay/retry, owner, provider dependency |
|---|---|---|---|
| PreflightPending | Required preflight check fails before any connection attempt; denial record is durable and proves no connection was opened | PreflightFailed | No connection; replay per approved Security/Compliance policy; verifier evidence |
| PreflightPending | Authorization consume, attempt record, and audit acknowledgement are atomically acknowledged under the selected approved design | AuthorizationConsumed | No retry under attempt; Security/Compliance; replay-store validation |
| PreflightPending | Consume or audit acknowledgement is missing, conflicting, or timed out | AUDIT_STATE_UNCERTAIN | No connection/migration; nonce blocked; Security/Compliance; independent marker |
| AuthorizationConsumed | Connection attempt is initiated and no conclusive denial exists | ConnectionOpening | No retry; target-identity approval; provider connection evidence |
| ConnectionOpening | Connection handle, authenticated target evidence, and open acknowledgement are durable | ConnectionOpened | No replay; target identity/provider approval |
| ConnectionOpening | Provider durably proves no session/connection was created and no lock/transaction effect exists | Failed | No retry under attempt; verifier/provider evidence |
| ConnectionOpening | Any handle, server response, redirect, cancellation, disposal, termination, or transport result is ambiguous | RecoveryRequired | No retry; recovery owner and target/provider evidence |
| ConnectionOpened | Transaction-start request is issued on the same physical connection | TransactionStarting | No execution; provider transaction validation |
| TransactionStarting | Transaction-start acknowledgement and transaction identity are durable | TransactionStarted | No retry; provider transaction evidence |
| TransactionStarting | Provider proves transaction did not begin and no lock/effect exists | Failed | No retry; provider evidence |
| TransactionStarting | Cancellation, timeout, disposal, termination, loss, or ambiguous acknowledgement prevents proof | RecoveryRequired | No execution; recovery owner/provider evidence |
| TransactionStarted | Approved lock request begins on the same physical connection | LockAcquisitionPending | No execution; database-owner lock approval |
| LockAcquisitionPending | Provider proves denial before ownership and no owner token/session was created | Failed | Ordinary failure; no lock stealing; database-owner/provider evidence |
| LockAcquisitionPending | Ownership, scope, and token/session are conclusively held and durably recorded | LockAcquired | No retry; database-owner/provider evidence |
| LockAcquisitionPending | Timeout, loss, termination, incomplete response, missing record, or conflicting ownership evidence | LOCK_OWNERSHIP_UNCERTAIN | No execution; no lock stealing; recovery/database owner |
| LOCK_OWNERSHIP_UNCERTAIN | Independent evidence proves no owner/transaction/session survived and release is complete | Failed | No retry under attempt; non-overlap evidence; database/recovery owner |
| LOCK_OWNERSHIP_UNCERTAIN | Ownership or release remains uncertain | ReconciliationPending | Read-only reconciliation; no retry; recovery owner/provider |
| LockAcquired | Scope, owner, and lifecycle/fencing evidence are durable | Executing | Lock held; database owner/provider validation |
| Executing | Candidate returns without confirmed error and postcondition collection begins | PostconditionsChecking | No commit until evidence durable; execution approval |
| Executing or PostconditionsChecking | Confirmed pre-commit error or interruption occurs before rollback initiation | RollbackRequested | Commit prohibited; preserve primary error; provider evidence |
| PostconditionsChecking | Required pre-commit evidence persistence fails before rollback initiation | PRECOMMIT_EVIDENCE_FAILURE | Commit prohibited; preserve primary/secondary errors; Security/Release |
| PRECOMMIT_EVIDENCE_FAILURE | Rollback request is durably acknowledged | RollbackOutcomePending | No retry; provider rollback validation |
| PRECOMMIT_EVIDENCE_FAILURE | Provider evidence proves rollback was already initiated, but request record was not acknowledged | RollbackOutcomePending | No retry; preserve evidence; provider validation |
| PRECOMMIT_EVIDENCE_FAILURE | Neither rollback request nor provider initiation can be proven | AUDIT_STATE_UNCERTAIN | No retry; independent marker; Security/Recovery |
| RollbackRequested | Rollback acknowledgement and observation begin | RollbackOutcomePending | No retry; preserve primary error; provider validation |
| RollbackOutcomePending | Rollback and lock release are both conclusive; final evidence durable | RolledBack | No migration retry; replay audited; database/recovery owner |
| RollbackOutcomePending | Rollback is conclusive, but lock release failed or is ambiguous | PRECOMMIT_RELEASE_FAILURE | No retry; release reconciliation; database/recovery owner |
| RollbackOutcomePending | Rollback failed or rollback outcome is ambiguous | RollbackUnconfirmed | No retry; recovery required; preserve primary/rollback errors |
| RollbackOutcomePending | Rollback is conclusive but final evidence alone is not durable and release is conclusive | AUDIT_STATE_UNCERTAIN | No retry; independent marker; Security/Compliance |
| PRECOMMIT_RELEASE_FAILURE | Independent evidence proves release and non-overlap | RolledBack | No retry under attempt; release evidence retained; database/recovery owner |
| PRECOMMIT_RELEASE_FAILURE | Release or non-overlap remains uncertain | ReconciliationPending | No retry; quarantine/recovery decision; provider evidence |
| PostconditionsChecking | Checks and all pre-commit evidence are durable | CommitRequested | Commit only after execution/recovery prerequisites; provider commit validation |
| CommitRequested | Commit response and success evidence are conclusive and durable | Committed | No retry; terminal success; provider acknowledgement |
| CommitRequested | Commit response is conclusive but success evidence is absent/ambiguous | COMMITTED_EVIDENCE_PENDING | Known commit; never rerun; Security/Compliance |
| CommitRequested | Commit response is non-conclusive | UNKNOWN_COMMIT | Non-retryable; read-only reconciliation; recovery owner |
| Committed | Commit is known, but lock release fails or is ambiguous | COMMITTED_RELEASE_FAILURE | No new attempt; prove non-overlap or quarantine; database/recovery owner |
| COMMITTED_EVIDENCE_PENDING | Immutable repair links the attempt to a known commit | Committed | Audit repair only; no rerun |
| COMMITTED_EVIDENCE_PENDING | Evidence cannot be linked or persistence remains uncertain | AUDIT_STATE_UNCERTAIN | No retry; independent audit reconciliation; Security/Recovery |
| UNKNOWN_COMMIT or RollbackUnconfirmed or COMMITTED_RELEASE_FAILURE | Recovery record and required marker are durable | ReconciliationPending | Read-only first; no execution; recovery owner |
| ReconciliationPending | Bound historical evidence proves this attempt committed | ReconciledCommitted | No retry; outcome-specific recovery decision |
| ReconciliationPending | Bound historical evidence proves rollback after transaction began | ReconciledRolledBack | No retry; recovery decision |
| ReconciliationPending | Bound evidence proves transaction never began or never committed, distinct from rollback | ReconciledNeverCommitted | Old replay remains consumed unless approved release; no retry |
| ReconciliationPending | Effects/contradictions prove incomplete or partial state | ReconciledPartial | No retry; recovery decision |
| ReconciliationPending | Evidence is absent, stale, contradictory, or cannot distinguish history | ReconciledIndeterminate | No retry; quarantine/evidence recovery |
| ReconciliationPending | Target/evidence source is unavailable and no historical class can be proven | ReconciledIndeterminate | No mutation; quarantine; recovery owner |
| ReconciledCommitted | Verification or separately approved corrective plan plus non-overlap proof is durable | RecoveryDecisionPending | No rerun; recovery owner/execution authority separate |
| ReconciledRolledBack | Verification/no-op or separately approved restore plan plus non-overlap proof is durable | RecoveryDecisionPending | No rerun; recovery owner |
| ReconciledNeverCommitted | Verification/no-op plan plus non-overlap proof is durable | RecoveryDecisionPending | No automatic replay; Security/Recovery |
| ReconciledPartial | Scoped quarantine/restore/forward-repair plan plus non-overlap proof is durable | RecoveryDecisionPending | No rerun; recovery/database owner |
| ReconciledIndeterminate | Quarantine/evidence-recovery plan plus non-overlap proof is durable | RecoveryDecisionPending | No rerun; recovery owner |
| RecoveryDecisionPending | Plan hash, executor, scope, target, backup, replay disposition, and owner approval are durable | RecoveryExecuting | Recovery only; provider plan validation |
| RecoveryExecuting | Recovery operation ends with immutable operation record acknowledged | PostRecoveryVerification | No migration; recovery executor |
| PostRecoveryVerification | Manifests, invariants, target, custody, and non-overlap proof pass | RecoveryCompleted | No migration authorization; recovery owner |
| PostRecoveryVerification | Any verification fails or is ambiguous | RecoveryFailed | Quarantine; no retry; recovery owner |
| RecoveryFailed | New evidence and approved plan establish safe next recovery/reconciliation | RecoveryRequired | No execution; fresh decision/evidence |
| RecoveryFailed | No safe plan or non-overlap proof exists | Quarantined | Terminal hold; no retry; recovery/security owner |
| RecoveryIndeterminate | Evidence becomes sufficient for one reconciliation class | ReconciliationPending | No mutation; recovery owner |
| RecoveryIndeterminate | Evidence remains insufficient/conflicting | Quarantined | Terminal hold; no retry; recovery/security owner |
| RecoveryRequired | Read-only target, lock, replay, and historical evidence can be collected | ReconciliationPending | No execution; recovery owner/provider |
| RecoveryRequired | Evidence or non-overlap cannot be established | Quarantined | Terminal hold; no retry; security/recovery owner |
| AUDIT_STATE_UNCERTAIN | Independent channel proves no connection/effect and replay disposition is approved | PreflightFailed | No connection; Security/Compliance; durable evidence |
| AUDIT_STATE_UNCERTAIN | Independent channel proves connection/effect may exist and reconciliation can run | RecoveryRequired | No execution; recovery owner; marker evidence |
| AUDIT_STATE_UNCERTAIN | Independent channel is unavailable or records conflict | Quarantined | No retry; out-of-band recovery authority |
| Failed | Owner-approved closure proves no transaction, lock, or migration effect | Failed (terminal closure) | No retry; durable diagnostic/replay record |
| Failed | Transaction/lock/effect cannot be excluded | RecoveryRequired | No execution; independent reconciliation |
| PreflightFailed | Terminal pre-connection denial record is durable | PreflightFailed | No connection; retry only as a new approved attempt |
| Quarantined | New recovery plan and non-overlap proof are durable | RecoveryDecisionPending | No migration rerun; recovery/security owner |
| Quarantined | No approved plan or evidence exists | Quarantined | Terminal hold; immutable audit record |
| RecoveryCompleted | Fresh authorization, nonce, target, baseline, and execution approval succeed | PreflightPending with new attempt ID | Never reuse old nonce/attempt |

PreflightFailed is exclusively a pre-connection denial. Failed is a post-start terminal closure. No uncertain, audit-uncertain, reconciled, recovery, failed, or quarantined state can transition directly to migration execution.

## 3. Historical outcome evidence model

Current catalog objects, rows, sequences, defaults, constraints, indexes, or postconditions are observations, not historical proof. Definitive committed, rolled-back, or never-committed results require an independently trusted append-only or tamper-evident record bound to migration 026, exact candidate hash, canonical target, attempt/authorization tuple, transaction/session correlation when available, outcome/time, and persistence surviving process and connection failure. No ledger is asserted to exist.

| Outcome | Required evidence | Effect |
|---|---|---|
| ReconciledCommitted | Bound record proves this attempt committed and current state/postconditions agree | No rerun; verification/corrective recovery only with approval |
| ReconciledRolledBack | Bound record proves transaction began and rollback completed with no partial effects | No rerun; recovery decision required |
| ReconciledNeverCommitted | Bound record proves transaction never began or never committed, distinct from rollback, with no effects | Replay release only by Security/Compliance approval; fresh authorization required |
| ReconciledPartial | Independent evidence proves incomplete/contradictory effects | Quarantine/approved repair only |
| ReconciledIndeterminate | Historical evidence absent, stale, conflicting, or insufficient | Quarantine/evidence recovery; nonce blocked |

Current-state similarity alone always yields ReconciledIndeterminate.

## 4. Lock ownership, release, and non-overlap

No production mechanism is selected. Transaction advisory evidence must prove approved key derivation, same physical connection/transaction, acquisition, transaction end, and release after commit/rollback/cancellation/disposal/termination. Session advisory evidence must additionally prove backend/session identity, pool binding, explicit release, termination, and pool isolation or disabled pooling. Durable records remain ineligible until provider-tested fencing rejects obsolete tokens before every migration and postcondition operation. Lock disappearance, timeout, missing records, lease expiry, or incomplete responses never prove denial or permit lock stealing.

A subsequent attempt requires mechanism-specific proof that the former owner cannot execute, the prior session/transaction ended or was fenced, release is conclusive, and the new owner has a fresh lock/token.

## 5. Recovery and terminal-state rules

Recovery mutation requires RecoveryDecisionPending with plan hash, executor, target verification, non-overlap proof, backup/baseline, replay disposition, owner approval, and immutable operation record. RecoveryCompleted never authorizes migration execution.

PreflightFailed is terminal for that pre-connection attempt. Failed is terminal only after proof of no effect; otherwise it must move to RecoveryRequired. Quarantined is terminal until a separately approved recovery plan and non-overlap proof exist. Every terminal record retains durable state, replay disposition, lock evidence, and integrity references.

## 6. Out-of-band recovery contract

The out-of-band recovery authority is an accountable recovery-owner role to be assigned by governance; no service or person is assumed to exist. When both primary and independent audit stores are unavailable, the runner must fail closed, prohibit all attempts for the target/hash/authorization tuple, and create no claim of durable state.

The recovery authority must open an externally controlled incident/artifact identifier and preserve, at minimum, attempt ID, authorization ID/nonce, migration ID/hash, canonical target digest, phase, last acknowledged state, timestamps, error/transport digests, and custody references. The artifact must be immutable or tamper-evident, access-controlled, independently reviewed by Security/Compliance and the database/recovery owners, and correlated to later store reconciliation.

Completion requires: both stores restored or independently reconciled; conflicting records resolved; replay consumed/released according to approved policy; target/hash/non-overlap evidence revalidated; final state written and acknowledged; independent reviewer sign-off; and no automatic retry. If the authority or artifact mechanism is not approved, this is a hard implementation blocker and the attempt remains Quarantined indefinitely.

## 7. Replay/audit atomicity design gate

Before implementation, Security/Compliance and Release owners must select and approve one design. Design A is not implementation-ready until its artifact identifies all participants, transaction boundary, acknowledgement, durability, idempotency, duplicate handling, crash behavior, and independently reviewed proof of atomicity. A database transaction plus unrelated log is not atomic by assertion.

Design B is the fallback: a primary attempt/audit store plus an owner-approved independent append-only marker channel. The marker includes attempt ID, authorization ID/nonce, migration/hash, target digest, phase, monotonic sequence, timestamp, and error digest, with its own durability acknowledgement. Marker writes are idempotent; conflicts quarantine. Before-marker, after-marker/before-primary, and after-both acknowledgements remain blocked until reconciled. If both stores fail, section 6 applies.

No nonce may be released for UNKNOWN_COMMIT, ownership uncertainty, partial/indeterminate outcomes, recovery failure, unresolved audit state, or quarantine. A preflight denial may be released only under the approved policy and durable denial evidence.

## 8. Phase-specific interruption matrix

| Phase | Evidence predicate | Exact state | Guarantee |
|---|---|---|---|
| Preflight | Durable denial proves no connection | PreflightFailed | No connection/retry under attempt |
| Preflight | Any consume/audit uncertainty | AUDIT_STATE_UNCERTAIN | No nonce reuse |
| Connection opening | No session/effect conclusively proven | Failed | No transaction |
| Connection opening | Any ambiguity/loss/termination | RecoveryRequired | No transaction/retry |
| Transaction start | Transaction identity acknowledged | TransactionStarted | Same connection |
| Transaction start | No transaction/effect conclusively proven | Failed | No execution |
| Transaction start | Any ambiguity/loss/termination | RecoveryRequired | No execution |
| Lock acquisition | Proven denial | Failed | No lock stealing |
| Lock acquisition | Any ownership ambiguity | LOCK_OWNERSHIP_UNCERTAIN | No execution |
| Migration/postconditions | Error/interruption before rollback | RollbackRequested | Commit prohibited |
| Pre-commit evidence | Persistence failure | PRECOMMIT_EVIDENCE_FAILURE | Rollback ordering required |
| Rollback | Request acknowledged | RollbackOutcomePending | No retry |
| Rollback | Rollback+release proven | RolledBack | No success ambiguity |
| Rollback | Rollback proven, release uncertain | PRECOMMIT_RELEASE_FAILURE | Non-overlap reconciliation |
| Rollback | Rollback failed/ambiguous | RollbackUnconfirmed | Recovery required |
| Commit in flight | Loss/cancel/disposal/timeout | UNKNOWN_COMMIT | No retry |
| Known commit | Release failure/ambiguity | COMMITTED_RELEASE_FAILURE | Not rollback/UNKNOWN_COMMIT |
| Post-commit evidence | Evidence absent/ambiguous | COMMITTED_EVIDENCE_PENDING or AUDIT_STATE_UNCERTAIN by store predicate | Never rerun |
| Recovery | Operation interrupted/ambiguous | RecoveryFailed | Quarantine/no migration |
| Post-recovery | Verification fails/ambiguous | RecoveryIndeterminate | Quarantine/no future attempt |

## 9. Proposed PostgreSQL/Npgsql validation plan

These are unexecuted plans requiring an approved disposable target, fixed candidate hash, independent identity verification, provider/version capture, database-owner approval, and independent evidence review.

| Validation | Controlled fixture/injection | Expected evidence and state | Negative assertion |
|---|---|---|---|
| Connection open/start | Refusal, timeout, cancellation, disposal, termination at open and BEGIN | ConnectionOpening/TransactionStarting evidence and exact Failed or RecoveryRequired classification | No execution on ambiguity |
| Advisory contention/release | Two physical connections; commit/rollback/cancel/dispose/terminate | Ownership/session identity, release, transaction end | No overlap; disappearance alone insufficient |
| Session pooling | Checkout/reuse/reconnect/backend termination | Pool binding and explicit release | Former session cannot overlap |
| Durable fencing | Two owners; token change before every operation | Obsolete token rejected per operation | One-time check fails validation |
| Rollback/release | Fault migration, rollback, release separately | RolledBack, PRECOMMIT_RELEASE_FAILURE, or RollbackUnconfirmed | No conflation or false success |
| Commit ambiguity | Fault after commit request before acknowledgement | UNKNOWN_COMMIT and reconciliation evidence | No retry |
| Evidence stores | Fail primary before/after ack; fail marker; fail both | Correct AUDIT_STATE_UNCERTAIN/Quarantined path | No durable claim when stores unavailable |
| Historical reconciliation | Ledger present/absent with coincidental current state | Definitive only with bound history; otherwise indeterminate | Catalog match alone insufficient |
| Recovery | Interrupted restore/repair/quarantine and post-check | RecoveryFailed/Indeterminate or RecoveryCompleted | Completion cannot authorize migration |

Provider version, privilege, fault injection, pooling, retention, and fixture details require owner decisions. Unit fakes cannot establish provider guarantees.

## 10. Runner capability gap

`Runner.cs:7` exposes only five states and lacks all v6 states, including connection phases, audit uncertainty, ownership uncertainty, pre-commit release failure, reconciliation, recovery, and quarantine. `Runner.cs:13-23` lacks replay, independent marker, durable state, lock release/fencing, postconditions, reconciliation, and recovery interfaces. `Runner.cs:82-89` maps rollback failure to `UnknownCommit`, not the proposed rollback/release distinctions. `Program.cs:1-2` remains disabled through `NoExecutionPolicy`.

The runner does not implement this contract. Interfaces and fakes are not provider evidence, and the entry point must remain disabled.

## 11. Owner decisions and blockers

| Decision | Proposed rule | Owner | Evidence/blocker |
|---|---|---|---|
| Connection/transaction evidence | Use provider-specific predicates above; no fallback on ambiguity | PostgreSQL/provider owner | Lifecycle fault traces; provider validation |
| Lock mechanism/fencing | No production selection; durable lock excluded initially | Database owner | Contention, release, crash, fencing evidence |
| Historical ledger | Required for definitive history | PostgreSQL/recovery owner | Integrity, retention, target/hash/attempt binding |
| Audit design | Select reviewed Design A or Design B before implementation | Security/Compliance/Release | Threat, crash, idempotency, custody review |
| Out-of-band authority | Assign accountable recovery role and artifact process | Recovery/Security governance | Incident/custody/reconciliation procedure |
| Replay disposition | Explicit consume/release policy; unsafe states never release | Security/Compliance | Replay race/crash evidence |
| Recovery actions | Outcome-specific plans and independent verification | Recovery owner | Recovery rehearsal |
| Provider validation | No claims before Npgsql/PostgreSQL evidence | PostgreSQL/provider owner | Section 9 matrix |
| Execution | Separate approval after every gate | Execution authority | Complete evidence package |

Gate T32 findings are addressed by the explicit reachable state machine, deterministic predicates, separate pre-commit release failure, terminal semantics, out-of-band contract, mandatory atomicity gate, and provider plan above. Independent audit remains required.

This proposal creates no approvals, credentials, target identities, ledger, marker channel, fencing mechanism, or provider evidence. It does not authorize implementation, provider testing, database access, or migration execution.

**GATE T33 COMPLETE — CONTRACT V6 DRAFTED, FINAL INDEPENDENT AUDIT REQUIRED**
