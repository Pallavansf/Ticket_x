namespace PA.Tickets.Domain.Workflow;
public sealed class WorkflowVersion
{
 private readonly List<WorkflowStepDefinition> _steps=[];private readonly List<WorkflowTransitionDefinition> _transitions=[];
 public long Id{get;} public long WorkflowId{get;} public int VersionNo{get;} public DateTimeOffset? EffectiveFrom{get;} public DateTimeOffset? EffectiveTo{get;} public WorkflowStatus Status{get;private set;} public IReadOnlyCollection<WorkflowStepDefinition> Steps=>_steps;public IReadOnlyCollection<WorkflowTransitionDefinition> Transitions=>_transitions;
 public WorkflowVersion(long id,long workflowId,int versionNo,IEnumerable<WorkflowStepDefinition> steps,IEnumerable<WorkflowTransitionDefinition> transitions,DateTimeOffset? effectiveFrom=null,DateTimeOffset? effectiveTo=null){if(effectiveTo is not null&&effectiveFrom is not null&&effectiveTo<=effectiveFrom)throw new ArgumentException("Workflow effective end must be after its start.");Id=id;WorkflowId=workflowId;VersionNo=versionNo;EffectiveFrom=effectiveFrom;EffectiveTo=effectiveTo;_steps.AddRange(steps);_transitions.AddRange(transitions);Status=WorkflowStatus.Draft;}
 public WorkflowValidationResult Validate()
 {
  var errors=new List<string>();
    if(Id<0)errors.Add("Workflow version identity cannot be negative.");
  if(WorkflowId<=0)errors.Add("Workflow identity must be positive.");
  if(VersionNo<=0)errors.Add("Workflow version number must be positive.");
  if(EffectiveFrom is not null&&EffectiveTo is not null&&EffectiveTo<=EffectiveFrom)errors.Add("Workflow effective end must be after its start.");
  if(_steps.Count==0)errors.Add("Workflow requires at least one step.");
  if(_steps.Any(x=>string.IsNullOrWhiteSpace(x.Code)||string.IsNullOrWhiteSpace(x.Name)))errors.Add("Every workflow step requires a code and name.");
  if(_steps.Any(x=>!Enum.IsDefined(x.Type)||!Enum.IsDefined(x.JoinRule)))errors.Add("Workflow steps must use supported step and join types.");
  if(_steps.Any(x=>x.DepartmentId is <=0||x.TeamId is <=0))errors.Add("Workflow routing IDs must be positive when supplied.");
  if(_steps.GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1))errors.Add("Workflow step codes must be unique.");
  if(_transitions.Any(x=>string.IsNullOrWhiteSpace(x.Code)))errors.Add("Every workflow transition requires a code.");
  if(_transitions.GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1))errors.Add("Workflow transition codes must be unique.");
  var codes=_steps.Select(x=>x.Code).Where(x=>!string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
  if(_transitions.Any(x=>!codes.Contains(x.FromStep)||!codes.Contains(x.ToStep)))errors.Add("Every transition must reference existing steps.");
  foreach(var transition in _transitions.Where(x=>x.Condition is not null))
  {
   var condition=transition.Condition!;
   if(condition.Field is not("TicketType" or "Priority" or "ClientId" or "ServiceId" or "ApprovalStatus" or "WorkStatus"))errors.Add($"Condition field {condition.Field} is not allowed.");
   if(condition.Operator is not("EQUALS" or "NOT_EQUALS" or "PRESENT"))errors.Add($"Condition operator {condition.Operator} is not allowed.");
   if(condition.Operator is not "PRESENT"&&string.IsNullOrWhiteSpace(condition.Value))errors.Add($"Condition value is required for operator {condition.Operator}.");
  }
  var validTransitions=_transitions.Where(x=>codes.Contains(x.FromStep)&&codes.Contains(x.ToStep)).ToArray();
  if(validTransitions.GroupBy(x=>$"{x.FromStep}\u001f{x.ToStep}\u001f{x.Condition?.Field}\u001f{x.Condition?.Operator}\u001f{x.Condition?.Value}",StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1))errors.Add("Duplicate or conflicting workflow transitions are not allowed.");
  var incoming=validTransitions.Select(x=>x.ToStep).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var starts=_steps.Where(x=>!incoming.Contains(x.Code)).Select(x=>x.Code).ToArray();
  if(_steps.Count>0&&starts.Length==0)errors.Add("Workflow requires a start step.");
  var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var queue=new Queue<string>(starts);
  while(queue.Count>0){var current=queue.Dequeue();if(!visited.Add(current))continue;foreach(var next in validTransitions.Where(x=>x.FromStep.Equals(current,StringComparison.OrdinalIgnoreCase)).Select(x=>x.ToStep))queue.Enqueue(next);}
  foreach(var step in _steps.Where(x=>!visited.Contains(x.Code)))errors.Add($"Step {step.Code} is unreachable.");
  if(_steps.Count>0&&!_steps.Any(x=>visited.Contains(x.Code)&&!validTransitions.Any(t=>t.FromStep.Equals(x.Code,StringComparison.OrdinalIgnoreCase))))errors.Add("Workflow requires at least one reachable terminal step.");
  if(HasCycle(_steps.Select(x=>x.Code).Where(x=>!string.IsNullOrWhiteSpace(x)),validTransitions))errors.Add("Workflow cycles are not supported.");
  return new(errors);
 }
 public void Publish(){EnsureDraft();var result=Validate();if(!result.IsValid)throw new InvalidOperationException(string.Join(" ",result.Errors));Status=WorkflowStatus.Published;}
 public void Activate(){if(Status!=WorkflowStatus.Published)throw new InvalidOperationException("Only published workflow versions can be activated.");Status=WorkflowStatus.Active;}
 public void Retire(){if(Status==WorkflowStatus.Draft)throw new InvalidOperationException("Draft workflow versions cannot be retired.");Status=WorkflowStatus.Retired;}
 public void EnsureDraft(){if(Status!=WorkflowStatus.Draft)throw new InvalidOperationException("A workflow version is immutable once published.");}
 private static bool HasCycle(IEnumerable<string> codes,IReadOnlyCollection<WorkflowTransitionDefinition> transitions)
 {
  var edges=codes.ToDictionary(x=>x,_=>new List<string>(),StringComparer.OrdinalIgnoreCase);
  foreach(var transition in transitions)if(edges.TryGetValue(transition.FromStep,out var targets)&&edges.ContainsKey(transition.ToStep))targets.Add(transition.ToStep);
  var visiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  bool Visit(string code){if(visiting.Contains(code))return true;if(!visited.Add(code))return false;visiting.Add(code);foreach(var target in edges[code])if(Visit(target))return true;visiting.Remove(code);return false;}
  return edges.Keys.Any(Visit);
 }
}
