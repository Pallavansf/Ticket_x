using Microsoft.Extensions.Diagnostics.HealthChecks;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Api;

public sealed class DatabaseReadinessCheck(TicketsDbContext tickets, ExecutionDbContext execution) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            if (!await tickets.Database.CanConnectAsync(timeout.Token) || !await execution.Database.CanConnectAsync(timeout.Token))
            {
                OperationalMetrics.DatabaseFailures.Add(1);
                OperationalMetrics.SetDatabaseAvailable(false);
                return HealthCheckResult.Unhealthy("A required database is unavailable.");
            }
            OperationalMetrics.SetDatabaseAvailable(true);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            OperationalMetrics.DatabaseFailures.Add(1);
                OperationalMetrics.SetDatabaseAvailable(false);
            return HealthCheckResult.Unhealthy("Database readiness check timed out.");
        }
        catch
        {
            OperationalMetrics.DatabaseFailures.Add(1);
                OperationalMetrics.SetDatabaseAvailable(false);
            return HealthCheckResult.Unhealthy("A required database is unavailable.");
        }
    }
}

public sealed class WorkerReadinessCheck(IConfiguration configuration, WorkerActivityRegistry activity) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue<bool>("Jobs:Enabled") || configuration.GetValue<bool>("Communication:Worker:Enabled");
        if (!enabled) return Task.FromResult(HealthCheckResult.Healthy("Background processing is disabled by configuration."));
        if (activity.LastPollUtc is null || DateTimeOffset.UtcNow - activity.LastPollUtc > TimeSpan.FromMinutes(3))
            return Task.FromResult(HealthCheckResult.Unhealthy("A background worker has not polled recently."));
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}