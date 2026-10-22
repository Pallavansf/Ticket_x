using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace PA.Tickets.Migration026Runner;

public enum ExecutionState { NotStarted, Running, Committed, RolledBack, UnknownCommit }
public sealed class UnknownCommitException(Exception inner) : Exception("Migration commit outcome is unknown; do not retry automatically.", inner);
public sealed class RollbackFailureException(Exception migrationFailure, Exception rollbackFailure) : Exception("Migration failed and rollback could not be confirmed.", new AggregateException(migrationFailure, rollbackFailure));
public sealed record RunnerOptions(string ExpectedHash, string ApprovedTargetFingerprint, string Authorization);
public sealed record ValidatedMigration(byte[] Bytes, string Sql, string CanonicalPath);

public interface IExecutionPolicy
{
    void ValidateAuthorization(RunnerOptions options);
    Task<string?> VerifyActualTargetFingerprintAsync(ITargetConnection connection, CancellationToken cancellationToken);
}
public interface ITargetConnection : IAsyncDisposable
{
    Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task AcquireMigrationLockAsync(DbTransaction transaction, CancellationToken cancellationToken);
    Task ExecuteAsync(ReadOnlyMemory<byte> exactUtf8Sql, DbTransaction transaction, CancellationToken cancellationToken);
}

public sealed class Migration026Runner
{
    public const string MigrationId = "026";
    public const string CandidateHash = "97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49";
    public const string CandidateRelativePath = "database/migrations/026_phase8_6_reporting.review.v3.sql";
    private readonly Func<CancellationToken, Task<ITargetConnection>> _connect;
    private readonly IExecutionPolicy _policy;
    public ExecutionState State { get; private set; }
    public Migration026Runner(Func<CancellationToken, Task<ITargetConnection>> connect, IExecutionPolicy policy) => (_connect, _policy) = (connect, policy);

    public static string ResolveCandidatePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.GetFullPath(Path.Combine(directory.FullName, CandidateRelativePath));
            if (File.Exists(candidate)) return ValidateCanonicalPath(candidate);
        }
        throw new FileNotFoundException("Approved migration candidate was not found.");
    }
    public static string ValidateCanonicalPath(string path)
    {
        var rawSegments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (rawSegments.Any(s => s == "..")) throw new InvalidOperationException("Path traversal is not permitted.");
        var full = Path.GetFullPath(path);
        var expected = Path.GetFullPath(Path.Combine(FindRepositoryRoot(full), CandidateRelativePath));
        if (!string.Equals(full, expected, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFileName(full), "026_phase8_6_reporting.review.v3.sql", StringComparison.Ordinal)) throw new InvalidOperationException("Only the canonical migration 026 v3 candidate path is permitted.");
        if ((new FileInfo(full).Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse-point migration candidates are not permitted.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(full)!); directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse-point migration directories are not permitted.");
            if (string.Equals(directory.FullName, FindRepositoryRoot(full), StringComparison.OrdinalIgnoreCase)) break;
        }
        return full;
    }
    public static async Task<ValidatedMigration> ReadAndValidateCandidateAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveCandidatePath();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        using var memory = new MemoryStream(); await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(CandidateHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Migration candidate hash mismatch.");
        try { return new ValidatedMigration(bytes, new UTF8Encoding(false, true).GetString(bytes), path); }
        catch (DecoderFallbackException ex) { throw new InvalidOperationException("Migration candidate encoding is unsupported.", ex); }
    }
    public async Task ExecuteAsync(RunnerOptions options, CancellationToken cancellationToken = default)
    {
        if (State != ExecutionState.NotStarted) throw new InvalidOperationException("This runner cannot be retried after execution began.");
        if (!options.ExpectedHash.Equals(CandidateHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Expected migration hash is not the approved 026 candidate hash.");
        if (string.IsNullOrWhiteSpace(options.ApprovedTargetFingerprint)) throw new InvalidOperationException("An approved target fingerprint is required.");
        _policy.ValidateAuthorization(options);
        var candidate = await ReadAndValidateCandidateAsync(cancellationToken);
        await using var connection = await _connect(cancellationToken);
        var actual = await _policy.VerifyActualTargetFingerprintAsync(connection, cancellationToken);
        if (string.IsNullOrWhiteSpace(actual) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(options.ApprovedTargetFingerprint))) throw new InvalidOperationException("Target fingerprint mismatch or unavailable.");
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken); State = ExecutionState.Running;
        try
        {
            await connection.AcquireMigrationLockAsync(transaction, cancellationToken); await connection.ExecuteAsync(candidate.Bytes, transaction, cancellationToken);
            try { await transaction.CommitAsync(cancellationToken); State = ExecutionState.Committed; } catch (Exception ex) { State = ExecutionState.UnknownCommit; throw new UnknownCommitException(ex); }
        }
        catch (UnknownCommitException) { throw; }
        catch (Exception migrationFailure)
        {
            try { await transaction.RollbackAsync(cancellationToken); State = ExecutionState.RolledBack; } catch (Exception rollbackFailure) { State = ExecutionState.UnknownCommit; throw new RollbackFailureException(migrationFailure, rollbackFailure); }
            throw;
        }
    }
    private static string FindRepositoryRoot(string path)
    {
        for (var d = new DirectoryInfo(Path.GetDirectoryName(path)!); d is not null; d = d.Parent) if (Directory.Exists(Path.Combine(d.FullName, ".git"))) return d.FullName;
        throw new InvalidOperationException("Repository root could not be established.");
    }
}
public sealed class NoExecutionPolicy : IExecutionPolicy
{
    public void ValidateAuthorization(RunnerOptions options) => throw new InvalidOperationException("No approved Gate T authorization protocol is defined; execution is disabled.");
    public Task<string?> VerifyActualTargetFingerprintAsync(ITargetConnection connection, CancellationToken cancellationToken) => throw new InvalidOperationException("No approved target fingerprint verification mechanism is defined; execution is disabled.");
}
