namespace PA.Tickets.Domain.Tests;

internal static class PostgreSqlTestEnvironment
{
    private const string OptInVariable = "PA_TICKETS_RUN_POSTGRESQL_TESTS";
    private const string ApprovedTargetVariable = "PA_TICKETS_APPROVED_TEST_TARGET";
    private const string OptInValue = "I_UNDERSTAND_POSTGRESQL_TEST_WRITES";

    public static string EnsureConfigured() => RequireConnectionString(
        Environment.GetEnvironmentVariable("PA_TICKETS_TEST_CONNECTION_STRING"),
        Environment.GetEnvironmentVariable(OptInVariable),
        Environment.GetEnvironmentVariable(ApprovedTargetVariable));

    public static string RequireConnectionString(string? connectionString) => RequireConnectionString(
        connectionString,
        Environment.GetEnvironmentVariable(OptInVariable),
        Environment.GetEnvironmentVariable(ApprovedTargetVariable));

    internal static string RequireConnectionString(string? connectionString, string? optIn, string? approvedTarget)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("PostgreSQL integration tests require PA_TICKETS_TEST_CONNECTION_STRING. Set it in the test process environment; the value is never logged.");
        if (!string.Equals(optIn, OptInValue, StringComparison.Ordinal))
            throw new InvalidOperationException($"PostgreSQL integration tests are disabled by default. Set {OptInVariable}={OptInValue} only for an explicitly approved disposable target.");

        var actualTarget = BuildTargetDescriptor(connectionString);
        var targetParts = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (string.Equals(targetParts.Database, "pa_tickets_test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetParts.Database, "pa_tickets_h_verify", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetParts.Username, "pa_tickets_test_user", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"PostgreSQL integration target verification failed: prohibited database or role in target descriptor; credentials are never logged.");
        if (string.IsNullOrWhiteSpace(approvedTarget) || !string.Equals(approvedTarget.Trim(), actualTarget, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"PostgreSQL integration target verification failed. Set {ApprovedTargetVariable} to the approved host/port/database/user descriptor; credentials are never logged.");
        return connectionString;
    }

    internal static string BuildTargetDescriptor(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Host) || builder.Port <= 0 || string.IsNullOrWhiteSpace(builder.Database) || string.IsNullOrWhiteSpace(builder.Username))
            throw new InvalidOperationException("The PostgreSQL integration connection must include host, port, database, and user; credentials are never logged.");
        if (builder.Host.Contains(',', StringComparison.Ordinal))
            throw new InvalidOperationException("The PostgreSQL integration connection must identify one host; multi-host failover targets are not independently verifiable and credentials are never logged.");
        return $"host={builder.Host};port={builder.Port};database={builder.Database};user={builder.Username}";
    }
}


