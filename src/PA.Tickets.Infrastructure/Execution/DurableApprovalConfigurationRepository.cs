using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Approvals;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Approvals;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class DurableApprovalConfigurationRepository(
    ExecutionDbContext db,
    TicketsDbContext tickets,
    IAuthorizationService authorization,
    AuditChainCoordinator auditLocks) : IApprovalRuleConfigurationRepository, IApprovalRuleVersionResolver
{
    public async Task<long> CreateRuleAsync(long actor, string code, string name, string? description, string key, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_ADMIN", null, ct);
        RequireKey(key); var normalizedCode = Required(code); var normalizedName = Required(name);
        var fingerprint = Fingerprint(new { Code = normalizedCode, Name = normalizedName, description });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await PriorAsync(actor, "APPROVAL_RULE_CREATE", key, ct);
        if (prior is not null)
        {
            EnsureFingerprint(prior, fingerprint);
            return await db.ApprovalRuleDefinitions.Where(x => x.Code == normalizedCode).Select(x => x.Id).SingleAsync(ct);
        }
        if (await db.ApprovalRuleDefinitions.AnyAsync(x => x.Code == normalizedCode, ct)) throw new InvalidOperationException("Approval rule code already exists.");
        var now = DateTimeOffset.UtcNow;
        var row = new ApprovalRuleDefinitionRow { Id = await NextSequenceAsync("PA_TICKETS_APPROVAL_RULE_SEQ", ct), Code = normalizedCode, Name = normalizedName, Description = description, Active = true, CreatedAt = now, CreatedBy = actor };
        db.ApprovalRuleDefinitions.Add(row); await db.SaveChangesAsync(ct);
        await AddAudit("APPROVAL_RULE", row.Id.ToString(), "APPROVAL_RULE_CREATED", actor, null, Identity(row), null, correlationId, ct);
        AddIdempotency(actor, "APPROVAL_RULE_CREATE", key, fingerprint, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return row.Id;
    }

    public async Task<IReadOnlyCollection<ApprovalRuleDefinitionView>> ListRulesAsync(long actor, string? search, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_READ", null, ct);
        var query = db.ApprovalRuleDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));
        return await query.OrderBy(x => x.Code).Select(x => new ApprovalRuleDefinitionView(x.Id, x.Code, x.Name, x.Description, x.Active)).ToListAsync(ct);
    }

    public async Task<ApprovalRuleDefinitionView> GetRuleAsync(long actor, long ruleId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_READ", null, ct);
        var row = await db.ApprovalRuleDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ruleId, ct) ?? throw new KeyNotFoundException("Approval rule was not found.");
        return Map(row);
    }

    public async Task<ApprovalRuleDefinitionView> UpdateRuleAsync(long actor, long ruleId, string code, string name, string? description, string? reason, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_ADMIN", null, ct);
        var row = await db.ApprovalRuleDefinitions.SingleOrDefaultAsync(x => x.Id == ruleId, ct) ?? throw new KeyNotFoundException("Approval rule was not found.");
        if (!row.Active) throw new InvalidOperationException("An inactive approval rule cannot be edited.");
        var normalizedCode = Required(code); var normalizedName = Required(name);
        if (await db.ApprovalRuleDefinitions.AnyAsync(x => x.Id != ruleId && x.Code == normalizedCode, ct)) throw new InvalidOperationException("Approval rule code already exists.");
        var old = Identity(row); row.Code = normalizedCode; row.Name = normalizedName; row.Description = description; row.UpdatedAt = DateTimeOffset.UtcNow; row.UpdatedBy = actor;
        await AddAudit("APPROVAL_RULE", row.Id.ToString(), "APPROVAL_RULE_UPDATED", actor, old, Identity(row), reason, correlationId, ct);
        await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<ApprovalRuleDefinitionView> SetRuleActiveAsync(long actor,long ruleId,bool active,string? reason,string? correlationId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"APPROVAL_ADMIN",null,ct);var row=await db.ApprovalRuleDefinitions.SingleOrDefaultAsync(x=>x.Id==ruleId,ct)??throw new KeyNotFoundException("Approval rule was not found.");
        var old=row.Active.ToString();row.Active=active;row.UpdatedAt=DateTimeOffset.UtcNow;row.UpdatedBy=actor;await AddAudit("APPROVAL_RULE",row.Id.ToString(),active?"APPROVAL_RULE_ACTIVATED":"APPROVAL_RULE_DEACTIVATED",actor,old,active.ToString(),reason,correlationId,ct);await db.SaveChangesAsync(ct);return Map(row);
    }

    public async Task<IReadOnlyCollection<ApprovalRuleVersionSummary>> ListVersionsAsync(long actor, long ruleId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_READ", null, ct);
        if (!await db.ApprovalRuleDefinitions.AnyAsync(x => x.Id == ruleId, ct)) throw new KeyNotFoundException("Approval rule was not found.");
        return await db.ApprovalRuleVersions.AsNoTracking().Where(x => x.RuleId == ruleId).OrderByDescending(x => x.VersionNo)
            .Select(x => new ApprovalRuleVersionSummary(x.Id, x.RuleId, x.VersionNo, x.Status, x.EffectiveFrom, x.EffectiveTo)).ToListAsync(ct);
    }

    public async Task<ApprovalRuleVersionView> GetVersionAsync(long actor, long versionId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_READ", null, ct);
        return await LoadVersionViewAsync(versionId, ct);
    }

    public async Task<IReadOnlyCollection<ApprovalInstanceHistoryView>> GetTicketRuntimeHistoryAsync(long actor,long ticketId,CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor,"APPROVAL_READ",null,ct);
        var ticket=await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
        await authorization.EnsurePermissionAsync(actor,"TICKET_READ",new AuthorizationResource(ticket.Id,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.ClientId,ticket.CurrentAssignee),ct);
        var instances=await db.ApprovalInstances.AsNoTracking().Where(x=>x.TicketId==ticketId).OrderBy(x=>x.Id).ToListAsync(ct);
        if(instances.Count==0)return Array.Empty<ApprovalInstanceHistoryView>();
        var ids=instances.Select(x=>x.Id).ToHashSet();
        var metadata=await db.ApprovalInstances.AsNoTracking().Where(x=>ids.Contains(x.Id)).Select(x=>new{InstanceId=x.Id,RuleVersionId=EF.Property<long?>(x,"ApprovalRuleVersionId"),DelegationAllowed=EF.Property<bool>(x,"DelegationAllowed")}).ToDictionaryAsync(x=>x.InstanceId,ct);
        var versionIds=metadata.Values.Where(x=>x.RuleVersionId.HasValue).Select(x=>x.RuleVersionId!.Value).Distinct().ToHashSet();
        var versionNumbers=await db.ApprovalRuleVersions.AsNoTracking().Where(x=>versionIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.VersionNo,ct);
        var approvals=await db.Approvals.AsNoTracking().Where(x=>x.ApprovalInstanceId.HasValue&&ids.Contains(x.ApprovalInstanceId.Value)).OrderBy(x=>x.Level).ThenBy(x=>x.Id).ToListAsync(ct);
        var approvalIds=approvals.Select(x=>x.Id).ToHashSet();var history=await db.ApprovalHistory.AsNoTracking().Where(x=>approvalIds.Contains(x.ApprovalId)).OrderBy(x=>x.CreatedAt).ToListAsync(ct);
        return instances.Select(instance=>
        {
            var config=metadata[instance.Id];
            var executions=approvals.Where(x=>x.ApprovalInstanceId==instance.Id).Select(approval=>new ApprovalExecutionHistoryView(approval.Id,approval.Level,approval.Required,approval.Active,approval.Status,approval.ApproverId,approval.ExpiresAt,approval.DecidedAt,approval.Decision,approval.Comments,
                history.Where(h=>h.ApprovalId==approval.Id).Select(h=>new ApprovalDecisionHistoryView(h.Id,h.ApprovalId,h.Action,h.Decision,h.ActorId,h.Comment,h.CreatedAt,h.CorrelationId,h.IdempotencyKey)).ToArray())).ToArray();
            return new ApprovalInstanceHistoryView(instance.Id,instance.TicketId,instance.WorkflowStepInstanceId,config.RuleVersionId,config.RuleVersionId.HasValue?versionNumbers.GetValueOrDefault(config.RuleVersionId.Value):null,instance.Mode,instance.Status,instance.RequesterUserId,instance.CreatorUserId,instance.ExecutorUserId,instance.RejectionBehavior,config.DelegationAllowed,instance.CreatedAt,instance.CompletedAt,executions);
        }).ToArray();
    }

    public async Task<ApprovalRuleVersionComparison> CompareVersionsAsync(long actor, long firstVersionId, long secondVersionId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_READ", null, ct);
        var first = await LoadVersionViewAsync(firstVersionId, ct); var second = await LoadVersionViewAsync(secondVersionId, ct);
        var firstSteps = first.Steps.GroupBy(x=>x.StepNo).ToDictionary(x=>x.Key,x=>x.OrderBy(s=>s.ApproverType).ThenBy(s=>s.TargetId).ToArray()); var secondSteps = second.Steps.GroupBy(x=>x.StepNo).ToDictionary(x=>x.Key,x=>x.OrderBy(s=>s.ApproverType).ThenBy(s=>s.TargetId).ToArray());
        var added = secondSteps.Keys.Except(firstSteps.Keys).ToArray(); var removed = firstSteps.Keys.Except(secondSteps.Keys).ToArray();
        var changed = firstSteps.Keys.Intersect(secondSteps.Keys).Where(x => !firstSteps[x].SequenceEqual(secondSteps[x])).ToArray();
        var configChanged = first.Mode != second.Mode || first.RejectionBehavior != second.RejectionBehavior || first.ExpiresAfterMinutes != second.ExpiresAfterMinutes || first.DelegationAllowed != second.DelegationAllowed || first.RequireRequesterSeparation != second.RequireRequesterSeparation || first.RequireCreatorSeparation != second.RequireCreatorSeparation || first.RequireExecutorSeparation != second.RequireExecutorSeparation;
        return new(added, removed, changed, configChanged);
    }

    public async Task<long> SaveDraftAsync(long ruleId, ApprovalRuleVersion version, long actor, string key, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_ADMIN", null, ct); RequireKey(key);
        if (version.RuleId != ruleId) throw new InvalidOperationException("Approval version belongs to a different rule.");
        var validation = version.Validate(); if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
        var fingerprint = Fingerprint(new { version.RuleId, version.VersionNo, version.Mode, version.RejectionBehavior, version.ExpiresAfterMinutes, version.DelegationAllowed, version.RequireRequesterSeparation, version.RequireCreatorSeparation, version.RequireExecutorSeparation, version.EffectiveFrom, version.EffectiveTo, version.Steps });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await PriorAsync(actor, "APPROVAL_RULE_DRAFT", key, ct);
        if (prior is not null)
        {
            EnsureFingerprint(prior, fingerprint);
            return await db.ApprovalRuleVersions.Where(x => x.RuleId == ruleId && x.VersionNo == version.VersionNo).Select(x => x.Id).SingleAsync(ct);
        }
        if (!await db.ApprovalRuleDefinitions.AnyAsync(x => x.Id == ruleId && x.Active, ct)) throw new KeyNotFoundException("Active approval rule was not found.");
        var id = version.Id > 0 ? version.Id : await NextSequenceAsync("PA_TICKETS_APPROVAL_RULE_VERSION_SEQ", ct);
        var row = await db.ApprovalRuleVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is not null && row.Status != "DRAFT") throw new InvalidOperationException("Published approval rule versions are immutable.");
        if (row is null && await db.ApprovalRuleVersions.AnyAsync(x => x.RuleId == ruleId && x.VersionNo == version.VersionNo, ct)) throw new InvalidOperationException("Approval rule version number already exists.");
        var now = DateTimeOffset.UtcNow;
        row ??= new ApprovalRuleVersionRow { Id = id, RuleId = ruleId, VersionNo = version.VersionNo, CreatedAt = now, CreatedBy = actor };
        row.RuleId = ruleId; row.VersionNo = version.VersionNo; row.Status = "DRAFT"; row.Mode = ToDatabaseMode(version.Mode); row.RejectionBehavior = version.RejectionBehavior.ToUpperInvariant();
        row.ExpiresAfterMinutes = version.ExpiresAfterMinutes; row.DelegationAllowed = version.DelegationAllowed; row.RequireRequesterSeparation = version.RequireRequesterSeparation; row.RequireCreatorSeparation = version.RequireCreatorSeparation; row.RequireExecutorSeparation = version.RequireExecutorSeparation;
        row.EffectiveFrom = version.EffectiveFrom; row.EffectiveTo = version.EffectiveTo; row.RowVersion++;
        if (db.Entry(row).State == EntityState.Detached) db.ApprovalRuleVersions.Add(row);
        await db.SaveChangesAsync(ct);
        var oldSteps = await db.ApprovalRuleStepConfigurations.Where(x => x.VersionId == row.Id).ToListAsync(ct);
        if (oldSteps.Count > 0) { db.ApprovalRuleStepConfigurations.RemoveRange(oldSteps); await db.SaveChangesAsync(ct); }
        foreach (var step in version.Steps.OrderBy(x => x.StepNo).ThenBy(x => x.ApproverType).ThenBy(x => x.TargetId))
        {
            var targetKey = $"{step.ApproverType.ToString().ToUpperInvariant()}:{step.TargetId}";
            var config = new ApprovalRuleStepConfigurationRow { Id = await NextSequenceAsync("PA_TICKETS_APPROVAL_RULE_STEP_SEQ", ct), RuleId = ruleId, VersionId = row.Id, StepNo = step.StepNo, ApproverType = step.ApproverType.ToString().ToUpperInvariant(), Required = step.Required, Active = true, ApprovalMode = row.Mode, TargetKey = targetKey };
            switch (step.ApproverType) { case ApprovalApproverType.User: config.UserId = step.TargetId; break; case ApprovalApproverType.Role: config.RoleId = step.TargetId; break; case ApprovalApproverType.Department: config.DepartmentId = step.TargetId; break; case ApprovalApproverType.Team: config.TeamId = step.TargetId; break; }
            db.ApprovalRuleStepConfigurations.Add(config);
        }
        await AddAudit("APPROVAL_RULE_VERSION", row.Id.ToString(), "APPROVAL_RULE_VERSION_DRAFT_SAVED", actor, null, VersionSummary(row), null, correlationId, ct);
        AddIdempotency(actor, "APPROVAL_RULE_DRAFT", key, fingerprint, now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return row.Id;
    }

    public async Task PublishAsync(long versionId, long actor, string key, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_PUBLISH", null, ct); RequireKey(key);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var fingerprint = Fingerprint(new { VersionId = versionId });
        var prior = await PriorAsync(actor, "APPROVAL_RULE_PUBLISH", key, ct); if (prior is not null) { EnsureFingerprint(prior, fingerprint); return; }
        var row = await db.ApprovalRuleVersions.SingleOrDefaultAsync(x => x.Id == versionId, ct) ?? throw new KeyNotFoundException("Approval rule version was not found.");
        if (row.Status != "DRAFT") throw new InvalidOperationException("Only draft approval rule versions can publish.");
        if (!await db.ApprovalRuleDefinitions.AnyAsync(x => x.Id == row.RuleId && x.Active, ct)) throw new InvalidOperationException("An inactive approval rule cannot be published.");
        var version = await LoadDomainVersionAsync(row, ct); var validation = version.Validate(); if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
        await ValidateTargetsAsync(version.Steps, ct);
        var siblings = await db.ApprovalRuleVersions.AsNoTracking().Where(x => x.RuleId == row.RuleId && x.Status == "PUBLISHED").ToListAsync(ct);
        if (siblings.Any(x => Overlaps(row.EffectiveFrom, row.EffectiveTo, x.EffectiveFrom, x.EffectiveTo))) throw new InvalidOperationException("Published approval rule versions cannot overlap effective dates.");
        var old = row.Status; row.Status = "PUBLISHED"; row.PublishedAt = DateTimeOffset.UtcNow; row.PublishedBy = actor; row.RowVersion++;
        await AddAudit("APPROVAL_RULE_VERSION", row.Id.ToString(), "APPROVAL_RULE_VERSION_PUBLISHED", actor, old, row.Status, null, correlationId, ct);
        AddIdempotency(actor, "APPROVAL_RULE_PUBLISH", key, fingerprint, DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task RetireAsync(long versionId, long actor, string key, string? reason, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "APPROVAL_PUBLISH", null, ct); RequireKey(key);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var fingerprint = Fingerprint(new { VersionId = versionId, reason });
        var prior = await PriorAsync(actor, "APPROVAL_RULE_RETIRE", key, ct); if (prior is not null) { EnsureFingerprint(prior, fingerprint); return; }
        var row = await db.ApprovalRuleVersions.SingleOrDefaultAsync(x => x.Id == versionId, ct) ?? throw new KeyNotFoundException("Approval rule version was not found.");
        if (row.Status != "PUBLISHED") throw new InvalidOperationException("Only a published approval rule version can be retired.");
        var old = row.Status; row.Status = "RETIRED"; row.RetiredAt = DateTimeOffset.UtcNow; row.RetiredBy = actor; row.RowVersion++;
        await AddAudit("APPROVAL_RULE_VERSION", row.Id.ToString(), "APPROVAL_RULE_VERSION_RETIRED", actor, old, row.Status, reason, correlationId, ct);
        AddIdempotency(actor, "APPROVAL_RULE_RETIRE", key, fingerprint, DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<SelectedApprovalRuleVersion> ResolveAsync(long versionId, DateTimeOffset at, CancellationToken ct)
    {
        var row = await db.ApprovalRuleVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.Status == "PUBLISHED" && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), ct)
            ?? throw new InvalidOperationException("No effective published approval rule version was found.");
        if (!await db.ApprovalRuleDefinitions.AsNoTracking().AnyAsync(x => x.Id == row.RuleId && x.Active, ct)) throw new InvalidOperationException("Approval rule is inactive.");
        var steps = await db.ApprovalRuleStepConfigurations.AsNoTracking().Where(x => x.VersionId == versionId && x.Active).OrderBy(x => x.StepNo).ThenBy(x => x.Id).ToListAsync(ct);
        var selected = new List<DurableApproverSelection>();
        foreach (var step in steps)
        {
            var type = Enum.Parse<ApprovalApproverType>(step.ApproverType, true); var users = await ResolveUsersAsync(type, TargetId(step), at, ct);
            selected.AddRange(users.Select(userId => new DurableApproverSelection(userId, step.StepNo, step.Required)));
        }
        if (selected.Count == 0) throw new InvalidOperationException("The effective approval rule has no active approvers.");
        return new(versionId, row.RuleId, row.VersionNo, Enum.Parse<ApprovalMode>(ToDomainMode(row.Mode), true), selected, row.RejectionBehavior, row.ExpiresAfterMinutes, row.DelegationAllowed);
    }

    public async Task ValidateRangeAsync(long versionId,DateTimeOffset from,DateTimeOffset? to,CancellationToken ct)
    {
        var row=await db.ApprovalRuleVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==versionId&&x.Status=="PUBLISHED",ct)??throw new KeyNotFoundException("Published approval rule version was not found.");
        if(!await db.ApprovalRuleDefinitions.AsNoTracking().AnyAsync(x=>x.Id==row.RuleId&&x.Active,ct))throw new InvalidOperationException("Approval rule is inactive.");
        if(row.EffectiveFrom>from||row.EffectiveTo is not null&&(to is null||to>row.EffectiveTo))throw new InvalidOperationException("Request Type effective dates must be within the selected approval rule version.");
    }

    private async Task<IReadOnlyCollection<long>> ResolveUsersAsync(ApprovalApproverType type, long targetId, DateTimeOffset at, CancellationToken ct)
    {
        return type switch
        {
            ApprovalApproverType.User => await tickets.AdministrationUsers.AsNoTracking().Where(x => x.Id == targetId && x.Active).Select(x => x.Id).ToListAsync(ct),
            ApprovalApproverType.Role => await (from membership in tickets.AdministrationUserRoles.AsNoTracking() join user in tickets.AdministrationUsers.AsNoTracking() on membership.UserId equals user.Id join role in tickets.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id where membership.RoleId == targetId && membership.Active && membership.ValidFrom <= at && (membership.ValidTo == null || membership.ValidTo > at) && user.Active && role.Active select user.Id).Distinct().ToListAsync(ct),
            ApprovalApproverType.Department => await (from user in tickets.AdministrationUsers.AsNoTracking() join department in tickets.AdministrationDepartments.AsNoTracking() on user.DepartmentId equals department.Id where user.DepartmentId == targetId && user.Active && department.Active select user.Id).ToListAsync(ct),
            ApprovalApproverType.Team => await (from user in tickets.AdministrationUsers.AsNoTracking() join team in tickets.AdministrationTeams.AsNoTracking() on user.TeamId equals team.Id join department in tickets.AdministrationDepartments.AsNoTracking() on team.DepartmentId equals department.Id where user.TeamId == targetId && user.Active && team.Active && department.Active select user.Id).ToListAsync(ct),
            _ => throw new InvalidOperationException("Unsupported approval approver type.")
        };
    }

    private async Task ValidateTargetsAsync(IEnumerable<ApprovalRuleStepDefinition> steps, CancellationToken ct)
    {
        foreach (var step in steps)
        {
            var exists = step.ApproverType switch
            {
                ApprovalApproverType.User => await tickets.AdministrationUsers.AnyAsync(x => x.Id == step.TargetId && x.Active, ct),
                ApprovalApproverType.Role => await tickets.AdministrationRoles.AnyAsync(x => x.Id == step.TargetId && x.Active, ct),
                ApprovalApproverType.Department => await tickets.AdministrationDepartments.AnyAsync(x => x.Id == step.TargetId && x.Active, ct),
                ApprovalApproverType.Team => await tickets.AdministrationTeams.AnyAsync(x => x.Id == step.TargetId && x.Active, ct),
                _ => false
            };
            if (!exists) throw new InvalidOperationException($"Approval step {step.StepNo} references a missing or inactive approver target.");
        }
    }

    private async Task<ApprovalRuleVersion> LoadDomainVersionAsync(ApprovalRuleVersionRow row, CancellationToken ct)
    {
        var steps = await db.ApprovalRuleStepConfigurations.AsNoTracking().Where(x => x.VersionId == row.Id).OrderBy(x => x.StepNo).ThenBy(x => x.Id).ToListAsync(ct);
        return new(row.Id, row.RuleId, row.VersionNo, Enum.Parse<ApprovalMode>(ToDomainMode(row.Mode), true), row.RejectionBehavior, row.ExpiresAfterMinutes, row.DelegationAllowed, row.RequireRequesterSeparation, row.RequireCreatorSeparation, row.RequireExecutorSeparation, row.EffectiveFrom, row.EffectiveTo,
            steps.Select(x => new ApprovalRuleStepDefinition(x.StepNo, Enum.Parse<ApprovalApproverType>(x.ApproverType, true), TargetId(x), x.Required)));
    }

    private async Task<ApprovalRuleVersionView> LoadVersionViewAsync(long versionId, CancellationToken ct)
    {
        var row = await db.ApprovalRuleVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, ct) ?? throw new KeyNotFoundException("Approval rule version was not found.");
        var version = await LoadDomainVersionAsync(row, ct);
        var audit = await db.Audits.AsNoTracking().Where(x => x.EntityType == "APPROVAL_RULE_VERSION" && x.EntityId == versionId.ToString()).OrderBy(x => x.Id).Select(x => new ApprovalRuleConfigurationAudit(x.Action, x.ActorId, x.At, x.OldValue, x.NewValue, x.Reason)).ToListAsync(ct);
        return new(row.Id, row.RuleId, row.VersionNo, row.Status, version.Mode, row.RejectionBehavior, row.ExpiresAfterMinutes, row.DelegationAllowed, row.RequireRequesterSeparation, row.RequireCreatorSeparation, row.RequireExecutorSeparation, row.EffectiveFrom, row.EffectiveTo, version.Steps, audit);
    }

    private async Task<IdempotencyRow?> PriorAsync(long actor, string scope, string key, CancellationToken ct)
        => await db.Idempotency.SingleOrDefaultAsync(x => x.Scope == scope && x.ActorId == actor && x.Key == key, ct);
    private void AddIdempotency(long actor, string scope, string key, string fingerprint, DateTimeOffset now)
        => db.Idempotency.Add(new IdempotencyRow { Scope = scope, Key = key, ActorId = actor, Fingerprint = fingerprint, Status = "COMPLETED", CreatedAt = now, CompletedAt = now });
    private static void EnsureFingerprint(IdempotencyRow row, string fingerprint) { if (row.Fingerprint != fingerprint) throw new InvalidOperationException("Idempotency key was reused for a different approval configuration command."); }
    private static void RequireKey(string key) { if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Idempotency-Key is required."); }
    private async Task<long> NextSequenceAsync(string sequence, CancellationToken ct)
    {
        if (sequence is not ("PA_TICKETS_APPROVAL_RULE_SEQ" or "PA_TICKETS_APPROVAL_RULE_VERSION_SEQ" or "PA_TICKETS_APPROVAL_RULE_STEP_SEQ")) throw new ArgumentException("Unsupported approval sequence.", nameof(sequence));
        var expression = DatabaseProviderCapabilities.Resolve(db.Database.ProviderName).SequenceDefaultSql($"{sequence}.NEXTVAL"); var connection = db.Database.GetDbConnection(); var closeWhenDone = connection.State == ConnectionState.Closed;
        if (closeWhenDone) await connection.OpenAsync(ct);
        try { await using var command = connection.CreateCommand(); command.CommandText = $"SELECT {expression}"; return Convert.ToInt64(await command.ExecuteScalarAsync(ct)); }
        finally { if (closeWhenDone) await connection.CloseAsync(); }
    }
    private async Task AddAudit(string type, string id, string action, long actor, string? oldValue, string? newValue, string? reason, string? correlationId, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, type, id, ct); var at = DateTimeOffset.UtcNow; var previous = await db.Audits.Where(x => x.EntityType == type && x.EntityId == id).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var auditNew = JsonSerializer.Serialize(new { value = newValue, correlationId }); var canonical = JsonSerializer.Serialize(new { entityType = type, entityId = id, action, actorUserId = actor, auditAtUtc = at.UtcDateTime, oldValue, newValue = auditNew, reason, previousHash = previous }); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = type, EntityId = id, Action = action, ActorId = actor, At = at, OldValue = oldValue, NewValue = auditNew, Reason = reason, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Approval rule code and name are required.") : value.Trim();
    private static ApprovalRuleDefinitionView Map(ApprovalRuleDefinitionRow row) => new(row.Id, row.Code, row.Name, row.Description, row.Active);
    private static string VersionSummary(ApprovalRuleVersionRow row) => $"RULE={row.RuleId}:VERSION={row.VersionNo}:MODE={row.Mode}:FROM={row.EffectiveFrom:O}:TO={row.EffectiveTo:O}";
    private static bool Overlaps(DateTimeOffset leftFrom,DateTimeOffset? leftTo,DateTimeOffset rightFrom,DateTimeOffset? rightTo)=>(leftTo is null||rightFrom<leftTo)&&(rightTo is null||leftFrom<rightTo);
    private static long TargetId(ApprovalRuleStepConfigurationRow row) => row.ApproverType.ToUpperInvariant() switch { "USER" => row.UserId ?? 0, "ROLE" => row.RoleId ?? 0, "DEPARTMENT" => row.DepartmentId ?? 0, "TEAM" => row.TeamId ?? 0, _ => 0 };
    private static string ToDomainMode(string mode) => mode.ToUpperInvariant() switch { "ANY_ONE" => nameof(ApprovalMode.AnyOne), "ALL_REQUIRED" => nameof(ApprovalMode.AllRequired), "SEQUENTIAL" => nameof(ApprovalMode.Sequential), _ => mode };
    private static string ToDatabaseMode(ApprovalMode mode) => mode switch { ApprovalMode.AnyOne => "ANY_ONE", ApprovalMode.AllRequired => "ALL_REQUIRED", ApprovalMode.Sequential => "SEQUENTIAL", _ => throw new InvalidOperationException("Unsupported approval mode.") };
    private static string Identity(ApprovalRuleDefinitionRow row) => $"{row.Code}:{row.Name}:{row.Description}";
}
