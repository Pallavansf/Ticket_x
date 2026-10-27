using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class FeatureSwitchRepository(
    TicketsDbContext db,
    IAuthorizationService authorization,
    OperationalMetadataAuditWriter auditWriter,
    TimeProvider clock) : IFeatureSwitchService
{
    public async Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct)
    {
        try { return await ListCoreAsync(actor, departmentId, ct); }
        catch (DbException ex) { throw new FeatureSwitchUnavailableException(ex); }
    }

    private async Task<IReadOnlyCollection<FeatureSwitchView>> ListCoreAsync(long actor, long? departmentId, CancellationToken ct)
    {
        await EnsureConfigurationPermissionAsync(actor, departmentId, ct);
        if (departmentId is <= 0) throw new ArgumentOutOfRangeException(nameof(departmentId), "Department id must be positive.");
        if (departmentId.HasValue && !await db.AdministrationDepartments.AnyAsync(x => x.Id == departmentId.Value, ct))
            throw new KeyNotFoundException("Department was not found.");
        var rows = await db.FeatureSwitches.AsNoTracking()
            .Where(x => x.DepartmentId == null || x.DepartmentId == departmentId)
            .ToListAsync(ct);
        var global = rows.Where(x => x.DepartmentId is null).ToDictionary(x => x.Code, StringComparer.Ordinal);
        var scoped = rows.Where(x => x.DepartmentId == departmentId && departmentId.HasValue)
            .ToDictionary(x => x.Code, StringComparer.Ordinal);
        return FeatureSwitchCodes.All.Select(item =>
        {
            global.TryGetValue(item.Key, out var globalRow);
            scoped.TryGetValue(item.Key, out var scopedRow);
            var enabled = FeatureSwitchResolution.IsEnabled(scopedRow?.Enabled, globalRow?.Enabled);
            var inherited = departmentId.HasValue && scopedRow is null && globalRow is not null;
            return new FeatureSwitchView(item.Key, item.Value, departmentId, enabled,
                scopedRow is not null || globalRow is not null, inherited,
                departmentId.HasValue ? scopedRow?.RowVersion ?? 0 : globalRow?.RowVersion ?? 0);
        }).ToArray();
    }

    public async Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool enabled,
        int? expectedRowVersion, string? reason, CancellationToken ct)
    {
        try { return await SetCoreAsync(actor, code, departmentId, enabled, expectedRowVersion, reason, ct); }
        catch (DbException ex) { throw new FeatureSwitchUnavailableException(ex); }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        { throw new FeatureSwitchUnavailableException(ex); }
    }

    private async Task<FeatureSwitchView> SetCoreAsync(long actor, string code, long? departmentId, bool enabled,
        int? expectedRowVersion, string? reason, CancellationToken ct)
    {
        code = NormalizeCode(code);
        if (!FeatureSwitchCodes.All.TryGetValue(code, out var name))
            throw new KeyNotFoundException("Feature switch was not found.");
        if (departmentId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(departmentId), "Department id must be positive.");
        if (reason?.Length > 1000)
            throw new ArgumentOutOfRangeException(nameof(reason), "Reason cannot exceed 1000 characters.");
        await EnsureConfigurationPermissionAsync(actor, departmentId, ct);
        if (departmentId.HasValue && !await db.AdministrationDepartments.AnyAsync(x => x.Id == departmentId.Value, ct))
            throw new KeyNotFoundException("Department was not found.");

        var scopeKey = ScopeKey(departmentId);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.FeatureSwitches.SingleOrDefaultAsync(x => x.Code == code && x.ScopeKey == scopeKey, ct);
        if (row is not null && expectedRowVersion.HasValue && expectedRowVersion.Value != row.RowVersion)
            throw new InvalidOperationException("Feature switch changed since it was read; reload before updating.");
        if (row is null && expectedRowVersion.GetValueOrDefault() != 0)
            throw new InvalidOperationException("Feature switch changed since it was read; reload before updating.");

        var oldValue = row is null ? null : Snapshot(row);
        var now = clock.GetUtcNow();
        if (row is null)
        {
            row = new FeatureSwitchRow
            {
                Code = code, ScopeKey = scopeKey, DepartmentId = departmentId, Enabled = enabled,
                RowVersion = 1, UpdatedAt = now, UpdatedBy = actor
            };
            db.FeatureSwitches.Add(row);
        }
        else
        {
            row.Enabled = enabled;
            row.RowVersion++;
            row.UpdatedAt = now;
            row.UpdatedBy = actor;
        }

        await auditWriter.AppendAsync("FEATURE_SWITCH", $"{code}:{scopeKey}", "FEATURE_SWITCH_UPDATED", actor,
            oldValue, Snapshot(row), reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new FeatureSwitchView(code, name, departmentId, enabled, true, false, row.RowVersion);
    }

    public async Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct)
    {
        try { return await IsEnabledCoreAsync(code, departmentId, ct); }
        catch (DbException ex) { throw new FeatureSwitchUnavailableException(ex); }
    }

    private async Task<bool> IsEnabledCoreAsync(string code, long? departmentId, CancellationToken ct)
    {
        if (!FeatureSwitchCodes.All.ContainsKey(code)) return false;
        if (departmentId is <= 0) return false;
        var global = await db.FeatureSwitches.AsNoTracking()
            .Where(x => x.Code == code && x.DepartmentId == null)
            .Select(x => (bool?)x.Enabled).SingleOrDefaultAsync(ct);
        var scoped = departmentId.HasValue
            ? await db.FeatureSwitches.AsNoTracking()
                .Where(x => x.Code == code && x.DepartmentId == departmentId)
                .Select(x => (bool?)x.Enabled).SingleOrDefaultAsync(ct)
            : null;
        return FeatureSwitchResolution.IsEnabled(scoped, global);
    }

    public async Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct)
    {
        try { return await ForActorCoreAsync(actor, ct); }
        catch (DbException ex) { throw new FeatureSwitchUnavailableException(ex); }
    }

    private async Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorCoreAsync(long actor, CancellationToken ct)
    {
        var actorRow = await db.AdministrationUsers.AsNoTracking()
            .Where(x => x.Id == actor)
            .Select(x => new { x.DepartmentId, x.Active })
            .SingleOrDefaultAsync(ct);
        if (actorRow is null || !actorRow.Active)
            return FeatureSwitchCodes.All.Keys.Select(code => new FeatureAvailabilityView(code, false)).ToArray();
        var departmentId = actorRow.DepartmentId;
        var rows = await db.FeatureSwitches.AsNoTracking()
            .Where(x => x.DepartmentId == null || x.DepartmentId == departmentId)
            .ToListAsync(ct);
        var global = rows.Where(x => x.DepartmentId is null).ToDictionary(x => x.Code, StringComparer.Ordinal);
        var scoped = rows.Where(x => x.DepartmentId == departmentId && departmentId.HasValue)
            .ToDictionary(x => x.Code, StringComparer.Ordinal);
        return FeatureSwitchCodes.All.Keys.Select(code =>
        {
            global.TryGetValue(code, out var globalRow);
            scoped.TryGetValue(code, out var scopedRow);
            return new FeatureAvailabilityView(code,
                FeatureSwitchResolution.IsEnabled(scopedRow?.Enabled, globalRow?.Enabled));
        }).ToArray();
    }

    private async Task EnsureConfigurationPermissionAsync(long actor, long? departmentId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CONFIG_ADMIN",
            departmentId.HasValue ? new AuthorizationResource(DepartmentId: departmentId) : null, ct);
    }

    private static string NormalizeCode(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();
    private static string ScopeKey(long? departmentId) => departmentId.HasValue ? $"DEPARTMENT/{departmentId.Value}" : "GLOBAL";
    private static string Snapshot(FeatureSwitchRow row) => JsonSerializer.Serialize(new
    {
        row.Code, row.ScopeKey, row.DepartmentId, row.Enabled, row.RowVersion
    });
}
