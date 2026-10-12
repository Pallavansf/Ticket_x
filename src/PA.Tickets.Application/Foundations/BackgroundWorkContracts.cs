namespace PA.Tickets.Application.Foundations;
public sealed record BackgroundWorkRequest(string WorkType,string CorrelationId,DateTimeOffset ScheduledAt,object Payload);
public interface IBackgroundWorkScheduler { Task ScheduleAsync(BackgroundWorkRequest request,CancellationToken ct); }
// A durable queue/worker implementation is required before SLA timers or escalations are enabled.
