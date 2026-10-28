using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PA.Tickets.Domain.Tests;

internal sealed record LocalE2eS503Authorization(
    string Operation,
    string TargetHost,
    int TargetPort,
    string Database,
    string Role,
    string ArtifactPath,
    string ArtifactSha256,
    string PolicySha256,
    string OwnerIdentity,
    string OperatorWindowsSid,
    string ApprovalReference,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset RevocationCheckedAtUtc,
    string RevocationStatus,
    string RevocationEvidenceReference);

internal sealed record LocalE2eS503PrivilegeEvidence(
    string TargetHost,
    int TargetPort,
    string Database,
    string Role,
    string EvidenceReference,
    DateTimeOffset CapturedAtUtc,
    bool ReadOnlyCatalogScopeOnly,
    bool IsSuperuser,
    bool CanCreateRole,
    bool CanCreateDatabase,
    bool CanReplicate,
    bool CanBypassRowLevelSecurity,
    bool CanWriteApplicationTables,
    bool CanWriteSchema,
    bool CanWriteSequences,
    IReadOnlyList<string> EffectiveRoleMemberships);

internal sealed record LocalE2eS503SessionIdentity(
    string Database,
    string Role,
    string? ServerAddress,
    int ServerPort,
    string TransactionReadOnly);

internal sealed record LocalE2eS503AuditEvent(
    string AttemptId,
    DateTimeOffset RecordedAtUtc,
    string Operation,
    string Outcome,
    string Stage,
    string ReasonCode,
    string Target,
    string? OwnerIdentity,
    string? OperatorWindowsSid,
    string? ApprovalReference,
    string ArtifactSha256,
    string? FingerprintSha256 = null,
    long? ResultRowCount = null);

internal sealed record LocalE2eS503InspectionResult(
    string FingerprintSha256,
    long ResultRowCount,
    string ArtifactSha256);

internal interface ILocalE2eS503AuditSink
{
    Task WriteAsync(LocalE2eS503AuditEvent auditEvent, CancellationToken cancellationToken);
}

internal sealed class FileLocalE2eS503AuditSink : ILocalE2eS503AuditSink
{
    private readonly string _directory;

