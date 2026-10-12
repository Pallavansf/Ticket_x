using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PA.Tickets.Application;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlReportScheduleExecutionTests
{
    [Fact]
    public async Task Worker_enqueues_only_due_active_schedules_and_retries_without_duplicate_execution()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var storageRoot = Path.Combine(Path.GetTempPath(), $"pa-tickets-report-schedule-{Guid.NewGuid():N}");
        await using var provider = BuildProvider(storageRoot);
        var activeScheduleId = 0L;
        var pausedScheduleId = 0L;
        var futureScheduleId = 0L;
        var dedupeKey = "";
        try
        {
            var now = DateTimeOffset.UtcNow;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var filters = System.Text.Json.JsonSerializer.Serialize(new ReportQueryRequest(Page: 1, PageSize: 25));
                var active = new ReportScheduleRow
                {
                    OwnerUserId = 9001,
                    ReportCode = "ticket-status-summary",
                    Format = "CSV",
                    FiltersJson = filters,
                    Frequency = "DAILY",
                    LocalHour = now.Hour,
                    LocalMinute = now.Minute,
                    TimeZone = "UTC",
                    NextRunAt = now.AddMinutes(-2),
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var paused = new ReportScheduleRow
                {
                    OwnerUserId = 9001,
                    ReportCode = "ticket-status-summary",
                    Format = "CSV",
                    FiltersJson = filters,
                    Frequency = "DAILY",
                    LocalHour = now.Hour,
                    LocalMinute = now.Minute,
                    TimeZone = "UTC",
                    NextRunAt = now.AddMinutes(-2),
                    Status = "PAUSED",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var future = new ReportScheduleRow
                {
                    OwnerUserId = 9001,
                    ReportCode = "ticket-status-summary",
                    Format = "CSV",
                    FiltersJson = filters,
                    Frequency = "DAILY",
                    LocalHour = now.Hour,
                    LocalMinute = now.Minute,
                    TimeZone = "UTC",
                    NextRunAt = now.AddDays(1),
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.ReportSchedules.AddRange(active, paused, future);
                await db.SaveChangesAsync();
                activeScheduleId = active.Id;
                pausedScheduleId = paused.Id;
                futureScheduleId = future.Id;
                var persistedRunAt = await db.ReportSchedules.Where(x => x.Id == active.Id).Select(x => x.NextRunAt).SingleAsync();
                dedupeKey = $"report-schedule:{active.Id}:{persistedRunAt:O}";

                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                Assert.Equal(1, await PersistentBackgroundWorker.EnqueueDueSchedulesAsync(db, jobs, TimeProvider.System, CancellationToken.None));
                Assert.Equal(0, await PersistentBackgroundWorker.EnqueueDueSchedulesAsync(db, jobs, TimeProvider.System, CancellationToken.None));
                Assert.Single(await db.Jobs.AsNoTracking().Where(x => x.DedupeKey == dedupeKey).ToArrayAsync());
                Assert.False(await db.Jobs.AsNoTracking().AnyAsync(x => x.Type == "REPORT_SCHEDULE" && x.DedupeKey.StartsWith($"report-schedule:{paused.Id}:")));
                Assert.False(await db.Jobs.AsNoTracking().AnyAsync(x => x.Type == "REPORT_SCHEDULE" && x.DedupeKey.StartsWith($"report-schedule:{future.Id}:")));
            }

            var logger = provider.GetRequiredService<ILogger<PersistentBackgroundWorker>>();
            var worker = new PersistentBackgroundWorker(
                provider.GetRequiredService<IServiceScopeFactory>(),
                logger,
                provider.GetRequiredService<WorkerActivityRegistry>(),
                TimeProvider.System);
            await worker.ProcessOnceAsync(CancellationToken.None);

            long exportId;
            BackgroundJobEnvelope retryEnvelope;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var jobState = await db.Jobs.SingleAsync(x => x.DedupeKey == dedupeKey);
                Assert.True(jobState.Status == "COMPLETED", $"Job status was {jobState.Status}; last error: {jobState.LastError}");
                var execution = await db.ReportScheduleExecutions.SingleAsync(x => x.ScheduleId == activeScheduleId);
                Assert.Equal("COMPLETED", execution.Status);
                exportId = Assert.IsType<long>(execution.ExportId);
                Assert.True(execution.CompletedAt.HasValue);
                Assert.True((await db.ReportSchedules.SingleAsync(x => x.Id == activeScheduleId)).NextRunAt > DateTimeOffset.UtcNow);
                Assert.Empty(await db.ReportScheduleExecutions.Where(x => x.ScheduleId == pausedScheduleId || x.ScheduleId == futureScheduleId).ToArrayAsync());

                var job = await db.Jobs.SingleAsync(x => x.DedupeKey == dedupeKey);
                Assert.Equal("COMPLETED", job.Status);
                retryEnvelope = new BackgroundJobEnvelope(job.Id, job.Type, job.DedupeKey, job.Payload!, job.ScheduledAt, job.CorrelationId);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().Single(x => x.CanHandle("REPORT_SCHEDULE"));
                await handler.HandleAsync(retryEnvelope, CancellationToken.None);
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.Single(await db.ReportScheduleExecutions.Where(x => x.ScheduleId == activeScheduleId).ToArrayAsync());
                Assert.Single(await db.ReportExports.Where(x => x.Id == exportId).ToArrayAsync());
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            var exports = await db.ReportExports.Where(x => x.CorrelationId == dedupeKey).ToListAsync();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            foreach (var export in exports.Where(x => !string.IsNullOrWhiteSpace(x.StorageKey)))
                await storage.DeleteAsync(export.StorageKey!, CancellationToken.None);
            db.ReportExports.RemoveRange(exports);
            await db.ReportScheduleExecutions.Where(x => x.ScheduleId == activeScheduleId || x.ScheduleId == pausedScheduleId || x.ScheduleId == futureScheduleId)
                .ExecuteDeleteAsync();
            await db.ReportSchedules.Where(x => x.Id == activeScheduleId || x.Id == pausedScheduleId || x.Id == futureScheduleId)
                .ExecuteDeleteAsync();
            if (!string.IsNullOrEmpty(dedupeKey))
                await db.Jobs.Where(x => x.DedupeKey == dedupeKey).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            if (Directory.Exists(storageRoot))
                Directory.Delete(storageRoot, recursive: true);
        }
    }

    private static ServiceProvider BuildProvider(string storageRoot)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["Storage:Provider"] = "Local",
            ["Storage:RootPath"] = storageRoot,
            ["Jobs:Enabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
