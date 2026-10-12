using PA.Tickets.Domain.Workflow;
namespace PA.Tickets.Application.Workflow;
public interface IWorkflowDefinitionService { WorkflowValidationResult Validate(WorkflowVersion version); void Publish(WorkflowVersion version); void Activate(WorkflowVersion version); }
public sealed class WorkflowDefinitionService: IWorkflowDefinitionService { public WorkflowValidationResult Validate(WorkflowVersion v)=>v.Validate();public void Publish(WorkflowVersion v)=>v.Publish();public void Activate(WorkflowVersion v)=>v.Activate(); }
public interface IWorkflowExecutionBoundary { void CompleteStep(WorkflowInstance instance,WorkflowVersion version,string step,IReadOnlyDictionary<string,string?> context,DateTimeOffset at); }
public sealed class WorkflowExecutionBoundary:IWorkflowExecutionBoundary { public void CompleteStep(WorkflowInstance i,WorkflowVersion v,string s,IReadOnlyDictionary<string,string?> c,DateTimeOffset at)=>i.CompleteStep(s,v,c,at); }
