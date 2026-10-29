[CmdletBinding()]
param(
    [ValidateSet('Validate', 'VerifyLocal', 'Execute')]
    [string] $Mode = 'Validate',

    [string] $RepositoryRoot,
    [string] $ConnectionProfilePath,
    [string] $AuthorizationProfilePath
)

# REVIEW-ONLY input format: these checks validate structure, not provenance.
# No profile/reference is trusted and this script has no database client path.
$ErrorActionPreference = 'Stop'
$expectedArtifactPath = 'tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql'
$expectedSha256 = '6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342'
$expectedTarget = [ordered]@{
    host = 'localhost'
    port = 5432
    database = 'pa_tickets_codex_e2e'
    role = 'pa_tickets_codex'
}
$protectedDatabases = @('pa_tickets_test', 'pa_tickets_h_verify')
$protectedRoles = @('pa_tickets_test_user')
$requiredExclusions = @(
    'pa_tickets_test', 'pa_tickets_h_verify', 'pa_tickets_test_user',
    'migration_026', 'DROP_DATABASE', 'SCHEMA_RESET', 'BROAD_CLEANUP'
)
$targetEnvironmentOverrides = @('PGHOST', 'PGPORT', 'PGDATABASE', 'PGUSER', 'PGSERVICE', 'PGSERVICEFILE', 'PGOPTIONS')

function Assert-ExactProperties($value, [string[]] $expected, [string] $description) {
    if ($null -eq $value -or $value -is [string] -or $value -is [System.Array]) {
        throw "$description must be a JSON object with explicit fields."
    }
    $actual = @($value.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($name in $expected) {
        if ($actual -cnotcontains $name) { throw "$description is missing required field '$name'." }
    }
    foreach ($name in $actual) {
        if ($expected -cnotcontains $name) { throw "$description contains unsupported field '$name'; allowed fields: $($expected -join ',')." }
    }
}

function Read-Profile([string] $path, [string] $description) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$description file is required and must exist."
    }
    try {
        return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -ErrorAction Stop)
    }
    catch {
        throw "$description is not valid JSON."
    }
}

function Assert-ExactTarget($target, [string] $description) {
    if ($null -eq $target -or $target -is [string] -or $target -is [System.Array]) {
        throw "$description must explicitly name host, port, database, and role."
    }
    foreach ($field in @('host', 'port', 'database', 'role')) {
        if ($target.PSObject.Properties.Name -cnotcontains $field) { throw "$description is missing required field '$field'." }
    }
    if ($target.database -is [string] -and $protectedDatabases -icontains $target.database) {
        throw "$description names a protected database."
    }
    if ($target.role -is [string] -and $protectedRoles -icontains $target.role) {
        throw "$description names a protected role."
    }
    foreach ($field in @('host', 'port', 'database', 'role')) {
        if ($target.$field -cne $expectedTarget[$field]) {
            throw "$description does not match the exact approved candidate target."
        }
    }
}

