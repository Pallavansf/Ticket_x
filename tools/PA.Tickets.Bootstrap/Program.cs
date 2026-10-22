using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

if (args.Length != 0 || Console.IsInputRedirected)
{
    Console.Error.WriteLine("Run interactively without command-line arguments.");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("PA_TICKETS_BOOTSTRAP_CONNECTION_STRING");
var expectedDatabase = Environment.GetEnvironmentVariable("PA_TICKETS_BOOTSTRAP_EXPECTED_DATABASE");
var expectedRole = Environment.GetEnvironmentVariable("PA_TICKETS_BOOTSTRAP_EXPECTED_ROLE");
if (string.IsNullOrWhiteSpace(connectionString) ||
    string.IsNullOrWhiteSpace(expectedDatabase) ||
    string.IsNullOrWhiteSpace(expectedRole))
{
    Console.Error.WriteLine("Bootstrap requires process-only connection, expected database, and expected role configuration.");
    return 2;
}

if (!string.Equals(Environment.GetEnvironmentVariable("Database__Provider"), "PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("This one-shot utility currently supports PostgreSQL only.");
    return 2;
}

var options = new DbContextOptionsBuilder<TicketsDbContext>().UseNpgsql(connectionString).Options;
await using var db = new TicketsDbContext(options);
try
{
    await db.Database.OpenConnectionAsync();
    await using var identityCommand = db.Database.GetDbConnection().CreateCommand();
    identityCommand.CommandText = "SELECT current_database() || '|' || current_user";
    var liveIdentity = (string?)await identityCommand.ExecuteScalarAsync();
    var expectedIdentity = $"{expectedDatabase}|{expectedRole}";
    if (!string.Equals(liveIdentity, expectedIdentity, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Connected database identity does not match the explicitly configured target; no changes made.");
        return 3;
    }
    Console.WriteLine($"Verified target: {liveIdentity}");

    Console.Write("Account login name: ");
    var loginName = Console.ReadLine() ?? "";
    Console.Write("Account display name: ");
    var displayName = Console.ReadLine() ?? "";
    Console.Write("Account email (optional): ");
    var email = Console.ReadLine();
    Console.Write("Type the exact confirmation INITIALIZE SUPER ADMIN <database> <login>: ");
    if (!string.Equals(Console.ReadLine(), $"INITIALIZE SUPER ADMIN {expectedDatabase} {loginName.Trim()}", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Confirmation did not match; no changes made.");
        return 4;
    }

    var password = ReadSecret("Temporary password (16+ characters): ");
    var confirmation = ReadSecret("Repeat temporary password: ");
    if (!string.Equals(password, confirmation, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Temporary passwords did not match; no changes made.");
        return 4;
    }

    var capabilities = PostgreSqlProviderCapabilities.Instance;
    var provisioner = new InitialAdministratorProvisioner(
        db,
        capabilities,
        new AuditChainCoordinator(capabilities),
        TimeProvider.System,
        new PasswordHasher<AdministrationUserRow>());
    await provisioner.ProvisionAsync(loginName, displayName, email, password, CancellationToken.None);
    password = "";
    confirmation = "";
    Console.WriteLine("Initial Super Admin provisioned. The account must change this password before any other API operation.");
    return 0;
}
catch (Exception exception) when (exception is DbException or DbUpdateException or InvalidOperationException
    or ArgumentException or CryptographicException or OperationCanceledException or TimeoutException)
{
    Console.Error.WriteLine($"Bootstrap failed ({exception.GetType().Name}); verify target state before retrying. Credential values were not emitted.");
    return 1;
}

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return new string(chars.ToArray());
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
            continue;
        }
        if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
    }
}
