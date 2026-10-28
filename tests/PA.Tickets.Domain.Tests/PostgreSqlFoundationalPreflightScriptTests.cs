using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlFoundationalPreflightScriptTests
{
    private const string ScriptRelativePath = "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql";
    private const string RunnerRelativePath = "tests/scripts/invoke-postgresql-foundational-fingerprint.ps1";
    private const string ExpectedSha256 = "2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8";

    [Fact]
    public void Successor_preflight_is_pinned_to_the_owner_identified_target_and_stays_read_only()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ScriptRelativePath));

        Assert.Contains("TARGET: host localhost", script, StringComparison.Ordinal);
        Assert.Contains("database pa_tickets_codex_e2e, role pa_tickets_codex", script, StringComparison.Ordinal);
        Assert.Contains("current_database() <> 'pa_tickets_codex_e2e'", script, StringComparison.Ordinal);
        Assert.Contains("current_user <> 'pa_tickets_codex'", script, StringComparison.Ordinal);
        Assert.Contains("inet_server_port() <> 5432", script, StringComparison.Ordinal);
        Assert.Contains("inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr() = '::1'::inet", script, StringComparison.Ordinal);
        Assert.DoesNotContain("pa_tickets_e2e_verify", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("current_user <> 'postgres'", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("SET TRANSACTION READ ONLY;", script, StringComparison.Ordinal);
        Assert.Contains("current_setting('transaction_read_only') <> 'on'", script, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK;", script, StringComparison.Ordinal);
        Assert.Contains("S5-01 remains blocked", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(
            new Regex(@"(?im)^\s*(CREATE|ALTER|DROP|TRUNCATE|INSERT|UPDATE|DELETE|GRANT|REVOKE)\b"),
            script);
    }

    [Fact]
    public async Task Runner_defaults_to_offline_validation_and_emits_sanitized_candidate_evidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = FindRepositoryRoot();
        var result = await RunPowerShell(root, "-File", Path.Combine(root, RunnerRelativePath));

        Assert.Equal(0, result.ExitCode);
        using var evidence = JsonDocument.Parse(result.StandardOutput.Trim());
        var json = evidence.RootElement;
        Assert.Equal("Validate", json.GetProperty("mode").GetString());
        Assert.Equal("S5-03_READ_ONLY_FINGERPRINT", json.GetProperty("operation").GetString());
        Assert.Equal(ScriptRelativePath, json.GetProperty("artifact").GetString());
        Assert.Equal(ExpectedSha256, json.GetProperty("sha256").GetString());
        Assert.Equal("NOT_PROVIDED", json.GetProperty("connectionProfile").GetString());
        Assert.Equal("NOT_PROVIDED", json.GetProperty("authorizationProfile").GetString());
        Assert.False(json.GetProperty("databaseClientInvoked").GetBoolean());
        Assert.False(json.GetProperty("executionEligible").GetBoolean());
    }

    [Fact]
    public async Task Runner_execute_mode_fails_closed_before_invoking_a_database_client()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = FindRepositoryRoot();
        var result = await RunPowerShell(root, "-File", Path.Combine(root, RunnerRelativePath), "-Mode", "Execute");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Owner-controlled local identity evidence is not provisioned", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No database client was invoked", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_validates_exact_target_and_keeps_supplied_trust_and_authorization_unverified()
    {
        var result = await RunWithProfiles();

        Assert.True(result.ExitCode == 0, result.StandardError);
        using var evidence = JsonDocument.Parse(result.StandardOutput.Trim());
        var json = evidence.RootElement;
        Assert.Equal("EXACT_TARGET; TLS_PIN_SHAPE_VALID; TRUST_UNVERIFIED", json.GetProperty("connectionProfile").GetString());
        Assert.Equal("STRUCTURALLY_VALID; SIGNATURE, IDENTITY, REVOCATION_AUTHORITY UNVERIFIED", json.GetProperty("authorizationProfile").GetString());
        Assert.False(json.GetProperty("executionEligible").GetBoolean());
        Assert.False(json.GetProperty("databaseClientInvoked").GetBoolean());
    }

    [Theory]
    [InlineData("missing-host")]
    [InlineData("missing-port")]
    [InlineData("wrong-host")]
    [InlineData("wrong-port")]
    [InlineData("wrong-database")]
    [InlineData("protected-database")]
    [InlineData("protected-role")]
    public async Task Runner_rejects_incomplete_mismatched_and_protected_connection_targets(string change)
    {
        var result = await RunWithProfiles((connection, _) =>
        {
            switch (change)
            {
                case "missing-host": connection.Remove("host"); break;
                case "missing-port": connection.Remove("port"); break;
                case "wrong-host": connection["host"] = "db.example"; break;
                case "wrong-port": connection["port"] = 5433; break;
                case "wrong-database": connection["database"] = "other_database"; break;
                case "protected-database": connection["database"] = "pa_tickets_test"; break;
                case "protected-role": connection["role"] = "pa_tickets_test_user"; break;
            }
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("psql", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_rejects_missing_server_identity_trust_material()
    {
        var result = await RunWithProfiles((connection, _) => connection.Remove("serverIdentity"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("serverIdentity", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_rejects_connection_profile_without_external_trust_anchor_reference()
    {
        var result = await RunWithProfiles((connection, _) =>
        {
            var identity = (Dictionary<string, object?>)connection["serverIdentity"]!;
            identity["trustAnchorReference"] = " ";
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("trust-anchor reference", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_accepts_consistent_ipv6_loopback_peer_evidence_as_unverified_only()
    {
        var result = await RunWithProfiles((connection, _) => connection["serverIdentity"] = ValidLocalPeerEvidence());

        Assert.True(result.ExitCode == 0, result.StandardError);
        using var evidence = JsonDocument.Parse(result.StandardOutput.Trim());
        var json = evidence.RootElement;
        Assert.Contains("PEER_EVIDENCE_STRUCTURALLY_CONSISTENT", json.GetProperty("connectionProfile").GetString());
        Assert.Contains("TRUST, SIGNATURE, PROCESS PROVENANCE AND REVOCATION UNVERIFIED", json.GetProperty("connectionProfile").GetString());
        Assert.False(json.GetProperty("executionEligible").GetBoolean());
        Assert.False(json.GetProperty("databaseClientInvoked").GetBoolean());
    }

    [Theory]
    [InlineData("listener-pid")]
    [InlineData("service-process")]
    [InlineData("image-path")]
    [InlineData("image-hash")]
    [InlineData("service-account")]
    public async Task Runner_rejects_mismatched_local_listener_process_identity(string mismatch)
    {
        var result = await RunWithProfiles((connection, _) =>
        {
            var peer = ValidLocalPeerEvidence();
            switch (mismatch)
            {
                case "listener-pid": peer["listenerProcessId"] = 4321; break;
                case "service-process": peer["serviceProcessId"] = 9753; peer["processAncestorIds"] = new[] { 1000, 1001 }; break;
                case "image-path": peer["processImagePath"] = @"C:\Other\postgres.exe"; break;
                case "image-hash": peer["processImageSha256"] = new string('F', 64); break;
                case "service-account": peer["serviceAccount"] = @"NT AUTHORITY\NetworkService"; break;
            }
            connection["serverIdentity"] = peer;
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("psql", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("InterNetwork", "127.0.0.1")]
    [InlineData("InterNetworkV6", "::")]
    public async Task Runner_rejects_unsupported_or_non_loopback_address_families(string family, string address)
    {
        var result = await RunWithProfiles((connection, _) =>
        {
            var peer = ValidLocalPeerEvidence();
            peer["addressFamily"] = family;
            peer["localAddress"] = address;
            connection["serverIdentity"] = peer;
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("IPv6 loopback endpoint ::1", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_rejects_stale_local_peer_evidence()
    {
        var result = await RunWithProfiles((connection, _) =>
        {
            var peer = ValidLocalPeerEvidence();
            peer["capturedAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O");
            connection["serverIdentity"] = peer;
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("peer evidence is stale", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("missing-anchor")]
    [InlineData("missing-signature")]
    [InlineData("revoked")]
    public async Task Runner_rejects_missing_or_revoked_local_peer_trust_evidence(string change)
    {
        var result = await RunWithProfiles((connection, _) =>
        {
            var peer = ValidLocalPeerEvidence();
            switch (change)
            {
                case "missing-anchor": peer.Remove("trustAnchorReference"); break;
                case "missing-signature": peer.Remove("evidenceSignatureReference"); break;
                case "revoked": peer["revocationStatus"] = "REVOKED"; break;
            }
            connection["serverIdentity"] = peer;
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("psql", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_rejects_environment_target_overrides_without_reading_credentials()
    {
        var result = await RunWithProfiles(environment: new Dictionary<string, string> { ["PGDATABASE"] = "other_database" });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PGDATABASE", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("other_database", result.StandardError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("wrong-operation")]
    [InlineData("wrong-artifact")]
    [InlineData("missing-exclusion")]
    public async Task Runner_rejects_invalid_authorization_profile_facts(string change)
    {
        var result = await RunWithProfiles((_, authorization) =>
        {
            switch (change)
            {
                case "expired": authorization["expiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"); break;
                case "revoked": authorization["revocationStatus"] = "REVOKED"; break;
                case "wrong-operation": authorization["operation"] = "S5-10_SCHEMA_APPLICATION"; break;
                case "wrong-artifact": authorization["artifactSha256"] = new string('0', 64); break;
                case "missing-exclusion": authorization["excludedOperations"] = new[] { "DROP_DATABASE" }; break;
            }
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("psql", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_rejects_execute_even_when_profiles_are_structurally_valid()
    {
        var result = await RunWithProfiles(mode: "Execute");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Owner-controlled local identity evidence is not provisioned", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No database client was invoked", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_rejects_changed_candidate_bytes_without_database_access()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), $"pa-tickets-fingerprint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "tests", "scripts"));
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, ScriptRelativePath),
                File.ReadAllText(Path.Combine(FindRepositoryRoot(), ScriptRelativePath)) + Environment.NewLine);
            var runner = Path.Combine(FindRepositoryRoot(), RunnerRelativePath);
            var result = await RunPowerShell(root, "-File", runner, "-RepositoryRoot", root);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("hash does not match", result.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("psql", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Runner_has_no_implicit_environment_or_database_client_execution_path()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), RunnerRelativePath));

        Assert.Contains("$Mode = 'Validate'", script, StringComparison.Ordinal);
        Assert.Contains("S5-03_READ_ONLY_FINGERPRINT", script, StringComparison.Ordinal);
        Assert.Contains("executionEligible = $false", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$env:", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("psql", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", script, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ScriptRelativePath)))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository-local PostgreSQL preflight script.");
    }

    private static Dictionary<string, object?> ValidLocalPeerEvidence()
    {
        var capturedAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        const string imagePath = @"C:\Program Files\PostgreSQL\18\bin\postgres.exe";
        const string dataDirectory = @"C:\Program Files\PostgreSQL\18\data";
        const string configurationFile = @"C:\Program Files\PostgreSQL\18\data\postgresql.conf";
        const string serviceName = "postgresql-x64-18";
        const string serviceAccount = @"NT SERVICE\postgresql-x64-18";
        const string imageHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        const string signerThumbprint = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        return new Dictionary<string, object?>
        {
            ["kind"] = "WINDOWS_IPV6_LOOPBACK_PROCESS",
            ["addressFamily"] = "InterNetworkV6",
            ["localAddress"] = "::1",
            ["localPort"] = 5432,
            ["listenerProcessId"] = 2468,
            ["processId"] = 2468,
            ["serviceProcessId"] = 1001,
            ["processAncestorIds"] = new[] { 1000, 1001 },
            ["processStartTimeUtc"] = capturedAt.AddDays(-1).ToString("O"),
            ["processImagePath"] = imagePath,
            ["processImageSha256"] = imageHash,
            ["processSignerThumbprint"] = signerThumbprint,
            ["serviceName"] = serviceName,
            ["serviceAccount"] = serviceAccount,
            ["dataDirectory"] = dataDirectory,
            ["configurationFile"] = configurationFile,
            ["expectedProcessImagePath"] = imagePath,
            ["expectedImageSha256"] = imageHash,
            ["expectedSignerThumbprint"] = signerThumbprint,
            ["expectedServiceName"] = serviceName,
            ["expectedServiceAccount"] = serviceAccount,
            ["expectedDataDirectory"] = dataDirectory,
            ["expectedConfigurationFile"] = configurationFile,
            ["capturedAtUtc"] = capturedAt.ToString("O"),
            ["expiresAtUtc"] = capturedAt.AddMinutes(2).ToString("O"),
            ["trustAnchorSha256"] = new string('C', 64),
            ["trustAnchorReference"] = "untrusted-test-anchor",
            ["signedPolicyReference"] = "untrusted-test-policy",
            ["evidenceSignatureReference"] = "untrusted-test-signature",
            ["revocationStatus"] = "ACTIVE",
            ["revocationEvidenceReference"] = "untrusted-test-revocation",
            ["targetHost"] = "localhost",
            ["targetPort"] = 5432,
            ["targetDatabase"] = "pa_tickets_codex_e2e",
            ["targetRole"] = "pa_tickets_codex",
            ["artifactPath"] = ScriptRelativePath,
            ["artifactSha256"] = ExpectedSha256,
            ["authorizationProfileSha256"] = "PENDING_TEST_BINDING"
        };
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunPowerShell(
        string repositoryRoot,
        params string[] arguments) => await RunPowerShell(repositoryRoot, arguments, null);

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunPowerShell(
        string repositoryRoot,
        string[] arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var pair in environment)
                start.Environment[pair.Key] = pair.Value;

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the offline PowerShell runner test.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await standardOutput, await standardError);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunWithProfiles(
        Action<Dictionary<string, object?>, Dictionary<string, object?>>? mutate = null,
        IReadOnlyDictionary<string, string>? environment = null,
        string mode = "Validate")
    {
        var root = FindRepositoryRoot();
        var temp = Path.Combine(Path.GetTempPath(), $"pa-tickets-auth-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var connection = new Dictionary<string, object?>
            {
                ["host"] = "localhost",
                ["port"] = 5432,
                ["database"] = "pa_tickets_codex_e2e",
                ["role"] = "pa_tickets_codex",
                ["serverIdentity"] = new Dictionary<string, object?>
                {
                    ["kind"] = "TLS_CERT_SHA256",
                    ["sha256"] = new string('A', 64),
                    ["trustAnchorReference"] = "untrusted-test-reference"
                }
            };
            var authorization = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["operation"] = "S5-03_READ_ONLY_FINGERPRINT",
                ["target"] = new Dictionary<string, object?>
                {
                    ["host"] = "localhost",
                    ["port"] = 5432,
                    ["database"] = "pa_tickets_codex_e2e",
                    ["role"] = "pa_tickets_codex"
                },
                ["artifactPath"] = ScriptRelativePath,
                ["artifactSha256"] = ExpectedSha256,
                ["validFromUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
                ["expiresAtUtc"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                ["revocationStatus"] = "ACTIVE",
                ["revocationEvidenceReference"] = "untrusted-test-reference",
                ["operatorIdentityReference"] = "untrusted-test-reference",
                ["approverIdentityReference"] = "untrusted-test-reference",
                ["approvalReference"] = "untrusted-test-reference",
                ["signatureReference"] = "untrusted-test-reference",
                ["excludedOperations"] = new[]
                {
                    "pa_tickets_test", "pa_tickets_h_verify", "pa_tickets_test_user",
                    "migration_026", "DROP_DATABASE", "SCHEMA_RESET", "BROAD_CLEANUP"
                }
            };
            mutate?.Invoke(connection, authorization);
            var authorizationJson = JsonSerializer.Serialize(authorization);
            if (connection.TryGetValue("serverIdentity", out var identityValue) &&
                identityValue is Dictionary<string, object?> peer &&
                Equals(peer.GetValueOrDefault("kind"), "WINDOWS_IPV6_LOOPBACK_PROCESS"))
            {
                peer["authorizationProfileSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationJson)));
            }
            var connectionPath = Path.Combine(temp, "connection-profile.json");
            var authorizationPath = Path.Combine(temp, "authorization-profile.json");
            await File.WriteAllTextAsync(connectionPath, JsonSerializer.Serialize(connection));
            await File.WriteAllTextAsync(authorizationPath, authorizationJson);
            return await RunPowerShell(root,
                new[]
                {
                    "-File", Path.Combine(root, RunnerRelativePath),
                    "-Mode", mode,
                    "-RepositoryRoot", root,
                    "-ConnectionProfilePath", connectionPath,
                    "-AuthorizationProfilePath", authorizationPath
                },
                environment);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
