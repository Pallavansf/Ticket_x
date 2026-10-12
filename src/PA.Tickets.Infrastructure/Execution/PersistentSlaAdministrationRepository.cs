using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Sla;
using PA.Tickets.Domain.Sla;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class PersistentSlaAdministrationRepository(
    ExecutionDbContext db,
    TicketsDbContext tickets,
    AuditChainCoordinator auditLocks,
    ProviderTransactionRetry retry,
    IAuthorizationService authorization) : ISlaAdministrationRepository
{
    public async Task<IReadOnlyCollection<SlaPolicyDefinitionView>> ListPoliciesAsync(long actor, string? search, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        var query = db.SlaPolicyDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));
        return await query.OrderBy(x => x.Code)
            .Select(x => new SlaPolicyDefinitionView(x.Id, x.Code, x.Name, x.Description, x.ActiveFlag != 0, x.RowVersion))
            .ToListAsync(ct);
    }

    public async Task<SlaPolicyDefinitionView> GetPolicyAsync(long actor, long policyId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        var row = await db.SlaPolicyDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == policyId, ct)
            ?? throw new KeyNotFoundException("SLA policy was not found.");
        return Map(row);
    }

    public async Task<long> CreatePolicyAsync(long actor, string code, string name, string? description, string idempotencyKey, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_ADMIN", null, ct);
        RequireKey(idempotencyKey);
        var normalizedCode = Required(code, "SLA policy code");
        var normalizedName = Required(name, "SLA policy name");
        var fingerprint = Fingerprint(new { normalizedCode, normalizedName, description });
        return await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await FindIdempotencyAsync("SLA_POLICY_CREATE", actor, idempotencyKey, ct);
            if (prior is not null)
            {
                EnsureFingerprint(prior, fingerprint);
                var priorRow = await db.SlaPolicyDefinitions.AsNoTracking().SingleAsync(x => x.Code == normalizedCode, ct);
                return priorRow.Id;
            }
            if (await db.SlaPolicyDefinitions.AnyAsync(x => x.Code == normalizedCode, ct))
                throw new InvalidOperationException("SLA policy code already exists.");
            var definitionId = await db.SlaPolicyDefinitions.Select(x => x.Id).DefaultIfEmpty().MaxAsync(ct);
            var usedPolicyId = await db.SlaPolicies.Select(x => x.PolicyId).DefaultIfEmpty().MaxAsync(ct);
            var pinnedPolicyId = await tickets.CatalogueRequestTypes.Where(x => x.SlaPolicyId.HasValue).Select(x => x.SlaPolicyId!.Value).DefaultIfEmpty().MaxAsync(ct);
            var id = Math.Max(Math.Max(definitionId, usedPolicyId), pinnedPolicyId) + 1;
            var row = new SlaPolicyDefinitionRow
            {
                Id = id, Code = normalizedCode, Name = normalizedName, Description = description,
                Active = true, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actor
            };
            db.SlaPolicyDefinitions.Add(row);
            await AddAuditAsync("SLA_POLICY", id.ToString(), "SLA_POLICY_CREATED", actor, null, Identity(row), null, correlationId, ct);
            AddIdempotency("SLA_POLICY_CREATE", actor, idempotencyKey, fingerprint);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return id;
        }, ct);
    }

    public async Task<SlaPolicyDefinitionView> UpdatePolicyAsync(long actor, long policyId, int expectedRowVersion, string code, string name, string? description, string? reason, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_ADMIN", null, ct);
        var normalizedCode = Required(code, "SLA policy code");
        var normalizedName = Required(name, "SLA policy name");
        return await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await db.SlaPolicyDefinitions.SingleOrDefaultAsync(x => x.Id == policyId, ct)
                ?? throw new KeyNotFoundException("SLA policy was not found.");
            if (row.RowVersion != expectedRowVersion) throw new InvalidOperationException("SLA policy changed since it was read; reload before updating.");
            if (!row.Active) throw new InvalidOperationException("An inactive SLA policy cannot be edited.");
            if (await db.SlaPolicyDefinitions.AnyAsync(x => x.Id != policyId && x.Code == normalizedCode, ct))
                throw new InvalidOperationException("SLA policy code already exists.");
            var oldValue = Identity(row);
            row.Code = normalizedCode;
            row.Name = normalizedName;
            row.Description = description;
            row.RowVersion++;
            await AddAuditAsync("SLA_POLICY", policyId.ToString(), "SLA_POLICY_UPDATED", actor, oldValue, Identity(row), reason, correlationId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(row);
        }, ct);
    }

    public async Task<IReadOnlyCollection<SlaPolicyVersionSummary>> ListVersionsAsync(long actor, long policyId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        if (!await db.SlaPolicyDefinitions.AnyAsync(x => x.Id == policyId, ct))
            throw new KeyNotFoundException("SLA policy was not found.");
        return await db.SlaPolicies.AsNoTracking().Where(x => x.PolicyId == policyId)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new SlaPolicyVersionSummary(x.Id, x.PolicyId, x.VersionNo, x.Status, x.EffectiveFrom, x.EffectiveTo))
            .ToListAsync(ct);
    }

    public async Task<SlaPolicyVersionView> GetVersionAsync(long actor, long versionId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        return await LoadVersionAsync(versionId, ct);
    }

    public async Task<SlaPolicyVersionComparison> CompareVersionsAsync(long actor, long firstVersionId, long secondVersionId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        var first = await db.SlaPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == firstVersionId, ct)
            ?? throw new KeyNotFoundException("SLA policy version was not found.");
        var second = await db.SlaPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == secondVersionId, ct)
            ?? throw new KeyNotFoundException("SLA policy version was not found.");
        if (first.PolicyId != second.PolicyId) throw new InvalidOperationException("SLA policy versions must belong to the same policy.");
        var fields = typeof(SlaPolicyVersion).GetProperties()
            .Where(property => !Equals(property.GetValue(ToDomain(first)), property.GetValue(ToDomain(second))))
            .Select(property => property.Name)
            .ToArray();
        return new(fields);
    }

    public async Task<SlaValidationResult> ValidateAsync(long actor, long policyId, SlaPolicyVersion version, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_ADMIN", null, ct);
        return await ValidateCoreAsync(policyId, version, ct);
    }

    public async Task<long> SaveDraftAsync(long policyId, SlaPolicyVersion version, long actor, string idempotencyKey, int? expectedRowVersion, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_ADMIN", null, ct);
        RequireKey(idempotencyKey);
        if (version.PolicyId != policyId) throw new InvalidOperationException("SLA policy version belongs to a different policy.");
        var validation = await ValidateCoreAsync(policyId, version, ct);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
        var fingerprint = Fingerprint(version);
        return await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await FindIdempotencyAsync("SLA_POLICY_DRAFT", actor, idempotencyKey, ct);
            if (prior is not null)
            {
                EnsureFingerprint(prior, fingerprint);
                return await db.SlaPolicies.Where(x => x.PolicyId == policyId && x.VersionNo == version.VersionNo).Select(x => x.Id).SingleAsync(ct);
            }
            if (!await db.SlaPolicyDefinitions.AnyAsync(x => x.Id == policyId && x.ActiveFlag != 0, ct))
                throw new KeyNotFoundException("Active SLA policy was not found.");
            var row = await db.SlaPolicies.SingleOrDefaultAsync(x => x.PolicyId == policyId && x.VersionNo == version.VersionNo, ct);
            if (row is not null && row.Status != "DRAFT")
                throw new InvalidOperationException("Published SLA policy versions are immutable.");
            if (row is not null && (!expectedRowVersion.HasValue || expectedRowVersion.Value != row.RowVersion))
                throw new InvalidOperationException("SLA policy version changed since it was read; reload before updating the draft.");
            if (row is null && expectedRowVersion.HasValue)
                throw new InvalidOperationException("A new SLA policy draft cannot specify an expected row version.");
            var isNew = row is null;
            row ??= new SlaPolicyRow
            {
                Id = await NextIdAsync(db.SlaPolicies.Select(x => x.Id), ct),
                PolicyId = policyId,
                VersionNo = version.VersionNo,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = actor,
                Status = "DRAFT"
            };
            if (isNew) db.SlaPolicies.Add(row);
            var oldValue = isNew ? null : PolicyJson(ToDomain(row));
            Copy(version, row);
            row.Active = false;
            row.Status = "DRAFT";
            row.RowVersion++;
            await AddAuditAsync("SLA_POLICY_VERSION", row.Id.ToString(), "SLA_POLICY_DRAFT_SAVED", actor, oldValue, PolicyJson(version), null, correlationId, ct);
            AddIdempotency("SLA_POLICY_DRAFT", actor, idempotencyKey, fingerprint);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return row.Id;
        }, ct);
    }

    public async Task PublishAsync(long versionId, long actor, string idempotencyKey, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_PUBLISH", null, ct);
        RequireKey(idempotencyKey);
        await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var fingerprint = Fingerprint(new { versionId });
            var prior = await FindIdempotencyAsync("SLA_POLICY_PUBLISH", actor, idempotencyKey, ct);
            if (prior is not null) { EnsureFingerprint(prior, fingerprint); await tx.CommitAsync(ct); return true; }
            var row = await db.SlaPolicies.SingleOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new KeyNotFoundException("SLA policy version was not found.");
            if (row.Status != "DRAFT") throw new InvalidOperationException("Only draft SLA policy versions can be published.");
            var validation = await ValidateCoreAsync(row.PolicyId, ToDomain(row), ct);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
            var overlap = await db.SlaPolicies.AnyAsync(x => x.PolicyId == row.PolicyId && x.Id != row.Id && x.Status == "PUBLISHED"
                && (x.EffectiveTo == null || row.EffectiveFrom < x.EffectiveTo)
                && (row.EffectiveTo == null || x.EffectiveFrom < row.EffectiveTo), ct);
            if (overlap) throw new InvalidOperationException("Published SLA policy versions cannot have overlapping effective dates.");
            var oldValue = row.Status;
            row.Status = "PUBLISHED";
            row.Active = true;
            row.PublishedAt = DateTimeOffset.UtcNow;
            row.PublishedBy = actor;
            row.RowVersion++;
            await AddAuditAsync("SLA_POLICY_VERSION", row.Id.ToString(), "SLA_POLICY_VERSION_PUBLISHED", actor, oldValue, row.Status, null, correlationId, ct);
            AddIdempotency("SLA_POLICY_PUBLISH", actor, idempotencyKey, fingerprint);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public async Task RetireAsync(long versionId, long actor, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_RETIRE", null, ct);
        RequireKey(idempotencyKey);
        await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var fingerprint = Fingerprint(new { versionId, reason });
            var prior = await FindIdempotencyAsync("SLA_POLICY_RETIRE", actor, idempotencyKey, ct);
            if (prior is not null) { EnsureFingerprint(prior, fingerprint); await tx.CommitAsync(ct); return true; }
            var row = await db.SlaPolicies.SingleOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new KeyNotFoundException("SLA policy version was not found.");
            if (row.Status != "PUBLISHED") throw new InvalidOperationException("Only published SLA policy versions can be retired.");
            var oldValue = row.Status;
            row.Status = "RETIRED";
            row.Active = false;
            row.RetiredAt = DateTimeOffset.UtcNow;
            row.RetiredBy = actor;
            row.RowVersion++;
            await AddAuditAsync("SLA_POLICY_VERSION", row.Id.ToString(), "SLA_POLICY_VERSION_RETIRED", actor, oldValue, row.Status, reason, correlationId, ct);
            AddIdempotency("SLA_POLICY_RETIRE", actor, idempotencyKey, fingerprint);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyCollection<SlaCalendarDefinitionView>> ListCalendarsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        return await db.SlaCalendars.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new SlaCalendarDefinitionView(x.Code, x.TimeZoneId, x.WorkingHoursJson, x.HolidaysJson, x.EffectiveFrom, x.EffectiveTo, x.Active, x.RowVersion))
            .ToListAsync(ct);
    }

    public async Task<SlaCalendarDefinitionView> GetCalendarAsync(long actor, string code, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_READ", null, ct);
        var row = await db.SlaCalendars.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct)
            ?? throw new KeyNotFoundException("SLA calendar was not found.");
        return Map(row);
    }

    public async Task CreateCalendarAsync(long actor, string code, BusinessCalendar calendar, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, string reason, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_ADMIN", null, ct);
        ValidateCalendar(calendar, effectiveFrom, effectiveTo);
        var normalizedCode = Required(code, "SLA calendar code");
        await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.SlaCalendars.AnyAsync(x => x.Code == normalizedCode, ct)) throw new InvalidOperationException("SLA calendar code already exists.");
            var hours = calendar.WorkingHours.ToDictionary(x => x.Key, x => new PersistentSlaCalendarStore.TimeWindow(x.Value.Start, x.Value.End));
            var row = new SlaCalendarRow
            {
                Id = await NextIdAsync(db.SlaCalendars.Select(x => x.Id), ct),
                Code = normalizedCode,
                Active = true,
                TimeZoneId = calendar.TimeZoneId,
                WorkingHoursJson = JsonSerializer.Serialize(hours),
                HolidaysJson = JsonSerializer.Serialize(calendar.Holidays),
                EffectiveFrom = effectiveFrom,
                EffectiveTo = effectiveTo
            };
            db.SlaCalendars.Add(row);
            await AddAuditAsync("SLA_CALENDAR", normalizedCode, "SLA_CALENDAR_CREATED", actor, null, CalendarJson(row), reason, correlationId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public async Task UpdateCalendarAsync(long actor, string code, int expectedRowVersion, BusinessCalendar calendar, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, bool active, string reason, string? correlationId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "SLA_ADMIN", null, ct);
        ValidateCalendar(calendar, effectiveFrom, effectiveTo);
        await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await db.SlaCalendars.SingleOrDefaultAsync(x => x.Code == code, ct)
                ?? throw new KeyNotFoundException("SLA calendar was not found.");
            if (row.RowVersion != expectedRowVersion) throw new InvalidOperationException("SLA calendar changed since it was read; reload before updating.");
            if (await db.SlaPolicies.AnyAsync(x => x.CalendarCode == code && (x.Status == "PUBLISHED" || x.Status == "RETIRED"), ct)
                || await db.Slas.AnyAsync(x => x.CalendarCode == code, ct))
                throw new InvalidOperationException("A calendar used by a published SLA version or runtime SLA is immutable; create a new calendar code.");
            var oldValue = CalendarJson(row);
            var hours = calendar.WorkingHours.ToDictionary(x => x.Key, x => new PersistentSlaCalendarStore.TimeWindow(x.Value.Start, x.Value.End));
            row.TimeZoneId = calendar.TimeZoneId;
            row.WorkingHoursJson = JsonSerializer.Serialize(hours);
            row.HolidaysJson = JsonSerializer.Serialize(calendar.Holidays);
            row.EffectiveFrom = effectiveFrom;
            row.EffectiveTo = effectiveTo;
            row.Active = active;
            row.RowVersion++;
            await AddAuditAsync("SLA_CALENDAR", code, "SLA_CALENDAR_UPDATED", actor, oldValue, CalendarJson(row), reason, correlationId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    private async Task<SlaValidationResult> ValidateCoreAsync(long policyId, SlaPolicyVersion version, CancellationToken ct)
    {
        var errors = new List<string>();
        if (version.PolicyId != policyId) errors.Add("The policy version references a different policy.");
        var definition = await db.SlaPolicyDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == policyId, ct);
        if (definition is null) errors.Add("The SLA policy definition was not found.");
        else if (definition.ActiveFlag == 0) errors.Add("The SLA policy is inactive.");
        else if (!string.Equals(version.Code, definition.Code, StringComparison.Ordinal)) errors.Add("The version code must match its SLA policy.");
        if (version.VersionNo <= 0) errors.Add("Version number must be positive.");
        if (version.EffectiveTo.HasValue && version.EffectiveTo <= version.EffectiveFrom) errors.Add("Effective To must be later than Effective From.");
        if (version.ResponseTargetMinutes is <= 0) errors.Add("Response target must be positive when configured.");
        if (version.ResolutionTargetMinutes is <= 0) errors.Add("Resolution target must be positive when configured.");
        if (!version.ResponseTargetMinutes.HasValue && !version.ResolutionTargetMinutes.HasValue) errors.Add("At least one SLA target is required.");
        var calendar = string.IsNullOrWhiteSpace(version.CalendarCode)
            ? null
            : await db.SlaCalendars.AsNoTracking().SingleOrDefaultAsync(x => x.Code == version.CalendarCode && x.Active, ct);
        if (calendar is null) errors.Add("An active SLA calendar must be selected.");
        else if (calendar.EffectiveFrom > version.EffectiveFrom ||
            (calendar.EffectiveTo.HasValue && (!version.EffectiveTo.HasValue || calendar.EffectiveTo.Value < version.EffectiveTo.Value)))
            errors.Add("The SLA calendar effective period must cover the policy version effective period.");
        if (version.ServiceId.HasValue && !await tickets.CatalogueServices.AsNoTracking().AnyAsync(x => x.Id == version.ServiceId && x.Active, ct))
            errors.Add("The Service target is missing or inactive.");
        if (version.CategoryId.HasValue)
        {
            var category = await tickets.CatalogueCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == version.CategoryId, ct);
            if (category is null || !category.Active) errors.Add("The Category target is missing or inactive.");
            else if (version.ServiceId.HasValue && category.ServiceId != version.ServiceId) errors.Add("The Category target does not belong to the selected Service.");
        }
        if (version.SubCategoryId.HasValue)
        {
            var subCategory = await tickets.CatalogueSubCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == version.SubCategoryId, ct);
            if (subCategory is null || !subCategory.Active) errors.Add("The SubCategory target is missing or inactive.");
            else
            {
                if (version.CategoryId.HasValue && subCategory.CategoryId != version.CategoryId) errors.Add("The SubCategory target does not belong to the selected Category.");
                var parent = await tickets.CatalogueCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subCategory.CategoryId, ct);
                if (parent is null || !parent.Active) errors.Add("The SubCategory target has a missing or inactive Category.");
                else if (version.ServiceId.HasValue && parent.ServiceId != version.ServiceId) errors.Add("The SubCategory target does not belong to the selected Service.");
            }
        }
        if (version.RequestTypeId.HasValue)
        {
            var requestType = await tickets.CatalogueRequestTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == version.RequestTypeId, ct);
            if (requestType is null || !requestType.Active || requestType.Status != "PUBLISHED") errors.Add("The RequestType target is missing, inactive, or unpublished.");
            else if ((version.ServiceId.HasValue && requestType.ServiceId != version.ServiceId)
                || (version.CategoryId.HasValue && requestType.CategoryId != version.CategoryId)
                || (version.SubCategoryId.HasValue && requestType.SubCategoryId != version.SubCategoryId))
                errors.Add("The RequestType target does not match the selected catalogue hierarchy.");
        }
        if (version.DepartmentId.HasValue && !await tickets.AdministrationDepartments.AsNoTracking().AnyAsync(x => x.Id == version.DepartmentId && x.Active, ct))
            errors.Add("The Department target is missing or inactive.");
        if (version.TeamId.HasValue && !await tickets.AdministrationTeams.AsNoTracking().AnyAsync(x => x.Id == version.TeamId && x.Active
            && (!version.DepartmentId.HasValue || x.DepartmentId == version.DepartmentId), ct))
            errors.Add("The Team target is missing, inactive, or outside the selected Department.");
        var overlaps = await db.SlaPolicies.AnyAsync(x => x.PolicyId == policyId && x.VersionNo != version.VersionNo && x.Status == "PUBLISHED"
            && (x.EffectiveTo == null || version.EffectiveFrom < x.EffectiveTo)
            && (version.EffectiveTo == null || x.EffectiveFrom < version.EffectiveTo), ct);
        if (overlaps) errors.Add("The effective period overlaps another published version of this policy.");
        return new(errors.Count == 0, errors);
    }

    private async Task<SlaPolicyVersionView> LoadVersionAsync(long versionId, CancellationToken ct)
    {
        var row = await db.SlaPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, ct)
            ?? throw new KeyNotFoundException("SLA policy version was not found.");
        var audit = await db.Audits.AsNoTracking().Where(x => x.EntityType == "SLA_POLICY_VERSION" && x.EntityId == versionId.ToString())
            .OrderBy(x => x.Id)
            .Select(x => new SlaConfigurationAudit(x.Action, x.ActorId, x.At, x.OldValue, x.NewValue, x.Reason))
            .ToListAsync(ct);
        return new(row.Id, ToDomain(row), row.Status, row.RowVersion, audit);
    }

    private async Task<IdempotencyRow?> FindIdempotencyAsync(string scope, long actor, string key, CancellationToken ct)
        => await db.Idempotency.SingleOrDefaultAsync(x => x.Scope == scope && x.ActorId == actor && x.Key == key, ct);

    private void AddIdempotency(string scope, long actor, string key, string fingerprint)
        => db.Idempotency.Add(new IdempotencyRow { Scope = scope, Key = key, ActorId = actor, Fingerprint = fingerprint, Status = "COMPLETED", CreatedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow });

    private async Task AddAuditAsync(string entityType, string id, string action, long actor, string? oldValue, string? newValue, string? reason, string? correlationId, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, entityType, id, ct);
        var at = DateTimeOffset.UtcNow;
        var previous = await db.Audits.Where(x => x.EntityType == entityType && x.EntityId == id).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var auditNewValue = JsonSerializer.Serialize(new { value = newValue, correlationId });
        var canonical = JsonSerializer.Serialize(new { entityType, entityId = id, action, actorUserId = actor, auditAtUtc = at.UtcDateTime, oldValue, newValue = auditNewValue, reason, previousHash = previous });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = entityType, EntityId = id, Action = action, ActorId = actor, At = at, OldValue = oldValue, NewValue = auditNewValue, Reason = reason, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private async Task<long> NextIdAsync(IQueryable<long> values, CancellationToken ct)
        => (await values.DefaultIfEmpty().MaxAsync(ct)) + 1;

    private static void Copy(SlaPolicyVersion version, SlaPolicyRow row)
    {
        row.Code = version.Code;
        row.EffectiveFrom = version.EffectiveFrom;
        row.EffectiveTo = version.EffectiveTo;
        row.ServiceId = version.ServiceId;
        row.RequestTypeId = version.RequestTypeId;
        row.PriorityId = version.PriorityId;
        row.ClientId = version.ClientId;
        row.DepartmentId = version.DepartmentId;
        row.TeamId = version.TeamId;
        row.TicketType = version.TicketType;
        row.CategoryId = version.CategoryId;
        row.SubCategoryId = version.SubCategoryId;
        row.ResponseTargetMinutes = version.ResponseTargetMinutes;
        row.ResolutionTargetMinutes = version.ResolutionTargetMinutes;
        row.CalendarCode = version.CalendarCode;
        row.WaitingRules = version.WaitingRules;
        row.PauseRules = version.PauseRules;
        row.EscalationRules = version.EscalationRules;
        row.BreachRules = version.BreachRules;
        row.NotificationRules = version.NotificationRules;
    }

    private static SlaPolicyVersion ToDomain(SlaPolicyRow row)
        => new(row.PolicyId, row.Code, row.VersionNo, row.EffectiveFrom, row.EffectiveTo, row.ResponseTargetMinutes, row.ResolutionTargetMinutes, row.CalendarCode,
            row.WaitingRules, row.PauseRules, row.EscalationRules, row.BreachRules, row.NotificationRules, row.ServiceId, row.RequestTypeId, row.PriorityId,
            row.ClientId, row.DepartmentId, row.TeamId, row.TicketType, row.CategoryId, row.SubCategoryId);

    private static SlaPolicyDefinitionView Map(SlaPolicyDefinitionRow row) => new(row.Id, row.Code, row.Name, row.Description, row.Active, row.RowVersion);
    private static SlaCalendarDefinitionView Map(SlaCalendarRow row) => new(row.Code, row.TimeZoneId, row.WorkingHoursJson, row.HolidaysJson, row.EffectiveFrom, row.EffectiveTo, row.Active, row.RowVersion);
    private static string Identity(SlaPolicyDefinitionRow row) => $"{row.Code}:{row.Name}:{row.Description}";
    private static string PolicyJson(SlaPolicyVersion policy) => JsonSerializer.Serialize(policy);
    private static string CalendarJson(SlaCalendarRow row) => JsonSerializer.Serialize(new { row.TimeZoneId, row.WorkingHoursJson, row.HolidaysJson, row.EffectiveFrom, row.EffectiveTo, row.Active });
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.") : value.Trim();
    private static void RequireKey(string key) { if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Idempotency-Key is required."); }
    private static void EnsureFingerprint(IdempotencyRow row, string fingerprint)
    {
        if (row.Fingerprint != fingerprint) throw new InvalidOperationException("Idempotency key was reused for a different SLA configuration.");
    }

    private static void ValidateCalendar(BusinessCalendar calendar, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (effectiveTo.HasValue && effectiveTo <= effectiveFrom) throw new ArgumentException("Calendar Effective To must be later than Effective From.");
        if (string.IsNullOrWhiteSpace(calendar.TimeZoneId)) throw new ArgumentException("Calendar timezone is required.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId); }
        catch (TimeZoneNotFoundException) { throw new ArgumentException("Calendar timezone is not recognized by the host."); }
        foreach (var (day, hours) in calendar.WorkingHours)
            if (!Enum.IsDefined(day) || hours.Start < TimeSpan.Zero || hours.End > TimeSpan.FromDays(1) || hours.Start >= hours.End)
                throw new ArgumentException("Calendar working intervals must have a valid weekday and a start before the end.");
    }
}
