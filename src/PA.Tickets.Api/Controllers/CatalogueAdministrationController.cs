using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/admin/catalogue")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class CatalogueAdministrationController(ICatalogueAdministrationService catalogue) : ControllerBase
{
    [HttpGet("services")] public Task<IReadOnlyCollection<CatalogueService>> Services(CancellationToken ct) => catalogue.ListServicesAsync(Actor(), ct);
    [HttpGet("services/{id:long}")] public Task<CatalogueService> Service(long id, CancellationToken ct) => catalogue.GetServiceAsync(Actor(), id, ct);
    [HttpPost("services")] public Task<CatalogueService> CreateService(ServiceRequest request, CancellationToken ct) => catalogue.CreateServiceAsync(Actor(), request.Code, request.Name, request.Description, request.OwnerDepartmentId, request.OwnerTeamId, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPut("services/{id:long}")] public Task<CatalogueService> UpdateService(long id, ServiceRequest request, CancellationToken ct) => catalogue.UpdateServiceAsync(Actor(), id, request.Code, request.Name, request.Description, request.OwnerDepartmentId, request.OwnerTeamId, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPost("services/{id:long}/active")] public Task<CatalogueService> SetServiceActive(long id, ActiveRequest request, CancellationToken ct) => catalogue.SetServiceActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("categories")] public Task<IReadOnlyCollection<CatalogueCategory>> Categories([FromQuery] long? serviceId, CancellationToken ct) => catalogue.ListCategoriesAsync(Actor(), serviceId, ct);
    [HttpGet("categories/{id:long}")] public Task<CatalogueCategory> Category(long id, CancellationToken ct) => catalogue.GetCategoryAsync(Actor(), id, ct);
    [HttpPost("categories")] public Task<CatalogueCategory> CreateCategory(CategoryRequest request, CancellationToken ct) => catalogue.CreateCategoryAsync(Actor(), request.ServiceId, request.Code, request.Name, request.Description, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPut("categories/{id:long}")] public Task<CatalogueCategory> UpdateCategory(long id, CategoryRequest request, CancellationToken ct) => catalogue.UpdateCategoryAsync(Actor(), id, request.ServiceId, request.Code, request.Name, request.Description, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPost("categories/{id:long}/active")] public Task<CatalogueCategory> SetCategoryActive(long id, ActiveRequest request, CancellationToken ct) => catalogue.SetCategoryActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("sub-categories")] public Task<IReadOnlyCollection<CatalogueSubCategory>> SubCategories([FromQuery] long? serviceId, [FromQuery] long? categoryId, CancellationToken ct) => catalogue.ListSubCategoriesAsync(Actor(), serviceId, categoryId, ct);
    [HttpGet("sub-categories/{id:long}")] public Task<CatalogueSubCategory> SubCategory(long id, CancellationToken ct) => catalogue.GetSubCategoryAsync(Actor(), id, ct);
    [HttpPost("sub-categories")] public Task<CatalogueSubCategory> CreateSubCategory(SubCategoryRequest request, CancellationToken ct) => catalogue.CreateSubCategoryAsync(Actor(), request.ServiceId, request.CategoryId, request.Code, request.Name, request.Description, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPut("sub-categories/{id:long}")] public Task<CatalogueSubCategory> UpdateSubCategory(long id, SubCategoryRequest request, CancellationToken ct) => catalogue.UpdateSubCategoryAsync(Actor(), id, request.ServiceId, request.CategoryId, request.Code, request.Name, request.Description, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPost("sub-categories/{id:long}/active")] public Task<CatalogueSubCategory> SetSubCategoryActive(long id, ActiveRequest request, CancellationToken ct) => catalogue.SetSubCategoryActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("request-types")] public Task<IReadOnlyCollection<CatalogueRequestType>> RequestTypes([FromQuery] long? serviceId, [FromQuery] long? categoryId, [FromQuery] long? subCategoryId, CancellationToken ct) => catalogue.ListRequestTypesAsync(Actor(), categoryId, ct, serviceId, subCategoryId);
    [HttpGet("request-types/{id:long}")] public Task<CatalogueRequestType> RequestType(long id, CancellationToken ct) => catalogue.GetRequestTypeAsync(Actor(), id, ct);
    [HttpPost("request-types")] public Task<CatalogueRequestType> CreateRequestType(RequestTypeRequest request, CancellationToken ct) => catalogue.CreateRequestTypeAsync(Actor(), request.ServiceId, request.CategoryId, request.TicketType, request.Code, request.Name, request.Description, request.DefaultPriorityId, request.DefaultDepartmentId, request.DefaultTeamId, request.WorkflowId, request.SlaPolicyId, request.SlaPolicyVersion, request.EvidenceRequired, request.EvidenceMinimumCount, request.VersionNo, request.EffectiveFrom, request.EffectiveTo, ct, request.SubCategoryId, request.WorkflowVersionId, request.ApprovalRuleVersionId);
    [HttpPut("request-types/{id:long}")] public Task<CatalogueRequestType> UpdateRequestType(long id, RequestTypeUpdateRequest request, CancellationToken ct) => catalogue.UpdateRequestTypeAsync(Actor(), id, request.ServiceId, request.CategoryId, request.TicketType, request.Code, request.Name, request.Description, request.DefaultPriorityId, request.DefaultDepartmentId, request.DefaultTeamId, request.WorkflowId, request.SlaPolicyId, request.SlaPolicyVersion, request.EvidenceRequired, request.EvidenceMinimumCount, request.EffectiveFrom, request.EffectiveTo, ct, request.SubCategoryId, request.WorkflowVersionId, request.ApprovalRuleVersionId);
    [HttpPost("request-types/{id:long}/publish")] public Task<CatalogueRequestType> PublishRequestType(long id, CancellationToken ct) => catalogue.PublishRequestTypeAsync(Actor(), id, ct);
    [HttpPost("request-types/{id:long}/active")] public Task<CatalogueRequestType> SetRequestTypeActive(long id, ActiveRequest request, CancellationToken ct) => catalogue.SetRequestTypeActiveAsync(Actor(), id, request.Active, ct);

    [HttpPost("fields")] public Task<CatalogueField> CreateField(FieldRequest request, CancellationToken ct) => catalogue.CreateFieldAsync(Actor(), request.Code, request.Name, request.DataType, request.Description, request.EffectiveFrom, request.EffectiveTo, ct);
    [HttpPost("fields/{id:long}/options")] public Task<CatalogueFieldOption> AddFieldOption(long id, FieldOptionRequest request, CancellationToken ct) => catalogue.AddFieldOptionAsync(Actor(), id, request.Code, request.Name, request.DisplayOrder, ct);
    [HttpGet("request-types/{id:long}/fields")] public Task<IReadOnlyCollection<CatalogueRequestField>> RequestFields(long id, CancellationToken ct) => catalogue.ListRequestFieldsAsync(Actor(), id, ct);
    [HttpPost("request-types/{id:long}/fields")] public Task<CatalogueRequestField> AssociateField(long id, RequestFieldRequest request, CancellationToken ct) => catalogue.AssociateFieldAsync(Actor(), id, request.FieldId, request.Required, request.DisplayOrder, ct);

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record ServiceRequest(string Code, string Name, string? Description, long? OwnerDepartmentId, long? OwnerTeamId, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record CategoryRequest(long ServiceId, string Code, string Name, string? Description, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record SubCategoryRequest(long ServiceId, long CategoryId, string Code, string Name, string? Description, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record RequestTypeRequest(long ServiceId, long CategoryId, int TicketType, string Code, string Name, string? Description, long? DefaultPriorityId, long? DefaultDepartmentId, long? DefaultTeamId, long? WorkflowId, long? SlaPolicyId, int? SlaPolicyVersion, bool EvidenceRequired, int EvidenceMinimumCount, int VersionNo, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, long? SubCategoryId = null, long? WorkflowVersionId = null, long? ApprovalRuleVersionId = null);
public sealed record RequestTypeUpdateRequest(long ServiceId, long CategoryId, int TicketType, string Code, string Name, string? Description, long? DefaultPriorityId, long? DefaultDepartmentId, long? DefaultTeamId, long? WorkflowId, long? SlaPolicyId, int? SlaPolicyVersion, bool EvidenceRequired, int EvidenceMinimumCount, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, long? SubCategoryId = null, long? WorkflowVersionId = null, long? ApprovalRuleVersionId = null);
public sealed record FieldRequest(string Code, string Name, string DataType, string? Description, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record FieldOptionRequest(string Code, string Name, int DisplayOrder);
public sealed record RequestFieldRequest(long FieldId, bool Required, int DisplayOrder);
