namespace PA.Tickets.Application.Execution;

public sealed record WorkflowRoutingContext(long TicketId,int TicketType,long? PriorityId,long? ClientId,long? ServiceId,string StepCode,long? ConfiguredDepartmentId=null,long? ConfiguredTeamId=null,long? ConfiguredUserId=null);
public sealed record RoutingDecision(long? DepartmentId,long? TeamId,long? UserId,string TargetType,string RuleCode);
public interface IWorkflowRouter
{
    RoutingDecision Resolve(WorkflowRoutingContext context);
}
