using System.Data;
using Npgsql;

namespace PA.Tickets.Domain.Tests;

internal sealed record PostgreSqlReadOnlyTarget(string ConnectionString, string ApprovedTargetDescriptor, string BaselineArtifact);

internal sealed record PostgreSqlCatalogRow(
    string Kind,
    string Schema,
    string Name,
    string? ColumnName = null,
    string? Type = null,
    bool? Nullable = null,
    string? DefaultExpression = null,
    string? Detail = null);

internal sealed class PostgreSqlCatalogReader
{
    private const string IdentitySql = "select current_database(), current_user, inet_server_port()";
    private const string TablesSql = "select table_schema, table_name from information_schema.tables where table_type = 'BASE TABLE' and table_schema not in ('pg_catalog', 'information_schema') order by table_schema, table_name";
    private const string ColumnsSql = "select table_schema, table_name, column_name, data_type, is_nullable, column_default from information_schema.columns where table_schema not in ('pg_catalog', 'information_schema') order by table_schema, table_name, ordinal_position";
    private const string ConstraintsSql = "select n.nspname, c.relname, con.conname from pg_constraint con join pg_class c on c.oid = con.conrelid join pg_namespace n on n.oid = c.relnamespace where n.nspname not in ('pg_catalog', 'information_schema') order by n.nspname, c.relname, con.conname";
    private const string IndexesSql = "select schemaname, tablename, indexname from pg_indexes where schemaname not in ('pg_catalog', 'information_schema') order by schemaname, tablename, indexname";
    private const string SequencesSql = "select schemaname, sequencename, data_type, start_value, increment_by from pg_sequences where schemaname not in ('pg_catalog', 'information_schema') order by schemaname, sequencename";

