using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Persistence;

[Table("PA_TICKETS_USER")]
public sealed class AdministrationUserRow
{
    [Key, Column("N_USER_ID")] public long Id { get; set; }
    [Column("S_USER_CODE")] public string UserCode { get; set; } = "";
    [Column("S_LOGIN_NAME")] public string LoginName { get; set; } = "";
    [Column("S_DISPLAY_NAME")] public string DisplayName { get; set; } = "";
    [Column("S_EMAIL")] public string? Email { get; set; }
    [Column("N_DEPARTMENT_ID")] public long? DepartmentId { get; set; }
    [Column("N_TEAM_ID")] public long? TeamId { get; set; }
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