    internal FileLocalE2eS503AuditSink(string existingEvidenceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(existingEvidenceDirectory);
        _directory = Path.GetFullPath(existingEvidenceDirectory);
        if (!Directory.Exists(_directory) ||
            (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("The configured S5-03 evidence-custody directory must already exist and cannot be a reparse point.");
    }

    public async Task WriteAsync(LocalE2eS503AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_directory, $"s5-03-{auditEvent.AttemptId}.json");
        var json = JsonSerializer.Serialize(auditEvent);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(json.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }
}

internal sealed class LocalE2eS503InspectionException : InvalidOperationException
{
    internal LocalE2eS503InspectionException(string reasonCode, string message) : base(message) => ReasonCode = reasonCode;
    internal string ReasonCode { get; }
}

internal static class LocalE2eS503ReadOnlyInspection
{
    internal const string Operation = "S5-03_READ_ONLY_FINGERPRINT";
    internal const string ArtifactRelativePath = "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql";
    internal const string ArtifactSha256 = "2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8";
    internal const string PolicySha256 = "17DCC7EBF56F696E11A4C86298B0A431BB46AB1D577A7ED4A35CFC5030448866";
    private static readonly TimeSpan MaximumAuthorization = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaximumEvidenceAge = TimeSpan.FromMinutes(5);
    private static readonly string ExpectedTarget = "::1:5432/pa_tickets_codex_e2e as pa_tickets_codex";

    internal static async Task<LocalE2eS503InspectionResult> ExecuteAsync(
        string repositoryRoot,
        LocalE2eS503Authorization? authorization,
        LocalE2eS503PrivilegeEvidence? privilegeEvidence,
        ILocalE2eS503AuditSink? auditSink,
        CancellationToken cancellationToken = default)
    {
        var attemptId = Guid.NewGuid().ToString("N");
        var nowUtc = DateTimeOffset.UtcNow;
        // These records are caller-supplied values, not evidence provenance.
        // Until the canonical owner-record/custody loader is implemented and
        // reviewed, no caller-created object may open a database connection.
        _ = attemptId;
        _ = nowUtc;
        _ = repositoryRoot;
        _ = authorization;
        _ = privilegeEvidence;
        _ = auditSink;
        _ = cancellationToken;
        throw new LocalE2eS503InspectionException(
            "EVIDENCE_PROVENANCE_NOT_CONFIGURED",
            "S5-03 is disabled because authorization and effective privilege evidence have no trusted owner-record/custody source.");

#pragma warning disable CS0162 // Retained implementation body is unreachable until provenance loading is completed.
        if (auditSink is null)
            throw new LocalE2eS503InspectionException("AUDIT_SINK_MISSING", "S5-03 is blocked because sanitized evidence custody is not configured.");

        var stage = "AUTHORIZATION";
        string? artifactHash = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
            var root = Path.GetFullPath(repositoryRoot);
            var currentSid = GetCurrentWindowsSid();
            ValidateAuthorization(authorization, nowUtc, currentSid);
            ValidatePrivilegeEvidence(privilegeEvidence, nowUtc);
            stage = "ARTIFACT";
            var artifact = LoadPinnedArtifact(root);
            artifactHash = artifact.Sha256;

            await auditSink.WriteAsync(CreateAudit(attemptId, nowUtc, "STARTED", stage, "PREFLIGHT_OK", authorization, artifactHash), cancellationToken);

            // The test-only loader is invoked only after every offline authorization gate passes.
            var configuration = LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(root);
            var connectionString = configuration["ConnectionStrings:TicketsPostgres"]
                ?? throw new LocalE2eS503InspectionException("CONNECTION_CONFIGURATION_MISSING", "S5-03 fixed-target configuration is missing.");
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (builder.Host != "::1" || builder.Port != 5432 ||
                builder.Database != "pa_tickets_codex_e2e" || builder.Username != "pa_tickets_codex" ||
                builder.SslMode != SslMode.Disable)
                throw new LocalE2eS503InspectionException("CONNECTION_TARGET_MISMATCH", "S5-03 connection configuration does not match the fixed IPv6 loopback target.");

            // Recheck the time and revocation evidence immediately before opening the socket.
            ValidateAuthorization(authorization, DateTimeOffset.UtcNow, currentSid);
            ValidatePrivilegeEvidence(privilegeEvidence, DateTimeOffset.UtcNow);

            stage = "CONNECT";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            stage = "READ_ONLY_TRANSACTION";
            await ExecuteNonQueryAsync(connection, transaction, "SET TRANSACTION READ ONLY", cancellationToken);
            var session = await ReadSessionIdentityAsync(connection, transaction, cancellationToken);
            ValidateSessionIdentity(session);

            stage = "FINGERPRINT";
            var sql = RemovePinnedTransactionEnvelope(artifact.Sql);
            var result = await ExecutePinnedFingerprintAsync(connection, transaction, sql, artifactHash, cancellationToken);
            await transaction.RollbackAsync(cancellationToken);

            await auditSink.WriteAsync(
                CreateAudit(attemptId, DateTimeOffset.UtcNow, "COMPLETED", "AUDIT", "PREFLIGHT_COMPLETE", authorization, artifactHash, result.FingerprintSha256, result.ResultRowCount),
                cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            var reasonCode = exception is LocalE2eS503InspectionException gateException
                ? gateException.ReasonCode
                : stage switch
                {
                    "CONNECT" => "CONNECTION_FAILED",
                    "READ_ONLY_TRANSACTION" => "READ_ONLY_OR_IDENTITY_CHECK_FAILED",
                    "FINGERPRINT" => "PINNED_FINGERPRINT_FAILED",
                    _ => "PREFLIGHT_DENIED"
                };
            var denied = CreateAudit(attemptId, DateTimeOffset.UtcNow, "DENIED", stage, reasonCode, authorization, artifactHash ?? ArtifactSha256);
            await auditSink.WriteAsync(denied, cancellationToken);
            if (exception is LocalE2eS503InspectionException) throw;
            // Do not propagate provider exceptions: they can contain endpoint or connection details.
            throw new LocalE2eS503InspectionException(reasonCode, "S5-03 failed closed; inspect the sanitized evidence record. No credential or connection string is included.");
        }
#pragma warning restore CS0162
    }

    internal static void ValidateAuthorization(LocalE2eS503Authorization? authorization, DateTimeOffset nowUtc, string currentWindowsSid)
    {
        if (authorization is null)
            throw new LocalE2eS503InspectionException("AUTHORIZATION_MISSING", "A separate S5-03 operation authorization is required.");
        if (authorization.Operation != Operation ||
            authorization.TargetHost != "::1" || authorization.TargetPort != 5432 ||
            authorization.Database != "pa_tickets_codex_e2e" || authorization.Role != "pa_tickets_codex" ||
            authorization.ArtifactPath != ArtifactRelativePath || authorization.ArtifactSha256 != ArtifactSha256 ||
            authorization.PolicySha256 != PolicySha256)
            throw new LocalE2eS503InspectionException("AUTHORIZATION_SCOPE_MISMATCH", "S5-03 authorization does not match the one approved target, operation, policy, and SQL artifact.");
        if (string.IsNullOrWhiteSpace(authorization.OwnerIdentity) ||
            string.IsNullOrWhiteSpace(authorization.ApprovalReference) ||
            string.IsNullOrWhiteSpace(authorization.RevocationEvidenceReference))
            throw new LocalE2eS503InspectionException("AUTHORIZATION_ATTRIBUTION_MISSING", "Owner attribution, approval reference, and revocation evidence are required.");
        if (authorization.OperatorWindowsSid != currentWindowsSid)
            throw new LocalE2eS503InspectionException("OPERATOR_IDENTITY_MISMATCH", "The current Windows account does not match the authorized operator SID.");
        if (authorization.ValidFromUtc.Offset != TimeSpan.Zero || authorization.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            authorization.ValidFromUtc >= authorization.ExpiresAtUtc ||
            authorization.ExpiresAtUtc - authorization.ValidFromUtc > MaximumAuthorization ||
            nowUtc < authorization.ValidFromUtc || nowUtc >= authorization.ExpiresAtUtc)
            throw new LocalE2eS503InspectionException("AUTHORIZATION_EXPIRED_OR_INVALID", "S5-03 authorization must be current, UTC, and valid for no more than 30 minutes.");
        if (authorization.RevocationStatus != "ACTIVE" ||
            authorization.RevocationCheckedAtUtc.Offset != TimeSpan.Zero ||
            authorization.RevocationCheckedAtUtc > nowUtc ||
            nowUtc - authorization.RevocationCheckedAtUtc > TimeSpan.FromMinutes(1))
            throw new LocalE2eS503InspectionException("REVOCATION_CHECK_MISSING_OR_STALE", "A current, active revocation check is required immediately before connection.");
    }

    internal static void ValidatePrivilegeEvidence(LocalE2eS503PrivilegeEvidence? evidence, DateTimeOffset nowUtc)
    {
        if (evidence is null)
            throw new LocalE2eS503InspectionException("PRIVILEGE_EVIDENCE_MISSING", "Owner-collected effective least-privilege evidence is required.");
        if (evidence.TargetHost != "::1" || evidence.TargetPort != 5432 ||
            evidence.Database != "pa_tickets_codex_e2e" || evidence.Role != "pa_tickets_codex" ||
            string.IsNullOrWhiteSpace(evidence.EvidenceReference) ||
            evidence.CapturedAtUtc.Offset != TimeSpan.Zero ||
            evidence.CapturedAtUtc > nowUtc || nowUtc - evidence.CapturedAtUtc > MaximumEvidenceAge)
            throw new LocalE2eS503InspectionException("PRIVILEGE_EVIDENCE_INVALID_OR_STALE", "Privilege evidence must be fresh, attributable, and bound to the exact target.");
        if (!evidence.ReadOnlyCatalogScopeOnly || evidence.IsSuperuser || evidence.CanCreateRole ||
            evidence.CanCreateDatabase || evidence.CanReplicate || evidence.CanBypassRowLevelSecurity ||
            evidence.CanWriteApplicationTables || evidence.CanWriteSchema || evidence.CanWriteSequences ||
            evidence.EffectiveRoleMemberships is null || evidence.EffectiveRoleMemberships.Count != 0)
            throw new LocalE2eS503InspectionException("PRIVILEGE_SCOPE_EXCESS", "Effective privileges exceed the read-only catalog-inspection boundary.");
    }

    internal static void ValidateSessionIdentity(LocalE2eS503SessionIdentity identity)
    {
        if (identity.Database != "pa_tickets_codex_e2e" ||
            identity.Role != "pa_tickets_codex" ||
            identity.ServerAddress != "::1" ||
            identity.ServerPort != 5432 ||
            !string.Equals(identity.TransactionReadOnly, "on", StringComparison.OrdinalIgnoreCase))
            throw new LocalE2eS503InspectionException("SESSION_IDENTITY_MISMATCH", "Database, role, IPv6 server address, port, or read-only transaction state did not match.");
    }

    internal static (string Sql, string Sha256) LoadPinnedArtifact(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var path = Path.GetFullPath(Path.Combine(root, ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new LocalE2eS503InspectionException("ARTIFACT_PATH_INVALID", "The pinned S5-03 SQL artifact must exist inside the repository root.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new LocalE2eS503InspectionException("ARTIFACT_PATH_INVALID", "The pinned S5-03 SQL artifact cannot be a reparse point.");
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(hash, ArtifactSha256, StringComparison.OrdinalIgnoreCase))
            throw new LocalE2eS503InspectionException("ARTIFACT_HASH_MISMATCH", "The pinned S5-03 SQL artifact hash changed; no connection was opened.");
        return (new UTF8Encoding(false, true).GetString(bytes), hash);
    }

    internal static string RemovePinnedTransactionEnvelope(string sql)
    {
        var begin = System.Text.RegularExpressions.Regex.Match(sql, @"(?m)^BEGIN;\r?\nSET TRANSACTION READ ONLY;\r?\n");
        var rollback = System.Text.RegularExpressions.Regex.Match(sql, @"(?m)^ROLLBACK;\s*\z");
        if (!begin.Success || !rollback.Success || rollback.Index <= begin.Index + begin.Length)
            throw new LocalE2eS503InspectionException("ARTIFACT_ENVELOPE_INVALID", "The pinned fingerprint transaction envelope is invalid.");
        return sql.Remove(rollback.Index, rollback.Length).Remove(begin.Index, begin.Length);
    }

    internal static string GetCurrentWindowsSid()
    {
        if (!OperatingSystem.IsWindows())
            throw new LocalE2eS503InspectionException("UNSUPPORTED_OPERATOR_PLATFORM", "S5-03 local inspection is supported only from the approved Windows owner account.");
        return WindowsIdentity.GetCurrent().User?.Value
            ?? throw new LocalE2eS503InspectionException("OPERATOR_SID_UNAVAILABLE", "The current Windows account SID could not be established.");
    }

    private static LocalE2eS503AuditEvent CreateAudit(
        string attemptId, DateTimeOffset recordedAtUtc, string outcome, string stage, string reasonCode,
        LocalE2eS503Authorization? authorization, string artifactHash, string? resultHash = null, long? rowCount = null) =>
        new(attemptId, recordedAtUtc.ToUniversalTime(), Operation, outcome, stage, reasonCode, ExpectedTarget,
            authorization?.OwnerIdentity, authorization?.OperatorWindowsSid, authorization?.ApprovalReference,
            artifactHash, resultHash, rowCount);

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<LocalE2eS503SessionIdentity> ReadSessionIdentityAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = "SELECT current_database(), current_user, inet_server_addr()::text, inet_server_port(), current_setting('transaction_read_only')";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.FieldCount != 5)
            throw new LocalE2eS503InspectionException("SESSION_IDENTITY_UNAVAILABLE", "The current session identity could not be verified.");
        return new LocalE2eS503SessionIdentity(
            reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4));
    }

    private static async Task<LocalE2eS503InspectionResult> ExecutePinnedFingerprintAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, string artifactHash, CancellationToken cancellationToken)
    {
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long rows = 0;
        var completeMarkerSeen = false;
        await using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        do
        {
            if (reader.FieldCount <= 0) continue;
            var headers = new string[reader.FieldCount];
            for (var i = 0; i < headers.Length; i++) headers[i] = reader.GetName(i);
            AppendCanonical(fingerprint, headers);
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new string[reader.FieldCount];
                for (var i = 0; i < values.Length; i++)
                    values[i] = reader.IsDBNull(i) ? "<NULL>" : CanonicalValue(reader.GetValue(i));
                AppendCanonical(fingerprint, values);
                rows++;
                if (headers.Length > 0 && headers[0] == "preflight_status" && values[0] == "PREFLIGHT_COMPLETE")
                    completeMarkerSeen = true;
            }
        } while (await reader.NextResultAsync(cancellationToken));

        if (!completeMarkerSeen)
            throw new LocalE2eS503InspectionException("FINGERPRINT_COMPLETION_MARKER_MISSING", "The pinned catalog fingerprint did not emit its completion marker.");
        return new LocalE2eS503InspectionResult(Convert.ToHexString(fingerprint.GetHashAndReset()), rows, artifactHash);
    }

    private static string CanonicalValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static void AppendCanonical(IncrementalHash hash, IReadOnlyList<string> values)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(values);
        hash.AppendData(payload);
        hash.AppendData([0x0A]);
    }
}
