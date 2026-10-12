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
    public async Task Configured_postgresql_test_database_is_reachable()
    {
        var connectionString = PostgreSqlTestEnvironment.EnsureConfigured();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.True(connection.State == System.Data.ConnectionState.Open);
    }
}
