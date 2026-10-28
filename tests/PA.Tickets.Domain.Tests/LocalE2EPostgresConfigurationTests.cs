using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PA.Tickets.Domain.Tests;

public sealed class LocalE2EPostgresConfigurationTests
{
    [Fact]
    public void Explicit_loader_reads_named_value_and_builds_fixed_local_target()
    {
        WithTemporaryRepository("OTHER_SETTING=ignored\nPA_TICKETS_E2E_DB_PASSWORD=offline-test-secret\n", root =>
        {
            var configuration = LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(root);
            var connection = new NpgsqlConnectionStringBuilder(configuration["ConnectionStrings:TicketsPostgres"]);
            Assert.Equal("PostgreSQL", configuration["Database:Provider"]);
            Assert.Equal("::1", connection.Host);
            Assert.Equal(5432, connection.Port);
            Assert.Equal("pa_tickets_codex_e2e", connection.Database);
            Assert.Equal("pa_tickets_codex", connection.Username);
            Assert.Equal("offline-test-secret", connection.Password);
            Assert.Equal(SslMode.Disable, connection.SslMode);
            Assert.False(connection.PersistSecurityInfo);
        });
    }

    [Theory]
    [InlineData("# comment\nPA_TICKETS_E2E_DB_PASSWORD='offline secret'\n")]
    [InlineData("PA_TICKETS_E2E_DB_PASSWORD=offline-test-secret\n")]
    public void Explicit_loader_accepts_common_single_line_forms(string fileContents)
    {
        WithTemporaryRepository(fileContents, root =>
        {
            var configuration = LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(root);
            Assert.Equal("::1", new NpgsqlConnectionStringBuilder(configuration["ConnectionStrings:TicketsPostgres"]).Host);
        });
    }

    [Fact]
    public void Missing_local_secret_file_fails_closed()
    {
        WithTemporaryDirectory(root =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(root));
            Assert.Contains(".env.test.local", error.Message, StringComparison.Ordinal);
            Assert.Contains("PA_TICKETS_E2E_DB_PASSWORD", error.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("OTHER_SETTING=present\n")]
    [InlineData("PA_TICKETS_E2E_DB_PASSWORD=\n")]
    [InlineData("PA_TICKETS_E2E_DB_PASSWORD=first\nPA_TICKETS_E2E_DB_PASSWORD=second\n")]
    [InlineData("PA_TICKETS_E2E_DB_PASSWORD='unterminated\n")]
    public void Invalid_or_ambiguous_local_secret_fails_closed(string fileContents)
    {
        WithTemporaryRepository(fileContents, root =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(root));
            Assert.DoesNotContain("first", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("second", error.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Empty_or_multiline_password_fails_without_echoing_value()
    {
        var settings = CreateDevelopmentSettings();
        foreach (var password in new[] { "", " \t", "first\nsecond" })
        {
            var error = Assert.Throws<InvalidOperationException>(() => { LocalE2EPostgresConfiguration.CreateConfiguration(settings, password); });
            Assert.Contains("PA_TICKETS_E2E_DB_PASSWORD", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("first", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Development_connection_settings_override_base_and_receive_only_file_password()
    {
        WithTemporaryRepository("PA_TICKETS_E2E_DB_PASSWORD=offline-test-secret\n", root =>
        {
            var apiSettings = Path.Combine(root, "src", "PA.Tickets.Api");
            Directory.CreateDirectory(apiSettings);
            File.WriteAllText(Path.Combine(apiSettings, "appsettings.json"), """
                { "Database": { "Provider": "Oracle" }, "ConnectionStrings": { "TicketsPostgres": "Host=127.0.0.1;Port=1;Database=base;Username=base" } }
                """);
            File.WriteAllText(Path.Combine(apiSettings, "appsettings.Development.json"), """
                { "Database": { "Provider": "PostgreSQL" }, "ConnectionStrings": { "TicketsPostgres": "Host=::1;Port=5432;Database=pa_tickets_codex_e2e;Username=pa_tickets_codex;SSL Mode=Disable;Persist Security Info=false" } }
                """);

            var configuration = LocalE2EPostgresConfiguration.LoadFromRepositoryRoot(root);
            var connection = new NpgsqlConnectionStringBuilder(configuration["ConnectionStrings:TicketsPostgres"]);
            Assert.Equal("PostgreSQL", configuration["Database:Provider"]);
            Assert.Equal("::1", connection.Host);
            Assert.Equal(5432, connection.Port);
            Assert.Equal("pa_tickets_codex_e2e", connection.Database);
            Assert.Equal("pa_tickets_codex", connection.Username);
            Assert.Equal("offline-test-secret", connection.Password);
        });
    }

    [Fact]
    public void Completed_connection_string_redaction_never_returns_password()
    {
        const string password = "offline-test-secret";
        var settings = CreateDevelopmentSettings();
        var configuration = LocalE2EPostgresConfiguration.CreateConfiguration(settings, password);

        var redacted = LocalE2EPostgresConfiguration.RedactConnectionString(configuration["ConnectionStrings:TicketsPostgres"]!);

        Assert.DoesNotContain(password, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
        Assert.Contains("pa_tickets_codex_e2e", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_settings_reject_embedded_password_without_echoing_it()
    {
        var settings = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["ConnectionStrings:TicketsPostgres"] = "Host=::1;Port=5432;Database=pa_tickets_codex_e2e;Username=pa_tickets_codex;Password=do-not-echo;SSL Mode=Disable"
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() => LocalE2EPostgresConfiguration.CreateConfiguration(settings, "offline-test-secret"));

        Assert.DoesNotContain("do-not-echo", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("offline-test-secret", error.Message, StringComparison.Ordinal);
    }

    private static void WithTemporaryRepository(string contents, Action<string> action) =>
        WithTemporaryDirectory(root =>
        {
            File.WriteAllText(Path.Combine(root, ".env.test.local"), contents);
            var apiSettings = Path.Combine(root, "src", "PA.Tickets.Api");
            Directory.CreateDirectory(apiSettings);
            File.WriteAllText(Path.Combine(apiSettings, "appsettings.json"), "{ \"Database\": { \"Provider\": \"Oracle\" } }");
            File.WriteAllText(Path.Combine(apiSettings, "appsettings.Development.json"), """
                { "Database": { "Provider": "PostgreSQL" }, "ConnectionStrings": { "TicketsPostgres": "Host=::1;Port=5432;Database=pa_tickets_codex_e2e;Username=pa_tickets_codex;SSL Mode=Disable;Persist Security Info=false" } }
                """);
            action(root);
        });

    private static Microsoft.Extensions.Configuration.IConfiguration CreateDevelopmentSettings() => new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["ConnectionStrings:TicketsPostgres"] = "Host=::1;Port=5432;Database=pa_tickets_codex_e2e;Username=pa_tickets_codex;SSL Mode=Disable;Persist Security Info=false"
        })
        .Build();

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), $"pa-tickets-local-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
