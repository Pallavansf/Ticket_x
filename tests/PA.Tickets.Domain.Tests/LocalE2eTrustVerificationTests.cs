using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PA.Tickets.Domain.Tests;

public sealed class LocalE2eTrustVerificationTests
{
    private static string ModulePath => Path.Combine(FindRepositoryRoot(), "tests", "scripts", "LocalE2eTrust.psm1");

    [Fact]
    public async Task Detached_signature_verifies_with_test_only_certificate()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=PA-TICKETS-ISOLATED-TEST-ONLY", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(5));
        var payload = Encoding.UTF8.GetBytes("isolated test payload");
        var signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var certPath = Path.Combine(Path.GetTempPath(), $"pa-tickets-test-cert-{Guid.NewGuid():N}.cer");
        try
        {
            await File.WriteAllBytesAsync(certPath, certificate.Export(X509ContentType.Cert));
            var result = await RunModuleAssertion($@"
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new('{PsQuote(certPath)}')
$ok = Test-DetachedRsaPssSignature -PayloadBytes ([Convert]::FromBase64String('{Convert.ToBase64String(payload)}')) -SignatureBase64 '{signature}' -Certificate $cert -PinnedThumbprint '{certificate.Thumbprint}'
if (-not $ok) {{ throw 'Expected signature verification success.' }}
");
            Assert.True(result.ExitCode == 0, result.StandardError + result.StandardOutput);
        }
        finally { File.Delete(certPath); }
    }

