using PA.Tickets.Application.Execution;
using PA.Tickets.Domain.Tickets;
using Microsoft.Extensions.Configuration;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class ConfigurationWorkflowRouter(IConfiguration configuration) : IWorkflowRouter
{
    public RoutingDecision Resolve(WorkflowRoutingContext context)
    {
        var rules = configuration.GetSection("WorkflowRouting:Rules").Get<RoutingRule[]>() ?? [];
        var matches = rules.Where(r => (string.IsNullOrWhiteSpace(r.StepCode) || string.Equals(r.StepCode, context.StepCode, StringComparison.OrdinalIgnoreCase))
            && (!r.TicketType.HasValue || r.TicketType.Value == context.TicketType)
            && (!r.PriorityId.HasValue || r.PriorityId == context.PriorityId)
            && (!r.ServiceId.HasValue || r.ServiceId == context.ServiceId)
            && (!r.ClientId.HasValue || r.ClientId == context.ClientId))
            .OrderByDescending(r => (r.StepCode is null ? 0 : 16) + (r.TicketType.HasValue ? 8 : 0) + (r.PriorityId.HasValue ? 4 : 0) + (r.ServiceId.HasValue ? 2 : 0) + (r.ClientId.HasValue ? 1 : 0))
            .ThenBy(r => r.RuleCode, StringComparer.OrdinalIgnoreCase).ToArray();
        if (matches.Length > 1 && matches[0].Specificity == matches[1].Specificity) throw new InvalidOperationException("Routing configuration is ambiguous for the workflow step.");
        var selected = matches.FirstOrDefault()
            ?? (context.ConfiguredDepartmentId.HasValue || context.ConfiguredTeamId.HasValue || context.ConfiguredUserId.HasValue
                ? new RoutingRule { RuleCode = "REQUEST_TYPE_DEFAULT", DepartmentId = context.ConfiguredDepartmentId, TeamId = context.ConfiguredTeamId, UserId = context.ConfiguredUserId }
                : configuration.GetSection("WorkflowRouting:Triage").Get<RoutingRule>());
        if (selected is null || (selected.DepartmentId is null && selected.TeamId is null && selected.UserId is null)) throw new InvalidOperationException("No configured routing target exists for the workflow step.");
        return new(selected.DepartmentId, selected.TeamId, selected.UserId, selected.UserId.HasValue ? "USER" : selected.TeamId.HasValue ? "TEAM" : "DEPARTMENT", selected.RuleCode ?? "TRIAGE");
    }

    public sealed class RoutingRule
    {
        public string? RuleCode { get; set; }
        public string? StepCode { get; set; }
        public int? TicketType { get; set; }
        public long? PriorityId { get; set; }
        public long? ServiceId { get; set; }
        public long? ClientId { get; set; }
        public long? DepartmentId { get; set; }
        public long? TeamId { get; set; }
        public long? UserId { get; set; }
        public int Specificity => (StepCode is null ? 0 : 16) + (TicketType.HasValue ? 8 : 0) + (PriorityId.HasValue ? 4 : 0) + (ServiceId.HasValue ? 2 : 0) + (ClientId.HasValue ? 1 : 0);
    }
}

