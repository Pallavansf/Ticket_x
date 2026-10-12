using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class TicketCatalogueRuntime(TicketsDbContext db, IAuthorizationService authorization) : ITicketCatalogueRuntime
{
    public async Task<CatalogueTicketConfiguration?> ResolveForCreationAsync(long actor, long? requestTypeId, DateTimeOffset at, IReadOnlyCollection<TicketFieldValue> values, CancellationToken ct)
    {
        if (!requestTypeId.HasValue)
        {
            if (values.Count != 0) throw new ArgumentException("Request-specific field values require a Request Type.");
            return null;
        }
        await authorization.EnsurePermissionAsync(actor, "REQUEST_TYPE_READ", null, ct);
        var requestType = await db.CatalogueRequestTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestTypeId.Value && x.Active && x.Status == "PUBLISHED" && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), ct)
            ?? throw new KeyNotFoundException("The request type is not active, published, or effective.");
        var service = await db.CatalogueServices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestType.ServiceId && x.Active && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), ct)
            ?? throw new InvalidOperationException("The request type service is not active or effective.");
        var category = await db.CatalogueCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestType.CategoryId && x.ServiceId == service.Id && x.Active && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), ct)
            ?? throw new InvalidOperationException("The request type category is not active, effective, or under its service.");
        if (requestType.SubCategoryId.HasValue)
        {
            var subCategory = await db.CatalogueSubCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestType.SubCategoryId.Value && x.CategoryId == category.Id && x.Active && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), ct)
                ?? throw new InvalidOperationException("The request type sub-category is not active, effective, or under its category.");
        }
        var fields = await db.CatalogueRequestFields.AsNoTracking().Where(x => x.RequestTypeId == requestType.Id && x.Active).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        await ValidateFieldsAsync(fields, values, at, ct);
        return new(requestType.Id, requestType.VersionNo, requestType.TicketType, service.Id, category.Id, requestType.DefaultPriorityId, requestType.DefaultDepartmentId, requestType.DefaultTeamId, requestType.WorkflowId, requestType.SlaPolicyId, requestType.SlaPolicyVersion, requestType.EvidenceRequired, requestType.EvidenceMinimumCount, fields.Select(x => new CatalogueRequestField(x.Id, x.RequestTypeId, x.FieldId, x.Required, x.DisplayOrder, x.Active)).ToArray(), requestType.SubCategoryId, requestType.WorkflowVersionId, requestType.ApprovalRuleVersionId);
    }

    private async Task ValidateFieldsAsync(IReadOnlyCollection<CatalogueRequestFieldRow> associations, IReadOnlyCollection<TicketFieldValue> values, DateTimeOffset at, CancellationToken ct)
    {
        var configured = associations.Select(x => x.FieldId).ToHashSet();
        if (values.GroupBy(x => x.FieldId).Any(x => x.Key <= 0 || x.Count() != 1)) throw new ArgumentException("Request field values must contain each field at most once.");
        if (values.Any(x => !configured.Contains(x.FieldId))) throw new ArgumentException("A supplied field is not configured for this request type.");
        var fields = await db.CatalogueFields.AsNoTracking().Where(x => configured.Contains(x.Id)).ToListAsync(ct);
        if (fields.Count != configured.Count || fields.Any(x => !x.Active || x.EffectiveFrom > at || (x.EffectiveTo != null && x.EffectiveTo <= at))) throw new InvalidOperationException("A configured request field is inactive or ineffective.");
        var supplied = values.Select(x => x.FieldId).ToHashSet();
        if (associations.Any(x => x.Required && !supplied.Contains(x.FieldId))) throw new ArgumentException("All required request fields must be supplied.");
        foreach (var value in values)
        {
            var field = fields.Single(x => x.Id == value.FieldId);
            ValidateValueShape(field, value);
            if (field.DataType is "SELECT" or "MULTISELECT")
            {
                var option = value.StringValue;
                if (string.IsNullOrWhiteSpace(option)) throw new ArgumentException($"A value is required for field '{field.Code}'.");
                var valid = await db.CatalogueFieldOptions.AsNoTracking().AnyAsync(x => x.FieldId == field.Id && x.Code == option && x.Active, ct);
                if (!valid) throw new ArgumentException($"The option '{option}' is not valid for field '{field.Code}'.");
            }
        }

        foreach (var association in associations.Where(x => x.Required))
        {
            var value = values.SingleOrDefault(x => x.FieldId == association.FieldId);
            if (value is not null &&
                ((value.StringValue is not null && string.IsNullOrWhiteSpace(value.StringValue)) ||
                 (value.TextValue is not null && string.IsNullOrWhiteSpace(value.TextValue))))
                throw new ArgumentException($"A value is required for field '{fields.Single(x => x.Id == association.FieldId).Code}'.");
        }
    }

    private static void ValidateValueShape(CatalogueFieldRow field, TicketFieldValue value)
    {
        var populatedValues = (value.StringValue is null ? 0 : 1) +
            (value.NumberValue.HasValue ? 1 : 0) +
            (value.DateValue.HasValue ? 1 : 0) +
            (value.TextValue is null ? 0 : 1);
        if (populatedValues != 1)
            throw new ArgumentException($"Exactly one value matching field '{field.Code}' must be supplied.");

        var dataType = field.DataType.ToUpperInvariant();
        var matchesType = dataType switch
        {
            "STRING" or "SELECT" or "MULTISELECT" => value.StringValue is not null,
            "NUMBER" => value.NumberValue.HasValue,
            "DATE" or "DATETIME" => value.DateValue.HasValue,
            "TEXT" => value.TextValue is not null,
            _ => true
        };
        if (!matchesType)
            throw new ArgumentException($"The supplied value does not match field '{field.Code}' type '{field.DataType}'.");
    }
}