    [Theory]
    [InlineData("altered-payload")]
    [InlineData("wrong-pin")]
    public async Task Detached_signature_rejects_altered_or_unpinned_evidence(string mutation)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=PA-TICKETS-ISOLATED-TEST-ONLY", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(5));
        var original = Encoding.UTF8.GetBytes("signed evidence");
        var bytes = mutation == "altered-payload" ? Encoding.UTF8.GetBytes("altered evidence") : original;
        var signature = Convert.ToBase64String(rsa.SignData(original, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var certPath = Path.Combine(Path.GetTempPath(), $"pa-tickets-test-cert-{Guid.NewGuid():N}.cer");
        try
        {
            await File.WriteAllBytesAsync(certPath, certificate.Export(X509ContentType.Cert));
            var pin = mutation == "wrong-pin" ? new string('0', 40) : certificate.Thumbprint;
            var result = await RunModuleAssertion($@"
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new('{PsQuote(certPath)}')
Test-DetachedRsaPssSignature -PayloadBytes ([Convert]::FromBase64String('{Convert.ToBase64String(bytes)}')) -SignatureBase64 '{signature}' -Certificate $cert -PinnedThumbprint '{pin}'
throw 'Invalid signature unexpectedly passed.'
");
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(mutation == "wrong-pin" ? "pinned certificate thumbprint" : "signature verification failed", result.StandardError + result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(certPath); }
    }

    [Theory]
    [InlineData("wrong-role")]
    [InlineData("wrong-operation-scope")]
    [InlineData("wrong-endpoint")]
    [InlineData("wrong-artifact")]
    [InlineData("expired")]
    [InlineData("excessive-write-privilege")]
    public async Task Authorization_claims_reject_scope_target_artifact_expiry_and_write_mismatches(string mutation)
    {
        if (!OperatingSystem.IsWindows()) return;
        var fields = mutation switch
        {
            "wrong-role" => "$a.role = 'postgres'",
            "wrong-operation-scope" => "$a.operation = 'S5-10_SCHEMA_APPLICATION'",
            "wrong-endpoint" => "$a.host = '127.0.0.1'",
            "wrong-artifact" => "$a.artifactSha256 = ('0' * 64)",
            "expired" => "$a.expiresAtUtc = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')",
            _ => "$a.leastPrivilege.canWriteTables = $true"
        };
        var result = await RunModuleAssertion($@"
        $a = [pscustomobject]@{{ schemaVersion=1; operation='S5-03_READ_ONLY_FINGERPRINT'; host='localhost'; port=5432; database='pa_tickets_codex_e2e'; role='pa_tickets_codex'; operatorSid='S-1-5-21-1-2-3-1001'; artifactPath='tests/scripts/query.sql'; artifactSha256=('A' * 64); expectedEffects='READ_ONLY_CATALOG_FINGERPRINT_NO_SCHEMA_OR_DATA_CHANGE'; validFromUtc=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o'); expiresAtUtc=[DateTimeOffset]::UtcNow.AddMinutes(10).ToString('o'); excludedOperations=@('pa_tickets_test','pa_tickets_h_verify','pa_tickets_test_user','migration_026','DROP_DATABASE','SCHEMA_RESET','BROAD_CLEANUP'); uncertainOutcome='STOP_NO_RETRY_MARK_UNKNOWN'; recoveryPlanReference='owner-recovery-plan'; evidenceCustodianSid='S-1-5-21-1-2-3-1001'; evidenceDirectory='C:\Evidence'; leastPrivilege=[pscustomobject]@{{ role='pa_tickets_codex'; database='pa_tickets_codex_e2e'; isSuperuser=$false; canCreateDatabase=$false; canCreateRoles=$false; canReplicate=$false; bypassRowLevelSecurity=$false; inheritsPrivilegedRole=$false; canCreateObjects=$false; canWriteTables=$false; canInsertTables=$false; canUpdateTables=$false; canDeleteTables=$false; canTruncateTables=$false; canAlterSchema=$false; canAdvanceSequences=$false; accessibleDatabases=@('pa_tickets_codex_e2e'); selectRelationAllowlist=@('PA_TICKETS_TICKET'); evidenceReference='owner-evidence' }}; leastPrivilegeEvidencePath='unused'; leastPrivilegeEvidenceSha256=('B' * 64) }}
{fields}
Assert-S5LocalAuthorization $a 'S-1-5-21-1-2-3-1001' 'tests/scripts/query.sql' ('A' * 64) 'C:\query.sql' 'C:\Evidence' ([DateTimeOffset]::UtcNow)
throw 'Invalid authorization unexpectedly passed.'
");
        Assert.NotEqual(0, result.ExitCode);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("stale")]
    [InlineData("wrong-authorization-binding")]
    public async Task Revocation_statement_must_be_fresh_active_and_bound(string mutation)
    {
        if (!OperatingSystem.IsWindows()) return;
        var issued = DateTimeOffset.UtcNow.AddSeconds(-10);
        var status = mutation == "revoked" ? "REVOKED" : "ACTIVE";
        var authHash = mutation == "wrong-authorization-binding" ? new string('0', 64) : new string('A', 64);
        var result = await RunModuleAssertion($@"
        $r = [pscustomobject]@{{ schemaVersion=1; authorizationVersion=1; peerPolicyVersion=1; status='{status}'; authorizationPayloadSha256='{authHash}'; peerPayloadSha256='{new string('B', 64)}'; issuedAtUtc='{(mutation == "stale" ? DateTimeOffset.UtcNow.AddMinutes(-8) : issued).ToString("o")}'; expiresAtUtc='{DateTimeOffset.UtcNow.AddMinutes(2):o}' }}
Assert-ActiveRevocation $r ('{new string('A', 64)}') ('{new string('B', 64)}') ([DateTimeOffset]::UtcNow)
throw 'Invalid revocation statement unexpectedly passed.'
");
        Assert.NotEqual(0, result.ExitCode);
    }

    [Theory]
    [InlineData("wrong-process")]
    [InlineData("wrong-service")]
    [InlineData("wrong-endpoint")]
    [InlineData("altered-artifact")]
    [InlineData("stale-peer")]
    public async Task Signed_peer_claims_must_match_windows_facts_and_fresh_target_binding(string mutation)
    {
        if (!OperatingSystem.IsWindows()) return;
        var captured = DateTimeOffset.UtcNow.AddSeconds(-10);
        var change = mutation switch
        {
            "wrong-process" => "$p.processId = 2469",
            "wrong-service" => "$p.serviceName = 'other-service'",
            "wrong-endpoint" => "$p.localAddress = '::'",
            "altered-artifact" => "$p.artifactSha256 = ('0' * 64)",
            _ => "$p.capturedAtUtc = [DateTimeOffset]::UtcNow.AddMinutes(-10).ToString('o')"
        };
        var result = await RunModuleAssertion($@"
$facts = [pscustomobject]@{{ AddressFamily='InterNetworkV6'; LocalAddress='::1'; LocalPort=5432; ProcessId=2468; ParentProcessIds=@(2468,1001); ProcessStartTimeUtc='{captured.AddDays(-1):o}'; ProcessImagePath='C:\postgres.exe'; ProcessImageSha256=('A' * 64); ProcessSignerThumbprint=('B' * 40); ServiceName='postgresql-e2e'; ServiceProcessId=1001; ServiceAccount='NT SERVICE\postgresql-e2e'; ServiceState='Running'; DataDirectory='C:\pgdata'; ConfigurationFile='C:\pgdata\postgresql.conf' }}
$p = [pscustomobject]@{{ schemaVersion=1; addressFamily='InterNetworkV6'; localAddress='::1'; localPort=5432; listenerProcessId=2468; processId=2468; processAncestorIds=@(2468,1001); processStartTimeUtc='{captured.AddDays(-1):o}'; processImagePath='C:\postgres.exe'; processImageSha256=('A' * 64); processSignerThumbprint=('B' * 40); serviceName='postgresql-e2e'; serviceProcessId=1001; serviceAccount='NT SERVICE\postgresql-e2e'; serviceState='Running'; dataDirectory='C:\pgdata'; configurationFile='C:\pgdata\postgresql.conf'; capturedAtUtc='{captured:o}'; expiresAtUtc='{captured.AddMinutes(4):o}'; host='localhost'; port=5432; database='pa_tickets_codex_e2e'; role='pa_tickets_codex'; artifactPath='tests/scripts/query.sql'; artifactSha256=('C' * 64); authorizationPayloadSha256=('D' * 64) }}
{change}
Assert-S5LocalPeerBinding $p $facts 'tests/scripts/query.sql' ('C' * 64) ('D' * 64) ([DateTimeOffset]::UtcNow)
throw 'Mismatched peer evidence unexpectedly passed.'
");
        Assert.NotEqual(0, result.ExitCode);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunModuleAssertion(string body)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"pa-tickets-trust-test-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(scriptPath, $"$ErrorActionPreference = 'Stop'\nImport-Module '{PsQuote(ModulePath)}' -Force\n{body}");
        try
        {
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-ExecutionPolicy"); start.ArgumentList.Add("Bypass"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(scriptPath);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start offline PowerShell test.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await stdout, await stderr);
        }
        finally { File.Delete(scriptPath); }
    }

    private static string PsQuote(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "scripts", "LocalE2eTrust.psm1"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository-local trust module.");
    }
}
