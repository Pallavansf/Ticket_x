using System.Diagnostics;
using System.Security.Cryptography;

namespace PA.Tickets.LocalE2eReadOnlyRunner;

public sealed record LocalReadOnlyInvocation(string Operation, string ArtifactPath, string ArtifactSha256);

public static class LocalReadOnlyCliPolicy
{
    private static readonly IReadOnlyDictionary<string, (string Path, string Hash)> Allowlist =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT"] = (
                "tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql",
                "0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034"),
            ["S5-03_READ_ONLY_FINGERPRINT"] = (
                "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql",
                "6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342"),
            ["M1_SEQUENCE_SAFETY_PREFLIGHT"] = (
                "Docs/reconciliation/proposed/postgresql/M1_sequence_safety_inspection_REVIEW.sql",
                "526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A"),
            ["M1_LIFECYCLE_REFERENCE_PREFLIGHT"] = (
                "Docs/reconciliation/proposed/postgresql/M1_lifecycle_reference_and_sequence_preflight_REVIEW.sql",
                "C719C6C0204527F262FAB51A3C4138FC09057744990DF242F232BA9FC88AE47D"),
            ["M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT"] = (
                "Docs/reconciliation/proposed/postgresql/M1_permission_catalogue_sequence_preflight_REVIEW.sql",
                "795838E1CE3A02ADFBBE755197DB2228D52ED16C457046F57818E3912DC88609")
        };

    public static bool TryParse(string[] args, out string? operation, out string? error)
    {
        operation = null;
        error = null;
        if (args.Length != 3 || args[0] != "--operation" || args[2] != "--execute-readonly")
        {
            error = "Usage: --operation <LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT|S5-03_READ_ONLY_FINGERPRINT|M1_SEQUENCE_SAFETY_PREFLIGHT|M1_LIFECYCLE_REFERENCE_PREFLIGHT|M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT> --execute-readonly";
            return false;
        }
        if (!Allowlist.ContainsKey(args[1]))
        {
            error = "Operation is not allowlisted.";
            return false;
        }
        operation = args[1];
        return true;
    }

    public static LocalReadOnlyInvocation VerifyPinnedArtifact(string repositoryRoot, string operation)
    {
        if (!Allowlist.TryGetValue(operation, out var pinned))
            throw new InvalidOperationException("Operation is not allowlisted.");
        var root = Path.GetFullPath(repositoryRoot);
        var path = Path.GetFullPath(Path.Combine(root, pinned.Path.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Pinned SQL artifact is missing or outside the repository.");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        if (!string.Equals(hash, pinned.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Pinned SQL artifact hash mismatch; child runner was not started.");
        return new LocalReadOnlyInvocation(operation, pinned.Path, hash);
    }
}

internal static class Program
{
    private const string Confirmation = "RUN_FIXED_READ_ONLY_OPERATION";

    private static int Main(string[] args)
    {
        if (!LocalReadOnlyCliPolicy.TryParse(args, out var operation, out var error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        try
        {
            var root = FindRepositoryRoot(Environment.CurrentDirectory);
            var invocation = LocalReadOnlyCliPolicy.VerifyPinnedArtifact(root, operation!);
            var project = Path.Combine(root, "tests", "PA.Tickets.Domain.Tests", "PA.Tickets.Domain.Tests.csproj");
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                WorkingDirectory = root
            };
            start.ArgumentList.Add("test");
            start.ArgumentList.Add(project);
            start.ArgumentList.Add("--no-restore");
            start.ArgumentList.Add("--filter");
            start.ArgumentList.Add("FullyQualifiedName~LocalE2eReadOnlyCatalogInspectionExecutionTests");
            start.Environment["PA_TICKETS_LOCAL_E2E_READONLY_OPERATION"] = invocation.Operation;
            start.Environment["PA_TICKETS_LOCAL_E2E_READONLY_CONFIRM"] = Confirmation;

            using var child = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the guarded .NET inspection engine.");
            child.WaitForExit();
            if (child.ExitCode != 0)
                Console.Error.WriteLine("Guarded local read-only operation failed; do not retry automatically.");
            return child.ExitCode;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Read-only invocation refused: {exception.Message}");
            return 3;
        }
    }

    private static string FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "PA.Tickets.Domain.Tests", "PA.Tickets.Domain.Tests.csproj")) &&
                File.Exists(Path.Combine(directory.FullName, "tests", "scripts", "postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql")))
                return directory.FullName;
        }
        throw new InvalidOperationException("Run from within the PA_TICKETS repository tree.");
    }
}
