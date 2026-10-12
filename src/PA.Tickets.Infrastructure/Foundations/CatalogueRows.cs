using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Foundations;

[Table("PA_TICKETS_SERVICE")]
public sealed class CatalogueServiceRow
{
    [Column("N_SERVICE_ID")] public long Id { get; set; }
    [Column("S_SERVICE_CODE")] public string Code { get; set; } = "";
    [Column("S_SERVICE_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_OWNER_DEPARTMENT_ID")] public long? OwnerDepartmentId { get; set; }
    [Column("N_OWNER_TEAM_ID")] public long? OwnerTeamId { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
}

[Table("PA_TICKETS_CATEGORY")]
public sealed class CatalogueCategoryRow
{
    [Column("N_CATEGORY_ID")] public long Id { get; set; }
    [Column("N_SERVICE_ID")] public long ServiceId { get; set; }
    [Column("S_CATEGORY_CODE")] public string Code { get; set; } = "";
    [Column("S_CATEGORY_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
}

[Table("PA_TICKETS_SUB_CATEGORY")]
public sealed class CatalogueSubCategoryRow
{
    [Column("N_SUB_CATEGORY_ID")] public long Id { get; set; }
    [Column("N_CATEGORY_ID")] public long CategoryId { get; set; }
    [Column("S_SUB_CATEGORY_CODE")] public string Code { get; set; } = "";
    [Column("S_SUB_CATEGORY_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
}

[Table("PA_TICKETS_REQUEST_TYPE")]
public sealed class CatalogueRequestTypeRow
{
    [Column("N_REQUEST_TYPE_ID")] public long Id { get; set; }
    [Column("N_SERVICE_ID")] public long ServiceId { get; set; }
    [Column("N_CATEGORY_ID")] public long CategoryId { get; set; }
    [Column("N_SUB_CATEGORY_ID")] public long? SubCategoryId { get; set; }
    [Column("N_TICKET_TYPE_ID")] public int TicketType { get; set; }
    [Column("S_REQUEST_TYPE_CODE")] public string Code { get; set; } = "";
    [Column("S_REQUEST_TYPE_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_DEFAULT_PRIORITY_ID")] public long? DefaultPriorityId { get; set; }
    [Column("N_DEFAULT_DEPARTMENT_ID")] public long? DefaultDepartmentId { get; set; }
    [Column("N_DEFAULT_TEAM_ID")] public long? DefaultTeamId { get; set; }
    [Column("N_WORKFLOW_ID")] public long? WorkflowId { get; set; }
    [Column("N_WORKFLOW_VERSION_ID")] public long? WorkflowVersionId { get; set; }
    [Column("N_APPROVAL_RULE_VERSION_ID")] public long? ApprovalRuleVersionId { get; set; }
    [Column("N_SLA_POLICY_ID")] public long? SlaPolicyId { get; set; }
    [Column("N_SLA_POLICY_VERSION")] public int? SlaPolicyVersion { get; set; }
    [Column("N_EVIDENCE_REQUIRED")] public bool EvidenceRequired { get; set; }
    [Column("N_EVIDENCE_MIN_COUNT")] public int EvidenceMinimumCount { get; set; }
    [Column("N_VERSION_NO")] public int VersionNo { get; set; }
    [Column("S_CONFIGURATION_STATUS")] public string Status { get; set; } = "DRAFT";
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("D_PUBLISHED_AT")] public DateTimeOffset? PublishedAt { get; set; }
}

[Table("PA_TICKETS_FIELD")]
public sealed class CatalogueFieldRow
{
    [Column("N_FIELD_ID")] public long Id { get; set; }
    [Column("S_FIELD_CODE")] public string Code { get; set; } = "";
    [Column("S_FIELD_NAME")] public string Name { get; set; } = "";
    [Column("S_DATA_TYPE")] public string DataType { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
}

[Table("PA_TICKETS_FIELD_OPTION")]
public sealed class CatalogueFieldOptionRow
{
    [Column("N_FIELD_OPTION_ID")] public long Id { get; set; }
    [Column("N_FIELD_ID")] public long FieldId { get; set; }
    [Column("S_OPTION_CODE")] public string Code { get; set; } = "";
    [Column("S_OPTION_NAME")] public string Name { get; set; } = "";
    [Column("N_DISPLAY_ORDER")] public int DisplayOrder { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
}

[Table("PA_TICKETS_REQUEST_FIELD")]
public sealed class CatalogueRequestFieldRow
{
    [Column("N_REQUEST_FIELD_ID")] public long Id { get; set; }
    [Column("N_REQUEST_TYPE_ID")] public long RequestTypeId { get; set; }
    [Column("N_FIELD_ID")] public long FieldId { get; set; }
    [Column("N_REQUIRED")] public bool Required { get; set; }
    [Column("N_DISPLAY_ORDER")] public int DisplayOrder { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
}

[Table("PA_TICKETS_VALUE")]
public sealed class TicketFieldValueRow
{
    [Column("N_TICKET_VALUE_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("N_FIELD_ID")] public long FieldId { get; set; }
    [Column("S_VALUE_STRING")] public string? StringValue { get; set; }
    [Column("N_VALUE_NUMBER")] public decimal? NumberValue { get; set; }
    [Column("D_VALUE_DATE")] public DateTimeOffset? DateValue { get; set; }
    [Column("S_VALUE_TEXT")] public string? TextValue { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}
