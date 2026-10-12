using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PA.Tickets.Infrastructure.Execution;

public static class OperationalMetrics
{
    public const string InstrumentationName = "PA.Tickets.Operational";
    private static long _workerAvailable;
    private static long _databaseAvailable;
    public static readonly ActivitySource ActivitySource = new(InstrumentationName);
    public static readonly Meter Meter = new(InstrumentationName, "1.0.0");
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("pa_tickets.requests", "requests");
    public static readonly Counter<long> RequestFailures = Meter.CreateCounter<long>("pa_tickets.request.failures", "requests");
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("pa_tickets.request.duration", "ms");
    public static readonly Counter<long> JobsQueued = Meter.CreateCounter<long>("pa_tickets.jobs.queued", "jobs");
    public static readonly Counter<long> JobsLeased = Meter.CreateCounter<long>("pa_tickets.jobs.leased", "jobs");
    public static readonly Counter<long> JobCompleted = Meter.CreateCounter<long>("pa_tickets.jobs.completed", "jobs");
    public static readonly Counter<long> JobFailed = Meter.CreateCounter<long>("pa_tickets.jobs.failed", "jobs");
    public static readonly Counter<long> JobDeadLettered = Meter.CreateCounter<long>("pa_tickets.jobs.dead_lettered", "jobs");
    public static readonly Counter<long> JobRetries = Meter.CreateCounter<long>("pa_tickets.jobs.retries", "jobs");
    public static readonly Histogram<double> JobProcessingDuration = Meter.CreateHistogram<double>("pa_tickets.jobs.processing.duration", "ms");
    public static readonly Counter<long> WorkerFailures = Meter.CreateCounter<long>("pa_tickets.worker.failures", "failures");
    public static readonly Counter<long> WorkerPolls = Meter.CreateCounter<long>("pa_tickets.worker.polls", "polls");
    public static readonly Counter<long> CommunicationAttempts = Meter.CreateCounter<long>("pa_tickets.communication.attempts", "attempts");
    public static readonly Counter<long> CommunicationDelivered = Meter.CreateCounter<long>("pa_tickets.communication.delivered", "deliveries");
    public static readonly Counter<long> CommunicationFailed = Meter.CreateCounter<long>("pa_tickets.communication.failed", "deliveries");
    public static readonly Counter<long> CommunicationRetries = Meter.CreateCounter<long>("pa_tickets.communication.retries", "deliveries");
    public static readonly Counter<long> DatabaseFailures = Meter.CreateCounter<long>("pa_tickets.database.failures", "failures");
    public static readonly ObservableGauge<long> WorkerAvailability = Meter.CreateObservableGauge("pa_tickets.worker.available", () => Interlocked.Read(ref _workerAvailable));
    public static readonly ObservableGauge<long> DatabaseAvailability = Meter.CreateObservableGauge("pa_tickets.database.available", () => Interlocked.Read(ref _databaseAvailable));
    public static void SetWorkerAvailable(bool available) => Interlocked.Exchange(ref _workerAvailable, available ? 1 : 0);
    public static void SetDatabaseAvailable(bool available) => Interlocked.Exchange(ref _databaseAvailable, available ? 1 : 0);
}