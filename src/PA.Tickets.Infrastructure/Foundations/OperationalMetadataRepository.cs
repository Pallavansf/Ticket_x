using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class TicketStatusConfigurationRepository(
    TicketsDbContext db,
    IAuthorizationService authorization,
    TimeProvider clock,
    OperationalMetadataAuditWriter auditWriter,
    IDatabaseProviderCapabilities capabilities) : ITicketStatusConfigurationRepository
{
    public async Task<IReadOnlyCollection<StatusDefinitionView>> ListStatusesAsync(long actor, string? search, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        var query = db.StatusDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));

        return await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
            .Select(x => new StatusDefinitionView(x.Id, x.Code, x.Name, x.Description, x.Active, x.IsInitial, x.IsOpen, x.IsClosed, x.IsCancelled, x.DisplayOrder, x.RowVersion))
            .ToListAsync(ct);
    }

    public async Task<StatusDefinitionView> GetStatusAsync(long actor, long statusId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        var row = await db.StatusDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == statusId, ct)
            ?? throw new KeyNotFoundException("Status definition was not found.");
        return Map(row);
    }

    public async Task<bool> IsStatusActiveAsync(long actor, string code, CancellationToken ct)
    {
        var normalizedCode = NormalizeCode(code, "Status code");
        return await db.StatusDefinitions.AsNoTracking().AnyAsync(x => x.Code == normalizedCode && x.Active, ct);
    }

    public async Task<StatusDefinitionView> CreateStatusAsync(long actor, string code, string name, string? description, bool isInitial, bool isOpen, bool isClosed, bool isCancelled, int displayOrder, string? reason, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        var normalizedCode = NormalizeCode(code, "Status code");
        var normalizedName = RequireText(name, "Status name");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.StatusDefinitions.AnyAsync(x => x.Code == normalizedCode, ct))
            throw new InvalidOperationException("A status definition with the same code already exists.");

        var row = new StatusDefinitionRow
        {
            Id = await NextIdAsync("PA_TICKETS_STATUS_SEQ", ct),
            Code = normalizedCode,
            Name = normalizedName,
            Description = description,
            IsInitial = isInitial,
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsCancelled = isCancelled,
            DisplayOrder = displayOrder,
            Active = true,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            RowVersion = 1
        };

        db.StatusDefinitions.Add(row);
        await AddAuditAsync(row.Id, "STATUS_DEFINITION", "STATUS_CREATED", actor, null, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<StatusDefinitionView> UpdateStatusAsync(long actor, long statusId, string code, string name, string? description, bool isInitial, bool isOpen, bool isClosed, bool isCancelled, int displayOrder, string? reason, CancellationToken ct, int? expectedRowVersion = null)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.StatusDefinitions.SingleOrDefaultAsync(x => x.Id == statusId, ct)
            ?? throw new KeyNotFoundException("Status definition was not found.");
        if (expectedRowVersion.HasValue && row.RowVersion != expectedRowVersion.Value)
            throw new InvalidOperationException("Status definition changed since it was read; reload before updating.");
        var normalizedCode = NormalizeCode(code, "Status code");
        var normalizedName = RequireText(name, "Status name");

        if (!string.Equals(row.Code, normalizedCode, StringComparison.Ordinal))
            throw new InvalidOperationException("A status code is immutable because it defines historical ticket meaning.");
        if (await db.StatusDefinitions.AnyAsync(x => x.Id != statusId && x.Code == normalizedCode, ct))
            throw new InvalidOperationException("A status definition with the same code already exists.");

        var oldValue = Snapshot(row);
        row.Code = normalizedCode;
        row.Name = normalizedName;
        row.Description = description;
        row.IsInitial = isInitial;
        row.IsOpen = isOpen;
        row.IsClosed = isClosed;
        row.IsCancelled = isCancelled;
        row.DisplayOrder = displayOrder;
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = actor;
        row.RowVersion++;

        await AddAuditAsync(row.Id, "STATUS_DEFINITION", "STATUS_UPDATED", actor, oldValue, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<StatusDefinitionView> SetStatusActiveAsync(long actor, long statusId, bool active, string? reason, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.StatusDefinitions.SingleOrDefaultAsync(x => x.Id == statusId, ct)
            ?? throw new KeyNotFoundException("Status definition was not found.");
        if (!active && row.IsInitial)
            throw new InvalidOperationException("The initial ticket status cannot be deactivated.");
        var oldValue = Snapshot(row);
        row.Active = active;
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = actor;
        row.RowVersion++;

        await AddAuditAsync(row.Id, "STATUS_DEFINITION", active ? "STATUS_ACTIVATED" : "STATUS_DEACTIVATED", actor, oldValue, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<IReadOnlyCollection<StatusTransitionDefinition>> ListTransitionsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        return await db.StatusTransitions.AsNoTracking()
            .OrderBy(x => x.FromCode).ThenBy(x => x.ToCode)
            .Select(x => new StatusTransitionDefinition(x.FromCode, x.ToCode, x.Condition, x.Allowed, x.RowVersion))
            .ToListAsync(ct);
    }

    public async Task<StatusTransitionDefinition> SetTransitionAsync(long actor, string fromCode, string toCode, string? condition, bool allowed, string? reason, CancellationToken ct, int? expectedRowVersion = null)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        var from = NormalizeCode(fromCode, "From status code");
        var to = NormalizeCode(toCode, "To status code");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var statuses = await db.StatusDefinitions.Where(x => x.Code == from || x.Code == to).ToListAsync(ct);
        if (statuses.Count != 2 || statuses.Any(x => !x.Active))
            throw new InvalidOperationException("Both transition statuses must exist and be active.");

        var row = await db.StatusTransitions.SingleOrDefaultAsync(x => x.FromCode == from && x.ToCode == to, ct);
        if (expectedRowVersion.HasValue && row?.RowVersion != expectedRowVersion.Value)
            throw new InvalidOperationException("Status transition changed since it was read; reload before updating.");
        var oldValue = row is null ? null : TransitionSnapshot(row);
        var action = row is null ? "STATUS_TRANSITION_CREATED" : "STATUS_TRANSITION_UPDATED";
        if (row is null)
        {
            row = new StatusTransitionRow
            {
                Id = await NextIdAsync("PA_TICKETS_STATUS_TRANSITION_SEQ", ct),
                FromCode = from,
                ToCode = to,
                CreatedAt = clock.GetUtcNow(),
                CreatedBy = actor,
                RowVersion = 1
            };
            db.StatusTransitions.Add(row);
        }

        row.Condition = condition;
        row.Allowed = allowed;
        if (oldValue is not null) row.RowVersion++;
        await auditWriter.AppendAsync("STATUS_TRANSITION", $"{from}:{to}", action, actor, oldValue, TransitionSnapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(row.FromCode, row.ToCode, row.Condition, row.Allowed, row.RowVersion);
    }

    public async Task<bool> CanTransitionAsync(long actor, string fromCode, string toCode, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_UPDATE", null, ct);
        var from = NormalizeCode(fromCode, "From status code");
        var to = NormalizeCode(toCode, "To status code");
        var statuses = await db.StatusDefinitions.AsNoTracking()
            .Where(x => x.Code == from || x.Code == to)
            .Select(x => new { x.Code, x.Active })
            .ToListAsync(ct);
        if (statuses.Count != 2 || !statuses.Any(x => x.Code == to && x.Active))
            return false;

        var rows = await db.StatusTransitions.AsNoTracking()
            .Where(x => x.FromCode == from && x.ToCode == to)
            .ToListAsync(ct);
        return rows.Count == 1 && rows[0].Allowed && string.IsNullOrWhiteSpace(rows[0].Condition);
    }

    public async Task<IReadOnlyCollection<StatusConfigurationAudit>> GetStatusAuditAsync(long actor, long statusId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        return await db.Audits.AsNoTracking()
            .Where(x => x.EntityType == "STATUS_DEFINITION" && x.EntityId == statusId.ToString())
            .OrderByDescending(x => x.At)
            .Select(x => new StatusConfigurationAudit(x.Action, x.ActorId, x.At, x.OldValue, x.NewValue, x.Reason))
            .ToListAsync(ct);
    }

    private static StatusDefinitionView Map(StatusDefinitionRow row) =>
        new(row.Id, row.Code, row.Name, row.Description, row.Active, row.IsInitial, row.IsOpen, row.IsClosed, row.IsCancelled, row.DisplayOrder, row.RowVersion);

    private static string Snapshot(StatusDefinitionRow row) =>
        $"{{\"id\":{row.Id},\"code\":\"{row.Code}\",\"name\":\"{row.Name}\",\"active\":{row.Active.ToString().ToLowerInvariant()},\"isInitial\":{row.IsInitial.ToString().ToLowerInvariant()},\"isOpen\":{row.IsOpen.ToString().ToLowerInvariant()},\"isClosed\":{row.IsClosed.ToString().ToLowerInvariant()},\"isCancelled\":{row.IsCancelled.ToString().ToLowerInvariant()},\"displayOrder\":{row.DisplayOrder}}}";

    private static string NormalizeCode(string value, string fieldName)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{fieldName} is required.");
        return normalized;
    }

    private static string RequireText(string value, string fieldName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{fieldName} is required.");
        return normalized;
    }

    private static string TransitionSnapshot(StatusTransitionRow row) => $"{row.FromCode}->{row.ToCode}:allowed={row.Allowed}:condition={row.Condition}:rowVersion={row.RowVersion}";

    private async Task<long> NextIdAsync(string sequence, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = capabilities.Kind == DatabaseProviderKind.PostgreSql
                ? $"select nextval('\"{sequence}\"'::regclass)"
                : $"select {sequence}.NEXTVAL from DUAL";
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private Task AddAuditAsync(long entityId, string entityType, string action, long actorId, string? oldValue, string? newValue, string? reason, CancellationToken ct) =>
        auditWriter.AppendAsync(entityType, entityId.ToString(), action, actorId, oldValue, newValue, reason, ct);
}

public sealed class TicketPriorityConfigurationRepository(
    TicketsDbContext db,
    IAuthorizationService authorization,
    TimeProvider clock,
    OperationalMetadataAuditWriter auditWriter,
    IDatabaseProviderCapabilities capabilities) : ITicketPriorityConfigurationRepository
{
    public async Task<IReadOnlyCollection<PriorityDefinitionView>> ListPrioritiesAsync(long actor, string? search, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        var query = db.PriorityDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));

        return await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
            .Select(x => new PriorityDefinitionView(x.Id, x.Code, x.Name, x.Description, x.Level, x.DisplayOrder, x.Active, x.IsDefault, x.RowVersion))
            .ToListAsync(ct);
    }

    public async Task<PriorityDefinitionView> GetPriorityAsync(long actor, long priorityId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        var row = await db.PriorityDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == priorityId, ct)
            ?? throw new KeyNotFoundException("Priority definition was not found.");
        return Map(row);
    }

    public async Task<PriorityDefinitionView> CreatePriorityAsync(long actor, string code, string name, string? description, int level, int displayOrder, bool isDefault, string? reason, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        var normalizedCode = NormalizeCode(code, "Priority code");
        var normalizedName = RequireText(name, "Priority name");
        if (level <= 0 || displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(level), "Priority level must be positive and display order cannot be negative.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.PriorityDefinitions.AnyAsync(x => x.Code == normalizedCode, ct))
            throw new InvalidOperationException("A priority definition with the same code already exists.");

        var row = new PriorityDefinitionRow
        {
            Id = await NextPriorityIdAsync(ct),
            Code = normalizedCode,
            Name = normalizedName,
            Description = description,
            Level = level,
            DisplayOrder = displayOrder,
            Active = true,
            IsDefault = isDefault,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            RowVersion = 1
        };

        if (isDefault)
        {
            await ClearDefaultPriorityAsync(ct, row.Id, actor, reason);
            await db.SaveChangesAsync(ct);
        }

        db.PriorityDefinitions.Add(row);
        await AddAuditAsync(row.Id, "PRIORITY_DEFINITION", "PRIORITY_CREATED", actor, null, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<PriorityDefinitionView> UpdatePriorityAsync(long actor, long priorityId, string code, string name, string? description, int level, int displayOrder, bool isDefault, string? reason, CancellationToken ct, int? expectedRowVersion = null)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        var row = await db.PriorityDefinitions.SingleOrDefaultAsync(x => x.Id == priorityId, ct)
            ?? throw new KeyNotFoundException("Priority definition was not found.");
        if (expectedRowVersion.HasValue && row.RowVersion != expectedRowVersion.Value)
            throw new InvalidOperationException("Priority definition changed since it was read; reload before updating.");
        var normalizedCode = NormalizeCode(code, "Priority code");
        var normalizedName = RequireText(name, "Priority name");
        if (!string.Equals(row.Code, normalizedCode, StringComparison.Ordinal))
            throw new InvalidOperationException("A priority code is immutable because it defines historical priority meaning.");
        if (row.Level != level &&
            (await db.Tickets.AnyAsync(x => x.PriorityId == priorityId, ct) ||
             await db.WorkItems.AnyAsync(x => x.PriorityId == priorityId, ct) ||
             await db.CatalogueRequestTypes.AnyAsync(x => x.DefaultPriorityId == priorityId, ct)))
            throw new InvalidOperationException("A priority level cannot change while tickets or request types reference it.");
        if (isDefault && !row.Active)
            throw new InvalidOperationException("An inactive priority cannot be set as the default.");

        if (await db.PriorityDefinitions.AnyAsync(x => x.Id != priorityId && x.Code == normalizedCode, ct))
            throw new InvalidOperationException("A priority definition with the same code already exists.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var oldValue = Snapshot(row);
        row.Code = normalizedCode;
        row.Name = normalizedName;
        row.Description = description;
        row.Level = level;
        row.DisplayOrder = displayOrder;
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = actor;
        row.RowVersion++;

        if (isDefault)
        {
            await ClearDefaultPriorityAsync(ct, row.Id, actor, reason);
            await db.SaveChangesAsync(ct);
        }
        row.IsDefault = isDefault;

        await AddAuditAsync(row.Id, "PRIORITY_DEFINITION", "PRIORITY_UPDATED", actor, oldValue, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<PriorityDefinitionView> SetPriorityActiveAsync(long actor, long priorityId, bool active, string? reason, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.PriorityDefinitions.SingleOrDefaultAsync(x => x.Id == priorityId, ct)
            ?? throw new KeyNotFoundException("Priority definition was not found.");
        if (!active && row.IsDefault)
            throw new InvalidOperationException("Set another active default priority before deactivating the current default.");
        var oldValue = Snapshot(row);
        row.Active = active;
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = actor;
        row.RowVersion++;

        await AddAuditAsync(row.Id, "PRIORITY_DEFINITION", active ? "PRIORITY_ACTIVATED" : "PRIORITY_DEACTIVATED", actor, oldValue, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<PriorityDefinitionView> SetDefaultPriorityAsync(long actor, long priorityId, string? reason, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_ADMIN", null, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.PriorityDefinitions.SingleOrDefaultAsync(x => x.Id == priorityId, ct)
            ?? throw new KeyNotFoundException("Priority definition was not found.");
        if (!row.Active)
            throw new InvalidOperationException("An inactive priority cannot be set as the default.");

        var oldValue = Snapshot(row);
        await ClearDefaultPriorityAsync(ct, priorityId, actor, reason);
        await db.SaveChangesAsync(ct);
        row.IsDefault = true;
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = actor;
        row.RowVersion++;

        await AddAuditAsync(row.Id, "PRIORITY_DEFINITION", "PRIORITY_SET_DEFAULT", actor, oldValue, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<PriorityDefinitionView> ResolvePriorityForSelectionAsync(long actor, long? priorityId, CancellationToken ct)
    {
        var row = priorityId.HasValue
            ? await db.PriorityDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == priorityId.Value, ct)
            : await db.PriorityDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.IsDefault && x.Active, ct);
        if (row is null || !row.Active)
            throw new InvalidOperationException(priorityId.HasValue
                ? "The selected priority is not active."
                : "No active default priority is configured.");
        return Map(row);
    }

    public async Task<IReadOnlyCollection<PriorityConfigurationAudit>> GetPriorityAuditAsync(long actor, long priorityId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "TICKET_READ", null, ct);
        return await db.Audits.AsNoTracking()
            .Where(x => x.EntityType == "PRIORITY_DEFINITION" && x.EntityId == priorityId.ToString())
            .OrderByDescending(x => x.At)
            .Select(x => new PriorityConfigurationAudit(x.Action, x.ActorId, x.At, x.OldValue, x.NewValue, x.Reason))
            .ToListAsync(ct);
    }

    private static PriorityDefinitionView Map(PriorityDefinitionRow row) =>
        new(row.Id, row.Code, row.Name, row.Description, row.Level, row.DisplayOrder, row.Active, row.IsDefault, row.RowVersion);

    private async Task ClearDefaultPriorityAsync(CancellationToken ct, long keepPriorityId, long actor, string? reason)
    {
        var existingDefault = await db.PriorityDefinitions.Where(x => x.IsDefault && x.Id != keepPriorityId).ToListAsync(ct);
        foreach (var current in existingDefault)
        {
            var oldValue = Snapshot(current);
            current.IsDefault = false;
            current.UpdatedAt = clock.GetUtcNow();
            current.UpdatedBy = actor;
            current.RowVersion++;
            await AddAuditAsync(current.Id, "PRIORITY_DEFINITION", "PRIORITY_DEFAULT_REMOVED", actor, oldValue, Snapshot(current), reason, ct);
        }
    }

    private static string Snapshot(PriorityDefinitionRow row) =>
        $"{{\"id\":{row.Id},\"code\":\"{row.Code}\",\"name\":\"{row.Name}\",\"level\":{row.Level},\"active\":{row.Active.ToString().ToLowerInvariant()},\"isDefault\":{row.IsDefault.ToString().ToLowerInvariant()},\"displayOrder\":{row.DisplayOrder}}}";

    private static string NormalizeCode(string value, string fieldName)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{fieldName} is required.");
        return normalized;
    }

    private static string RequireText(string value, string fieldName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{fieldName} is required.");
        return normalized;
    }

    private async Task<long> NextPriorityIdAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = capabilities.Kind == DatabaseProviderKind.PostgreSql
                ? "select nextval('\"PA_TICKETS_PRIORITY_SEQ\"'::regclass)"
                : "select PA_TICKETS_PRIORITY_SEQ.NEXTVAL from DUAL";
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private Task AddAuditAsync(long entityId, string entityType, string action, long actorId, string? oldValue, string? newValue, string? reason, CancellationToken ct) =>
        auditWriter.AppendAsync(entityType, entityId.ToString(), action, actorId, oldValue, newValue, reason, ct);
}
