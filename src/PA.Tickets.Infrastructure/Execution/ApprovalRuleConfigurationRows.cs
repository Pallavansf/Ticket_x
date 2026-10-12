using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Execution;

[Table("PA_TICKETS_APPROVAL_RULE")]
public sealed class ApprovalRuleDefinitionRow
{
    [Key, Column("N_APPROVAL_RULE_ID")] public long Id { get; set; }
    [Column("S_APPROVAL_CODE")] public string Code { get; set; } = "";
    [Column("S_APPROVAL_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; } = true;
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
}

[Table("PA_TICKETS_APPROVAL_RULE_VERSION")]
public sealed class ApprovalRuleVersionRow
{
    [Key, Column("N_APPROVAL_RULE_VERSION_ID")] public long Id { get; set; }
    [Column("N_APPROVAL_RULE_ID")] public long RuleId { get; set; }
    [Column("N_VERSION_NO")] public int VersionNo { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "DRAFT";
    [Column("S_MODE")] public string Mode { get; set; } = "SEQUENTIAL";
    [Column("S_REJECTION_BEHAVIOR")] public string RejectionBehavior { get; set; } = "REJECT";
    [Column("N_EXPIRY_MINUTES")] public int? ExpiresAfterMinutes { get; set; }
    [Column("N_DELEGATION_ALLOWED")] public bool DelegationAllowed { get; set; }
    [Column("N_REQUIRE_REQUESTER_SOD")] public bool RequireRequesterSeparation { get; set; } = true;
    [Column("N_REQUIRE_CREATOR_SOD")] public bool RequireCreatorSeparation { get; set; } = true;
    [Column("N_REQUIRE_EXECUTOR_SOD")] public bool RequireExecutorSeparation { get; set; } = true;
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_PUBLISHED_AT")] public DateTimeOffset? PublishedAt { get; set; }
    [Column("N_PUBLISHED_BY")] public long? PublishedBy { get; set; }
    [Column("D_RETIRED_AT")] public DateTimeOffset? RetiredAt { get; set; }
    [Column("N_RETIRED_BY")] public long? RetiredBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}

[Table("PA_TICKETS_APPROVAL_RULE_STEP")]
public sealed class ApprovalRuleStepConfigurationRow
{
    [Key, Column("N_APPROVAL_RULE_STEP_ID")] public long Id { get; set; }
    [Column("N_APPROVAL_RULE_ID")] public long RuleId { get; set; }
    [Column("N_APPROVAL_RULE_VERSION_ID")] public long? VersionId { get; set; }
    [Column("N_STEP_NO")] public int StepNo { get; set; }
    [Column("S_APPROVER_TYPE")] public string ApproverType { get; set; } = "USER";
    [Column("N_ROLE_ID")] public long? RoleId { get; set; }
    [Column("N_DEPARTMENT_ID")] public long? DepartmentId { get; set; }
    [Column("N_TEAM_ID")] public long? TeamId { get; set; }
    [Column("N_USER_ID")] public long? UserId { get; set; }
    [Column("S_RULE")] public string? Rule { get; set; }
    [Column("S_APPROVAL_MODE")] public string ApprovalMode { get; set; } = "ALL";
    [Column("S_TARGET_KEY")] public string? TargetKey { get; set; }
    [Column("N_REQUIRED")] public bool Required { get; set; } = true;
    [Column("N_ACTIVE")] public bool Active { get; set; } = true;
}
