namespace PA.Tickets.Infrastructure.Execution;

/// Captures process-local worker activity for readiness; the durable job and communication states remain the recovery source of truth.
public sealed class WorkerActivityRegistry
{
    private long _lastPollUtcTicks;
    public DateTimeOffset? LastPollUtc
    {
        get { var ticks = Interlocked.Read(ref _lastPollUtcTicks); return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero); }
    }
    public void MarkPoll() { Interlocked.Exchange(ref _lastPollUtcTicks, DateTimeOffset.UtcNow.UtcTicks); OperationalMetrics.SetWorkerAvailable(true); OperationalMetrics.WorkerPolls.Add(1); }
}