function Validate-ConnectionProfile([string] $path, [string] $authorizationProfilePath) {
    if ([string]::IsNullOrWhiteSpace($path)) { return 'NOT_PROVIDED' }
    $profile = Read-Profile $path 'Connection profile'
    Assert-ExactProperties -value $profile -expected @('host', 'port', 'database', 'role', 'serverIdentity') -description 'Connection profile'
    Assert-ExactTarget $profile 'Connection profile'
    if ($null -eq $profile.serverIdentity) { throw 'Connection profile serverIdentity evidence is required.' }
    if ($profile.serverIdentity.kind -ceq 'TLS_CERT_SHA256') {
        Assert-ExactProperties -value $profile.serverIdentity -expected @('kind', 'sha256', 'trustAnchorReference') -description 'Server identity'
        if ($profile.serverIdentity.sha256 -cnotmatch '^[A-Fa-f0-9]{64}$' -or
            [string]::IsNullOrWhiteSpace([string]$profile.serverIdentity.trustAnchorReference)) {
            throw 'Server identity pin and external trust-anchor reference are required.'
        }
        return 'EXACT_TARGET; TLS_PIN_SHAPE_VALID; TRUST_UNVERIFIED'
    }

    if ($profile.serverIdentity.kind -cne 'WINDOWS_IPV6_LOOPBACK_PROCESS') {
        throw 'Server identity kind is unsupported; only TLS pin structure or Windows IPv6 loopback process evidence is accepted.'
    }

    # These are consistency checks on supplied evidence, not OS inspection or
    # cryptographic verification. Until a trusted collector and trust root are
    # approved, every accepted peer profile remains explicitly unverified.
    $peer = $profile.serverIdentity
    $peerFields = @(
        'kind', 'addressFamily', 'localAddress', 'localPort', 'listenerProcessId', 'processId',
        'serviceProcessId', 'processAncestorIds', 'processStartTimeUtc',
        'processImagePath', 'processImageSha256', 'processSignerThumbprint', 'serviceName',
        'serviceAccount', 'dataDirectory', 'configurationFile', 'expectedImageSha256',
        'expectedProcessImagePath', 'expectedSignerThumbprint', 'expectedServiceName', 'expectedServiceAccount',
        'expectedDataDirectory', 'expectedConfigurationFile', 'capturedAtUtc', 'expiresAtUtc',
        'trustAnchorSha256', 'trustAnchorReference', 'signedPolicyReference',
        'evidenceSignatureReference', 'revocationStatus', 'revocationEvidenceReference',
        'targetHost', 'targetPort', 'targetDatabase', 'targetRole', 'artifactPath',
        'artifactSha256', 'authorizationProfileSha256'
    )
    Assert-ExactProperties -value $peer -expected $peerFields -description 'Windows loopback peer evidence'

    if ($peer.addressFamily -cne 'InterNetworkV6' -or $peer.localAddress -cne '::1') {
        throw 'Windows loopback peer evidence must identify the supported IPv6 loopback endpoint ::1.'
    }
    if ($peer.localPort -ne $expectedTarget.port -or $peer.listenerProcessId -le 0 -or
        $peer.processId -le 0 -or $peer.listenerProcessId -ne $peer.processId) {
        throw 'Windows loopback listener endpoint or owning process identity does not match the expected target.'
    }
    if ($peer.serviceProcessId -le 0 -or $peer.processAncestorIds -isnot [System.Array] -or
        ($peer.serviceProcessId -ne $peer.processId -and @($peer.processAncestorIds) -notcontains $peer.serviceProcessId)) {
        throw 'Windows loopback listener process is not bound to the stated PostgreSQL service process.'
    }
    if ($peer.targetHost -cne $expectedTarget.host -or $peer.targetPort -ne $expectedTarget.port -or
        $peer.targetDatabase -cne $expectedTarget.database -or $peer.targetRole -cne $expectedTarget.role -or
        $peer.artifactPath -cne $expectedArtifactPath -or $peer.artifactSha256 -cne $expectedSha256 -or
        [string]::IsNullOrWhiteSpace($authorizationProfilePath) -or
        -not (Test-Path -LiteralPath $authorizationProfilePath -PathType Leaf)) {
        throw 'Windows loopback peer evidence is not bound to the exact S5-03 target, operation artifact, and authorization profile.'
    }
    $authorizationSha256 = (Get-FileHash -LiteralPath $authorizationProfilePath -Algorithm SHA256).Hash
    if (-not [string]::Equals([string]$peer.authorizationProfileSha256, $authorizationSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Windows loopback peer evidence does not bind the exact authorization profile bytes.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$peer.processImagePath) -or
        -not [System.IO.Path]::IsPathRooted([string]$peer.processImagePath) -or
        [string]::IsNullOrWhiteSpace([string]$peer.expectedProcessImagePath) -or
        -not [string]::Equals([string]$peer.processImagePath, [string]$peer.expectedProcessImagePath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Windows loopback process image and approved process identity evidence are required.'
    }
    if ($peer.processImageSha256 -cnotmatch '^[A-Fa-f0-9]{64}$' -or
        $peer.expectedImageSha256 -cnotmatch '^[A-Fa-f0-9]{64}$' -or
        -not [string]::Equals([string]$peer.processImageSha256, [string]$peer.expectedImageSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Windows loopback process image hash does not match the approved process identity claim.'
    }
    if ($peer.processSignerThumbprint -cnotmatch '^(?:[A-Fa-f0-9]{40}|[A-Fa-f0-9]{64})$' -or
        -not [string]::Equals([string]$peer.processSignerThumbprint, [string]$peer.expectedSignerThumbprint, [StringComparison]::OrdinalIgnoreCase) -or
        [string]::IsNullOrWhiteSpace([string]$peer.serviceName) -or
        -not [string]::Equals([string]$peer.serviceName, [string]$peer.expectedServiceName, [StringComparison]::OrdinalIgnoreCase) -or
        [string]::IsNullOrWhiteSpace([string]$peer.serviceAccount) -or
        -not [string]::Equals([string]$peer.serviceAccount, [string]$peer.expectedServiceAccount, [StringComparison]::OrdinalIgnoreCase) -or
        [string]::IsNullOrWhiteSpace([string]$peer.dataDirectory) -or
        -not [string]::Equals([string]$peer.dataDirectory, [string]$peer.expectedDataDirectory, [StringComparison]::OrdinalIgnoreCase) -or
        [string]::IsNullOrWhiteSpace([string]$peer.configurationFile) -or
        -not [string]::Equals([string]$peer.configurationFile, [string]$peer.expectedConfigurationFile, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Windows loopback process, service, or instance identity does not match the approved process identity claim.'
    }
    if ($peer.trustAnchorSha256 -cnotmatch '^[A-Fa-f0-9]{64}$' -or
        [string]::IsNullOrWhiteSpace([string]$peer.trustAnchorReference) -or
        [string]::IsNullOrWhiteSpace([string]$peer.signedPolicyReference) -or
        [string]::IsNullOrWhiteSpace([string]$peer.evidenceSignatureReference)) {
        throw 'Independent trust-anchor, signed-policy, and evidence-signature references are required.'
    }
    if ($peer.revocationStatus -cnotin @('ACTIVE', 'REVOKED') -or
        [string]::IsNullOrWhiteSpace([string]$peer.revocationEvidenceReference)) {
        throw 'Windows loopback peer revocation evidence is missing or invalid.'
    }
    if ($peer.revocationStatus -ceq 'REVOKED') { throw 'Windows loopback peer trust policy is revoked.' }

    try {
        $processStartedAt = [DateTimeOffset]::Parse([string]$peer.processStartTimeUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        $capturedAt = [DateTimeOffset]::Parse([string]$peer.capturedAtUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        $expiresAt = [DateTimeOffset]::Parse([string]$peer.expiresAtUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    }
    catch { throw 'Windows loopback peer evidence must contain parseable ISO timestamps.' }
    $now = [DateTimeOffset]::UtcNow
    if ($processStartedAt.Offset -ne [TimeSpan]::Zero -or $processStartedAt -gt $capturedAt -or
        $capturedAt.Offset -ne [TimeSpan]::Zero -or $expiresAt.Offset -ne [TimeSpan]::Zero -or
        $capturedAt -gt $now -or $capturedAt -lt $now.AddMinutes(-2) -or
        $expiresAt -le $now -or $expiresAt -gt $capturedAt.AddMinutes(5)) {
        throw 'Windows loopback peer evidence is stale, expired, future-dated, or outside its maximum five-minute validity.'
    }

    return 'EXACT_TARGET; PEER_EVIDENCE_STRUCTURALLY_CONSISTENT; TRUST, SIGNATURE, PROCESS PROVENANCE AND REVOCATION UNVERIFIED'
}

function Validate-AuthorizationProfile([string] $path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return 'NOT_PROVIDED' }
    $profile = Read-Profile $path 'Authorization profile'
    $fields = @(
        'schemaVersion', 'operation', 'target', 'artifactPath', 'artifactSha256',
        'validFromUtc', 'expiresAtUtc', 'revocationStatus', 'revocationEvidenceReference',
        'operatorIdentityReference', 'approverIdentityReference', 'approvalReference',
        'signatureReference', 'excludedOperations'
    )
    Assert-ExactProperties -value $profile -expected $fields -description 'Authorization profile'
    if ($profile.schemaVersion -ne 1) { throw 'Authorization profile schemaVersion is unsupported.' }
    if ($profile.operation -cne 'S5-03_READ_ONLY_FINGERPRINT') { throw 'Authorization operation scope does not match S5-03 read-only fingerprinting.' }
    Assert-ExactProperties -value $profile.target -expected @('host', 'port', 'database', 'role') -description 'Authorization target'
    Assert-ExactTarget $profile.target 'Authorization target'
    if ($profile.artifactPath -cne $expectedArtifactPath -or
        $profile.artifactSha256 -cnotmatch '^[A-Fa-f0-9]{64}$' -or
        $profile.artifactSha256 -cne $expectedSha256) {
        throw 'Authorization artifact path or SHA-256 does not match the pinned S5-03 candidate.'
    }

    try {
        $validFrom = [DateTimeOffset]::Parse([string]$profile.validFromUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        $expiresAt = [DateTimeOffset]::Parse([string]$profile.expiresAtUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    }
    catch { throw 'Authorization validity window must contain parseable ISO timestamps.' }
    if ($validFrom.Offset -ne [TimeSpan]::Zero -or $expiresAt.Offset -ne [TimeSpan]::Zero -or $validFrom -ge $expiresAt) {
        throw 'Authorization validity window must be ordered UTC timestamps.'
    }
    $now = [DateTimeOffset]::UtcNow
    if ($validFrom -gt $now) { throw 'Authorization is not yet valid.' }
    if ($expiresAt -le $now) { throw 'Authorization has expired.' }
    if ($profile.revocationStatus -cnotin @('ACTIVE', 'REVOKED')) { throw 'Authorization revocation state is missing or invalid.' }
    if ([string]::IsNullOrWhiteSpace([string]$profile.revocationEvidenceReference)) { throw 'Authorization revocation evidence reference is required.' }
    if ($profile.revocationStatus -ceq 'REVOKED') { throw 'Authorization is revoked.' }
    foreach ($field in @('operatorIdentityReference', 'approverIdentityReference', 'approvalReference', 'signatureReference')) {
        if ([string]::IsNullOrWhiteSpace([string]$profile.$field)) { throw "Authorization attribution field '$field' is required." }
    }
    if ($profile.excludedOperations -isnot [System.Array]) { throw 'Authorization exclusions must be an explicit JSON array.' }
    foreach ($excluded in $requiredExclusions) {
        if (@($profile.excludedOperations) -inotcontains $excluded) { throw "Authorization exclusions omit required boundary '$excluded'." }
    }
    return 'STRUCTURALLY_VALID; SIGNATURE, IDENTITY, REVOCATION_AUTHORITY UNVERIFIED'
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

foreach ($variableName in $targetEnvironmentOverrides) {
    if (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($variableName, 'Process'))) {
        throw "Environment target override '$variableName' is prohibited; provide no implicit PostgreSQL target defaults."
    }
}

$artifactPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $expectedArtifactPath))
$rootPrefix = $RepositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $artifactPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
    throw 'The pinned fingerprint artifact must exist inside the selected repository root.'
}

$actualSha256 = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash
if (-not [string]::Equals($actualSha256, $expectedSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The pinned fingerprint artifact hash does not match the reviewed candidate; no database operation was attempted.'
}

if ($Mode -eq 'VerifyLocal') {
    if ([string]::IsNullOrWhiteSpace($AuthorizationProfilePath) -or [string]::IsNullOrWhiteSpace($ConnectionProfilePath)) {
        throw 'VerifyLocal requires signed authorization and signed peer evidence files.'
    }
    Import-Module (Join-Path $PSScriptRoot 'LocalE2eTrust.psm1') -Force -ErrorAction Stop
    $localVerification = Invoke-OwnerLocalEvidenceVerification $AuthorizationProfilePath $ConnectionProfilePath $RepositoryRoot $expectedArtifactPath $expectedSha256
    [ordered]@{
        mode = 'VerifyLocal'
        operation = 'S5-03_READ_ONLY_FINGERPRINT'
        verification = 'OWNER_CONTROLLED_SIGNED_EVIDENCE_VERIFIED; PRIVILEGE EVIDENCE OWNER-ATTESTED'
        assurance = $localVerification.evidence.assurance
        evidenceRecord = $localVerification.recordPath
        evidenceRecordSha256 = $localVerification.recordSha256
        databaseClientInvoked = $false
        executionEligible = $false
    } | ConvertTo-Json -Depth 6 -Compress
    exit 0
}

$authorizationStatus = Validate-AuthorizationProfile $AuthorizationProfilePath
$connectionStatus = Validate-ConnectionProfile $ConnectionProfilePath $AuthorizationProfilePath
if ($Mode -eq 'Execute') {
    throw 'S5-03 execution is disabled by policy. Owner-controlled local identity evidence is not provisioned and operation-specific authorization is absent. No database client was invoked.'
}

$evidence = [ordered]@{
    mode = 'Validate'
    operation = 'S5-03_READ_ONLY_FINGERPRINT'
    artifact = $expectedArtifactPath
    sha256 = $actualSha256.ToUpperInvariant()
    candidateTarget = $expectedTarget
    connectionProfile = $connectionStatus
    authorizationProfile = $authorizationStatus
    databaseClientInvoked = $false
    executionEligible = $false
}
$evidence | ConvertTo-Json -Depth 5 -Compress
