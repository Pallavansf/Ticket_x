using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

/// <summary>Evaluates the supported, persisted FR-021 assignment rule shape.</summary>
internal static class Fr021AssignmentRuleEvaluator
{
    internal static string? DenialReason(
        Fr021AssignmentAuthorityRuleRow? rule,
        bool assignmentPermissionAndTicketScopeSatisfied,
        bool effectiveOwnTeamAuthority,
        bool targetTeamIsActive,
        bool targetAssigneeIsValidForTeam,
        string mode,
        DateTimeOffset now)
    {
        if (!assignmentPermissionAndTicketScopeSatisfied) return "ASSIGNMENT_PERMISSION_OR_TICKET_SCOPE_MISSING";
        if (rule is null) return "ASSIGNMENT_RULE_MISSING";
        if (!rule.Enabled || rule.ValidFrom > now || (rule.ValidTo.HasValue && rule.ValidTo <= now)) return "ASSIGNMENT_RULE_INACTIVE_OR_EXPIRED";
        if (rule.Mode != mode || mode != "ASSIGN") return "ASSIGNMENT_MODE_NOT_ALLOWED";
        if (rule.TargetRelation != "OWN_TEAM") return "ASSIGNMENT_RELATION_NOT_SUPPORTED";
        if (rule.NotifyTargetManager || rule.RequireManagerAcceptance) return "ASSIGNMENT_RULE_ACTION_UNSUPPORTED";
        if (!effectiveOwnTeamAuthority) return "ASSIGNMENT_TEAM_RELATION_NOT_SATISFIED";
        if (!targetTeamIsActive) return "ASSIGNMENT_TARGET_TEAM_INVALID";
        if (!targetAssigneeIsValidForTeam) return "ASSIGNMENT_TARGET_USER_INVALID";
        return null;
    }
}
