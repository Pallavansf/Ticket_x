namespace PA.Tickets.Domain.Workflow;
public enum WorkflowStatus { Draft, Validation, Published, Active, Retired }
public enum WorkflowStepType { Work, Approval, Decision, Notification, Wait, Subprocess }
public enum WorkflowStepStatus { Pending, Active, Waiting, Completed, Failed, Cancelled, Skipped }
public enum WorkflowJoinRule { AllRequired, AnyOne, Condition }
public sealed record WorkflowCondition(string Field,string Operator,string Value)
{
 public bool Evaluate(IReadOnlyDictionary<string,string?> context){if(!context.TryGetValue(Field,out var actual))return false;return Operator.ToUpperInvariant() switch{"EQUALS"=>string.Equals(actual,Value,StringComparison.OrdinalIgnoreCase),"NOT_EQUALS"=>!string.Equals(actual,Value,StringComparison.OrdinalIgnoreCase),"PRESENT"=>!string.IsNullOrWhiteSpace(actual),_=>throw new InvalidOperationException($"Unsupported workflow condition operator: {Operator}")};}
}
public sealed record WorkflowStepDefinition(string Code,string Name,WorkflowStepType Type,WorkflowJoinRule JoinRule=WorkflowJoinRule.AllRequired,bool Required=true,long? DepartmentId=null,long? TeamId=null);
public sealed record WorkflowTransitionDefinition(string Code,string FromStep,string ToStep,WorkflowCondition? Condition=null);
public sealed record WorkflowExecutionEvent(string Action,string StepCode,DateTimeOffset OccurredAt,string? Reason=null);
public sealed class WorkflowValidationResult(IReadOnlyCollection<string> errors){public IReadOnlyCollection<string> Errors{get;}=errors;public bool IsValid=>Errors.Count==0;}
