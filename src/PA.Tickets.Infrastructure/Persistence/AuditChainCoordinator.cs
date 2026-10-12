using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PA.Tickets.Infrastructure.Persistence;

public sealed class AuditChainCoordinator(IDatabaseProviderCapabilities capabilities)
{
    public async Task AcquireAsync(DbContext db, string entityType, string entityId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Audit-chain locks must be acquired inside the transaction that persists the audit row.");

        var keyType = entityType.Replace("'", "''", StringComparison.Ordinal);
        var keyId = entityId.Replace("'", "''", StringComparison.Ordinal);
        var insert = capabilities.Kind == DatabaseProviderKind.PostgreSql
            ? $"INSERT INTO \"PA_TICKETS_AUDIT_CHAIN_LOCK\" (\"S_ENTITY_TYPE\", \"S_ENTITY_ID\") VALUES ('{keyType}', '{keyId}') ON CONFLICT DO NOTHING"
            : $"MERGE INTO PA_TICKETS_AUDIT_CHAIN_LOCK target USING (SELECT '{keyType}' S_ENTITY_TYPE, '{keyId}' S_ENTITY_ID FROM DUAL) source ON (target.S_ENTITY_TYPE = source.S_ENTITY_TYPE AND target.S_ENTITY_ID = source.S_ENTITY_ID) WHEN NOT MATCHED THEN INSERT (S_ENTITY_TYPE, S_ENTITY_ID) VALUES (source.S_ENTITY_TYPE, source.S_ENTITY_ID)";
        await db.Database.ExecuteSqlRawAsync(insert, ct);

        var lockSql = capabilities.Kind == DatabaseProviderKind.PostgreSql
            ? $"SELECT \"S_ENTITY_TYPE\" AS \"EntityType\", \"S_ENTITY_ID\" AS \"EntityId\" FROM \"PA_TICKETS_AUDIT_CHAIN_LOCK\" WHERE \"S_ENTITY_TYPE\" = '{keyType}' AND \"S_ENTITY_ID\" = '{keyId}' FOR UPDATE"
            : $"SELECT S_ENTITY_TYPE AS \"EntityType\", S_ENTITY_ID AS \"EntityId\" FROM PA_TICKETS_AUDIT_CHAIN_LOCK WHERE S_ENTITY_TYPE = '{keyType}' AND S_ENTITY_ID = '{keyId}' FOR UPDATE";

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = lockSql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Audit chain lock row was not found after acquisition.");
    }
}
