namespace PA.Tickets.Domain.Tests;

internal static class PostgreSqlTestEnvironment
{
    public static string EnsureConfigured()
    {
        return RequireConnectionString(Environment.GetEnvironmentVariable("PA_TICKETS_TEST_CONNECTION_STRING"));
    }

    public static string RequireConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests require PA_TICKETS_TEST_CONNECTION_STRING. Set it in the test process environment; the value is never logged.");
        }

        return connectionString;
    }
}
