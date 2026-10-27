using System.Data;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class DurableWorkflowConfigurationRepository(ExecutionDbContext db,TicketsDbContext tickets,AuditChainCoordinator auditLocks,IAuthorizationService authorization) : IWorkflowConfigurationRepository
{
    public async Task<long> CreateWorkflowAsync(long actor,string code,string name,string? description,string idempotencyKey,string? correlationId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_ADMIN",null,ct);
        var normalizedCode=Required(code);var normalizedName=Required(name);var fingerprint=Fingerprint(new{Code=normalizedCode,Name=normalizedName,Description=description});
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_CREATE"&&x.ActorId==actor&&x.Key==idempotencyKey,ct);
        if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different workflow.");return await db.WorkflowDefinitions.Where(x=>x.Code==normalizedCode).Select(x=>x.Id).SingleAsync(ct);}
        if(await db.WorkflowDefinitions.AnyAsync(x=>x.Code==normalizedCode,ct))throw new InvalidOperationException("Workflow code already exists.");
        var row=new WorkflowDefinitionRow{Id=await NextSequenceAsync("PA_TICKETS_WORKFLOW_SEQ",ct),Code=normalizedCode,Name=normalizedName,Description=description,Active=true,CreatedBy=actor,CreatedAt=DateTimeOffset.UtcNow};
        db.WorkflowDefinitions.Add(row);await db.SaveChangesAsync(ct);await AddAudit("WORKFLOW",row.Id.ToString(),"WORKFLOW_CREATED",actor,null,WorkflowIdentity(row),null,correlationId,ct);
        db.Idempotency.Add(new IdempotencyRow{Scope="WORKFLOW_CREATE",Key=idempotencyKey,ActorId=actor,Fingerprint=fingerprint,Status="COMPLETED",CreatedAt=DateTimeOffset.UtcNow,CompletedAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return row.Id;
    }

    public async Task<IReadOnlyCollection<WorkflowDefinitionView>> ListWorkflowsAsync(long actor,string? search,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_READ",null,ct);
        var query=db.WorkflowDefinitions.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(search))query=query.Where(x=>x.Code.Contains(search)||x.Name.Contains(search));
        return await query.OrderBy(x=>x.Code).Select(x=>new WorkflowDefinitionView(x.Id,x.Code,x.Name,x.Description,x.Active)).ToListAsync(ct);
    }

    public async Task<WorkflowDefinitionView> GetWorkflowAsync(long actor,long workflowId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_READ",null,ct);
        var row=await db.WorkflowDefinitions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==workflowId,ct)??throw new KeyNotFoundException("Workflow was not found.");
        return Map(row);
    }

    public async Task<WorkflowDefinitionView> UpdateWorkflowAsync(long actor,long workflowId,string code,string name,string? description,string? reason,string? correlationId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_ADMIN",null,ct);var row=await db.WorkflowDefinitions.SingleOrDefaultAsync(x=>x.Id==workflowId,ct)??throw new KeyNotFoundException("Workflow was not found.");
        if(!row.Active)throw new InvalidOperationException("An inactive workflow cannot be edited.");var normalizedCode=Required(code);var normalizedName=Required(name);
        if(await db.WorkflowDefinitions.AnyAsync(x=>x.Id!=workflowId&&x.Code==normalizedCode,ct))throw new InvalidOperationException("Workflow code already exists.");
        var oldValue=WorkflowIdentity(row);row.Code=normalizedCode;row.Name=normalizedName;row.Description=description;
        await AddAudit("WORKFLOW",row.Id.ToString(),"WORKFLOW_UPDATED",actor,oldValue,WorkflowIdentity(row),reason,correlationId,ct);await db.SaveChangesAsync(ct);return Map(row);
    }

    public async Task<WorkflowDefinitionView> SetWorkflowActiveAsync(long actor,long workflowId,bool active,string? reason,string? correlationId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_ADMIN",null,ct);var row=await db.WorkflowDefinitions.SingleOrDefaultAsync(x=>x.Id==workflowId,ct)??throw new KeyNotFoundException("Workflow was not found.");
        var old=row.Active.ToString();row.Active=active;await AddAudit("WORKFLOW",row.Id.ToString(),active?"WORKFLOW_ACTIVATED":"WORKFLOW_DEACTIVATED",actor,old,active.ToString(),reason,correlationId,ct);await db.SaveChangesAsync(ct);return Map(row);
    }

    public async Task<IReadOnlyCollection<WorkflowVersionSummary>> ListVersionsAsync(long actor,long workflowId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_READ",null,ct);if(!await db.WorkflowDefinitions.AnyAsync(x=>x.Id==workflowId,ct))throw new KeyNotFoundException("Workflow was not found.");
        var rows=await db.WorkflowVersions.AsNoTracking().Where(x=>x.WorkflowId==workflowId).OrderByDescending(x=>x.VersionNo).ToListAsync(ct);
        return rows.Select(x=>{var dates=ReadDates(x.Definition);return new WorkflowVersionSummary(x.Id,x.WorkflowId,x.VersionNo,x.Status,dates.From,dates.To);}).ToArray();
    }

    public async Task<WorkflowVersionView> GetVersionAsync(long actor,long versionId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_READ",null,ct);return await LoadVersionViewAsync(versionId,ct);
    }

    public async Task<IReadOnlyCollection<WorkflowInstanceHistoryView>> GetTicketRuntimeHistoryAsync(long actor,long ticketId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_READ",null,ct);
        var ticket=await tickets.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
        await authorization.EnsurePermissionAsync(actor,"TICKET_READ",new AuthorizationResource(ticket.Id,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.ClientId,ticket.CurrentAssignee,RequesterUserId:ticket.RequestedBy),ct);
        var instances=await db.WorkflowInstances.AsNoTracking().Where(x=>x.TicketId==ticketId).OrderBy(x=>x.Id).ToListAsync(ct);
        if(instances.Count==0)return Array.Empty<WorkflowInstanceHistoryView>();
        var versionIds=instances.Select(x=>x.VersionId).ToHashSet();
        var versionNumbers=await db.WorkflowVersions.AsNoTracking().Where(x=>versionIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.VersionNo,ct);
        var versionSteps=await db.WorkflowStepDefinitions.AsNoTracking().Where(x=>versionIds.Contains(x.VersionId)).ToDictionaryAsync(x=>x.Id,x=>x.Code,ct);
        var instanceIds=instances.Select(x=>x.Id).ToHashSet();var steps=await db.WorkflowSteps.AsNoTracking().Where(x=>instanceIds.Contains(x.WorkflowInstanceId)).OrderBy(x=>x.Id).ToListAsync(ct);
        var stepIds=steps.Select(x=>x.Id).ToHashSet();var histories=await db.WorkflowStepHistory.AsNoTracking().Where(x=>stepIds.Contains(x.StepInstanceId)).OrderBy(x=>x.EventAt).ToListAsync(ct);
        return instances.Select(instance=>new WorkflowInstanceHistoryView(instance.Id,instance.TicketId,instance.VersionId,versionNumbers.GetValueOrDefault(instance.VersionId),instance.Status,instance.CurrentStepId,
            steps.Where(x=>x.WorkflowInstanceId==instance.Id).Select(step=>new WorkflowStepHistoryView(step.Id,versionSteps.GetValueOrDefault(step.WorkflowStepId,""),step.Status,step.WorkItemId,step.ApprovalId,
                histories.Where(h=>h.StepInstanceId==step.Id).Select(h=>new WorkflowRuntimeEventView(h.Action,h.OldStatus,h.NewStatus,h.ActorId,h.EventAt,h.CorrelationId,h.IdempotencyKey)).ToArray())).ToArray())).ToArray();
    }

    public async Task<WorkflowVersionComparison> CompareVersionsAsync(long actor,long firstVersionId,long secondVersionId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_READ",null,ct);var first=await LoadVersionViewAsync(firstVersionId,ct);var second=await LoadVersionViewAsync(secondVersionId,ct);
        var firstSteps=first.Steps.ToDictionary(x=>x.Code,StringComparer.OrdinalIgnoreCase);var secondSteps=second.Steps.ToDictionary(x=>x.Code,StringComparer.OrdinalIgnoreCase);
        var addedSteps=secondSteps.Keys.Except(firstSteps.Keys,StringComparer.OrdinalIgnoreCase).ToArray();var removedSteps=firstSteps.Keys.Except(secondSteps.Keys,StringComparer.OrdinalIgnoreCase).ToArray();var changedSteps=firstSteps.Keys.Intersect(secondSteps.Keys,StringComparer.OrdinalIgnoreCase).Where(x=>firstSteps[x]!=secondSteps[x]).ToArray();
        var firstTransitions=first.Transitions.ToDictionary(x=>x.Code,StringComparer.OrdinalIgnoreCase);var secondTransitions=second.Transitions.ToDictionary(x=>x.Code,StringComparer.OrdinalIgnoreCase);
        var addedTransitions=secondTransitions.Keys.Except(firstTransitions.Keys,StringComparer.OrdinalIgnoreCase).ToArray();var removedTransitions=firstTransitions.Keys.Except(secondTransitions.Keys,StringComparer.OrdinalIgnoreCase).ToArray();var changedTransitions=firstTransitions.Keys.Intersect(secondTransitions.Keys,StringComparer.OrdinalIgnoreCase).Where(x=>firstTransitions[x]!=secondTransitions[x]).ToArray();
        return new(addedSteps,removedSteps,changedSteps,addedTransitions,removedTransitions,changedTransitions);
    }

    public async Task<WorkflowValidationResult> ValidateAsync(long actor,long workflowId,WorkflowVersion version,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_ADMIN",null,ct);
        if(version.WorkflowId!=workflowId)throw new InvalidOperationException("Workflow version belongs to a different workflow.");
        if(!await db.WorkflowDefinitions.AnyAsync(x=>x.Id==workflowId&&x.Active,ct))throw new KeyNotFoundException("Active workflow was not found.");
        var result=version.Validate();if(!result.IsValid)return result;
        var errors=new List<string>();
        foreach(var step in version.Steps)
        {
            if(step.DepartmentId.HasValue&&!await tickets.AdministrationDepartments.AnyAsync(x=>x.Id==step.DepartmentId&&x.Active,ct))errors.Add($"Step {step.Code} references an inactive or missing Department.");
            if(step.TeamId.HasValue&&!await tickets.AdministrationTeams.AnyAsync(x=>x.Id==step.TeamId&&x.Active&&(step.DepartmentId==null||x.DepartmentId==step.DepartmentId),ct))errors.Add($"Step {step.Code} references an inactive Team or a Team outside its Department.");
        }
        return new WorkflowValidationResult(errors);
    }

    public async Task<long> SaveDraftAsync(long workflowId, WorkflowVersion version, long actor, string key, CancellationToken ct,string? correlationId=null)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_ADMIN",null,ct);
        if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency-Key is required.");
        if(version.WorkflowId!=workflowId)throw new InvalidOperationException("Workflow version belongs to a different workflow.");
        var validation=await ValidateAsync(actor,workflowId,version,ct);if(!validation.IsValid)throw new InvalidOperationException(string.Join(" ",validation.Errors));
        var fingerprint=Fingerprint(new{version.WorkflowId,version.VersionNo,version.EffectiveFrom,version.EffectiveTo,version.Steps,version.Transitions});
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_DRAFT"&&x.ActorId==actor&&x.Key==key,ct);
        if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different workflow draft.");return await db.WorkflowVersions.Where(x=>x.WorkflowId==workflowId&&x.VersionNo==version.VersionNo).Select(x=>x.Id).SingleAsync(ct);}
        if(!await db.WorkflowDefinitions.AnyAsync(x=>x.Id==workflowId&&x.Active,ct))throw new KeyNotFoundException("Active workflow was not found.");

        var versionId=version.Id>0?version.Id:await NextSequenceAsync("PA_TICKETS_WORKFLOW_VERSION_SEQ",ct);
        var row = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.Id == versionId, ct);
        if (row is not null && row.Status != "DRAFT") throw new InvalidOperationException("Published workflow versions are immutable.");
        if(row is null&&await db.WorkflowVersions.AnyAsync(x=>x.WorkflowId==workflowId&&x.VersionNo==version.VersionNo,ct))throw new InvalidOperationException("Workflow version number already exists.");
        row ??= new WorkflowVersionRow { Id = versionId, WorkflowId = workflowId, VersionNo = version.VersionNo, Status = "DRAFT", CreatedBy=actor,CreatedAt=DateTimeOffset.UtcNow };
        row.WorkflowId = workflowId;
        row.VersionNo = version.VersionNo;
        row.Status = "DRAFT";
        row.Definition = JsonSerializer.Serialize(new { effectiveFrom = version.EffectiveFrom, effectiveTo = version.EffectiveTo, steps = version.Steps, transitions = version.Transitions });
        if (db.Entry(row).State == EntityState.Detached) db.WorkflowVersions.Add(row);
        await db.SaveChangesAsync(ct);

        var oldSteps = await db.WorkflowStepDefinitions.Where(x => x.VersionId == row.Id).ToListAsync(ct);
        if (oldSteps.Count > 0)
        {
            var oldIds = oldSteps.Select(x => x.Id).ToArray();
            var oldTransitions = await db.WorkflowTransitions.Where(x => x.VersionId == row.Id).ToListAsync(ct);
            db.WorkflowTransitions.RemoveRange(oldTransitions);
            db.WorkflowStepDefinitions.RemoveRange(oldSteps);
            await db.SaveChangesAsync(ct);
        }

        var steps = version.Steps.Select((s, i) => new WorkflowStepDefinitionRow
        {
            VersionId = row.Id,
            StepNo = i + 1,
            Code = s.Code,
            Name = s.Name,
            Type = s.Type.ToString().ToUpperInvariant(),
            ExecutionMode = s.JoinRule.ToString().ToUpperInvariant(),
            DepartmentId=s.DepartmentId,
            TeamId=s.TeamId
        }).ToList();
        foreach(var step in steps)step.Id=await NextSequenceAsync("PA_TICKETS_WORKFLOW_STEP_SEQ",ct);
        db.WorkflowStepDefinitions.AddRange(steps);
        await db.SaveChangesAsync(ct);

        var byCode = steps.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var transition in version.Transitions)
        {
            if (!byCode.TryGetValue(transition.FromStep, out var from) || !byCode.TryGetValue(transition.ToStep, out var to))
                throw new InvalidOperationException($"Transition '{transition.Code}' references an unknown step.");
            db.WorkflowTransitions.Add(new WorkflowTransitionRow
            {
                Id=await NextSequenceAsync("PA_TICKETS_WORKFLOW_TRANSITION_SEQ",ct),
                VersionId = row.Id,
                FromStepId = from.Id,
                ToStepId = to.Id,
                Code = transition.Code,
                Condition = transition.Condition is null ? null : JsonSerializer.Serialize(transition.Condition)
            });
        }

        var newValue=JsonSerializer.Serialize(new{workflowId=row.WorkflowId,versionNo=row.VersionNo,effectiveFrom=version.EffectiveFrom,effectiveTo=version.EffectiveTo,steps=version.Steps,transitions=version.Transitions});
        await AddAudit("WORKFLOW_VERSION",row.Id.ToString(), "WORKFLOW_DRAFT_SAVED", actor,null,newValue,null,correlationId,ct);
        db.Idempotency.Add(new IdempotencyRow { Scope = "WORKFLOW_DRAFT", Key = key,ActorId=actor,Fingerprint=fingerprint, Status = "COMPLETED", CreatedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return row.Id;
    }

    public async Task PublishAsync(long versionId, long actor, string key, CancellationToken ct,string? correlationId=null)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_PUBLISH",null,ct);if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency-Key is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var fingerprint=Fingerprint(new{VersionId=versionId});var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_PUBLISH"&&x.ActorId==actor&&x.Key==key,ct);if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different workflow publish.");return;}
        var row = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.Id == versionId, ct) ?? throw new KeyNotFoundException("Workflow version was not found.");
        if (row.Status != "DRAFT") throw new InvalidOperationException("Only draft versions can publish.");
        if (!await db.WorkflowDefinitions.AnyAsync(x=>x.Id==row.WorkflowId&&x.Active,ct))throw new InvalidOperationException("An inactive workflow cannot be published.");
        var domainVersion=await LoadDomainVersionAsync(row,ct);var validation=await ValidateAsync(actor,row.WorkflowId,domainVersion,ct);if(!validation.IsValid)throw new InvalidOperationException(string.Join(" ",validation.Errors));
        var dates=ReadDates(row.Definition);var siblings=await db.WorkflowVersions.AsNoTracking().Where(x=>x.WorkflowId==row.WorkflowId&&x.Status=="PUBLISHED").ToListAsync(ct);
        if(siblings.Any(x=>Overlaps(dates,ReadDates(x.Definition))))throw new InvalidOperationException("Published workflow versions cannot have overlapping effective dates.");
        var oldValue=row.Status;
        row.Status = "PUBLISHED";
        await AddAudit("WORKFLOW_VERSION",versionId.ToString(), "WORKFLOW_PUBLISHED", actor,oldValue,"PUBLISHED",null,correlationId,ct);
        db.Idempotency.Add(new IdempotencyRow { Scope = "WORKFLOW_PUBLISH", Key = key,ActorId=actor,Fingerprint=fingerprint, Status = "COMPLETED", CreatedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RetireAsync(long versionId,long actor,string key,string? reason,string? correlationId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"WORKFLOW_PUBLISH",null,ct);if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency-Key is required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var fingerprint=Fingerprint(new{VersionId=versionId,Reason=reason});var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_RETIRE"&&x.ActorId==actor&&x.Key==key,ct);
        if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different workflow retirement.");return;}
        var row=await db.WorkflowVersions.SingleOrDefaultAsync(x=>x.Id==versionId,ct)??throw new KeyNotFoundException("Workflow version was not found.");
        if(row.Status!="PUBLISHED")throw new InvalidOperationException("Only a published workflow version can be retired.");var old=row.Status;row.Status="RETIRED";
        await AddAudit("WORKFLOW_VERSION",versionId.ToString(),"WORKFLOW_VERSION_RETIRED",actor,old,row.Status,reason,correlationId,ct);
        db.Idempotency.Add(new IdempotencyRow{Scope="WORKFLOW_RETIRE",Key=key,ActorId=actor,Fingerprint=fingerprint,Status="COMPLETED",CreatedAt=DateTimeOffset.UtcNow,CompletedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }

    private async Task<WorkflowVersionView> LoadVersionViewAsync(long versionId,CancellationToken ct)
    {
        var row=await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==versionId,ct)??throw new KeyNotFoundException("Workflow version was not found.");
        var version=await LoadDomainVersionAsync(row,ct);var dates=ReadDates(row.Definition);
        var audit=await db.Audits.AsNoTracking().Where(x=>x.EntityType=="WORKFLOW_VERSION"&&x.EntityId==versionId.ToString()).OrderBy(x=>x.Id).Select(x=>new WorkflowConfigurationAudit(x.Action,x.ActorId,x.At,x.OldValue,x.NewValue,x.Reason)).ToListAsync(ct);
        return new(row.Id,row.WorkflowId,row.VersionNo,row.Status,dates.From,dates.To,version.Steps,version.Transitions,audit);
    }

    private async Task<WorkflowVersion> LoadDomainVersionAsync(WorkflowVersionRow row,CancellationToken ct)
    {
        var stepRows=await db.WorkflowStepDefinitions.AsNoTracking().Where(x=>x.VersionId==row.Id).OrderBy(x=>x.StepNo).ToListAsync(ct);
        var transitionRows=await db.WorkflowTransitions.AsNoTracking().Where(x=>x.VersionId==row.Id).ToListAsync(ct);
        var stepCodes=stepRows.ToDictionary(x=>x.Id,x=>x.Code);var dates=ReadDates(row.Definition);
        var steps=stepRows.Select(x=>new WorkflowStepDefinition(x.Code,x.Name,Enum.Parse<WorkflowStepType>(x.Type,true),Enum.Parse<WorkflowJoinRule>(x.ExecutionMode,true),x.Required,x.DepartmentId,x.TeamId)).ToArray();
        var transitions=transitionRows.Select(x=>new WorkflowTransitionDefinition(x.Code,x.FromStepId.HasValue&&stepCodes.TryGetValue(x.FromStepId.Value,out var from)?from:throw new InvalidOperationException("A workflow transition has no source step."),stepCodes.TryGetValue(x.ToStepId,out var to)?to:throw new InvalidOperationException("A workflow transition has no target step."),string.IsNullOrWhiteSpace(x.Condition)?null:JsonSerializer.Deserialize<WorkflowCondition>(x.Condition))).ToArray();
        return new(row.Id,row.WorkflowId,row.VersionNo,steps,transitions,dates.From,dates.To);
    }

    private async Task AddAudit(string entityType,string id,string action,long actor,string? oldValue,string? newValue,string? reason,string? correlationId,CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db,entityType,id,ct);var at=DateTimeOffset.UtcNow;
        var previous=await db.Audits.Where(x=>x.EntityType==entityType&&x.EntityId==id).OrderByDescending(x=>x.Id).Select(x=>x.Hash).FirstOrDefaultAsync(ct);
        var auditNewValue=JsonSerializer.Serialize(new{value=newValue,correlationId});
        var canonical=JsonSerializer.Serialize(new{entityType,entityId=id,action,actorUserId=actor,auditAtUtc=at.UtcDateTime,oldValue,newValue=auditNewValue,reason,previousHash=previous});
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow{EntityType=entityType,EntityId=id,Action=action,ActorId=actor,At=at,OldValue=oldValue,NewValue=auditNewValue,Reason=reason,Hash=hash,PreviousHash=previous});
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private async Task<long> NextSequenceAsync(string sequence,CancellationToken ct)
    {
        if(sequence is not ("PA_TICKETS_WORKFLOW_SEQ" or "PA_TICKETS_WORKFLOW_VERSION_SEQ" or "PA_TICKETS_WORKFLOW_STEP_SEQ" or "PA_TICKETS_WORKFLOW_TRANSITION_SEQ"))throw new ArgumentException("Unsupported workflow sequence.",nameof(sequence));
        var expression=DatabaseProviderCapabilities.Resolve(db.Database.ProviderName).SequenceDefaultSql($"{sequence}.NEXTVAL");
        var connection=db.Database.GetDbConnection();var closeWhenDone=connection.State==ConnectionState.Closed;
        if(closeWhenDone)await connection.OpenAsync(ct);
        try{await using var command=connection.CreateCommand();command.CommandText=$"SELECT {expression}";var value=await command.ExecuteScalarAsync(ct);return Convert.ToInt64(value);}
        finally{if(closeWhenDone)await connection.CloseAsync();}
    }

    private static (DateTimeOffset? From,DateTimeOffset? To) ReadDates(string? definition)
    {
        if(string.IsNullOrWhiteSpace(definition))return(null,null);using var document=JsonDocument.Parse(definition);var root=document.RootElement;
        return(ReadDate(root,"effectiveFrom"),ReadDate(root,"effectiveTo"));
    }
    private static DateTimeOffset? ReadDate(JsonElement root,string name)=>root.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null&&value.TryGetDateTimeOffset(out var date)?date:null;
    private static bool Overlaps((DateTimeOffset? From,DateTimeOffset? To) left,(DateTimeOffset? From,DateTimeOffset? To) right)=>(left.To is null||right.From is null||right.From<left.To)&&(right.To is null||left.From is null||left.From<right.To);
    private static string Fingerprint<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private static string Required(string value)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("Workflow code and name are required."):value.Trim();
    private static string WorkflowIdentity(WorkflowDefinitionRow row)=>$"{row.Code}:{row.Name}:{row.Description}";
    private static WorkflowDefinitionView Map(WorkflowDefinitionRow row)=>new(row.Id,row.Code,row.Name,row.Description,row.Active);
}