    internal static PostgreSqlReadOnlyTarget CreateTarget(string connectionString, string approvedTargetDescriptor, string baselineArtifact)
    {
        var actual = PostgreSqlTestEnvironment.BuildTargetDescriptor(connectionString);
        if (string.IsNullOrWhiteSpace(baselineArtifact) || string.IsNullOrWhiteSpace(approvedTargetDescriptor) ||
            !string.Equals(actual, approvedTargetDescriptor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL read-only reconciliation target verification failed; credentials are never logged.");
        if (actual.Contains("database=pa_tickets_test", StringComparison.OrdinalIgnoreCase) ||
            actual.Contains("database=pa_tickets_h_verify", StringComparison.OrdinalIgnoreCase) ||
            actual.Contains("user=pa_tickets_test_user", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL read-only reconciliation target verification failed: prohibited target; credentials are never logged.");
        return new(connectionString, actual, baselineArtifact.Trim());
    }

    internal static PostgreSqlSchemaSnapshot FromRows(string targetDescriptor, IEnumerable<PostgreSqlCatalogRow> rows)
    {
        var input = rows?.ToArray() ?? throw new InvalidOperationException("PostgreSQL catalog metadata is unavailable.");
        var allowedKinds = new[] { "table", "column", "constraint", "index", "sequence" };
        if (input.Any(x => !allowedKinds.Contains(x.Kind, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("PostgreSQL catalog metadata contains an unsupported object kind.");
        var tables = input.Where(x => x.Kind.Equals("table", StringComparison.OrdinalIgnoreCase)).Select(x => $"{x.Schema}:{x.Name}").ToArray();
        if (tables.Length != tables.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("PostgreSQL catalog metadata contains duplicate table rows.");
        var objects = new Dictionary<string, MutableObject>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenColumns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in input)
        {
            if (string.IsNullOrWhiteSpace(row.Kind) || string.IsNullOrWhiteSpace(row.Schema) || string.IsNullOrWhiteSpace(row.Name))
                throw new InvalidOperationException("PostgreSQL catalog metadata contains an incomplete object identity.");
            var isTableMetadata = row.Kind.Equals("column", StringComparison.OrdinalIgnoreCase) || row.Kind.Equals("constraint", StringComparison.OrdinalIgnoreCase) || row.Kind.Equals("index", StringComparison.OrdinalIgnoreCase);
            if (isTableMetadata && !tables.Contains($"{row.Schema}:{row.Name}", StringComparer.Ordinal))
                throw new InvalidOperationException("PostgreSQL catalog metadata references an undeclared table.");
            var objectKind = isTableMetadata || row.Kind.Equals("table", StringComparison.OrdinalIgnoreCase) ? "table" : row.Kind;
            var key = $"{objectKind}:{row.Schema}:{row.Name}";
            var rowKey = $"{row.Kind}:{row.Schema}:{row.Name}:{row.ColumnName}:{row.Type}:{row.Nullable}:{row.DefaultExpression}:{row.Detail}";
            if (!seen.Add(rowKey))
                throw new InvalidOperationException("PostgreSQL catalog metadata contains duplicate rows.");
            if (!objects.TryGetValue(key, out var value))
                objects[key] = value = new(objectKind, row.Schema, row.Name);
            if (row.Kind.Equals("column", StringComparison.OrdinalIgnoreCase) && row.ColumnName is not null)
            {
                if (string.IsNullOrWhiteSpace(row.ColumnName) || string.IsNullOrWhiteSpace(row.Type) || row.Nullable is null)
                    throw new InvalidOperationException("PostgreSQL catalog metadata contains an incomplete column definition.");
                if (!seenColumns.Add($"{row.Schema}:{row.Name}:{row.ColumnName}"))
                    throw new InvalidOperationException("PostgreSQL catalog metadata contains contradictory or duplicate column rows.");
                value.Columns.Add(new(row.ColumnName, row.Type, row.Nullable.Value, row.DefaultExpression));
            }
            else if (row.Detail is not null)
                value.AddDetail(row.Kind, row.Detail);
        }
        return new(targetDescriptor, objects.Values.OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Schema, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal).Select(x => x.ToSnapshot()).ToArray());
    }

    internal async Task<PostgreSqlSchemaSnapshot> ReadAsync(PostgreSqlReadOnlyTarget target, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var identity = new NpgsqlCommand(IdentitySql, connection);
        await using var identityReader = await identity.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, cancellationToken);
        if (!await identityReader.ReadAsync(cancellationToken) ||
            !string.Equals(identityReader.GetString(0), GetField(target.ApprovedTargetDescriptor, "database"), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(identityReader.GetString(1), GetField(target.ApprovedTargetDescriptor, "user"), StringComparison.OrdinalIgnoreCase) ||
            identityReader.GetInt32(2) != int.Parse(GetField(target.ApprovedTargetDescriptor, "port")))
            throw new InvalidOperationException("PostgreSQL read-only reconciliation target identity verification failed.");
        await identityReader.DisposeAsync();

        var rows = new List<PostgreSqlCatalogRow>();
        await ReadRows(connection, TablesSql, rows, "table", cancellationToken);
        await ReadRows(connection, ColumnsSql, rows, "column", cancellationToken);
        await ReadRows(connection, ConstraintsSql, rows, "constraint", cancellationToken);
        await ReadRows(connection, IndexesSql, rows, "index", cancellationToken);
        await ReadRows(connection, SequencesSql, rows, "sequence", cancellationToken);
        return FromRows(target.ApprovedTargetDescriptor, rows);
    }

    private static async Task ReadRows(NpgsqlConnection connection, string sql, ICollection<PostgreSqlCatalogRow> rows, string kind, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (kind == "table") rows.Add(new(kind, reader.GetString(0), reader.GetString(1)));
            else if (kind == "column") rows.Add(new("column", reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4).Equals("YES", StringComparison.Ordinal), reader.IsDBNull(5) ? null : reader.GetString(5)));
            else if (kind == "sequence") rows.Add(new(kind, reader.GetString(0), reader.GetString(1), Detail: $"{reader.GetString(2)}|start={reader.GetValue(3)}|increment={reader.GetValue(4)}"));
            else rows.Add(new(kind, reader.GetString(0), reader.GetString(1), Detail: reader.GetString(2)));
        }
    }

    private static string GetField(string descriptor, string field) => descriptor.Split(';').Select(x => x.Split('=', 2)).First(x => x.Length == 2 && x[0].Equals(field, StringComparison.OrdinalIgnoreCase))[1];

    private sealed class MutableObject(string kind, string schema, string name)
    {
        internal readonly List<PostgreSqlCatalogColumn> Columns = [];
        internal readonly List<string> Constraints = [];
        internal readonly List<string> Indexes = [];
        internal readonly List<string> Sequences = [];
        internal string Schema => schema;
        internal string Name => name;
        internal void AddDetail(string detailKind, string detail) => (detailKind.ToLowerInvariant() switch { "constraint" => Constraints, "index" => Indexes, "sequence" => Sequences, _ => throw new InvalidOperationException("Unsupported PostgreSQL catalog metadata kind.") }).Add(detail);
        internal PostgreSqlCatalogObject ToSnapshot() => new(Kind: kind, Schema: schema, Name: name, Columns: Columns.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), Constraints: Constraints.OrderBy(x => x, StringComparer.Ordinal).ToArray(), Indexes: Indexes.OrderBy(x => x, StringComparer.Ordinal).ToArray(), Sequences: Sequences.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        internal string Kind => kind;
    }
}
