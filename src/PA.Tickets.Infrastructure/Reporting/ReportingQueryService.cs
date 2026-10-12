using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Execution;

namespace PA.Tickets.Infrastructure.Reporting;

public sealed class ReportingQueryService(TicketsDbContext db, ExecutionDbContext execution, IAuthorizationService authorization, TimeProvider clock) : IReportingQueryService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private static readonly TimeZoneInfo BusinessZone = ResolveBusinessZone();
    private static readonly IReadOnlyCollection<ReportDefinition> Definitions = new[]
    {
        new ReportDefinition("ticket-volume","Ticket Volume","Ticket counts by local date, type, priority, and status.",
            new[]{"createdFrom","createdTo","departmentId","teamId","clientId","serviceId","categoryId","subCategoryId","requestTypeId","ticketType","priorityId","status","requesterId","assigneeId","sort","page","pageSize"},
            new[]{"date","ticketType","priority","status","ticketCount"},"TICKET_READ"),
        new ReportDefinition("ticket-status-summary","Ticket Status Summary","Ticket counts grouped by status.",
            new[]{"createdFrom","createdTo","departmentId","teamId","clientId","serviceId","categoryId","subCategoryId","requestTypeId","ticketType","priorityId","status","requesterId","assigneeId","sort","page","pageSize"},
            new[]{"status","ticketCount"},"TICKET_READ"),
        new ReportDefinition("ticket-aging","Ticket Aging","Currently open tickets ordered by age.",
            new[]{"createdFrom","createdTo","departmentId","teamId","clientId","serviceId","categoryId","subCategoryId","requestTypeId","ticketType","priorityId","status","requesterId","assigneeId","sort","page","pageSize"},
            new[]{"ticketNumber","subject","ticketType","priority","status","createdAt","ageDays"},"TICKET_READ"),
        new ReportDefinition("assignment-workload","Assignment Workload","Open work item counts by department, team, and assignee.",
            new[]{"createdFrom","createdTo","departmentId","teamId","clientId","serviceId","categoryId","subCategoryId","requestTypeId","ticketType","priorityId","status","requesterId","assigneeId","sort","page","pageSize"},
            new[]{"department","team","assignee","assignedOpenWorkload"},"TICKET_READ"),        new ReportDefinition("department-workload","Department Workload","Active ticket workload grouped by department.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"department","assignedOpenWorkload"},"TICKET_READ"),
        new ReportDefinition("team-workload","Team Workload","Active ticket workload grouped by team.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"team","assignedOpenWorkload"},"TICKET_READ"),
        new ReportDefinition("assignee-workload","Assignee Workload","Active ticket workload grouped by assignee.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"assignee","assignedOpenWorkload"},"TICKET_READ"),
        new ReportDefinition("open-ticket-backlog","Open Ticket Backlog","Open tickets grouped by status.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"status","ticketCount"},"TICKET_READ"),
        new ReportDefinition("ticket-type-summary","Ticket Type Summary","Ticket counts grouped by ticket type.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticketType","ticketCount"},"TICKET_READ"),
        new ReportDefinition("ticket-aging-buckets","Ticket Aging Buckets","Open tickets grouped into documented age buckets.",new[]{"departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"bucket","ticketCount"},"TICKET_READ"),
        new ReportDefinition("tickets-waiting","Tickets Waiting","Open work items with explicit waiting reasons.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticketNumber","waitingReason","waitingStartedAt","waitingMinutes"},"TICKET_READ"),
        new ReportDefinition("incident-summary","Incident Summary","Incident tickets grouped by status.",new[]{"createdFrom","createdTo","departmentId","teamId","priorityId","status","sort","page","pageSize"},new[]{"status","ticketCount"},"TICKET_READ"),
        new ReportDefinition("problem-summary","Problem Summary","Problem tickets grouped by status.",new[]{"createdFrom","createdTo","departmentId","teamId","priorityId","status","sort","page","pageSize"},new[]{"status","ticketCount"},"TICKET_READ"),
        new ReportDefinition("change-summary","Change Summary","Change tickets grouped by status.",new[]{"createdFrom","createdTo","departmentId","teamId","priorityId","status","sort","page","pageSize"},new[]{"status","ticketCount"},"TICKET_READ"),
        new ReportDefinition("sla-response-performance","SLA Response Performance","Persisted SLA response state where available.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticket","responseDueAt","breachedAt","status"},"TICKET_READ"),
        new ReportDefinition("sla-resolution-performance","SLA Resolution Performance","Persisted SLA resolution state where available.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticket","resolutionDueAt","completedAt","breachedAt","status"},"TICKET_READ"),
        new ReportDefinition("sla-breached-tickets","SLA Breached Tickets","Tickets with a persisted SLA breach.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticket","breachedAt","status"},"TICKET_READ"),
        new ReportDefinition("pending-approvals","Pending Approvals","Persisted pending approval steps.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticket","approvalLevel","approver","createdAt","status"},"TICKET_READ"),
        new ReportDefinition("approval-history","Approval History","Persisted approval history for authorized tickets.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","priorityId","status","sort","page","pageSize"},new[]{"ticket","approval","decision","decisionAt","comment"},"TICKET_READ"),
        new ReportDefinition("lifecycle","Lifecycle Events","Persisted lifecycle events for authorized tickets.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","sort","page","pageSize"},new[]{"ticketNumber","eventType","eventAt","oldValue","newValue"},"TICKET_READ"),
        new ReportDefinition("reopened-tickets","Reopened Tickets","Persisted reopen events for authorized tickets.",new[]{"createdFrom","createdTo","departmentId","teamId","ticketType","sort","page","pageSize"},new[]{"ticketNumber","eventAt","reason"},"TICKET_READ")
    };

    public IReadOnlyCollection<ReportDefinition> Catalogue() => Definitions;
    public ReportDefinition GetDefinition(string code) => Definitions.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException("The requested report was not found.");

    public async Task<ReportResult> QueryAsync(string code, ReportQueryRequest request, long actor, string? correlationId, CancellationToken ct)
    {
        var definition = GetDefinition(code);
        Validate(request, definition);
        await authorization.EnsurePermissionAsync(actor, definition.Permission, null, ct);
        var tickets = await AuthorizedTickets(actor, ct);
        return definition.Code switch
        {
            "ticket-volume" => await Volume(definition, request, tickets, ct),
            "ticket-status-summary" => await StatusSummary(definition, request, tickets, ct),
            "ticket-aging" => await Aging(definition, request, tickets, ct),
            "assignment-workload" or "department-workload" or "team-workload" or "assignee-workload" => await Assignment(definition, request, tickets, ct),
            "open-ticket-backlog" => await StatusSummary(definition, request, tickets.Where(x => x.StatusId != (int)TicketStatus.Closed && x.StatusId != (int)TicketStatus.Cancelled), ct),
            "ticket-type-summary" => await TypeSummary(definition, request, tickets, ct),
            "ticket-aging-buckets" => await AgingBuckets(definition, request, tickets, ct),
            "tickets-waiting" => await Waiting(definition, request, tickets, ct),
            "sla-response-performance" or "sla-resolution-performance" or "sla-breached-tickets" => await Sla(definition, request, tickets, ct),
            "pending-approvals" or "approval-history" => await Approvals(definition, request, tickets, ct),
            "lifecycle" or "reopened-tickets" => await Lifecycle(definition, request, tickets, ct),
            "incident-summary" => await StatusSummary(definition, request, tickets.Where(x => x.TypeId == (int)TicketType.Incident), ct),
            "problem-summary" => await StatusSummary(definition, request, tickets.Where(x => x.TypeId == (int)TicketType.Problem), ct),
            "change-summary" => await StatusSummary(definition, request, tickets.Where(x => x.TypeId == (int)TicketType.Change), ct),
            _ => throw new KeyNotFoundException("The requested report was not found.")
        };
    }

    private async Task<IQueryable<TicketRow>> AuthorizedTickets(long actor, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var user = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor && x.Active, ct)
            ?? throw new UnauthorizedAccessException("The authenticated subject is not an active user.");
        var admin = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                           join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                           where membership.UserId == actor && membership.Active && membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now)
                               && role.Active && role.Code == "ADMINISTRATOR"
                           select role.Id).AnyAsync(ct);
        if (admin) return db.Tickets.AsNoTracking();
        var clients = db.AdministrationUserClients.AsNoTracking().Where(x => x.UserId == actor && x.Active && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now)).Select(x => x.ClientId);
        return db.Tickets.AsNoTracking().Where(x =>
            (x.CurrentAssignee == actor &&
             (!x.CurrentDepartmentId.HasValue || x.CurrentDepartmentId == user.DepartmentId) &&
             (!x.CurrentTeamId.HasValue || x.CurrentTeamId == user.TeamId) &&
             (!x.ClientId.HasValue || clients.Contains(x.ClientId.Value))) ||
            (user.DepartmentId.HasValue && x.CurrentDepartmentId == user.DepartmentId) ||
            (user.TeamId.HasValue && x.CurrentTeamId == user.TeamId) ||
            (x.ClientId.HasValue && clients.Contains(x.ClientId.Value)));
    }

    private async Task<ReportResult> Volume(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var grouped = ApplyFilters(source, r).GroupBy(x => new { Day = x.CreatedAt.Date, x.TypeId, x.PriorityId, x.StatusId }).Select(g => new { g.Key.Day, g.Key.TypeId, g.Key.PriorityId, g.Key.StatusId, Count = g.Count() });
        var total = await grouped.CountAsync(ct);
        var ordered = r.Sort.ToLowerInvariant() switch
        {
            "tickettype" => r.Descending ? grouped.OrderByDescending(x => x.TypeId).ThenBy(x => x.Day).ThenBy(x => x.TypeId) : grouped.OrderBy(x => x.TypeId).ThenBy(x => x.Day).ThenBy(x => x.TypeId),
            "priority" => r.Descending ? grouped.OrderByDescending(x => x.PriorityId).ThenBy(x => x.Day).ThenBy(x => x.TypeId) : grouped.OrderBy(x => x.PriorityId).ThenBy(x => x.Day).ThenBy(x => x.TypeId),
            "status" => r.Descending ? grouped.OrderByDescending(x => x.StatusId).ThenBy(x => x.Day).ThenBy(x => x.TypeId) : grouped.OrderBy(x => x.StatusId).ThenBy(x => x.Day).ThenBy(x => x.TypeId),
            "count" => r.Descending ? grouped.OrderByDescending(x => x.Count).ThenBy(x => x.Day).ThenBy(x => x.TypeId) : grouped.OrderBy(x => x.Count).ThenBy(x => x.Day).ThenBy(x => x.TypeId),
            _ => r.Descending ? grouped.OrderByDescending(x => x.Day).ThenBy(x => x.TypeId).ThenBy(x => x.StatusId) : grouped.OrderBy(x => x.Day).ThenBy(x => x.TypeId).ThenBy(x => x.StatusId)
        };
        var rows = await ordered.Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).ToListAsync(ct);
        var priorities = await PriorityMap(ct); var statuses = await StatusMap(ct);
        var result = rows.Select(x => (object)new TicketVolumeRow(DateOnly.FromDateTime(x.Day), TypeName(x.TypeId), priorities.GetValueOrDefault(x.PriorityId ?? 0, "Unspecified"), statuses.GetValueOrDefault(x.StatusId, x.StatusId.ToString()), x.Count)).ToArray();
        return Result(d, r, total, result);
    }

    private async Task<ReportResult> StatusSummary(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var grouped = ApplyFilters(source, r).GroupBy(x => x.StatusId).Select(g => new { StatusId = g.Key, Count = g.Count() });
        var total = await grouped.CountAsync(ct);
        var ordered = r.Descending ? grouped.OrderByDescending(x => x.Count).ThenBy(x => x.StatusId) : grouped.OrderBy(x => x.StatusId).ThenByDescending(x => x.Count);
        var rows = await ordered.Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).ToListAsync(ct);
        var statuses = await StatusMap(ct);
        return Result(d, r, total, rows.Select(x => (object)new TicketStatusSummaryRow(statuses.GetValueOrDefault(x.StatusId, x.StatusId.ToString()), x.Count)).ToArray());
    }

    private async Task<ReportResult> Aging(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var q = ApplyFilters(source, r).Where(x => x.StatusId != (int)TicketStatus.Closed && x.StatusId != (int)TicketStatus.Cancelled);
        var total = await q.CountAsync(ct);
        var rows = await (r.Descending ? q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id) : q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).Select(x => new AgingProjection(x.Id,x.Number,x.Subject,x.TypeId,x.PriorityId,x.StatusId,x.CreatedAt)).ToListAsync(ct);
        var priorities = await PriorityMap(ct); var statuses = await StatusMap(ct); var now = clock.GetUtcNow();
        return Result(d, r, total, rows.Select(x => (object)new TicketAgingRow(x.Number,x.Subject,TypeName(x.TypeId),priorities.GetValueOrDefault(x.PriorityId ?? 0,"Unspecified"),statuses.GetValueOrDefault(x.StatusId,x.StatusId.ToString()),x.CreatedAt,Math.Max(0,(now-x.CreatedAt).Days))).ToArray());
    }

    private async Task<ReportResult> Assignment(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var ticketIds = ApplyFilters(source, r).Select(x => x.Id);
        var grouped = db.WorkItems.AsNoTracking().Where(x => ticketIds.Contains(x.TicketId) && x.Status != "COMPLETED" && x.Status != "CANCELLED")
            .GroupBy(x => new { x.ToDepartmentId, x.ToTeamId, x.AssignedToUserId }).Select(g => new { DepartmentId = g.Key.ToDepartmentId, TeamId = g.Key.ToTeamId, AssigneeId = g.Key.AssignedToUserId, Count = g.Count() });
        var total = await grouped.CountAsync(ct);
        var ordered = r.Descending ? grouped.OrderByDescending(x => x.Count).ThenBy(x => x.DepartmentId).ThenBy(x => x.TeamId).ThenBy(x => x.AssigneeId) : grouped.OrderBy(x => x.DepartmentId).ThenBy(x => x.TeamId).ThenBy(x => x.AssigneeId);
        var rows = await ordered.Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).ToListAsync(ct);
        var departments = await db.AdministrationDepartments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var teams = await db.AdministrationTeams.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var users = await db.AdministrationUsers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        return Result(d, r, total, rows.Select(x => (object)new AssignmentWorkloadRow(x.DepartmentId, x.DepartmentId is long dep && departments.TryGetValue(dep,out var dn)?dn:null,x.TeamId,x.TeamId is long team && teams.TryGetValue(team,out var tn)?tn:null,x.AssigneeId,x.AssigneeId is long assignee && users.TryGetValue(assignee,out var un)?un:null,x.Count)).ToArray());
    }

    private async Task<ReportResult> Sla(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var ids=await ApplyFilters(source,r).Select(x=>x.Id).ToArrayAsync(ct);
        var q=execution.Slas.AsNoTracking().Where(s=>ids.Contains(s.TicketId));
        if(d.Code=="sla-breached-tickets") q=q.Where(x=>x.BreachedAt!=null);
        var total=await q.CountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.BreachedAt??x.StartedAt).ThenBy(x=>x.Id).Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToListAsync(ct);
        var ticketIds=rows.Select(y=>y.TicketId).ToArray();
        var tickets=await db.Tickets.AsNoTracking().Where(x=>ticketIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        return Result(d,r,total,rows.Select(x=>(object)new{ticket=tickets.GetValueOrDefault(x.TicketId)?.Number,ticketType=tickets.GetValueOrDefault(x.TicketId) is { } t?TypeName(t.TypeId):null,status=x.Status,responseDueAt=x.ResponseDueAt,resolutionDueAt=x.ResolutionDueAt,completedAt=x.CompletedAt,breachedAt=x.BreachedAt}).ToArray());
    }

    private async Task<ReportResult> Approvals(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var ids=await ApplyFilters(source,r).Select(x=>x.Id).ToArrayAsync(ct);
        var tickets=await db.Tickets.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var approvalIds=await execution.Approvals.AsNoTracking().Where(a=>ids.Contains(a.TicketId)).Select(a=>a.Id).ToArrayAsync(ct);
        if(d.Code=="pending-approvals")
        {
            var q=execution.Approvals.AsNoTracking().Where(a=>approvalIds.Contains(a.Id)&&a.Active&&a.Status=="PENDING");
            var total=await q.CountAsync(ct);var rows=await q.OrderBy(x=>x.Id).Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToListAsync(ct);
            return Result(d,r,total,rows.Select(x=>(object)new{ticket=tickets.GetValueOrDefault(x.TicketId)?.Number,approvalLevel=x.Level,approver=x.ApproverId,status=x.Status}).ToArray());
        }
        var h=execution.ApprovalHistory.AsNoTracking().Where(x=>approvalIds.Contains(x.ApprovalId));
        var count=await h.CountAsync(ct);
        var historyRows=await h.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToListAsync(ct);
        return Result(d,r,count,historyRows.Select(x=>(object)new{approval=x.ApprovalId,approver=x.ActorId,decision=x.Decision,decisionAt=x.CreatedAt,comment=x.Comment}).ToArray());
    }

