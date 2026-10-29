using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Persistence;

[Table("PA_TICKETS_USER")]
public sealed class AdministrationUserRow
{
    [Key, Column("N_USER_ID")] public long Id { get; set; }
    [Column("S_USER_CODE")] public string UserCode { get; set; } = "";
    [Column("S_LOGIN_NAME")] public string LoginName { get; set; } = "";
    [Column("S_PASSWORD_HASH")] public string? PasswordHash { get; set; }
    [Column("N_PASSWORD_ROTATION_REQUIRED")] public bool PasswordRotationRequired { get; set; }
    [Column("S_DISPLAY_NAME")] public string DisplayName { get; set; } = "";
    [Column("S_EMAIL")] public string? Email { get; set; }
    [Column("N_EMAIL_VERIFIED")] public bool EmailVerified { get; set; }
    [Column("S_PHONE")] public string? Phone { get; set; }
    [Column("N_DEPARTMENT_ID")] public long? DepartmentId { get; set; }
    [Column("N_TEAM_ID")] public long? TeamId { get; set; }
    [Column("N_MANAGER_USER_ID")] public long? ManagerUserId { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
}

[Table("PA_TICKETS_DEPARTMENT")]
public sealed class AdministrationDepartmentRow
{
    [Key, Column("N_DEPARTMENT_ID")] public long Id { get; set; }
    [Column("S_DEPARTMENT_CODE")] public string Code { get; set; } = "";
    [Column("S_DEPARTMENT_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_TEAM")]
public sealed class AdministrationTeamRow
{
    [Key, Column("N_TEAM_ID")] public long Id { get; set; }
    [Column("N_DEPARTMENT_ID")] public long DepartmentId { get; set; }
    [Column("S_TEAM_CODE")] public string Code { get; set; } = "";
    [Column("S_TEAM_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_ROLE")]
public sealed class AdministrationRoleRow
{
    [Key, Column("N_ROLE_ID")] public long Id { get; set; }
    [Column("S_ROLE_CODE")] public string Code { get; set; } = "";
    [Column("S_ROLE_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_SYSTEM_ROLE")] public bool SystemRole { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
}

[Table("PA_TICKETS_PERMISSION")]
public sealed class AdministrationPermissionRow
{
    [Key, Column("N_PERMISSION_ID")] public long Id { get; set; }
    [Column("S_PERMISSION_CODE")] public string Code { get; set; } = "";
    [Column("S_PERMISSION_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("S_MODULE_CODE")] public string ModuleCode { get; set; } = "";
    [Column("S_ACTION_CODE")] public string ActionCode { get; set; } = "";
    [Column("S_SCOPE_CLASS")] public string ScopeClass { get; set; } = "";
    [Column("S_SECURITY_CLASS")] public string SecurityClass { get; set; } = "";
    [Column("N_READ_ONLY")] public bool ReadOnly { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
}

[Table("PA_TICKETS_USER_ROLE")]
public sealed class AdministrationUserRoleRow
{
    [Key, Column("N_USER_ROLE_ID")] public long Id { get; set; }
    [Column("N_USER_ID")] public long UserId { get; set; }
    [Column("N_ROLE_ID")] public long RoleId { get; set; }
    [Column("D_VALID_FROM")] public DateTimeOffset ValidFrom { get; set; }
    [Column("D_VALID_TO")] public DateTimeOffset? ValidTo { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_ROLE_PERMISSION")]
public sealed class AdministrationRolePermissionRow
{
    [Key, Column("N_ROLE_PERMISSION_ID")] public long Id { get; set; }
    [Column("N_ROLE_ID")] public long RoleId { get; set; }
    [Column("N_PERMISSION_ID")] public long PermissionId { get; set; }
    [Column("D_VALID_FROM")] public DateTimeOffset? ValidFrom { get; set; }
    [Column("D_VALID_TO")] public DateTimeOffset? ValidTo { get; set; }
    [Column("S_EFFECTIVE_STATUS")] public string EffectiveStatus { get; set; } = "UNKNOWN";
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("S_PROVENANCE_REF")] public string? ProvenanceRef { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
}

[Table("PA_TICKETS_USER_ROLE_SCOPE")]
public sealed class AdministrationUserRoleScopeRow
{
    [Key, Column("N_ROLE_SCOPE_ID")] public long Id { get; set; }
    [Column("N_USER_ROLE_ID")] public long UserRoleId { get; set; }
    [Column("S_SCOPE_TYPE")] public string ScopeType { get; set; } = "";
    [Column("N_ORGANIZATION_ID")] public long? OrganizationId { get; set; }
    [Column("N_DEPARTMENT_ID")] public long? DepartmentId { get; set; }
    [Column("N_TEAM_ID")] public long? TeamId { get; set; }
    [Column("N_CLIENT_ID")] public long? ClientId { get; set; }
    [Column("S_RESOURCE_TYPE")] public string? ResourceType { get; set; }
    [Column("S_RESOURCE_ID")] public string? ResourceId { get; set; }
    [Column("D_VALID_FROM")] public DateTimeOffset ValidFrom { get; set; }
    [Column("D_VALID_TO")] public DateTimeOffset? ValidTo { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_USER_CLIENT")]
public sealed class AdministrationUserClientRow
{
    [Key, Column("N_USER_CLIENT_ID")] public long Id { get; set; }
    [Column("N_USER_ID")] public long UserId { get; set; }
    [Column("N_CLIENT_ID")] public long ClientId { get; set; }
    [Column("D_VALID_FROM")] public DateTimeOffset ValidFrom { get; set; }
    [Column("D_VALID_TO")] public DateTimeOffset? ValidTo { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE")]
public sealed class Fr021AssignmentAuthorityRuleRow
{
    [Key, Column("N_ASSIGNMENT_RULE_ID")] public long Id { get; set; }
    [Column("S_PERMISSION_CODE")] public string PermissionCode { get; set; } = "";
    [Column("S_TARGET_RELATION")] public string TargetRelation { get; set; } = "";
    [Column("S_MODE")] public string Mode { get; set; } = "";
    [Column("N_NOTIFY_TARGET_MANAGER")] public bool NotifyTargetManager { get; set; }
    [Column("N_REQUIRE_MANAGER_ACCEPTANCE")] public bool RequireManagerAcceptance { get; set; }
    [Column("N_ENABLED")] public bool Enabled { get; set; }
    [Column("D_VALID_FROM")] public DateTimeOffset ValidFrom { get; set; }
    [Column("D_VALID_TO")] public DateTimeOffset? ValidTo { get; set; }
    [Column("N_VERSION")] public int Version { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_ADMIN_HEAD_DELEGATION")]
public sealed class AdminHeadDelegationRow
{
    [Key, Column("N_ADMIN_DELEGATION_ID")] public long Id { get; set; }
    [Column("N_ADMIN_HEAD_USER_ID")] public long AdminHeadUserId { get; set; }
    [Column("N_ADMIN_USER_ID")] public long AdminUserId { get; set; }
    [Column("D_VALID_FROM")] public DateTimeOffset ValidFrom { get; set; }
    [Column("D_VALID_TO")] public DateTimeOffset? ValidTo { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}

[Table("PA_TICKETS_RBAC_PROPOSAL")]
public sealed class RbacProposalRow
{
    [Key, Column("S_PROPOSAL_ID"), StringLength(32)] public string Id { get; set; } = "";
    [Column("S_OPERATION_TYPE"), StringLength(40)] public string OperationType { get; set; } = "";
    [Column("S_STATUS"), StringLength(20)] public string Status { get; set; } = "PENDING";
    [Column("N_TARGET_ROLE_ID")] public long TargetRoleId { get; set; }
    [Column("N_TARGET_PERMISSION_ID")] public long TargetPermissionId { get; set; }
    [Column("N_INITIATOR_USER_ID")] public long InitiatorUserId { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_EXPIRES_AT")] public DateTimeOffset ExpiresAt { get; set; }
    [Column("S_REASON"), StringLength(1000)] public string Reason { get; set; } = "";
    [Column("S_IDEMPOTENCY_KEY"), StringLength(100)] public string IdempotencyKey { get; set; } = "";
    [Column("S_PAYLOAD_JSON")] public string PayloadJson { get; set; } = "";
    [Column("S_PAYLOAD_SHA256"), StringLength(64)] public string PayloadSha256 { get; set; } = "";
    [Column("N_APPROVER_USER_ID")] public long? ApproverUserId { get; set; }
    [Column("N_CANCELLED_BY_USER_ID")] public long? CancelledByUserId { get; set; }
    [Column("D_DECISION_AT")] public DateTimeOffset? DecisionAt { get; set; }
    [Column("S_DECISION_REASON"), StringLength(1000)] public string? DecisionReason { get; set; }
    [Column("S_APPLICATION_OUTCOME"), StringLength(80)] public string? ApplicationOutcome { get; set; }
    [Column("S_CORRELATION_ID"), StringLength(100)] public string CorrelationId { get; set; } = "";
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}

