using System.Security.Cryptography;
using PA.Tickets.LocalE2eReadOnlyRunner;

namespace PA.Tickets.Domain.Tests;

public sealed class LocalE2eReadOnlyCliTests
{
    [Theory]
    [InlineData("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT")]
    [InlineData("S5-03_READ_ONLY_FINGERPRINT")]
    [InlineData("M1_SEQUENCE_SAFETY_PREFLIGHT")]
    [InlineData("M1_LIFECYCLE_REFERENCE_PREFLIGHT")]
    [InlineData("M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT")]
    public void Parser_accepts_only_named_operation_with_explicit_flag(string operation)
    {
        Assert.True(LocalReadOnlyCliPolicy.TryParse(
            ["--operation", operation, "--execute-readonly"], out var actual, out var error));
        Assert.Equal(operation, actual);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("missing-flag")]
    [InlineData("unknown-operation")]
    [InlineData("extra-sql")]
    [InlineData("target-override")]
    public void Parser_rejects_missing_confirmation_unknown_operation_and_extra_arguments(string scenario)
    {
        var args = scenario switch
        {
            "empty" => [],
            "missing-flag" => new[] { "--operation", "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT" },
            "unknown-operation" => new[] { "--operation", "arbitrary", "--execute-readonly" },
            "extra-sql" => new[] { "--operation", "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT", "--execute-readonly", "--sql", "x" },
            "target-override" => new[] { "--target", "other-db", "--execute-readonly" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        Assert.False(LocalReadOnlyCliPolicy.TryParse(args, out var operation, out var error));
        Assert.Null(operation);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT", "0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034")]
    [InlineData("S5-03_READ_ONLY_FINGERPRINT", "6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342")]
    [InlineData("M1_SEQUENCE_SAFETY_PREFLIGHT", "526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A")]
    [InlineData("M1_LIFECYCLE_REFERENCE_PREFLIGHT", "C719C6C0204527F262FAB51A3C4138FC09057744990DF242F232BA9FC88AE47D")]
    [InlineData("M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT", "795838E1CE3A02ADFBBE755197DB2228D52ED16C457046F57818E3912DC88609")]
    public void Exact_checked_in_artifact_matches_allowlist(string operation, string expectedHash)
    {
        var root = FindRepositoryRoot();
        var result = LocalReadOnlyCliPolicy.VerifyPinnedArtifact(root, operation);
        Assert.Equal(expectedHash, result.ArtifactSha256);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, result.ArtifactPath.Replace('/', Path.DirectorySeparatorChar))))));
    }

    [Fact]
    public void Altered_artifact_is_rejected_before_child_launch()
    {
        const string operation = "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT";
        var root = Path.Combine(Path.GetTempPath(), "pa-cli-artifact-" + Guid.NewGuid().ToString("N"));
        var artifactPath = Path.Combine(root, "tests", "scripts", "postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        try
        {
            File.WriteAllText(artifactPath, "altered");
            Assert.Throws<InvalidOperationException>(() => LocalReadOnlyCliPolicy.VerifyPinnedArtifact(root, operation));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "scripts", "postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
