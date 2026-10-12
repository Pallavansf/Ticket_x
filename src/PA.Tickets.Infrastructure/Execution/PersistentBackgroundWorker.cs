using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PA.Tickets.Application.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public interface IBackgroundJobHandler { bool CanHandle(string type); Task HandleAsync(BackgroundJobEnvelope job, CancellationToken ct); }

public static class BackgroundJobFailureClassifier
{
    public static bool IsRetryable(Exception exception, IDatabaseProviderCapabilities capabilities)
    {
        if (exception is TimeoutException) return true;
        if (exception is HttpRequestException http) return http.StatusCode is null || http.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests || (int)http.StatusCode.Value >= 500;
        if (exception is OperationCanceledException) return true;
        if (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException or JsonException) return false;
        return capabilities.IsTransientTransactionFailure(exception);
    }
}

/// Leases durable jobs; persisted next-attempt state remains authoritative across process restarts.
public sealed class PersistentBackgroundWorker(IServiceScopeFactory scopes, ILogger<PersistentBackgroundWorker> log, WorkerActivityRegistry activity, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Process(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { OperationalMetrics.WorkerFailures.Add(1, new KeyValuePair<string, object?>("worker", "jobs")); OperationalMetrics.SetWorkerAvailable(false); log.LogError("Background job cycle failed {FailureType}; durable rows remain available for recovery.", ex.GetType().Name); }
        }
    }

    internal static async Task<int> EnqueueDueSchedulesAsync(ExecutionDbContext db, IBackgroundJobRepository repo, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.ReportSchedules.AsNoTracking().Where(x => x.Status == "ACTIVE" && x.NextRunAt <= now)
            .OrderBy(x => x.NextRunAt).ThenBy(x => x.Id).Take(25).ToListAsync(ct);
        var enqueued = 0;
        foreach (var schedule in due)
        {
            var dedupe = $"report-schedule:{schedule.Id}:{schedule.NextRunAt:O}";
            if (await repo.EnqueueAsync("REPORT_SCHEDULE", dedupe, JsonSerializer.Serialize(new { ScheduleId = schedule.Id }), schedule.NextRunAt, ct))
                enqueued++;
        }
        return enqueued;
    }
    internal Task ProcessOnceAsync(CancellationToken ct) => Process(ct);

    private async Task Process(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().ToArray();
        if (handlers.Length == 0) return;
        var repo = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        var capabilities = scope.ServiceProvider.GetRequiredService<IDatabaseProviderCapabilities>();
        await EnqueueDueSchedulesAsync(db, repo, clock, ct);
        await scope.ServiceProvider.GetRequiredService<SlaDeadlineMonitor>().ProcessDueAsync(ct);
        var jobs = await repo.LeaseReadyAsync(Environment.MachineName, 25, ct);
        activity.MarkPoll();
        foreach (var job in jobs)
        {
            var handler = handlers.FirstOrDefault(x => x.CanHandle(job.Type));
            if (handler is null)
            {
                await repo.FailAsync(job.Id, "No handler is registered for this job type.", true, ct);
                continue;
            }
            var jobStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using var activity = OperationalMetrics.ActivitySource.StartActivity("background_job.process");
            activity?.SetTag("job.id", job.Id);
            activity?.SetTag("job.type", job.Type);
            log.LogInformation("Durable job started {JobId} {JobType}", job.Id, job.Type);
            try
            {
                await handler.HandleAsync(job, ct);
                await repo.CompleteAsync(job.Id, ct);
                OperationalMetrics.JobCompleted.Add(1, new KeyValuePair<string, object?>("job.type", job.Type));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                var retryable = BackgroundJobFailureClassifier.IsRetryable(ex, capabilities);
                OperationalMetrics.JobFailed.Add(1, new KeyValuePair<string, object?>("job.type", job.Type), new KeyValuePair<string, object?>("failure.class", retryable ? "transient" : "permanent"));
                log.LogError("Durable job failed {JobId} {JobType} {FailureType} {FailureClassification}", job.Id, job.Type, ex.GetType().Name, retryable ? "transient" : "permanent");
                await repo.FailAsync(job.Id, $"{ex.GetType().Name}: {ex.Message}", !retryable, ct);
            }
        }
    }
}