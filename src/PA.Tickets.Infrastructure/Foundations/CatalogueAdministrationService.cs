using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Approvals;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class CatalogueAdministrationService(
    TicketsDbContext db,
    IAuthorizationService authorization,
    AuditChainCoordinator auditLocks,
    ProviderTransactionRetry retry,
    TimeProvider clock,
    IWorkflowVersionSelector workflowVersions,
    IApprovalRuleVersionResolver approvalVersions) : ICatalogueAdministrationService
{
    public async Task<IReadOnlyCollection<CatalogueService>> ListServicesAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SERVICE_READ", null, ct);
        return (await db.CatalogueServices.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<CatalogueService> GetServiceAsync(long actor, long id, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SERVICE_READ", null, ct);
        return Map(await db.CatalogueServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Service was not found."));
    }

    public Task<CatalogueService> CreateServiceAsync(long actor, string code, string name, string? description, long? ownerDepartmentId, long? ownerTeamId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct) => retry.ExecuteAsync(db, () => CreateServiceOnceAsync(actor, code, name, description, ownerDepartmentId, ownerTeamId, effectiveFrom, effectiveTo, ct), ct);

    private async Task<CatalogueService> CreateServiceOnceAsync(long actor, string code, string name, string? description, long? ownerDepartmentId, long? ownerTeamId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SERVICE_ADMIN", null, ct);
        ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name);
        await ValidateOwnerScopeAsync(ownerDepartmentId, ownerTeamId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var row = new CatalogueServiceRow { Code = Required(code), Name = Required(name), Description = description, OwnerDepartmentId = ownerDepartmentId, OwnerTeamId = ownerTeamId, Active = true, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo };
        db.CatalogueServices.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync(row.Id, "SERVICE_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }

    public async Task<CatalogueService> UpdateServiceAsync(long actor, long id, string code, string name, string? description, long? ownerDepartmentId, long? ownerTeamId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SERVICE_ADMIN", null, ct);
        ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name); await ValidateOwnerScopeAsync(ownerDepartmentId, ownerTeamId, ct);
        var row = await db.CatalogueServices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Service was not found.");
        row.Code = Required(code); row.Name = Required(name); row.Description = description; row.OwnerDepartmentId = ownerDepartmentId; row.OwnerTeamId = ownerTeamId; row.EffectiveFrom = effectiveFrom; row.EffectiveTo = effectiveTo;
        await AddAuditAsync(row.Id, "SERVICE_UPDATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public Task<CatalogueService> SetServiceActiveAsync(long actor, long id, bool active, CancellationToken ct) => SetServiceActiveInternalAsync(actor, id, active, ct);
    private async Task<CatalogueService> SetServiceActiveInternalAsync(long actor, long id, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SERVICE_ADMIN", null, ct);
        var row = await db.CatalogueServices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Service was not found.");
        row.Active = active; await AddAuditAsync(row.Id, active ? "SERVICE_ACTIVATED" : "SERVICE_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<IReadOnlyCollection<CatalogueCategory>> ListCategoriesAsync(long actor, long? serviceId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_READ", null, ct);
        var query = db.CatalogueCategories.AsNoTracking().AsQueryable(); if (serviceId.HasValue) query = query.Where(x => x.ServiceId == serviceId);
        return (await query.OrderBy(x => x.Code).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<CatalogueCategory> GetCategoryAsync(long actor, long id, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_READ", null, ct);
        return Map(await db.CatalogueCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Category was not found."));
    }

    public Task<CatalogueCategory> CreateCategoryAsync(long actor, long serviceId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct) => retry.ExecuteAsync(db, () => CreateCategoryOnceAsync(actor, serviceId, code, name, description, effectiveFrom, effectiveTo, ct), ct);

    private async Task<CatalogueCategory> CreateCategoryOnceAsync(long actor, long serviceId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_ADMIN", null, ct); ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name);
        var service = await db.CatalogueServices.SingleOrDefaultAsync(x => x.Id == serviceId, ct) ?? throw new KeyNotFoundException("Service was not found."); ValidateChildDates(service.Active, service.EffectiveFrom, service.EffectiveTo, effectiveFrom, effectiveTo);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var row = new CatalogueCategoryRow { ServiceId = serviceId, Code = Required(code), Name = Required(name), Description = description, Active = true, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo };
        db.CatalogueCategories.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync(row.Id, "CATEGORY_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }

    public async Task<CatalogueCategory> UpdateCategoryAsync(long actor, long id, long serviceId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_ADMIN", null, ct); ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name);
        var service = await db.CatalogueServices.SingleOrDefaultAsync(x => x.Id == serviceId, ct) ?? throw new KeyNotFoundException("Service was not found."); ValidateChildDates(service.Active, service.EffectiveFrom, service.EffectiveTo, effectiveFrom, effectiveTo);
        var row = await db.CatalogueCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Category was not found.");
        if (row.ServiceId != serviceId && (await db.CatalogueRequestTypes.AnyAsync(x => x.CategoryId == id, ct) || await db.CatalogueSubCategories.AnyAsync(x => x.CategoryId == id, ct))) throw new InvalidOperationException("A category with catalogue children cannot be moved to another service.");
        row.ServiceId = serviceId; row.Code = Required(code); row.Name = Required(name); row.Description = description; row.EffectiveFrom = effectiveFrom; row.EffectiveTo = effectiveTo;
        await AddAuditAsync(row.Id, "CATEGORY_UPDATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public Task<CatalogueCategory> SetCategoryActiveAsync(long actor, long id, bool active, CancellationToken ct) => SetCategoryActiveInternalAsync(actor, id, active, ct);
    private async Task<CatalogueCategory> SetCategoryActiveInternalAsync(long actor, long id, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_ADMIN", null, ct); var row = await db.CatalogueCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Category was not found.");
        if (active) { var service = await db.CatalogueServices.AsNoTracking().SingleAsync(x => x.Id == row.ServiceId, ct); ValidateChildDates(service.Active, service.EffectiveFrom, service.EffectiveTo, row.EffectiveFrom, row.EffectiveTo); }
        row.Active = active; await AddAuditAsync(row.Id, active ? "CATEGORY_ACTIVATED" : "CATEGORY_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<IReadOnlyCollection<CatalogueSubCategory>> ListSubCategoriesAsync(long actor, long? serviceId, long? categoryId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_READ", null, ct);
        var query = db.CatalogueSubCategories.AsNoTracking().AsQueryable();
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (serviceId.HasValue) query = query.Where(x => db.CatalogueCategories.Any(category => category.Id == x.CategoryId && category.ServiceId == serviceId.Value));
        return (await query.OrderBy(x => x.Code).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<CatalogueSubCategory> GetSubCategoryAsync(long actor, long id, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_READ", null, ct);
        return Map(await db.CatalogueSubCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Sub-category was not found."));
    }

    public Task<CatalogueSubCategory> CreateSubCategoryAsync(long actor, long serviceId, long categoryId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
        => retry.ExecuteAsync(db, () => CreateSubCategoryOnceAsync(actor, serviceId, categoryId, code, name, description, effectiveFrom, effectiveTo, ct), ct);

    private async Task<CatalogueSubCategory> CreateSubCategoryOnceAsync(long actor, long serviceId, long categoryId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_ADMIN", null, ct); ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name);
        await ValidateSubCategoryHierarchyAsync(serviceId, categoryId, effectiveFrom, effectiveTo, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var row = new CatalogueSubCategoryRow { CategoryId = categoryId, Code = Required(code), Name = Required(name), Description = description, Active = true, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo };
        db.CatalogueSubCategories.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync("SUB_CATEGORY", row.Id.ToString(), "SUB_CATEGORY_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }

    public async Task<CatalogueSubCategory> UpdateSubCategoryAsync(long actor, long id, long serviceId, long categoryId, string code, string name, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_ADMIN", null, ct); ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name);
        var row = await db.CatalogueSubCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Sub-category was not found.");
        if (row.CategoryId != categoryId) throw new InvalidOperationException("A sub-category cannot be moved to another category.");
        await ValidateSubCategoryHierarchyAsync(serviceId, categoryId, effectiveFrom, effectiveTo, ct);
        var publishedTypes = await db.CatalogueRequestTypes.AsNoTracking().Where(x => x.SubCategoryId == id && x.Status == "PUBLISHED").ToListAsync(ct);
        if (publishedTypes.Any(x => IsAfterAtDatabasePrecision(effectiveFrom, x.EffectiveFrom) || effectiveTo is not null && (x.EffectiveTo is null || IsAfterAtDatabasePrecision(x.EffectiveTo.Value, effectiveTo.Value)))) throw new InvalidOperationException("Sub-category dates cannot exclude a published Request Type version.");
        row.Code = Required(code); row.Name = Required(name); row.Description = description; row.EffectiveFrom = effectiveFrom; row.EffectiveTo = effectiveTo;
        await AddAuditAsync("SUB_CATEGORY", row.Id.ToString(), "SUB_CATEGORY_UPDATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<CatalogueSubCategory> SetSubCategoryActiveAsync(long actor, long id, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATEGORY_ADMIN", null, ct);
        var row = await db.CatalogueSubCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Sub-category was not found.");
        if (active)
        {
            var category = await db.CatalogueCategories.AsNoTracking().SingleAsync(x => x.Id == row.CategoryId, ct);
            await ValidateSubCategoryHierarchyAsync(category.ServiceId, row.CategoryId, row.EffectiveFrom, row.EffectiveTo, ct);
        }
        row.Active = active; await AddAuditAsync("SUB_CATEGORY", row.Id.ToString(), active ? "SUB_CATEGORY_ACTIVATED" : "SUB_CATEGORY_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<IReadOnlyCollection<CatalogueRequestType>> ListRequestTypesAsync(long actor, long? categoryId, CancellationToken ct, long? serviceId = null, long? subCategoryId = null)
    {
        if (subCategoryId.HasValue && (!serviceId.HasValue || !categoryId.HasValue)) throw new ArgumentException("Service and Category are required when filtering by SubCategory.");
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_READ", null, ct); var query = db.CatalogueRequestTypes.AsNoTracking().AsQueryable();
        if (serviceId.HasValue) query = query.Where(x => x.ServiceId == serviceId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (subCategoryId.HasValue) query = query.Where(x => x.SubCategoryId == subCategoryId.Value);
        else if (categoryId.HasValue) query = query.Where(x => x.SubCategoryId == null);
        return (await query.OrderBy(x => x.Code).ThenByDescending(x => x.VersionNo).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<CatalogueRequestType> GetRequestTypeAsync(long actor, long id, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_READ", null, ct); return Map(await db.CatalogueRequestTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Request type was not found."));
    }

    public Task<CatalogueRequestType> CreateRequestTypeAsync(long actor, long serviceId, long categoryId, int ticketType, string code, string name, string? description, long? defaultPriorityId, long? defaultDepartmentId, long? defaultTeamId, long? workflowId, long? slaPolicyId, int? slaPolicyVersion, bool evidenceRequired, int evidenceMinimumCount, int versionNo, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct, long? subCategoryId = null, long? workflowVersionId = null, long? approvalRuleVersionId = null) => retry.ExecuteAsync(db, () => CreateRequestTypeOnceAsync(actor, serviceId, categoryId, ticketType, code, name, description, defaultPriorityId, defaultDepartmentId, defaultTeamId, workflowId, slaPolicyId, slaPolicyVersion, evidenceRequired, evidenceMinimumCount, versionNo, effectiveFrom, effectiveTo, ct, subCategoryId, workflowVersionId, approvalRuleVersionId), ct);

    private async Task<CatalogueRequestType> CreateRequestTypeOnceAsync(long actor, long serviceId, long categoryId, int ticketType, string code, string name, string? description, long? defaultPriorityId, long? defaultDepartmentId, long? defaultTeamId, long? workflowId, long? slaPolicyId, int? slaPolicyVersion, bool evidenceRequired, int evidenceMinimumCount, int versionNo, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct, long? subCategoryId, long? workflowVersionId, long? approvalRuleVersionId)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_ADMIN", null, ct); ValidateRequestType(ticketType, code, name, evidenceMinimumCount, versionNo, effectiveFrom, effectiveTo);
        await ValidateHierarchyAsync(serviceId, categoryId, subCategoryId, effectiveFrom, effectiveTo, ct); await ValidateConfigurationAssociationsAsync(workflowId, workflowVersionId, approvalRuleVersionId, effectiveFrom, effectiveTo, ct); await ValidateOwnerScopeAsync(defaultDepartmentId, defaultTeamId, ct);
        if (workflowVersionId.HasValue && workflowId is null) throw new ArgumentException("WorkflowId is required when a WorkflowVersion is selected.");
        var row = new CatalogueRequestTypeRow { ServiceId = serviceId, CategoryId = categoryId, SubCategoryId = subCategoryId, TicketType = ticketType, Code = Required(code), Name = Required(name), Description = description, DefaultPriorityId = defaultPriorityId, DefaultDepartmentId = defaultDepartmentId, DefaultTeamId = defaultTeamId, WorkflowId = workflowId, WorkflowVersionId = workflowVersionId, ApprovalRuleVersionId = approvalRuleVersionId, SlaPolicyId = slaPolicyId, SlaPolicyVersion = slaPolicyVersion, EvidenceRequired = evidenceRequired, EvidenceMinimumCount = evidenceMinimumCount, VersionNo = versionNo, Status = "DRAFT", Active = false, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo };
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct); db.CatalogueRequestTypes.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync(row.Id, "REQUEST_TYPE_CREATED", actor, RequestTypePath(row), ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(row);
    }

    public async Task<CatalogueRequestType> UpdateRequestTypeAsync(long actor, long id, long serviceId, long categoryId, int ticketType, string code, string name, string? description, long? defaultPriorityId, long? defaultDepartmentId, long? defaultTeamId, long? workflowId, long? slaPolicyId, int? slaPolicyVersion, bool evidenceRequired, int evidenceMinimumCount, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct, long? subCategoryId = null, long? workflowVersionId = null, long? approvalRuleVersionId = null)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_ADMIN", null, ct); ValidateRequestType(ticketType, code, name, evidenceMinimumCount, 1, effectiveFrom, effectiveTo);
        var row = await db.CatalogueRequestTypes.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Request type was not found."); if (row.Status != "DRAFT") throw new InvalidOperationException("Published request types are immutable; create a new version.");
        await ValidateHierarchyAsync(serviceId, categoryId, subCategoryId, effectiveFrom, effectiveTo, ct); await ValidateConfigurationAssociationsAsync(workflowId, workflowVersionId, approvalRuleVersionId, effectiveFrom, effectiveTo, ct); await ValidateOwnerScopeAsync(defaultDepartmentId, defaultTeamId, ct);
        if (workflowVersionId.HasValue && workflowId is null) throw new ArgumentException("WorkflowId is required when a WorkflowVersion is selected.");
        row.ServiceId = serviceId; row.CategoryId = categoryId; row.SubCategoryId = subCategoryId; row.TicketType = ticketType; row.Code = Required(code); row.Name = Required(name); row.Description = description; row.DefaultPriorityId = defaultPriorityId; row.DefaultDepartmentId = defaultDepartmentId; row.DefaultTeamId = defaultTeamId; row.WorkflowId = workflowId; row.WorkflowVersionId = workflowVersionId; row.ApprovalRuleVersionId = approvalRuleVersionId; row.SlaPolicyId = slaPolicyId; row.SlaPolicyVersion = slaPolicyVersion; row.EvidenceRequired = evidenceRequired; row.EvidenceMinimumCount = evidenceMinimumCount; row.EffectiveFrom = effectiveFrom; row.EffectiveTo = effectiveTo;
        await AddAuditAsync(row.Id, "REQUEST_TYPE_UPDATED", actor, $"{row.Code}:{row.ServiceId}/{row.CategoryId}/{row.SubCategoryId?.ToString() ?? "DIRECT"}", ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<CatalogueRequestType> PublishRequestTypeAsync(long actor, long id, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CATALOGUE_PUBLISH", null, ct); var row = await db.CatalogueRequestTypes.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Request type was not found."); if (row.Status != "DRAFT") throw new InvalidOperationException("Only draft request types can be published.");
        await ValidateHierarchyAsync(row.ServiceId, row.CategoryId, row.SubCategoryId, row.EffectiveFrom, row.EffectiveTo, ct);
        await ValidateConfigurationAssociationsAsync(row.WorkflowId, row.WorkflowVersionId, row.ApprovalRuleVersionId, row.EffectiveFrom, row.EffectiveTo, ct);
        var overlap = await db.CatalogueRequestTypes.AnyAsync(x => x.Id != id && x.ServiceId == row.ServiceId && x.Code == row.Code && x.Status == "PUBLISHED" && (x.EffectiveTo == null || row.EffectiveFrom < x.EffectiveTo) && (row.EffectiveTo == null || x.EffectiveFrom < row.EffectiveTo), ct); if (overlap) throw new InvalidOperationException("An overlapping published request type version already exists.");
        row.Status = "PUBLISHED"; row.Active = true; row.PublishedAt = clock.GetUtcNow(); await AddAuditAsync(row.Id, "REQUEST_TYPE_PUBLISHED", actor, RequestTypePath(row), ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public Task<CatalogueRequestType> SetRequestTypeActiveAsync(long actor, long id, bool active, CancellationToken ct) => SetRequestTypeActiveInternalAsync(actor, id, active, ct);
    private async Task<CatalogueRequestType> SetRequestTypeActiveInternalAsync(long actor, long id, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_ADMIN", null, ct); var row = await db.CatalogueRequestTypes.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Request type was not found."); if (active && row.Status != "PUBLISHED") throw new InvalidOperationException("Only published request types can be activated."); row.Active = active; if (!active && row.Status == "PUBLISHED") row.Status = "RETIRED"; await AddAuditAsync(row.Id, active ? "REQUEST_TYPE_ACTIVATED" : "REQUEST_TYPE_RETIRED", actor, row.Code, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<CatalogueField> CreateFieldAsync(long actor, string code, string name, string dataType, string? description, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_ADMIN", null, ct); ValidateDates(effectiveFrom, effectiveTo); ValidateRequired(code, name); var allowed = new[] { "STRING", "NUMBER", "DATE", "DATETIME", "BOOLEAN", "TEXT", "SELECT", "MULTISELECT", "USER", "DEPARTMENT", "TEAM", "CLIENT" }; if (!allowed.Contains(dataType.ToUpperInvariant())) throw new ArgumentException("Unsupported field data type.");
        var row = new CatalogueFieldRow { Code = Required(code), Name = Required(name), DataType = dataType.ToUpperInvariant(), Description = description, Active = true, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo }; db.CatalogueFields.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync(row.Id, "FIELD_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<CatalogueFieldOption> AddFieldOptionAsync(long actor, long fieldId, string code, string name, int displayOrder, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_ADMIN", null, ct); if (displayOrder < 0) throw new ArgumentException("Display order cannot be negative."); var field = await db.CatalogueFields.SingleOrDefaultAsync(x => x.Id == fieldId && x.Active, ct) ?? throw new KeyNotFoundException("Active field was not found."); if (field.DataType is not ("SELECT" or "MULTISELECT")) throw new InvalidOperationException("Options are supported only for select fields.");
        var row = new CatalogueFieldOptionRow { FieldId = fieldId, Code = Required(code), Name = Required(name), DisplayOrder = displayOrder, Active = true }; db.CatalogueFieldOptions.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync(row.Id, "FIELD_OPTION_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<CatalogueRequestField> AssociateFieldAsync(long actor, long requestTypeId, long fieldId, bool required, int displayOrder, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_ADMIN", null, ct); if (displayOrder < 0) throw new ArgumentException("Display order cannot be negative."); if (!await db.CatalogueRequestTypes.AnyAsync(x => x.Id == requestTypeId && x.Status == "DRAFT", ct)) throw new InvalidOperationException("Fields can only be changed on a draft request type."); if (!await db.CatalogueFields.AnyAsync(x => x.Id == fieldId && x.Active, ct)) throw new KeyNotFoundException("Active field was not found.");
        var row = new CatalogueRequestFieldRow { RequestTypeId = requestTypeId, FieldId = fieldId, Required = required, DisplayOrder = displayOrder, Active = true }; db.CatalogueRequestFields.Add(row); await db.SaveChangesAsync(ct); await AddAuditAsync(row.Id, "REQUEST_FIELD_ASSOCIATED", actor, $"{requestTypeId}:{fieldId}", ct); await db.SaveChangesAsync(ct); return Map(row);
    }

    public async Task<IReadOnlyCollection<CatalogueRequestField>> ListRequestFieldsAsync(long actor, long requestTypeId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_READ", null, ct); return (await db.CatalogueRequestFields.AsNoTracking().Where(x => x.RequestTypeId == requestTypeId).OrderBy(x => x.DisplayOrder).ToListAsync(ct)).Select(Map).ToArray();
    }

    private async Task ValidateHierarchyAsync(long serviceId, long categoryId, long? subCategoryId, DateTimeOffset from, DateTimeOffset? to, CancellationToken ct)
    {
        var service = await db.CatalogueServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId, ct) ?? throw new KeyNotFoundException("Service was not found."); ValidateChildDates(service.Active, service.EffectiveFrom, service.EffectiveTo, from, to);
        var category = await db.CatalogueCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == categoryId && x.ServiceId == serviceId, ct) ?? throw new KeyNotFoundException("Category was not found under the selected service."); ValidateChildDates(category.Active, category.EffectiveFrom, category.EffectiveTo, from, to);
        if (subCategoryId.HasValue)
        {
            var subCategory = await db.CatalogueSubCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subCategoryId.Value && x.CategoryId == categoryId, ct) ?? throw new InvalidOperationException("The sub-category does not belong to the selected category and service.");
            ValidateChildDates(subCategory.Active, subCategory.EffectiveFrom, subCategory.EffectiveTo, from, to);
        }
    }

    private async Task ValidateSubCategoryHierarchyAsync(long serviceId, long categoryId, DateTimeOffset from, DateTimeOffset? to, CancellationToken ct)
    {
        var service = await db.CatalogueServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId, ct) ?? throw new KeyNotFoundException("Service was not found.");
        ValidateChildDates(service.Active, service.EffectiveFrom, service.EffectiveTo, from, to);
        var category = await db.CatalogueCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == categoryId && x.ServiceId == serviceId, ct) ?? throw new InvalidOperationException("The category does not belong to the selected service.");
        ValidateChildDates(category.Active, category.EffectiveFrom, category.EffectiveTo, from, to);
    }

    private async Task ValidateConfigurationAssociationsAsync(long? workflowId,long? workflowVersionId,long? approvalRuleVersionId,DateTimeOffset from,DateTimeOffset? to,CancellationToken ct)
    {
        if(workflowVersionId.HasValue)
        {
            if(!workflowId.HasValue)throw new ArgumentException("WorkflowId is required when a WorkflowVersion is selected.");
            await workflowVersions.ValidateRangeAsync(workflowId.Value,workflowVersionId.Value,from,to,ct);
        }
        if(approvalRuleVersionId.HasValue)await approvalVersions.ValidateRangeAsync(approvalRuleVersionId.Value,from,to,ct);
    }

    private async Task ValidateOwnerScopeAsync(long? departmentId, long? teamId, CancellationToken ct)
    { if (teamId.HasValue && (!departmentId.HasValue || !await db.AdministrationTeams.AnyAsync(x => x.Id == teamId && x.DepartmentId == departmentId && x.Active, ct))) throw new ArgumentException("The team does not belong to the selected active department."); }

    private Task AddAuditAsync(long id, string action, long actor, string? value, CancellationToken ct) => AddAuditAsync("CATALOGUE", id.ToString(), action, actor, value, ct);
    private async Task AddAuditAsync(string entityType, string entityId, string action, long actor, string? value, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, entityType, entityId, ct);
        var now = clock.GetUtcNow();
        var previous = await db.Audits.Where(x => x.EntityType == entityType && x.EntityId == entityId).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var canonical = System.Text.Json.JsonSerializer.Serialize(new { entityType, entityId, action, actorUserId = actor, auditAtUtc = now.UtcDateTime, newValue = value, previousHash = previous });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = entityType, EntityId = entityId, Action = action, ActorId = actor, At = now, NewValue = value, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }
    private static void ValidateRequestType(int ticketType, string code, string name, int evidenceMinimumCount, int versionNo, DateTimeOffset from, DateTimeOffset? to) { ValidateDates(from, to); ValidateRequired(code, name); if (ticketType is < 1 or > 5) throw new ArgumentException("Ticket type is invalid."); if (evidenceMinimumCount < 0) throw new ArgumentException("Evidence minimum count cannot be negative."); if (versionNo <= 0) throw new ArgumentException("Version must be positive."); }
    private static void ValidateDates(DateTimeOffset from, DateTimeOffset? to) { if (to is not null && to <= from) throw new ArgumentException("Effective end must be after effective start."); }
    private static void ValidateChildDates(bool parentActive, DateTimeOffset parentFrom, DateTimeOffset? parentTo, DateTimeOffset from, DateTimeOffset? to) { if (!parentActive) throw new InvalidOperationException("An inactive parent cannot expose an active child."); if (from < parentFrom || (parentTo is not null && (to is null || to > parentTo))) throw new InvalidOperationException("Child effective dates must be inside the parent effective dates."); }
    private static bool DatesOverlap(DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo) => (aTo is null || bFrom < aTo) && (bTo is null || aFrom < bTo);
    private static bool IsAfterAtDatabasePrecision(DateTimeOffset value, DateTimeOffset boundary) => value > boundary && value - boundary > TimeSpan.FromTicks(10);
    private static void ValidateRequired(string code, string name) { Required(code); Required(name); }
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A required value is missing.") : value.Trim();
    private static CatalogueService Map(CatalogueServiceRow x) => new(x.Id, x.Code, x.Name, x.Description, x.OwnerDepartmentId, x.OwnerTeamId, x.Active, x.EffectiveFrom, x.EffectiveTo);
    private static CatalogueCategory Map(CatalogueCategoryRow x) => new(x.Id, x.ServiceId, x.Code, x.Name, x.Description, x.Active, x.EffectiveFrom, x.EffectiveTo);
    private static CatalogueSubCategory Map(CatalogueSubCategoryRow x) => new(x.Id, x.CategoryId, x.Code, x.Name, x.Description, x.Active, x.EffectiveFrom, x.EffectiveTo);
    private static CatalogueRequestType Map(CatalogueRequestTypeRow x) => new(x.Id, x.ServiceId, x.CategoryId, x.TicketType, x.Code, x.Name, x.Description, x.DefaultPriorityId, x.DefaultDepartmentId, x.DefaultTeamId, x.WorkflowId, x.SlaPolicyId, x.SlaPolicyVersion, x.EvidenceRequired, x.EvidenceMinimumCount, x.VersionNo, x.Status, x.Active, x.EffectiveFrom, x.EffectiveTo, x.SubCategoryId, x.WorkflowVersionId, x.ApprovalRuleVersionId);
    private static string RequestTypePath(CatalogueRequestTypeRow x) => $"{x.Code}:SERVICE={x.ServiceId}:CATEGORY={x.CategoryId}:SUB_CATEGORY={x.SubCategoryId?.ToString() ?? "DIRECT"}:WORKFLOW_VERSION={x.WorkflowVersionId?.ToString() ?? "EFFECTIVE"}:APPROVAL_VERSION={x.ApprovalRuleVersionId?.ToString() ?? "NONE"}:VERSION={x.VersionNo}";
    private static CatalogueField Map(CatalogueFieldRow x) => new(x.Id, x.Code, x.Name, x.DataType, x.Description, x.Active, x.EffectiveFrom, x.EffectiveTo);
    private static CatalogueFieldOption Map(CatalogueFieldOptionRow x) => new(x.Id, x.FieldId, x.Code, x.Name, x.DisplayOrder, x.Active);
    private static CatalogueRequestField Map(CatalogueRequestFieldRow x) => new(x.Id, x.RequestTypeId, x.FieldId, x.Required, x.DisplayOrder, x.Active);
}
