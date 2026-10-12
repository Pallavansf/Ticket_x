using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class CommunicationBackgroundWorker(IServiceScopeFactory scopes,ILogger<CommunicationBackgroundWorker> logger, PA.Tickets.Infrastructure.Execution.WorkerActivityRegistry activity) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var startupScope=scopes.CreateScope())
            startupScope.ServiceProvider.GetRequiredService<ICommunicationProviderResolver>().ValidateConfiguration();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ICommunicationDeliveryProcessor>().ProcessOnceAsync(Environment.MachineName,stoppingToken);
                activity.MarkPoll();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { PA.Tickets.Infrastructure.Execution.OperationalMetrics.WorkerFailures.Add(1, new KeyValuePair<string, object?>("worker", "communication")); PA.Tickets.Infrastructure.Execution.OperationalMetrics.SetWorkerAvailable(false); logger.LogError(ex,"Communication delivery cycle failed; durable rows remain available for recovery."); }
        }
    }
}
