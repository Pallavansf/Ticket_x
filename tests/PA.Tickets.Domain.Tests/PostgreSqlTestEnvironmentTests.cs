using Npgsql;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlTestEnvironmentTests
{
    [Fact]
    public void Missing_postgresql_connection_configuration_is_a_test_failure()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => PostgreSqlTestEnvironment.RequireConnectionString(null));
        Assert.Contains("PA_TICKETS_TEST_CONNECTION_STRING", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSQL_tests_require_explicit_write_opt_in()
    {
        const string connection = "Host=localhost;Port=5432;Database=pa_tickets_test;Username=tester;Password=secret";
        var target = PostgreSqlTestEnvironment.BuildTargetDescriptor(connection);
        var error = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlTestEnvironment.RequireConnectionString(connection, null, target));
        Assert.Contains("PA_TICKETS_RUN_POSTGRESQL_TESTS", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSQL_tests_require_an_exact_independent_target_descriptor()
    {
        const string connection = "Host=localhost;Port=5432;Database=pa_tickets_test;Username=tester;Password=secret";
        var error = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlTestEnvironment.RequireConnectionString(
                connection,
                "I_UNDERSTAND_POSTGRESQL_TEST_WRITES",
                "host=localhost;port=5432;database=pa_tickets_h_verify;user=tester"));
        Assert.Contains("target verification failed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pa_tickets_test", "tester")]
    [InlineData("pa_tickets_h_verify", "tester")]
    [InlineData("pa_tickets_e2e_verify", "pa_tickets_test_user")]
    public void PostgreSQL_tests_reject_prohibited_database_or_role_even_when_descriptor_matches(string database, string user)
    {
        const string optIn = "I_UNDERSTAND_POSTGRESQL_TEST_WRITES";
        var connection = $"Host=localhost;Port=5432;Database={database};Username={user};Password=secret";
        var descriptor = PostgreSqlTestEnvironment.BuildTargetDescriptor(connection);

        var error = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlTestEnvironment.RequireConnectionString(connection, optIn, descriptor));

        Assert.Contains("prohibited", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Target_descriptor_excludes_credentials_and_contains_identity_fields()
    {
        var descriptor = PostgreSqlTestEnvironment.BuildTargetDescriptor(
            "Host=db.example;Port=5433;Database=pa_tickets_test;Username=tester;Password=secret");
        Assert.Equal("host=db.example;port=5433;database=pa_tickets_test;user=tester", descriptor);
        Assert.DoesNotContain("secret", descriptor, StringComparison.Ordinal);
    }

    [Fact]
    public void Target_guard_rejects_multi_host_connection_strings()
    {
        const string connection = "Host=localhost,failover.example;Port=5432;Database=pa_tickets_e2e_verify;Username=postgres;Password=secret";

        var error = Assert.Throws<InvalidOperationException>(
            () => PostgreSqlTestEnvironment.BuildTargetDescriptor(connection));

        Assert.Contains("one host", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_reconciliation_requires_explicit_baseline_and_is_deterministic()
    {
        const string target = "host=localhost;port=5432;database=pa_tickets_e2e_verify;user=postgres";
        var expected = new PostgreSqlSchemaSnapshot(target,
        [new("table", "public", "PA_TICKETS", [new("N_TICKET_ID", "bigint", false, "nextval(...)"), new("S_SUBJECT", "text", false, null)], ["PK_PA_TICKETS"], ["IX_PA_TICKETS_SUBJECT"], [])]);
        var observed = new PostgreSqlSchemaSnapshot(target,
        [new("table", "public", "PA_TICKETS", [new("N_TICKET_ID", "bigint", false, "nextval(...)"), new("S_SUBJECT", "text", true, null)], ["PK_PA_TICKETS"], [], [])]);

        var result = PostgreSqlSchemaReconciler.Compare(new(target, "tests/scripts/postgresql-foundational-ddl.sql"), expected, observed);

        Assert.False(result.IsEquivalent);
        Assert.Equal("tests/scripts/postgresql-foundational-ddl.sql", result.BaselineArtifact);
        Assert.Equal(
            ["MissingColumn:table:public:PA_TICKETS:S_SUBJECT|text|nullable=False|default=<null>", "UnexpectedColumn:table:public:PA_TICKETS:S_SUBJECT|text|nullable=True|default=<null>", "MissingIndex:table:public:PA_TICKETS:IX_PA_TICKETS_SUBJECT"],
            result.Differences.Select(x => $"{x.Kind}:{x.Object}:{x.Detail}"));
    }

    [Fact]
    public void Schema_reconciliation_rejects_target_mismatch_before_comparison()
    {
        var expected = new PostgreSqlSchemaSnapshot("host=other;port=5432;database=pa_tickets_e2e_verify;user=postgres", []);
        var observed = new PostgreSqlSchemaSnapshot("host=other;port=5432;database=pa_tickets_e2e_verify;user=postgres", []);

        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlSchemaReconciler.Compare(
            new("host=localhost;port=5432;database=pa_tickets_e2e_verify;user=postgres", "baseline.sql"), expected, observed));

        Assert.Contains("target identity verification failed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schema_reconciliation_rejects_prohibited_descriptor()
    {
        var expected = new PostgreSqlSchemaSnapshot("host=localhost;port=5432;database=pa_tickets_test;user=tester", []);
        var observed = new PostgreSqlSchemaSnapshot("host=localhost;port=5432;database=pa_tickets_test;user=tester", []);

        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlSchemaReconciler.Compare(
            new("host=localhost;port=5432;database=pa_tickets_test;user=tester", "baseline.sql"), expected, observed));

        Assert.Contains("prohibited", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalog_rows_map_to_sorted_snapshot_metadata()
    {
        var snapshot = PostgreSqlCatalogReader.FromRows("host=localhost;port=5432;database=pa_tickets_e2e_verify;user=postgres", [
            new("table", "public", "PA_TICKETS"),
            new("column", "public", "PA_TICKETS", "S_SUBJECT", "text", true),
            new("column", "public", "PA_TICKETS", "N_TICKET_ID", "bigint", false, "nextval(...)"),
            new("index", "public", "PA_TICKETS", Detail: "IX_SUBJECT"),
            new("constraint", "public", "PA_TICKETS", Detail: "PK_TICKETS")
        ]);

        var table = Assert.Single(snapshot.Objects);
        Assert.Equal(["N_TICKET_ID", "S_SUBJECT"], table.Columns.Select(x => x.Name));
        Assert.Equal(["PK_TICKETS"], table.Constraints);
        Assert.Equal(["IX_SUBJECT"], table.Indexes);
        Assert.Equal("nextval(...)", table.Columns[0].DefaultExpression);
    }

    [Fact]
    public void Catalog_target_factory_requires_explicit_baseline_without_write_opt_in()
    {
        var target = PostgreSqlCatalogReader.CreateTarget(
            "Host=localhost;Port=5432;Database=pa_tickets_e2e_verify;Username=postgres;Password=secret",
            "host=localhost;port=5432;database=pa_tickets_e2e_verify;user=postgres",
            "baseline.sql");

        Assert.Equal("baseline.sql", target.BaselineArtifact);
        Assert.DoesNotContain("secret", target.ApprovedTargetDescriptor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("column")]
    public void Catalog_mapping_fails_closed_for_unsupported_or_undeclared_rows(string kind)
    {
        PostgreSqlCatalogRow[] rows = kind == "unknown"
            ? new[] { new PostgreSqlCatalogRow(kind, "public", "PA_TICKETS") }
            : new[] { new PostgreSqlCatalogRow(kind, "public", "MISSING", "S_VALUE", "text", true) };

        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlCatalogReader.FromRows("target", rows));

        Assert.Contains("catalog metadata", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalog_mapping_rejects_contradictory_duplicate_columns()
    {
        var rows = new[]
        {
            new PostgreSqlCatalogRow("table", "public", "PA_TICKETS"),
            new PostgreSqlCatalogRow("column", "public", "PA_TICKETS", "S_VALUE", "text", true),
            new PostgreSqlCatalogRow("column", "public", "PA_TICKETS", "S_VALUE", "integer", false)
        };

        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlCatalogReader.FromRows("target", rows));

        Assert.Contains("duplicate column", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_preflight_rejects_unverified_historical_baseline_hash_without_provider_access()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlProviderPreflight.Verify(
            Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName,
            new("host=localhost;port=5432;database=pa_tickets_e2e_verify;user=postgres", "tests/scripts/postgresql-foundational-ddl.sql", "5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7", "owner-approval-reference", "separately retained evidence", "READ_ONLY_CATALOG_RECONCILIATION", "OWNER_APPROVED", DateTimeOffset.UtcNow.AddHours(1), "target-evidence-reference", "least-privilege-decision-reference")));

        Assert.Contains("hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_preflight_fails_closed_when_authorization_is_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlProviderPreflight.Verify(Environment.CurrentDirectory, null));
        Assert.Contains("authorization", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_preflight_rejects_reference_without_approval_state_or_expiry()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlProviderPreflight.Verify(
            Environment.CurrentDirectory,
            new("host=localhost;port=5432;database=pa_tickets_e2e_verify;user=postgres", "tests/scripts/postgresql-foundational-ddl.sql", "5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7", "reference-only", "retention", "READ_ONLY_CATALOG_RECONCILIATION", "PENDING", DateTimeOffset.UtcNow.AddHours(1), "", "")));

        Assert.Contains("authorization", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Configured_postgresql_test_database_is_reachable()
    {
        var connectionString = PostgreSqlTestEnvironment.EnsureConfigured();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.True(connection.State == System.Data.ConnectionState.Open);
    }
}
