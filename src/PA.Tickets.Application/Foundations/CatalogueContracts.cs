namespace PA.Tickets.Application.Foundations;

public sealed record CatalogueService(long Id, string Code, string Name, string? Description, long? OwnerDepartmentId, long? OwnerTeamId, bool Active, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record CatalogueCategory(long Id, long ServiceId, string Code, string Name, string? Description, bool Active, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record CatalogueSubCategory(long Id, long CategoryId, string Code, string Name, string? Description, bool Active, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record CatalogueRequestType(long Id, long ServiceId, long CategoryId, int TicketType, string Code, string Name, string? Description, long? DefaultPriorityId, long? DefaultDepartmentId, long? DefaultTeamId, long? WorkflowId, long? SlaPolicyId, int? SlaPolicyVersion, bool EvidenceRequired, int EvidenceMinimumCount, int VersionNo, string Status, bool Active, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, long? SubCategoryId = null, long? WorkflowVersionId = null, long? ApprovalRuleVersionId = null);
public sealed record CatalogueField(long Id, string Code, string Name, string DataType, string? Description, bool Active, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record CatalogueFieldOption(long Id, long FieldId, string Code, string Name, int DisplayOrder, bool Active);
public sealed record CatalogueRequestField(long Id, long RequestTypeId, long FieldId, bool Required, int DisplayOrder, bool Active);
public sealed record TicketFieldValue(long FieldId, string? StringValue, decimal? NumberValue, DateTimeOffset? DateValue, string? TextValue);
public sealed record CatalogueTicketConfiguration(long RequestTypeId, int VersionNo, int TicketType, long ServiceId, long CategoryId, long? DefaultPriorityId, long? DefaultDepartmentId, long? DefaultTeamId, long? WorkflowId, long? SlaPolicyId, int? SlaPolicyVersion, bool EvidenceRequired, int EvidenceMinimumCount, IReadOnlyCollection<CatalogueRequestField> Fields, long? SubCategoryId = null, long? WorkflowVersionId = null, long? ApprovalRuleVersionId = null);

public interface ITicketCatalogueRuntime
{
    Task<CatalogueTicketConfiguration?> ResolveForCreationAsync(long actor, long? requestTypeId, DateTimeOffset at, IReadOnlyCollection<TicketFieldValue> values, CancellationToken ct);
}

public sealed record TicketCreationIdempotencyClaim(bool Replay, long? TicketId, bool Resume = false);
public interface ITicketCreationIdempotencyStore
{
    Task<TicketCreationIdempotencyClaim> ClaimAsync(long actor, string key, string fingerprint, CancellationToken ct);
    Task SetTicketAsync(long actor, string key, long ticketId, CancellationToken ct);
    Task CompleteAsync(long actor, string key, CancellationToken ct);
    Task FailAsync(long actor, string key, CancellationToken ct);
}

public interface ICatalogueAdministrationService
{
    Task<IReadOnlyCollection<CatalogueService>> ListServicesAsync(long actor, CancellationToken ct);
    Task<CatalogueService> GetServiceAsync(long actor, long id, CancellationToken ct);
    Task<CatalogueService> CreateServiceAsync(long actor, string code, string name, string? description, long? ownerDepartmentId, long? ownerTeamId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueService> UpdateServiceAsync(long actor, long id, string code, string name, string? description, long? ownerDepartmentId, long? ownerTeamId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueService> SetServiceActiveAsync(long actor, long id, bool active, CancellationToken ct);

    Task<IReadOnlyCollection<CatalogueCategory>> ListCategoriesAsync(long actor, long? serviceId, CancellationToken ct);
    Task<CatalogueCategory> GetCategoryAsync(long actor, long id, CancellationToken ct);
    Task<CatalogueCategory> CreateCategoryAsync(long actor, long serviceId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueCategory> UpdateCategoryAsync(long actor, long id, long serviceId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueCategory> SetCategoryActiveAsync(long actor, long id, bool active, CancellationToken ct);

    Task<IReadOnlyCollection<CatalogueSubCategory>> ListSubCategoriesAsync(long actor, long? serviceId, long? categoryId, CancellationToken ct);
    Task<CatalogueSubCategory> GetSubCategoryAsync(long actor, long id, CancellationToken ct);
    Task<CatalogueSubCategory> CreateSubCategoryAsync(long actor, long serviceId, long categoryId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueSubCategory> UpdateSubCategoryAsync(long actor, long id, long serviceId, long categoryId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueSubCategory> SetSubCategoryActiveAsync(long actor, long id, bool active, CancellationToken ct);

    Task<IReadOnlyCollection<CatalogueRequestType>> ListRequestTypesAsync(long actor, long? categoryId, CancellationToken ct, long? serviceId = null, long? subCategoryId = null);
    Task<CatalogueRequestType> GetRequestTypeAsync(long actor, long id, CancellationToken ct);
    Task<CatalogueRequestType> CreateRequestTypeAsync(long actor, long serviceId, long categoryId, int ticketType, string code, string name, string? description, long? defaultPriorityId, long? defaultDepartmentId, long? defaultTeamId, long? workflowId, long? slaPolicyId, int? slaPolicyVersion, bool evidenceRequired, int evidenceMinimumCount, int versionNo, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct, long? subCategoryId = null, long? workflowVersionId = null, long? approvalRuleVersionId = null);
    Task<CatalogueRequestType> UpdateRequestTypeAsync(long actor, long id, long serviceId, long categoryId, int ticketType, string code, string name, string? description, long? defaultPriorityId, long? defaultDepartmentId, long? defaultTeamId, long? workflowId, long? slaPolicyId, int? slaPolicyVersion, bool evidenceRequired, int evidenceMinimumCount, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct, long? subCategoryId = null, long? workflowVersionId = null, long? approvalRuleVersionId = null);
    Task<CatalogueRequestType> PublishRequestTypeAsync(long actor, long id, CancellationToken ct);
    Task<CatalogueRequestType> SetRequestTypeActiveAsync(long actor, long id, bool active, CancellationToken ct);

    Task<CatalogueField> CreateFieldAsync(long actor, string code, string name, string dataType, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct);
    Task<CatalogueFieldOption> AddFieldOptionAsync(long actor, long fieldId, string code, string name, int displayOrder, CancellationToken ct);
    Task<CatalogueRequestField> AssociateFieldAsync(long actor, long requestTypeId, long fieldId, bool required, int displayOrder, CancellationToken ct);
    Task<IReadOnlyCollection<CatalogueRequestField>> ListRequestFieldsAsync(long actor, long requestTypeId, CancellationToken ct);
}
