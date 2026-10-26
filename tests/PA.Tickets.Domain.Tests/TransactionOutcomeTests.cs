using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class TransactionOutcomeTests
{
    [Fact]
    public void Successful_commit_is_confirmed_and_successful()
    {
        var result = TransactionOutcome.ConfirmedCommitted();

        Assert.Equal(TransactionOutcomeStatus.ConfirmedCommitted, result.Status);
        Assert.Equal(TransactionOwnership.LocallyOwned, result.Ownership);
        Assert.True(result.IsSuccessful);
        Assert.False(result.RetryAllowed);
    }

    [Fact]
    public void Work_failure_with_confirmed_abort_is_retryable_and_preserves_primary_error()
    {
        var primary = new InvalidOperationException("work failed");

        var result = TransactionOutcomeMapping.WorkFailed(primary, abortConfirmed: true);

        Assert.Equal(TransactionOutcomeStatus.ConfirmedAborted, result.Status);
        Assert.Same(primary, result.PrimaryException);
        Assert.True(result.RetryAllowed);
    }

    [Fact]
    public void Work_failure_with_abort_failure_is_unknown_and_preserves_both_errors()
    {
        var primary = new InvalidOperationException("work failed");
        var cleanup = new InvalidOperationException("abort failed");

        var result = TransactionOutcomeMapping.WorkFailed(primary, abortConfirmed: false, cleanup);

        Assert.Equal(TransactionOutcomeStatus.OutcomeUnknown, result.Status);
        Assert.Same(primary, result.PrimaryException);
        Assert.Same(cleanup, result.CleanupException);
        Assert.False(result.RetryAllowed);
    }

    [Fact]
    public void Abort_exception_overrides_a_contradictory_confirmed_flag()
    {
        var primary = new InvalidOperationException("work failed");
        var cleanup = new InvalidOperationException("abort failed");

        var result = TransactionOutcomeMapping.WorkFailed(primary, abortConfirmed: true, cleanup);

        Assert.Equal(TransactionOutcomeStatus.OutcomeUnknown, result.Status);
        Assert.Same(primary, result.PrimaryException);
        Assert.Same(cleanup, result.CleanupException);
    }

    [Fact]
    public void Commit_failure_with_explicit_abort_evidence_is_confirmed_aborted()
    {
        var commitFailure = new InvalidOperationException("commit failed");

        var result = TransactionOutcomeMapping.CommitFailed(commitFailure, abortConfirmed: true);

        Assert.Equal(TransactionOutcomeStatus.ConfirmedAborted, result.Status);
        Assert.Same(commitFailure, result.PrimaryException);
    }

    [Fact]
    public void Commit_failure_without_abort_evidence_is_unknown_and_not_retryable()
    {
        var commitFailure = new InvalidOperationException("commit outcome unavailable");

        var result = TransactionOutcomeMapping.CommitFailed(commitFailure, abortConfirmed: false);

        Assert.Equal(TransactionOutcomeStatus.OutcomeUnknown, result.Status);
        Assert.Same(commitFailure, result.PrimaryException);
        Assert.False(result.RetryAllowed);
        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public void Connection_loss_during_commit_is_unknown()
    {
        var connectionFailure = new IOException("connection lost");

        var result = TransactionOutcomeMapping.ConnectionLostDuringCommit(connectionFailure);

        Assert.Equal(TransactionOutcomeStatus.OutcomeUnknown, result.Status);
        Assert.Same(connectionFailure, result.PrimaryException);
        Assert.False(result.RetryAllowed);
    }

    [Fact]
    public void Disposal_failure_before_outcome_evidence_is_unknown()
    {
        var disposalFailure = new InvalidOperationException("dispose failed");

        var result = TransactionOutcomeMapping.DisposalFailed(disposalFailure);

        Assert.Equal(TransactionOutcomeStatus.OutcomeUnknown, result.Status);
        Assert.Same(disposalFailure, result.PrimaryException);
        Assert.False(result.RetryAllowed);
    }

    [Fact]
    public void Caller_owned_transaction_remains_pending_and_is_not_local_success()
    {
        var result = TransactionOutcomeMapping.OuterWorkCompleted();

        Assert.Equal(TransactionOutcomeStatus.OuterCommitPending, result.Status);
        Assert.Equal(TransactionOwnership.CallerOwned, result.Ownership);
        Assert.False(result.IsSuccessful);
        Assert.False(result.RetryAllowed);
    }

    [Fact]
    public void Audit_callback_failure_uses_the_same_abort_or_unknown_mapping()
    {
        var auditFailure = new InvalidOperationException("audit write failed");

        var aborted = TransactionOutcomeMapping.WorkFailed(auditFailure, abortConfirmed: true);
        var unknown = TransactionOutcomeMapping.WorkFailed(auditFailure, abortConfirmed: false);

        Assert.Equal(TransactionOutcomeStatus.ConfirmedAborted, aborted.Status);
        Assert.Equal(TransactionOutcomeStatus.OutcomeUnknown, unknown.Status);
        Assert.Same(auditFailure, aborted.PrimaryException);
        Assert.Same(auditFailure, unknown.PrimaryException);
    }

    [Fact]
    public void Authorization_denial_is_not_a_transaction_outcome_or_execution_callback()
    {
        var callbackInvoked = false;
        var authorized = false;

        var result = ExecuteAfterAuthorization(authorized, () =>
        {
            callbackInvoked = true;
            return TransactionOutcome.ConfirmedCommitted();
        });

        Assert.False(callbackInvoked);
        Assert.Null(result);
    }

    private static TransactionOutcome? ExecuteAfterAuthorization(bool authorized, Func<TransactionOutcome> callback) =>
        authorized ? callback() : null;
}
