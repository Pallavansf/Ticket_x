namespace PA.Tickets.Infrastructure.Persistence;

/// <summary>
/// The evidence-backed terminal state of a transaction execution attempt.
/// This type contains no provider-specific behavior and does not serialize
/// exception details or connection information.
/// </summary>
public enum TransactionOutcomeStatus
{
    ConfirmedCommitted,
    ConfirmedAborted,
    OutcomeUnknown,
    OuterCommitPending
}

public enum TransactionOwnership
{
    LocallyOwned,
    CallerOwned
}

public sealed record TransactionOutcome
{
    private TransactionOutcome(
        TransactionOutcomeStatus status,
        TransactionOwnership ownership,
        Exception? primaryException,
        Exception? cleanupException)
    {
        Status = status;
        Ownership = ownership;
        PrimaryException = primaryException;
        CleanupException = cleanupException;
    }

    public TransactionOutcomeStatus Status { get; }
    public TransactionOwnership Ownership { get; }
    public Exception? PrimaryException { get; }
    public Exception? CleanupException { get; }

    /// <summary>Only a confirmed abort is safe for a caller to retry.</summary>
    public bool RetryAllowed => Status == TransactionOutcomeStatus.ConfirmedAborted;

    /// <summary>Only a confirmed local commit is execution success.</summary>
    public bool IsSuccessful => Status == TransactionOutcomeStatus.ConfirmedCommitted;

    public static TransactionOutcome ConfirmedCommitted(Exception? cleanupException = null) =>
        new(TransactionOutcomeStatus.ConfirmedCommitted, TransactionOwnership.LocallyOwned, null, cleanupException);

    public static TransactionOutcome ConfirmedAborted(
        Exception? primaryException = null,
        Exception? cleanupException = null) =>
        new(TransactionOutcomeStatus.ConfirmedAborted, TransactionOwnership.LocallyOwned, primaryException, cleanupException);

    public static TransactionOutcome Unknown(
        Exception? primaryException = null,
        Exception? cleanupException = null,
        TransactionOwnership ownership = TransactionOwnership.LocallyOwned) =>
        new(TransactionOutcomeStatus.OutcomeUnknown, ownership, primaryException, cleanupException);

    public static TransactionOutcome OuterCommitPending() =>
        new(TransactionOutcomeStatus.OuterCommitPending, TransactionOwnership.CallerOwned, null, null);
}

/// <summary>
/// Maps provider-independent execution signals to the outcome contract.
/// Callers must supply explicit abort evidence; an exception is never treated
/// as proof that a transaction rolled back.
/// </summary>
public static class TransactionOutcomeMapping
{
    public static TransactionOutcome WorkFailed(
        Exception primaryException,
        bool abortConfirmed,
        Exception? abortException = null) =>
        abortConfirmed && abortException is null
            ? TransactionOutcome.ConfirmedAborted(primaryException)
            : TransactionOutcome.Unknown(primaryException, abortException);

    public static TransactionOutcome CommitFailed(
        Exception primaryException,
        bool abortConfirmed,
        Exception? cleanupException = null) =>
        abortConfirmed && cleanupException is null
            ? TransactionOutcome.ConfirmedAborted(primaryException, cleanupException)
            : TransactionOutcome.Unknown(primaryException, cleanupException);

    public static TransactionOutcome ConnectionLostDuringCommit(Exception primaryException) =>
        TransactionOutcome.Unknown(primaryException);

    public static TransactionOutcome DisposalFailed(
        Exception disposalException,
        Exception? primaryException = null) =>
        TransactionOutcome.Unknown(primaryException ?? disposalException, primaryException is null ? null : disposalException);

    public static TransactionOutcome OuterWorkCompleted() =>
        TransactionOutcome.OuterCommitPending();
}
