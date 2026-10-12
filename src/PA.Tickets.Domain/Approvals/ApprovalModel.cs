namespace PA.Tickets.Domain.Approvals;
public enum ApprovalMode { AnyOne, AllRequired, Sequential }
public enum ApprovalDecision { Pending, Approved, Rejected, Cancelled, Expired }
public enum ApprovalRuleStatus { Draft, Published, Retired }
public enum ApprovalApproverType { User, Role, Department, Team }
public sealed record ApprovalRuleStepDefinition(int StepNo, ApprovalApproverType ApproverType, long TargetId, bool Required = true);
public sealed class ApprovalRuleValidationResult(IReadOnlyCollection<string> errors) { public IReadOnlyCollection<string> Errors { get; } = errors; public bool IsValid => Errors.Count == 0; }
public sealed class ApprovalRuleVersion
{
	public long Id { get; }
	public long RuleId { get; }
	public int VersionNo { get; }
	public ApprovalMode Mode { get; }
	public string RejectionBehavior { get; }
	public int? ExpiresAfterMinutes { get; }
	public bool DelegationAllowed { get; }
	public bool RequireRequesterSeparation { get; }
	public bool RequireCreatorSeparation { get; }
	public bool RequireExecutorSeparation { get; }
	public DateTimeOffset EffectiveFrom { get; }
	public DateTimeOffset? EffectiveTo { get; }
	public IReadOnlyCollection<ApprovalRuleStepDefinition> Steps { get; }

	public ApprovalRuleVersion(long id, long ruleId, int versionNo, ApprovalMode mode, string rejectionBehavior, int? expiresAfterMinutes, bool delegationAllowed, bool requireRequesterSeparation, bool requireCreatorSeparation, bool requireExecutorSeparation, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, IEnumerable<ApprovalRuleStepDefinition> steps)
	{
		Id = id; RuleId = ruleId; VersionNo = versionNo; Mode = mode; RejectionBehavior = rejectionBehavior; ExpiresAfterMinutes = expiresAfterMinutes; DelegationAllowed = delegationAllowed;
		RequireRequesterSeparation = requireRequesterSeparation; RequireCreatorSeparation = requireCreatorSeparation; RequireExecutorSeparation = requireExecutorSeparation;
		EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo; Steps = steps.ToArray();
	}

	public ApprovalRuleValidationResult Validate()
	{
		var errors = new List<string>();
		if (Id < 0 || RuleId <= 0 || VersionNo <= 0) errors.Add("Approval rule and version identities must be valid.");
		if (!Enum.IsDefined(Mode)) errors.Add("Approval mode is unsupported.");
		if (EffectiveTo is not null && EffectiveTo <= EffectiveFrom) errors.Add("Approval effective end must be after its start.");
		if (ExpiresAfterMinutes is <= 0) errors.Add("Approval expiry must be a positive number of minutes.");
		if (RejectionBehavior is not ("REJECT" or "CANCEL")) errors.Add("Approval rejection behavior must be REJECT or CANCEL.");
		if (!RequireRequesterSeparation || !RequireCreatorSeparation || !RequireExecutorSeparation) errors.Add("Runtime separation-of-duties protections cannot be disabled.");
		if (Steps.Count == 0) errors.Add("Approval rule requires at least one approver step.");
		if (Steps.Any(x => x.StepNo <= 0 || x.TargetId <= 0 || !Enum.IsDefined(x.ApproverType))) errors.Add("Approval steps require positive order and target identities.");
		if (Steps.GroupBy(x => new { x.StepNo, x.ApproverType, x.TargetId }).Any(x => x.Count() > 1)) errors.Add("Approval targets cannot be duplicated within a level.");
		if (Mode != ApprovalMode.Sequential && Steps.Any(x => x.StepNo != 1)) errors.Add("Any-One and All-Required approvals use step order 1.");
		return new(errors);
	}
}
public sealed record ApprovalRule(long RuleId,string Code,ApprovalMode Mode,IReadOnlyCollection<long> ApproverUserIds,bool RequireRequesterSeparation=true,bool RequireCreatorSeparation=true,bool RequireExecutorSeparation=true);
public sealed record ApprovalRecord(long Id,long TicketId,long RequestedBy,long ApproverUserId,int Level,ApprovalDecision Decision,DateTimeOffset RequestedAt,DateTimeOffset? DecidedAt,string? Comments,long? DelegatedFrom);
public sealed class ApprovalInstance(ApprovalRule rule,long ticketId,long requester,long creator,long? executor){private readonly List<ApprovalRecord> _records=[];public IReadOnlyCollection<ApprovalRecord> Records=>_records;public bool IsRejected=>_records.Any(x=>x.Decision==ApprovalDecision.Rejected);public bool IsComplete=>rule.Mode switch{ApprovalMode.AnyOne=>_records.Any(x=>x.Decision==ApprovalDecision.Approved),ApprovalMode.AllRequired=>rule.ApproverUserIds.All(id=>_records.Any(x=>x.ApproverUserId==id&&x.Decision==ApprovalDecision.Approved)),ApprovalMode.Sequential=>_records.OrderBy(x=>x.Level).Where(x=>x.Decision==ApprovalDecision.Approved).Count()==rule.ApproverUserIds.Count,_=>false};public void Request(long actor,DateTimeOffset now){foreach(var (approver,index) in rule.ApproverUserIds.Select((x,i)=>(x,i))) {if((rule.RequireRequesterSeparation&&approver==requester)||(rule.RequireCreatorSeparation&&approver==creator)||(rule.RequireExecutorSeparation&&approver==executor))throw new InvalidOperationException("Approval rule violates segregation of duties.");_records.Add(new ApprovalRecord(_records.Count+1,ticketId,requester,approver,index+1,ApprovalDecision.Pending,now,null,null,null));}}public void Decide(long approver,ApprovalDecision decision,string? comments,DateTimeOffset now){var record=_records.LastOrDefault(x=>x.ApproverUserId==approver&&x.Decision==ApprovalDecision.Pending)??throw new InvalidOperationException("No pending approval exists for this approver.");if(rule.Mode==ApprovalMode.Sequential&&_records.Any(x=>x.Level<record.Level&&x.Decision!=ApprovalDecision.Approved))throw new InvalidOperationException("Sequential approval must follow approval level order.");_records[_records.IndexOf(record)]=record with{Decision=decision,DecidedAt=now,Comments=comments};}public void Delegate(long original,long delegateUser,string reason,DateTimeOffset now){if(original==delegateUser)throw new InvalidOperationException("Self-delegation is not allowed.");var record=_records.LastOrDefault(x=>x.ApproverUserId==original&&x.Decision==ApprovalDecision.Pending)??throw new InvalidOperationException("No pending approval exists for the original approver.");_records[_records.IndexOf(record)]=record with{ApproverUserId=delegateUser,DelegatedFrom=original};}}

