using PA.Tickets.Domain.Sla;
namespace PA.Tickets.Application.Foundations;
public interface ISlaPolicyResolver { Task<SlaPolicyVersion?> ResolveAsync(SlaEvaluationContext context,DateTimeOffset at,CancellationToken ct); }
public interface ISlaConfigurationService { Task<SlaPolicyVersion> CreateVersionAsync(SlaPolicyVersion policy,long actor,string reason,CancellationToken ct); Task<SlaPolicyVersion> ActivateAsync(long policyId,int version,long actor,string reason,CancellationToken ct); Task CreateCalendarAsync(string code,BusinessCalendar calendar,long actor,string reason,CancellationToken ct); }
public interface ISlaAuthorizer { Task EnsureCanConfigureAsync(long actor,CancellationToken ct); }
public interface ISlaClock { SlaClockState Start(SlaPolicyVersion policy,DateTimeOffset at); SlaClockState Pause(SlaClockState state,string reason,DateTimeOffset at); SlaClockState Resume(SlaClockState state,DateTimeOffset at); SlaClockState MarkBreach(SlaClockState state); }
