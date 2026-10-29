using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlExistingDatabaseFingerprintArtifactTests
{
    private const string OriginalPath = "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql";
    private const string CandidatePath = "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql";
    private const string OriginalSha256 = "2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8";
    private const string HistoricalBaselineSha256 = "4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19";

    [Fact]
    public void Candidate_preserves_approved_artifact_and_adds_only_a_review_successor()
    {
        var root = FindRepositoryRoot();
        Assert.Equal(OriginalSha256, HashFile(Path.Combine(root, OriginalPath)));
        var candidate = ReadCandidate(root);

        Assert.Contains("EXISTING_DB_REVIEW", CandidatePath, StringComparison.Ordinal);
        Assert.Contains(HistoricalBaselineSha256, candidate, StringComparison.Ordinal);
        Assert.Contains("HISTORICAL_BASELINE_CANDIDATE_NOT_ADOPTED", candidate, StringComparison.Ordinal);
        Assert.DoesNotContain("Baseline not established: existing public objects", candidate, StringComparison.Ordinal);
        Assert.Contains("inet_server_addr() IS DISTINCT FROM '::1'::inet", candidate, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "EXPECTED_PRESENT")]
    [InlineData(true, "EXPECTED_PRESENT")]
    public void Inventory_classifies_populated_and_empty_observation_sets(bool populated, string expectedStatus)
    {
        var candidate = ReadCandidate(FindRepositoryRoot());
        var expected = ParseExpectedObjects(candidate);
        var observed = populated ? new HashSet<string>(expected, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        var statuses = Compare(expected, observed);

        if (populated)
            Assert.Contains(expectedStatus, statuses.Values);
        else
            Assert.All(statuses.Values, status => Assert.Equal("MISSING_EXPECTED", status));
    }

    [Fact]
    public void Populated_inventory_can_include_unexpected_objects()
    {
        var candidate = ReadCandidate(FindRepositoryRoot());
        var expected = ParseExpectedObjects(candidate);
        var observed = new HashSet<string>(expected, StringComparer.Ordinal) { "TABLE:UNEXPECTED_LOCAL_OBJECT" };

        Assert.Equal("UNEXPECTED_OBSERVED", Compare(expected, observed)["TABLE:UNEXPECTED_LOCAL_OBJECT"]);
    }

    [Fact]
    public void Inventory_reports_unexpected_and_missing_objects_without_aborting()
    {
        var candidate = ReadCandidate(FindRepositoryRoot());
        var expected = ParseExpectedObjects(candidate);
        var observed = new HashSet<string>(expected, StringComparer.Ordinal);
        observed.Remove("TABLE:PA_TICKETS");
        observed.Add("TABLE:UNEXPECTED_LOCAL_OBJECT");

        var statuses = Compare(expected, observed);

        Assert.Equal("MISSING_EXPECTED", statuses["TABLE:PA_TICKETS"]);
        Assert.Equal("UNEXPECTED_OBSERVED", statuses["TABLE:UNEXPECTED_LOCAL_OBJECT"]);
        Assert.DoesNotContain("RAISE EXCEPTION", ExtractInventoryQuery(candidate), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_history_is_always_reported_unknown_and_ledger_presence_is_descriptive()
    {
        var candidate = ReadCandidate(FindRepositoryRoot());
        var historySection = candidate[candidate.IndexOf("-- Migration history remains UNKNOWN", StringComparison.Ordinal)..];
        historySection = historySection[..historySection.IndexOf("-- Read-only duplicate checks", StringComparison.Ordinal)];

        Assert.Contains("'UNKNOWN'::text AS migration_history_status", historySection, StringComparison.Ordinal);
        Assert.Contains("ledger_object_present", historySection, StringComparison.Ordinal);
        Assert.DoesNotContain("RAISE EXCEPTION", historySection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("S_MIGRATION_SHA256", historySection, StringComparison.Ordinal);
    }

    [Fact]
    public void Candidate_inventories_catalog_contract_without_mutations_or_sequence_allocation()
    {
        var candidate = ReadCandidate(FindRepositoryRoot());
        var normalized = StripSqlComments(candidate);

        Assert.Contains("pg_catalog.pg_attribute", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pg_get_constraintdef", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pg_indexes", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pg_catalog.pg_sequence", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("owned_by_relation", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schema_acl", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*(CREATE|ALTER|DROP|TRUNCATE|INSERT|UPDATE|DELETE|GRANT|REVOKE)\b"), normalized);
        Assert.DoesNotMatch(new Regex(@"(?i)\b(nextval|setval|currval)\s*\("), normalized);
        Assert.Contains("SET TRANSACTION READ ONLY;", candidate, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK;", candidate, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_shape_and_inventory_order_are_deterministic()
    {
        var candidate = ReadCandidate(FindRepositoryRoot());
        var inventory = ExtractInventoryQuery(candidate);
        Assert.Contains("ORDER BY object_kind, object_name, inventory_status", inventory, StringComparison.Ordinal);
        Assert.Contains("ORDER BY ns.nspname, cls.relname, att.attnum", candidate, StringComparison.Ordinal);
        Assert.Contains("EXPECTED_PRESENT", inventory, StringComparison.Ordinal);
        Assert.Contains("MISSING_EXPECTED", inventory, StringComparison.Ordinal);
        Assert.Contains("UNEXPECTED_OBSERVED", inventory, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Compare(HashSet<string> expected, HashSet<string> observed)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in expected.Union(observed, StringComparer.Ordinal))
            result[item] = expected.Contains(item)
                ? observed.Contains(item) ? "EXPECTED_PRESENT" : "MISSING_EXPECTED"
                : "UNEXPECTED_OBSERVED";
        return result;
    }

    private static HashSet<string> ParseExpectedObjects(string candidate)
    {
        var inventory = ExtractInventoryQuery(candidate);
        return Regex.Matches(inventory, @"\('(?<kind>TABLE|SEQUENCE|INDEX)',\s*'(?<name>[^']+)'\)")
            .Select(match => $"{match.Groups["kind"].Value}:{match.Groups["name"].Value}")
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ExtractInventoryQuery(string candidate)
    {
        var start = candidate.IndexOf("WITH expected(object_kind, object_name)", StringComparison.Ordinal);
        var end = candidate.IndexOf("-- Ledger presence is reported separately", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Expected-object comparison query was not found.");
        return candidate[start..end];
    }

    private static string StripSqlComments(string sql) => Regex.Replace(sql, @"(?m)^\s*--.*$", string.Empty);
    private static string ReadCandidate(string root) => File.ReadAllText(Path.Combine(root, CandidatePath));
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, CandidatePath)) && File.Exists(Path.Combine(directory.FullName, OriginalPath)))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the review-only PostgreSQL inventory candidate.");
    }
}