private async Task<ReportResult> TypeSummary(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var grouped=ApplyFilters(source,r).GroupBy(x=>x.TypeId).Select(g=>new{TypeId=g.Key,Count=g.Count()});
        var total=await grouped.CountAsync(ct);
        var rows=await (r.Descending?grouped.OrderByDescending(x=>x.Count).ThenBy(x=>x.TypeId):grouped.OrderBy(x=>x.TypeId)).Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToListAsync(ct);
        return Result(d,r,total,rows.Select(x=>(object)new OperationalCountRow(TypeName(x.TypeId),x.Count)).ToArray());
    }

    private async Task<ReportResult> AgingBuckets(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var q=ApplyFilters(source,r).Where(x=>x.StatusId!=(int)TicketStatus.Closed&&x.StatusId!=(int)TicketStatus.Cancelled);
        var now=clock.GetUtcNow();
        var boundaries=new[]{(0,2,"0-1 days"),(2,4,"2-3 days"),(4,8,"4-7 days"),(8,15,"8-14 days"),(15,31,"15-30 days"),(31,61,"31-60 days"),(61,91,"61-90 days")};
        var rows=new List<object>();
        foreach(var b in boundaries){var upper=now.AddDays(-b.Item1);var lower=now.AddDays(-b.Item2);var count=await q.Where(x=>x.CreatedAt<=upper&&x.CreatedAt>lower).CountAsync(ct);rows.Add(new AgingBucketRow(b.Item3,count));}
        var older=await q.Where(x=>x.CreatedAt<=now.AddDays(-91)).CountAsync(ct);rows.Add(new AgingBucketRow("90+ days",older));
        return Result(d,r,rows.Count,rows.Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToArray());
    }

    private async Task<ReportResult> Waiting(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var ids=await ApplyFilters(source,r).Select(x=>x.Id).ToArrayAsync(ct);
        var q=from w in db.WorkItems.AsNoTracking() join t in db.Tickets.AsNoTracking() on w.TicketId equals t.Id where ids.Contains(w.TicketId)&&w.WaitingStartedAt!=null&&w.WaitingResumedAt==null select new{w.Id,t.Number,t.Subject,t.TypeId,w.WaitingFor,w.WaitingStartedAt,w.WaitingMinutes};
        var total=await q.CountAsync(ct);
        var rows=await q.OrderBy(x=>x.WaitingStartedAt).ThenBy(x=>x.Id).Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToListAsync(ct);
        return Result(d,r,total,rows.Select(x=>(object)new WaitingTicketRow(x.Number,x.Subject,TypeName(x.TypeId),x.WaitingFor,x.WaitingStartedAt,x.WaitingMinutes)).ToArray());
    }

    private async Task<ReportResult> Lifecycle(ReportDefinition d, ReportQueryRequest r, IQueryable<TicketRow> source, CancellationToken ct)
    {
        var ids=await ApplyFilters(source,r).Select(x=>x.Id).ToArrayAsync(ct);
        var q=from e in db.Events.AsNoTracking() join t in db.Tickets.AsNoTracking() on e.TicketId equals t.Id where ids.Contains(e.TicketId) select new{e.Id,t.Number,e.Type,e.At,e.OldValue,e.NewValue,e.Reason};
        if(d.Code=="reopened-tickets") q=q.Where(x=>x.Type.Contains("REOPEN"));
        var total=await q.CountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.At).ThenByDescending(x=>x.Id).Skip((r.Page-1)*r.PageSize).Take(r.PageSize).ToListAsync(ct);
        return Result(d,r,total,rows.Select(x=>(object)new{ticketNumber=x.Number,eventType=x.Type,eventAt=x.At,oldValue=x.OldValue,newValue=x.NewValue,reason=x.Reason}).ToArray());
    }

    private IQueryable<TicketRow> ApplyFilters(IQueryable<TicketRow> q, ReportQueryRequest r)
    {
        if (r.CreatedFrom is DateOnly from) q = q.Where(x => x.CreatedAt >= Boundary(from, false));
        if (r.CreatedTo is DateOnly to) q = q.Where(x => x.CreatedAt < Boundary(to, true));
        if (r.ClosedFrom is DateOnly closedFrom) q = q.Where(x => x.ClosedAt >= Boundary(closedFrom, false));
        if (r.ClosedTo is DateOnly closedTo) q = q.Where(x => x.ClosedAt < Boundary(closedTo, true));
        if (r.DepartmentId is long department) q = q.Where(x => x.CurrentDepartmentId == department);
        if (r.TeamId is long team) q = q.Where(x => x.CurrentTeamId == team);
        if (r.ClientId is long client) q = q.Where(x => x.ClientId == client);
        if (r.ServiceId is long service) q = q.Where(x => x.ServiceId == service);
        if (r.CategoryId is long category) q = q.Where(x => x.CategoryId == category);
        if (r.SubCategoryId is long subCategory) q = q.Where(x => EF.Property<long?>(x, "SubCategoryId") == subCategory);
        if (r.RequestTypeId is long requestType) q = q.Where(x => x.RequestTypeId == requestType);
        if (r.TicketType is TicketType type) q = q.Where(x => x.TypeId == (int)type);
        if (r.PriorityId is long priority) q = q.Where(x => x.PriorityId == priority);
        if (r.Status is TicketStatus status) q = q.Where(x => x.StatusId == (int)status);
        if (r.RequesterId is long requester) q = q.Where(x => x.RequestedBy == requester);
        if (r.AssigneeId is long assignee) q = q.Where(x => x.CurrentAssignee == assignee);
        return q;
    }

    private static void Validate(ReportQueryRequest r, ReportDefinition d)
    {
        if (r.Page < 1) throw new ArgumentException("Page must be at least 1.");
        if (r.PageSize < 1 || r.PageSize > MaxPageSize) throw new ArgumentException($"PageSize must be between 1 and {MaxPageSize}.");
        if (r.CreatedFrom is DateOnly from && r.CreatedTo is DateOnly to && from > to) throw new ArgumentException("CreatedFrom must not be after CreatedTo.");
        if (r.ClosedFrom is DateOnly closedFrom && r.ClosedTo is DateOnly closedTo && closedFrom > closedTo) throw new ArgumentException("ClosedFrom must not be after ClosedTo.");
        if (!string.IsNullOrWhiteSpace(r.SlaState)) throw new ArgumentException("SlaState is not supported by the current reporting schema.");
        var allowed = d.Code switch { "ticket-volume" or "ticket-type-summary" => new[]{"createdDate","ticketType","priority","status","count"}, "ticket-status-summary" or "open-ticket-backlog" or "incident-summary" or "problem-summary" or "change-summary" => new[]{"status","count","createdDate"}, "ticket-aging" or "ticket-aging-buckets" => new[]{"age","createdDate","ticketNumber","bucket"}, "assignment-workload" or "department-workload" or "team-workload" or "assignee-workload" or "tickets-waiting" or "lifecycle" or "reopened-tickets" or "sla-response-performance" or "sla-resolution-performance" or "sla-breached-tickets" or "pending-approvals" or "approval-history" => new[]{"department","team","assignee","count","createdDate","age","ticketNumber"}, _ => Array.Empty<string>() };
        if (!allowed.Contains(r.Sort, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("The requested sort field is not supported by this report.");
        if (r.UnsupportedFilters is { Count: > 0 }) throw new ArgumentException("One or more filters are not supported.");
    }

    private static DateTimeOffset Boundary(DateOnly date, bool exclusiveEnd)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (exclusiveEnd) local = local.AddDays(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, BusinessZone), TimeSpan.Zero);
    }
    private static TimeZoneInfo ResolveBusinessZone(){try{return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");}catch(TimeZoneNotFoundException){return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");}}
    private static string TypeName(int id)=>Enum.IsDefined(typeof(TicketType),id)?((TicketType)id).ToString():id.ToString();
    private async Task<Dictionary<long,string>> PriorityMap(CancellationToken ct)=>await db.PriorityDefinitions.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);
    private async Task<Dictionary<int,string>> StatusMap(CancellationToken ct)=>await db.StatusDefinitions.AsNoTracking().ToDictionaryAsync(x=>(int)x.Id,x=>x.Name,ct);
    private static ReportResult Result(ReportDefinition d,ReportQueryRequest r,int total,IReadOnlyCollection<object> rows)=>new(d.Code,d.Name,DateTimeOffset.UtcNow,r,r.Page,r.PageSize,total,rows);
    private static IQueryable<VolumeProjection> OrderVolume(IQueryable<VolumeProjection> q,ReportQueryRequest r)=>r.Sort.ToLowerInvariant() switch { "tickettype"=>r.Descending?q.OrderByDescending(x=>x.TypeId).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId):q.OrderBy(x=>x.TypeId).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId), "priority"=>r.Descending?q.OrderByDescending(x=>x.PriorityId).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId):q.OrderBy(x=>x.PriorityId).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId), "status"=>r.Descending?q.OrderByDescending(x=>x.StatusId).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId):q.OrderBy(x=>x.StatusId).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId), "count"=>r.Descending?q.OrderByDescending(x=>x.Count).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId):q.OrderBy(x=>x.Count).ThenBy(x=>x.Day).ThenBy(x=>x.TypeId), _=>r.Descending?q.OrderByDescending(x=>x.Day).ThenBy(x=>x.TypeId).ThenBy(x=>x.StatusId):q.OrderBy(x=>x.Day).ThenBy(x=>x.TypeId).ThenBy(x=>x.StatusId)};
    private sealed record VolumeProjection(DateTime Day,int TypeId,long? PriorityId,int StatusId,int Count);
    private sealed record StatusProjection(int StatusId,int Count);
    private sealed record AgingProjection(long Id,string Number,string Subject,int TypeId,long? PriorityId,int StatusId,DateTimeOffset CreatedAt);
    private sealed record AssignmentProjection(long? DepartmentId,long? TeamId,long? AssigneeId,int Count);
}