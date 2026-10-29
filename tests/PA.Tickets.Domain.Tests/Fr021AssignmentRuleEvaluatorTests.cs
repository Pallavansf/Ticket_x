using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class Fr021AssignmentRuleEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Allows_active_assign_rule_when_permission_ticket_scope_and_own_team_authority_match()
    {
        Assert.Null(Evaluate(rule: Rule(), permissionAndRecordScope: true, ownTeam: true));
    }

    [Fact]
    public void Denies_when_destination_team_is_outside_the_actor_own_team_scope()
    {
        Assert.Equal("ASSIGNMENT_TEAM_RELATION_NOT_SATISFIED",
            Evaluate(rule: Rule(), permissionAndRecordScope: true, ownTeam: false));
    }

    [Fact]
    public void Reporting_manager_relationship_alone_does_not_satisfy_assignment_authority()
    {
        // FRD §FR-021 explicitly says N_MANAGER_USER_ID is informational only;
        // the evaluator requires an effective TEAM scope instead.
        Assert.Equal("ASSIGNMENT_TEAM_RELATION_NOT_SATISFIED",
            Evaluate(rule: Rule(), permissionAndRecordScope: true, ownTeam: false));
    }

    [Fact]
    public void Denies_when_rule_is_missing()
    {
        Assert.Equal("ASSIGNMENT_RULE_MISSING",
            Evaluate(rule: null, permissionAndRecordScope: true, ownTeam: true));
    }

    [Fact]
    public void Denies_when_rule_is_revoked_or_expired()
    {
        Assert.Equal("ASSIGNMENT_RULE_INACTIVE_OR_EXPIRED",
            Evaluate(rule: Rule(enabled: false), permissionAndRecordScope: true, ownTeam: true));
        Assert.Equal("ASSIGNMENT_RULE_INACTIVE_OR_EXPIRED",
            Evaluate(rule: Rule(validTo: Now), permissionAndRecordScope: true, ownTeam: true));
    }

    [Fact]
    public void Denies_when_assignment_permission_or_exact_ticket_scope_is_missing()
    {
        Assert.Equal("ASSIGNMENT_PERMISSION_OR_TICKET_SCOPE_MISSING",
            Evaluate(rule: Rule(), permissionAndRecordScope: false, ownTeam: true));
    }

    [Fact]
    public void Ordinary_user_with_only_ticket_update_is_denied_without_role_name_shortcut()
    {
        // The permission service cannot satisfy the independent assignment permission for the USER defaults.
        Assert.Equal("ASSIGNMENT_PERMISSION_OR_TICKET_SCOPE_MISSING",
            Evaluate(rule: Rule(), permissionAndRecordScope: false, ownTeam: true));
    }

    [Fact]
    public void Denies_delegate_mode_and_rule_actions_not_supported_by_this_lifecycle_path()
    {
        Assert.Equal("ASSIGNMENT_MODE_NOT_ALLOWED",
            Evaluate(rule: Rule(mode: "DELEGATE"), permissionAndRecordScope: true, ownTeam: true));
        Assert.Equal("ASSIGNMENT_RULE_ACTION_UNSUPPORTED",
            Evaluate(rule: Rule(notifyManager: true), permissionAndRecordScope: true, ownTeam: true));
    }

    private static string? Evaluate(Fr021AssignmentAuthorityRuleRow? rule, bool permissionAndRecordScope, bool ownTeam) =>
        Fr021AssignmentRuleEvaluator.DenialReason(rule, permissionAndRecordScope, ownTeam,
            targetTeamIsActive: true, targetAssigneeIsValidForTeam: true, mode: "ASSIGN", now: Now);

    private static Fr021AssignmentAuthorityRuleRow Rule(
        bool enabled = true,
        DateTimeOffset? validTo = null,
        string mode = "ASSIGN",
        bool notifyManager = false) => new()
    {
        PermissionCode = "TICKET_ASSIGN_TEAM",
        TargetRelation = "OWN_TEAM",
        Mode = mode,
        Enabled = enabled,
        ValidFrom = Now.AddDays(-1),
        ValidTo = validTo,
        NotifyTargetManager = notifyManager,
        Version = 1
    };
}
