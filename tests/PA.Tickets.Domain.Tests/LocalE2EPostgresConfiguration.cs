using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PA.Tickets.Domain.Tests;

/// <summary>
/// Explicit local-only configuration for the dedicated PostgreSQL E2E target.
/// This type is never called by test startup; callers must opt into loading it.
/// It only prepares configuration and never opens a connection.
/// </summary>
internal static class LocalE2EPostgresConfiguration
{
    private const string PasswordVariable = "PA_TICKETS_E2E_DB_PASSWORD";
    private const string LocalSecretFile = ".env.test.local";
    private const string BaseSettingsFile = "src/PA.Tickets.Api/appsettings.json";
    private const string DevelopmentSettingsFile = "src/PA.Tickets.Api/appsettings.Development.json";

    internal static IConfiguration LoadFromRepositoryRoot(string repositoryRoot)
        => LoadFromRepositoryRoot(repositoryRoot, "TicketsPostgres", PasswordVariable, "pa_tickets_codex");

    private static IConfiguration LoadFromRepositoryRoot(string repositoryRoot, string connectionName, string passwordVariable, string expectedRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var root = Path.GetFullPath(repositoryRoot);
        var secretPath = Path.Combine(root, LocalSecretFile);
        if (!File.Exists(secretPath))
        {
            throw new InvalidOperationException(
                $"Local PostgreSQL E2E configuration is missing {LocalSecretFile} or {passwordVariable}. " +
                "Create the local-only file and set the required variable; the credential value is never logged.");
        }
        if ((File.GetAttributes(secretPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"Local PostgreSQL E2E configuration rejects a reparse-point {LocalSecretFile}; the credential value is never logged.");
        }
        var settings = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, BaseSettingsFile), optional: false, reloadOnChange: false)
            .AddJsonFile(Path.Combine(root, DevelopmentSettingsFile), optional: false, reloadOnChange: false)
            .Build();

        return CreateConfiguration(settings, ReadPassword(secretPath, passwordVariable), connectionName, passwordVariable, expectedRole);
    }

    internal static IConfiguration CreateConfiguration(IConfiguration settings, string password)
        => CreateConfiguration(settings, password, "TicketsPostgres", PasswordVariable, "pa_tickets_codex");

    private static IConfiguration CreateConfiguration(IConfiguration settings, string password, string connectionName, string passwordVariable, string expectedRole)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(password) || password.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException(
                $"Local PostgreSQL E2E configuration requires a non-empty single-line {passwordVariable}; the credential value is never logged.");
        }

        var provider = settings["Database:Provider"];
        if (!string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Local PostgreSQL E2E configuration requires Database:Provider=PostgreSQL.");

        var configuredConnectionString = settings.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
            throw new InvalidOperationException($"Local PostgreSQL E2E configuration requires ConnectionStrings:{connectionName} in Development settings.");

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(configuredConnectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Local PostgreSQL E2E ConnectionStrings configuration is invalid.");
        }

        if (!string.IsNullOrEmpty(builder.Password))
            throw new InvalidOperationException("Local PostgreSQL E2E settings must not contain a password; use the local-only credential file.");
        if (!string.Equals(builder.Host, "::1", StringComparison.OrdinalIgnoreCase) ||
            builder.Port != 5432 ||
            !string.Equals(builder.Database, "pa_tickets_codex_e2e", StringComparison.Ordinal) ||
            !string.Equals(builder.Username, expectedRole, StringComparison.Ordinal) ||
            builder.SslMode != SslMode.Disable)
        {
            throw new InvalidOperationException("Local PostgreSQL E2E settings do not match the approved fixed IPv6 loopback target.");
        }

        builder.Password = password;
        builder.PersistSecurityInfo = false;
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider,
                ["ConnectionStrings:TicketsPostgres"] = builder.ConnectionString
            })
            .Build();
    }

    internal static string RedactConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Password = "[REDACTED]",
                PersistSecurityInfo = false
            };
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            return "[INVALID CONNECTION STRING REDACTED]";
        }
    }

    private static string ReadPassword(string secretPath, string passwordVariable)
    {
        string? password = null;
        foreach (var originalLine in File.ReadLines(secretPath))
        {
            var line = originalLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator < 0 || !string.Equals(line[..separator].Trim(), passwordVariable, StringComparison.OrdinalIgnoreCase)) continue;
            if (password is not null)
                throw new InvalidOperationException($"Local PostgreSQL E2E configuration contains duplicate {passwordVariable} entries; the credential value is never logged.");
            var value = line[(separator + 1)..].Trim();
            if (value.Length > 0 && (value[0] == '\'' || value[0] == '"'))
            {
                var quote = value[0];
                if (value.Length < 2 || value[^1] != quote)
                    throw new InvalidOperationException($"Local PostgreSQL E2E configuration has malformed quoting for {passwordVariable}; the credential value is never logged.");
                value = value[1..^1];
            }
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                throw new InvalidOperationException($"Local PostgreSQL E2E configuration has an empty or invalid {passwordVariable}; the credential value is never logged.");
            password = value;
        }
        return password ?? throw new InvalidOperationException(
            $"Local PostgreSQL E2E configuration does not define {passwordVariable} in {LocalSecretFile}; the credential value is never logged.");
    }
}
