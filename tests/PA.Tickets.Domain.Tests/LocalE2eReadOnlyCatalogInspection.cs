using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PA.Tickets.Domain.Tests;

internal sealed record LocalE2eReadOnlyArtifact(string Operation, string RelativePath, string Sha256);

internal sealed record LocalE2eReadOnlyAuditRecord(
    string AttemptId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    string Operation,
    string Target,
    string ArtifactSha256,
    string Outcome,
    string? FailureCode,
    string Stage,
    bool ConnectionOpened,
    bool CatalogQueryStarted,
    bool CatalogQueryCompleted,
    bool ReadOnlyTransactionSet,
    bool SessionIdentityVerified,
    string? PostgreSqlSqlState,
    string? SqlErrorCategory,
    bool RollbackSucceeded,
    bool ConnectionDisposed,
    int ResultSetCount,
    long ResultRowCount,
    string? ResultSha256,
    string OwnerDisplayIdentity,
    IReadOnlyList<LocalE2eReadOnlyResultSet> SanitizedResultSets);

internal sealed record LocalE2eReadOnlyResultSet(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

internal sealed record LocalE2eReadOnlyResult(
    string Operation,
    string ArtifactSha256,
    int ResultSetCount,
    long ResultRowCount,
    string ResultSha256,
    string EvidencePath);

internal sealed class LocalE2eReadOnlyInspectionException : InvalidOperationException
{
    internal LocalE2eReadOnlyInspectionException(string reasonCode, string message) : base(message) => ReasonCode = reasonCode;
    internal string ReasonCode { get; }
}

/// <summary>
/// Fixed local-only catalog inspection. The operation is selected from a fixed
/// allowlist; callers cannot supply SQL, a connection string, or a target.
/// </summary>
internal static class LocalE2eReadOnlyCatalogInspection
{
    internal const string Target = "[::1]:5432/pa_tickets_codex_e2e as pa_tickets_codex";
    internal const string InvocationVariable = "PA_TICKETS_LOCAL_E2E_READONLY_OPERATION";
    internal const string ConfirmationVariable = "PA_TICKETS_LOCAL_E2E_READONLY_CONFIRM";
    internal const string ConfirmationValue = "RUN_FIXED_READ_ONLY_OPERATION";

    private static readonly IReadOnlyDictionary<string, LocalE2eReadOnlyArtifact> Artifacts =
        new Dictionary<string, LocalE2eReadOnlyArtifact>(StringComparer.Ordinal)
        {
            ["LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT"] = new(
                "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT",
                "tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql",
                "0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034"),
            ["S5-03_READ_ONLY_FINGERPRINT"] = new(
                "S5-03_READ_ONLY_FINGERPRINT",
                "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql",
                "6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342"),
            ["M1_SEQUENCE_SAFETY_PREFLIGHT"] = new(
                "M1_SEQUENCE_SAFETY_PREFLIGHT",
                "Docs/reconciliation/proposed/postgresql/M1_sequence_safety_inspection_REVIEW.sql",
                "526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A"),
            ["M1_LIFECYCLE_REFERENCE_PREFLIGHT"] = new(
                "M1_LIFECYCLE_REFERENCE_PREFLIGHT",
                "Docs/reconciliation/proposed/postgresql/M1_lifecycle_reference_and_sequence_preflight_REVIEW.sql",
                "C719C6C0204527F262FAB51A3C4138FC09057744990DF242F232BA9FC88AE47D"),
            ["M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT"] = new(
                "M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT",
                "Docs/reconciliation/proposed/postgresql/M1_permission_catalogue_sequence_preflight_REVIEW.sql",
                "795838E1CE3A02ADFBBE755197DB2228D52ED16C457046F57818E3912DC88609")
        };

    // The sequence operation's one approved invocation has been consumed.
    // Keep its artifact pinned for audit/testing, but deny any further run.
    private static readonly HashSet<string> PolicyApprovedOperations = new(StringComparer.Ordinal)
    {
        "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT",
        "S5-03_READ_ONLY_FINGERPRINT",
        "M1_LIFECYCLE_REFERENCE_PREFLIGHT",
        "M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT"
    };

    internal static IReadOnlyCollection<LocalE2eReadOnlyArtifact> AllowedArtifacts => Artifacts.Values.ToArray();

    internal static LocalE2eReadOnlyArtifact RequireArtifact(string operation) =>
        Artifacts.TryGetValue(operation, out var artifact)
            ? artifact
            : throw new InvalidOperationException("The requested local read-only operation is not allowlisted.");

    internal static bool IsOwnerApprovedLocalRoleException(string operation, string? role) =>
        PolicyApprovedOperations.Contains(operation) && string.Equals(role, "pa_tickets_codex", StringComparison.Ordinal);

    internal static bool ShouldCaptureResultRows(string operation) => Artifacts.ContainsKey(operation);

    internal static void RequireExplicitInvocation(string operation, string? selectedOperation, string? confirmation)
    {
        _ = RequireArtifact(operation);
        if (!PolicyApprovedOperations.Contains(operation))
            throw new LocalE2eReadOnlyInspectionException("OPERATION_POLICY_NOT_ADOPTED", "This pinned operation is not enabled by the adopted local E2E policy.");
        if (!string.Equals(selectedOperation, operation, StringComparison.Ordinal) ||
            !string.Equals(confirmation, ConfirmationValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Explicit opt-in for the exact fixed local read-only operation is required.");
    }

    internal static (string Sql, string Sha256) LoadPinnedArtifact(string repositoryRoot, LocalE2eReadOnlyArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var root = Path.GetFullPath(repositoryRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPath) ||
            (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("The pinned read-only SQL artifact is missing or outside the repository.");

        var bytes = File.ReadAllBytes(fullPath);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(digest, artifact.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The pinned read-only SQL artifact hash does not match the approved value.");

        var sql = new UTF8Encoding(false, true).GetString(bytes);
        ValidateReadOnlyArtifactEnvelope(sql);
        return (sql, digest);
    }

    internal static void ValidateReadOnlyArtifactEnvelope(string sql)
    {
        var begin = System.Text.RegularExpressions.Regex.Match(sql, @"\A(?:--[^\r\n]*(?:\r?\n|\z))*\s*BEGIN;\r?\nSET TRANSACTION READ ONLY;\r?\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rollback = System.Text.RegularExpressions.Regex.Match(sql, @"(?m)^ROLLBACK;\s*\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!begin.Success || !rollback.Success || rollback.Index <= begin.Index + begin.Length)
            throw new InvalidOperationException("Pinned artifact must have one read-only transaction envelope and a final rollback.");

        var body = sql[(begin.Index + begin.Length)..rollback.Index];
        body = System.Text.RegularExpressions.Regex.Replace(body, @"(?m)--[^\r\n]*", string.Empty);
        body = System.Text.RegularExpressions.Regex.Replace(body, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        // SQL keywords inside privilege labels such as 'CREATE' are values, not commands.
        body = System.Text.RegularExpressions.Regex.Replace(body, @"'(?:''|[^'])*'", "''");
        if (System.Text.RegularExpressions.Regex.IsMatch(body,
                @"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|CREATE|ALTER|DROP|GRANT|REVOKE|CALL|DO\s+\$[^$]*\$\s*BEGIN\s*[^$]*(?:INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|GRANT|REVOKE))\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Pinned artifact contains a prohibited write or schema operation.");
    }

    internal static string RemoveTransactionEnvelope(string sql)
    {
        var begin = System.Text.RegularExpressions.Regex.Match(sql, @"\A(?:--[^\r\n]*(?:\r?\n|\z))*\s*BEGIN;\r?\nSET TRANSACTION READ ONLY;\r?\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rollback = System.Text.RegularExpressions.Regex.Match(sql, @"(?m)^ROLLBACK;\s*\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!begin.Success || !rollback.Success || rollback.Index <= begin.Index + begin.Length)
            throw new InvalidOperationException("Pinned artifact transaction envelope is invalid.");
        return sql[(begin.Index + begin.Length)..rollback.Index];
    }

    internal static void ValidateSession(LocalE2eS503SessionIdentity session)
    {
        LocalE2eS503ReadOnlyInspection.ValidateSessionIdentity(session);
    }

    internal static async Task<LocalE2eReadOnlyResult> ExecuteAsync(
        string repositoryRoot,
        string operation,
        CancellationToken cancellationToken = default)
    {
        var selected = Environment.GetEnvironmentVariable(InvocationVariable);
        var confirmation = Environment.GetEnvironmentVariable(ConfirmationVariable);
        RequireExplicitInvocation(operation, selected, confirmation);

        var artifact = RequireArtifact(operation);
        var (sql, artifactHash) = LoadPinnedArtifact(repositoryRoot, artifact);
        var evidenceDirectory = Path.Combine(Path.GetFullPath(repositoryRoot), "test-results", "evidence");
        Directory.CreateDirectory(evidenceDirectory);
        var attemptId = Guid.NewGuid().ToString("N");
        var started = DateTimeOffset.UtcNow;
        var opened = false;
        var catalogQueryStarted = false;
        var catalogQueryCompleted = false;
        var readOnlyTransactionSet = false;
        var sessionIdentityVerified = false;
        string? postgresSqlState = null;
        string? sqlErrorCategory = null;
        var rollbackSucceeded = false;
        var connectionDisposed = false;
        var stage = "CONFIGURATION";
        var resultSets = 0;
        long rows = 0;
        var sanitizedResultSets = new List<LocalE2eReadOnlyResultSet>();
        string? resultHash = null;
        string outcome = "DENIED";
        string failure = "READ_ONLY_PREFLIGHT_DENIED";
        NpgsqlConnection? connection = null;
        NpgsqlTransaction? transaction = null;
        try
        {
            var configuration = LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(repositoryRoot);
            var configured = configuration["ConnectionStrings:TicketsPostgres"]
                ?? throw new InvalidOperationException("Fixed Development PostgreSQL configuration is missing.");
            var builder = new NpgsqlConnectionStringBuilder(configured)
            {
                Timeout = 5,
                CommandTimeout = 30,
                PersistSecurityInfo = false
            };
            if (builder.Host != "::1" || builder.Port != 5432 ||
                builder.Database != "pa_tickets_codex_e2e" || !IsOwnerApprovedLocalRoleException(operation, builder.Username) ||
                builder.SslMode != SslMode.Disable)
                throw new LocalE2eReadOnlyInspectionException("FIXED_TARGET_CONFIGURATION_MISMATCH", "Development connection configuration does not match the fixed local E2E target.");

            stage = "CONNECT";
            connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            opened = true;

            stage = "READ_ONLY_ENFORCEMENT";
            transaction = await connection.BeginTransactionAsync(cancellationToken);
            await ExecuteNonQueryAsync(connection, transaction, "SET TRANSACTION READ ONLY", cancellationToken);
            readOnlyTransactionSet = true;

            stage = "SESSION_IDENTITY";
            var session = await ReadSessionIdentityAsync(connection, transaction, cancellationToken);
            ValidateSession(session);
            sessionIdentityVerified = true;

            stage = "CATALOG_SQL";
            catalogQueryStarted = true;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var command = new NpgsqlCommand(RemoveTransactionEnvelope(sql), connection, transaction) { CommandTimeout = 30 })
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                do
                {
                    if (reader.FieldCount == 0) continue;
                    resultSets++;
                    var headers = new string[reader.FieldCount];
                    for (var i = 0; i < headers.Length; i++) headers[i] = reader.GetName(i);
                    AppendCanonical(digest, headers);
                    var resultRows = new List<IReadOnlyList<string>>();
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var values = new string[reader.FieldCount];
                        for (var i = 0; i < values.Length; i++)
                            values[i] = reader.IsDBNull(i) ? "<NULL>" : CanonicalValue(reader.GetValue(i));
                        AppendCanonical(digest, values);
                        // Both allowlisted artifacts return catalog/privilege metadata only.
                        // The fingerprint's table-data checks run inside fixed DO blocks and
                        // return no application rows; retaining its catalog rows is necessary
                        // for baseline and EF-mapping comparison without another DB operation.
                        if (ShouldCaptureResultRows(operation)) resultRows.Add(values);
                        rows++;
                    }
                    sanitizedResultSets.Add(new LocalE2eReadOnlyResultSet(headers, resultRows));
                } while (await reader.NextResultAsync(cancellationToken));
            }
            resultHash = Convert.ToHexString(digest.GetHashAndReset());
            catalogQueryCompleted = true;
            stage = "ROLLBACK";
            await transaction.RollbackAsync(cancellationToken);
            rollbackSucceeded = true;
            outcome = "COMPLETED_READ_ONLY_ROLLED_BACK";
            failure = string.Empty;
        }
        catch (Exception exception)
        {
            // Provider exception text can contain endpoint information. Persist only a bounded reason code.
            outcome = catalogQueryStarted ? "UNKNOWN_STOP_NO_RETRY" : "DENIED_NO_CATALOG_QUERY";
            failure = ClassifyFailure(exception, stage, opened);
            if (exception is PostgresException postgresException)
            {
                postgresSqlState = postgresException.SqlState;
                sqlErrorCategory = ClassifyPostgreSqlError(postgresException.SqlState);
            }
            else if (stage == "CATALOG_SQL")
            {
                sqlErrorCategory = exception is NpgsqlException ? "NPGSQL_PROVIDER_ERROR" : "CATALOG_EXECUTION_ERROR";
            }
            throw new InvalidOperationException($"Local read-only inspection failed closed ({failure}); see sanitized evidence.");
        }
        finally
        {
            if (transaction is not null)
            {
                if (!rollbackSucceeded)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                        rollbackSucceeded = true;
                    }
                    catch
                    {
                        outcome = "UNKNOWN_STOP_NO_RETRY";
                        failure = "ROLLBACK_FAILED_STOP_NO_RETRY";
                        stage = "ROLLBACK";
                    }
                }
                try { await transaction.DisposeAsync(); } catch { outcome = "UNKNOWN_STOP_NO_RETRY"; failure = "TRANSACTION_DISPOSAL_UNCERTAIN"; stage = "TRANSACTION_DISPOSAL"; }
            }
            if (connection is not null)
            {
                try { await connection.DisposeAsync(); connectionDisposed = true; }
                catch { outcome = "UNKNOWN_STOP_NO_RETRY"; failure = "CONNECTION_DISPOSAL_UNCERTAIN"; stage = "CONNECTION_DISPOSAL"; }
            }
            var record = new LocalE2eReadOnlyAuditRecord(
                attemptId, started, DateTimeOffset.UtcNow, artifact.Operation, Target, artifactHash,
                outcome, string.IsNullOrEmpty(failure) ? null : failure, stage, opened, catalogQueryStarted,
                catalogQueryCompleted, readOnlyTransactionSet, sessionIdentityVerified, postgresSqlState,
                sqlErrorCategory, rollbackSucceeded, connectionDisposed, resultSets, rows, resultHash, "Mr.H", sanitizedResultSets);
            try
            {
                await WriteSanitizedEvidenceAsync(evidenceDirectory, record, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                throw new InvalidOperationException("Sanitized evidence output failed; stop and treat the operation record as unavailable.");
            }
        }

        return new LocalE2eReadOnlyResult(artifact.Operation, artifactHash, resultSets, rows, resultHash!,
            Path.Combine(evidenceDirectory, $"local-e2e-readonly-{attemptId}.json"));
    }

    private static async Task WriteSanitizedEvidenceAsync(string directory, LocalE2eReadOnlyAuditRecord record, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, $"local-e2e-readonly-{record.AttemptId}.json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, record, cancellationToken: cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static string ClassifyFailure(Exception exception, string stage, bool connectionOpened)
    {
        if (exception is LocalE2eReadOnlyInspectionException localGuard) return localGuard.ReasonCode;
        if (exception is LocalE2eS503InspectionException sessionGuard) return sessionGuard.ReasonCode;
        if (exception is OperationCanceledException) return "CANCELLED_STOP_NO_RETRY";
        return stage switch
        {
            "CONFIGURATION" => "LOCAL_CONFIGURATION_MISSING_OR_INVALID",
            "CONNECT" => connectionOpened ? "POST_CONNECT_SETUP_FAILURE" : "CONNECTION_OR_AUTHENTICATION_FAILED",
            "READ_ONLY_ENFORCEMENT" => "READ_ONLY_ENFORCEMENT_FAILED",
            "SESSION_IDENTITY" => "SESSION_IDENTITY_CHECK_FAILED",
            "CATALOG_SQL" => "PINNED_CATALOG_SQL_FAILED",
            "ROLLBACK" => "ROLLBACK_FAILED_STOP_NO_RETRY",
            "EVIDENCE_OUTPUT" => "SANITIZED_EVIDENCE_OUTPUT_FAILED",
            _ => "LOCAL_INSPECTION_INTERNAL_FAILURE"
        };
    }

    internal static string ClassifyPostgreSqlError(string? sqlState)
    {
        if (string.IsNullOrWhiteSpace(sqlState) || sqlState.Length != 5) return "UNKNOWN_POSTGRESQL_ERROR";
        if (sqlState == "42501") return "INSUFFICIENT_PRIVILEGE";
        if (sqlState == "57014") return "QUERY_CANCELLED_OR_TIMEOUT";
        if (sqlState.StartsWith("08", StringComparison.Ordinal)) return "POSTGRESQL_CONNECTION_ERROR";
        if (sqlState.StartsWith("25", StringComparison.Ordinal)) return "TRANSACTION_STATE_ERROR";
        if (sqlState.StartsWith("42", StringComparison.Ordinal)) return "SQL_SYNTAX_OR_CATALOG_ERROR";
        return "POSTGRESQL_EXECUTION_ERROR";
    }

    private static async Task<LocalE2eS503SessionIdentity> ReadSessionIdentityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = "SELECT current_database() AS database_name, current_user AS role_name, inet_server_addr()::text AS server_address, inet_server_port() AS server_port, current_setting('transaction_read_only') AS transaction_read_only";
        await using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.FieldCount != 5)
            throw new InvalidOperationException("Same-session target identity could not be verified.");
        return new LocalE2eS503SessionIdentity(
            reader.IsDBNull(reader.GetOrdinal("database_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("database_name")),
            reader.IsDBNull(reader.GetOrdinal("role_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("role_name")),
            reader.IsDBNull(reader.GetOrdinal("server_address")) ? null : reader.GetString(reader.GetOrdinal("server_address")),
            reader.IsDBNull(reader.GetOrdinal("server_port")) ? -1 : reader.GetInt32(reader.GetOrdinal("server_port")),
            reader.IsDBNull(reader.GetOrdinal("transaction_read_only")) ? string.Empty : reader.GetString(reader.GetOrdinal("transaction_read_only")));
    }

    private static string CanonicalValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static void AppendCanonical(IncrementalHash hash, IReadOnlyList<string> values)
    {
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(values));
        hash.AppendData([0x0A]);
    }
}
