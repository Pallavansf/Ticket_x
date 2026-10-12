using PA.Tickets.Domain.Sla;
namespace PA.Tickets.Application.Sla;
public sealed record StartSlaCommand(SlaPolicyVersion Policy,BusinessCalendar Calendar,DateTimeOffset StartedAt);
public interface ISlaExecutionEngine { SlaClock Start(StartSlaCommand command); void Pause(SlaClock clock,string reason,DateTimeOffset at); void Resume(SlaClock clock,BusinessCalendar calendar,DateTimeOffset at); bool CheckBreach(SlaClock clock,DateTimeOffset at); void Complete(SlaClock clock,DateTimeOffset at); }
public sealed class SlaExecutionEngine:ISlaExecutionEngine { public SlaClock Start(StartSlaCommand c)=>new(c.Policy,c.Calendar,c.StartedAt);public void Pause(SlaClock c,string r,DateTimeOffset at)=>c.Pause(r,at);public void Resume(SlaClock c,BusinessCalendar cal,DateTimeOffset at)=>c.Resume(cal,at);public bool CheckBreach(SlaClock c,DateTimeOffset at)=>c.CheckBreach(at);public void Complete(SlaClock c,DateTimeOffset at)=>c.Complete(at); }
