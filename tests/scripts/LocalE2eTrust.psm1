Set-StrictMode -Version Latest

function Get-JsonObject([string] $path, [string] $description) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$description is missing."
    }
    try { return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -ErrorAction Stop) }
    catch { throw "$description is not valid JSON." }
}

function Assert-JsonFields($value, [string[]] $fields, [string] $description) {
    if ($null -eq $value -or $value -is [string] -or $value -is [array]) { throw "$description must be an object." }
    $actual = @($value.PSObject.Properties | ForEach-Object Name)
    foreach ($field in $fields) { if ($actual -cnotcontains $field) { throw "$description is missing '$field'." } }
    foreach ($field in $actual) { if ($fields -cnotcontains $field) { throw "$description contains unsupported field '$field'." } }
}

function Test-DetachedRsaPssSignature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][byte[]] $PayloadBytes,
        [Parameter(Mandatory)][string] $SignatureBase64,
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [Parameter(Mandatory)][string] $PinnedThumbprint
    )
    $pin = ($PinnedThumbprint -replace '\s', '').ToUpperInvariant()
    if ($Certificate.Thumbprint -ne $pin) { throw 'Evidence signer does not match the pinned certificate thumbprint.' }
    $now = [DateTime]::UtcNow
    if ($Certificate.NotBefore.ToUniversalTime() -gt $now -or $Certificate.NotAfter.ToUniversalTime() -le $now) {
        throw 'Evidence signer certificate is not currently valid.'
    }
    try { $signature = [Convert]::FromBase64String($SignatureBase64) }
    catch { throw 'Evidence signature is not valid base64.' }
    $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($Certificate)
    if ($null -eq $rsa -or $rsa.KeySize -lt 3072) { throw 'Evidence signer must have an RSA public key of at least 3072 bits.' }
    try {
        if (-not $rsa.VerifyData($PayloadBytes, $signature, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)) {
            throw 'Evidence signature verification failed.'
        }
    }
    finally { $rsa.Dispose() }
    return $true
}

function Read-VerifiedEnvelope([string] $path, [System.Security.Cryptography.X509Certificates.X509Certificate2] $certificate, [string] $thumbprint, [string] $description) {
    $envelope = Get-JsonObject $path $description
    Assert-JsonFields $envelope @('schemaVersion', 'signerThumbprint', 'payloadBase64', 'signatureBase64') $description
    if ($envelope.schemaVersion -ne 1 -or $envelope.signerThumbprint -cne (($thumbprint -replace '\s', '').ToUpperInvariant())) {
        throw "$description signer or schema version does not match local trust policy."
    }
    try { $payloadBytes = [Convert]::FromBase64String([string]$envelope.payloadBase64) }
    catch { throw "$description payload is not valid base64." }
    [void](Test-DetachedRsaPssSignature $payloadBytes ([string]$envelope.signatureBase64) $certificate $thumbprint)
    try { $payload = [Text.Encoding]::UTF8.GetString($payloadBytes) | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "$description signed payload is not valid UTF-8 JSON." }
    $payloadHash = [Security.Cryptography.SHA256]::Create()
    try { $payloadSha = [BitConverter]::ToString($payloadHash.ComputeHash($payloadBytes)).Replace('-', '') }
    finally { $payloadHash.Dispose() }
    return [pscustomobject]@{
        Payload = $payload
        PayloadBytes = $payloadBytes
        PayloadSha256 = $payloadSha
        EnvelopeSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}

function Get-PinnedMachineCertificate([string] $thumbprint, [string] $purpose) {
    if ($thumbprint -cnotmatch '^[A-Fa-f0-9]{40}$') { throw "$purpose thumbprint is missing or invalid." }
    $store = [Security.Cryptography.X509Certificates.X509Store]::new('My', 'LocalMachine')
    try {
        $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        $matches = @($store.Certificates | Where-Object { $_.Thumbprint -ceq $thumbprint.ToUpperInvariant() })
        if ($matches.Count -ne 1) { throw "$purpose pinned certificate is unavailable or ambiguous in LocalMachine\My." }
        $certificate = $matches[0]
        $publicKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($certificate)
        if ($null -eq $publicKey -or $publicKey.KeySize -lt 3072) { throw "$purpose pinned certificate must contain an RSA public key of at least 3072 bits." }
        $publicKey.Dispose()
        return $certificate
    }
    finally { $store.Close() }
}

function Assert-LocalPolicyAcl([string] $path, [string] $ownerSid) {
    $programData = [IO.Path]::GetFullPath($env:ProgramData).TrimEnd('\')
    $expected = [IO.Path]::GetFullPath((Join-Path $programData 'Genesis\PA_TICKETS\LocalE2E\trust-policy.json'))
    $actual = [IO.Path]::GetFullPath($path)
    if (-not [string]::Equals($actual, $expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Local trust policy must use the fixed ProgramData path.'
    }
    $acl = Get-Acl -LiteralPath $actual
    $owner = ([Security.Principal.NTAccount]$acl.Owner).Translate([Security.Principal.SecurityIdentifier]).Value
    if ($owner -ne $ownerSid -and $owner -ne 'S-1-5-32-544' -and $owner -ne 'S-1-5-18') {
        throw 'Local trust policy owner is not the configured owner, Administrators, or SYSTEM.'
    }
    $allowedWriters = @($ownerSid, 'S-1-5-32-544', 'S-1-5-18')
    $writeRights = [Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Modify -bor [Security.AccessControl.FileSystemRights]::FullControl -bor [Security.AccessControl.FileSystemRights]::Delete
    foreach ($rule in $acl.Access) {
        $sid = try { ([Security.Principal.NTAccount]$rule.IdentityReference).Translate([Security.Principal.SecurityIdentifier]).Value } catch { '' }
        if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and
            $allowedWriters -cnotcontains $sid -and ($rule.FileSystemRights -band $writeRights) -ne 0) {
            throw 'Local trust policy is writable by an unapproved Windows principal.'
        }
    }
}

function Assert-OwnerEvidenceDirectory([string] $path, [string] $ownerSid) {
    $programData = [IO.Path]::GetFullPath($env:ProgramData).TrimEnd('\')
    $expected = [IO.Path]::GetFullPath((Join-Path $programData 'Genesis\PA_TICKETS\LocalE2E\Evidence'))
    $actual = [IO.Path]::GetFullPath($path)
    if (-not [string]::Equals($actual, $expected, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $actual -PathType Container)) {
        throw 'Owner evidence custody directory is missing or differs from the fixed ProgramData path.'
    }
    $acl = Get-Acl -LiteralPath $actual
    $owner = ([Security.Principal.NTAccount]$acl.Owner).Translate([Security.Principal.SecurityIdentifier]).Value
    if ($owner -ne $ownerSid -and $owner -ne 'S-1-5-32-544' -and $owner -ne 'S-1-5-18') { throw 'Evidence custody directory owner is not the configured owner, Administrators, or SYSTEM.' }
    $allowedWriters = @($ownerSid, 'S-1-5-32-544', 'S-1-5-18')
    $writeRights = [Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Modify -bor [Security.AccessControl.FileSystemRights]::FullControl -bor [Security.AccessControl.FileSystemRights]::Delete
    foreach ($rule in $acl.Access) {
        $sid = try { ([Security.Principal.NTAccount]$rule.IdentityReference).Translate([Security.Principal.SecurityIdentifier]).Value } catch { '' }
        if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and $allowedWriters -cnotcontains $sid -and ($rule.FileSystemRights -band $writeRights) -ne 0) { throw 'Evidence custody directory is writable by an unapproved Windows principal.' }
    }
}

function Get-OwnerLocalPolicy([string] $path) {
    $policy = Get-JsonObject $path 'Owner local trust policy'
    $fields = @('schemaVersion', 'ownerSid', 'ownerSignerThumbprint', 'revocationSignerThumbprint', 'serviceName', 'serviceAccount', 'processImagePath', 'processImageSha256', 'processSignerThumbprint', 'dataDirectory', 'configurationFile', 'evidenceDirectory')
    Assert-JsonFields $policy $fields 'Owner local trust policy'
    if ($policy.schemaVersion -ne 1 -or $policy.ownerSid -cnotmatch '^S-1-5-21-(?:\d+-){3}\d+$' -or
        $policy.ownerSignerThumbprint -cnotmatch '^[A-Fa-f0-9]{40}$' -or
        $policy.revocationSignerThumbprint -cnotmatch '^[A-Fa-f0-9]{40}$' -or
        $policy.ownerSignerThumbprint -ceq $policy.revocationSignerThumbprint) {
        throw 'Owner local trust policy identity or separated signer pins are invalid.'
    }
    Assert-LocalPolicyAcl $path $policy.ownerSid
    Assert-OwnerEvidenceDirectory $policy.evidenceDirectory $policy.ownerSid
    $currentSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    if ($currentSid -ne $policy.ownerSid) { throw 'Current Windows account SID does not match the approved local owner.' }
    return $policy
}

function Get-LocalPostgresListenerFacts($policy) {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Local E2E verification requires Windows.' }
    try { $listeners = @(Get-NetTCPConnection -State Listen -LocalAddress '::1' -LocalPort 5432 -ErrorAction Stop) }
    catch { throw 'Could not collect the Windows IPv6 loopback listener facts.' }
    $pids = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique)
    if ($listeners.Count -ne 1 -or $pids.Count -ne 1 -or $pids[0] -le 0) { throw 'Expected exactly one Windows listener on [::1]:5432.' }
    $listenerPid = [int]$pids[0]
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $listenerPid" -ErrorAction Stop
    if ($null -eq $proc -or [string]::IsNullOrWhiteSpace($proc.ExecutablePath)) { throw 'Listener process identity could not be collected.' }
    $image = [IO.Path]::GetFullPath($proc.ExecutablePath)
    $imageHash = (Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash
    $signature = Get-AuthenticodeSignature -FilePath $image
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -cne $policy.processSignerThumbprint) { throw 'PostgreSQL executable Authenticode signer does not match the pinned local policy.' }
    if (-not [string]::Equals($image, [string]$policy.processImagePath, [StringComparison]::OrdinalIgnoreCase) -or
        $imageHash -cne $policy.processImageSha256) { throw 'PostgreSQL executable path or SHA-256 does not match the pinned local policy.' }
    $escapedServiceName = [string]$policy.serviceName -replace "'", "''"
    $service = Get-CimInstance Win32_Service -Filter "Name = '$escapedServiceName'" -ErrorAction Stop
    if ($null -eq $service -or $service.StartName -cne $policy.serviceAccount -or $service.State -cne 'Running' -or [int]$service.ProcessId -le 0) { throw 'PostgreSQL Windows service identity or state does not match local policy.' }

    $ancestors = [Collections.Generic.List[int]]::new()
    $cursor = $proc
    for ($depth = 0; $depth -lt 16 -and $null -ne $cursor; $depth++) {
        $ancestors.Add([int]$cursor.ProcessId)
        if ([int]$cursor.ProcessId -eq [int]$service.ProcessId) { break }
        $cursor = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$cursor.ParentProcessId)" -ErrorAction SilentlyContinue
    }
    if ($ancestors -notcontains [int]$service.ProcessId) { throw 'Listener process is not descended from the configured PostgreSQL service process.' }

    $commandLine = [string]$proc.CommandLine
    $dataMatch = [regex]::Match($commandLine, '(?i)(?:^|\s)-D\s+(?:"(?<path>[^"]+)"|(?<path>[^\s]+))')
    if (-not $dataMatch.Success -or -not [string]::Equals([IO.Path]::GetFullPath($dataMatch.Groups['path'].Value), [IO.Path]::GetFullPath([string]$policy.dataDirectory), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'PostgreSQL process command line does not bind the expected data directory.'
    }
    $configMatch = [regex]::Match($commandLine, '(?i)config_file\s*=\s*(?:"(?<path>[^"]+)"|(?<path>[^\s]+))')
    $configPath = if ($configMatch.Success) { [IO.Path]::GetFullPath($configMatch.Groups['path'].Value) } else { [IO.Path]::GetFullPath((Join-Path $policy.dataDirectory 'postgresql.conf')) }
    if (-not [string]::Equals($configPath, [IO.Path]::GetFullPath([string]$policy.configurationFile), [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw 'PostgreSQL configuration path does not match the pinned local policy.' }
    $creation = [Management.ManagementDateTimeConverter]::ToDateTime($proc.CreationDate).ToUniversalTime()
    return [pscustomobject]@{ AddressFamily = 'InterNetworkV6'; LocalAddress = '::1'; LocalPort = 5432; ProcessId = $listenerPid; ParentProcessIds = @($ancestors); ProcessStartTimeUtc = $creation.ToString('o'); ProcessImagePath = $image; ProcessImageSha256 = $imageHash; ProcessSignerThumbprint = $signature.SignerCertificate.Thumbprint; ServiceName = $service.Name; ServiceProcessId = [int]$service.ProcessId; ServiceAccount = [string]$service.StartName; ServiceState = [string]$service.State; DataDirectory = [IO.Path]::GetFullPath([string]$policy.dataDirectory); ConfigurationFile = $configPath }
}

function Assert-S5LocalAuthorization($authorization, [string] $ownerSid, [string] $artifactPath, [string] $artifactSha256, [string] $artifactFullPath, [string] $evidenceDirectory, [DateTimeOffset] $now) {
    $fields = @('schemaVersion','operation','host','port','database','role','operatorSid','artifactPath','artifactSha256','expectedEffects','validFromUtc','expiresAtUtc','excludedOperations','uncertainOutcome','recoveryPlanReference','evidenceCustodianSid','evidenceDirectory','leastPrivilege','leastPrivilegeEvidencePath','leastPrivilegeEvidenceSha256')
    Assert-JsonFields $authorization $fields 'Signed authorization'
    if ($authorization.schemaVersion -ne 1 -or $authorization.operation -cne 'S5-03_READ_ONLY_FINGERPRINT' -or $authorization.host -cne 'localhost' -or $authorization.port -ne 5432 -or $authorization.database -cne 'pa_tickets_codex_e2e' -or $authorization.role -cne 'pa_tickets_codex' -or $authorization.operatorSid -cne $ownerSid -or $authorization.artifactPath -cne $artifactPath -or $authorization.artifactSha256 -cne $artifactSha256 -or $authorization.expectedEffects -cne 'READ_ONLY_CATALOG_FINGERPRINT_NO_SCHEMA_OR_DATA_CHANGE' -or $authorization.uncertainOutcome -cne 'STOP_NO_RETRY_MARK_UNKNOWN' -or [string]::IsNullOrWhiteSpace([string]$authorization.recoveryPlanReference) -or $authorization.evidenceCustodianSid -cne $ownerSid -or $authorization.evidenceDirectory -cne $evidenceDirectory) {
        throw 'Signed authorization does not match the exact approved operation, owner, target, or artifact.'
    }
    $validFrom = [DateTimeOffset]::Parse([string]$authorization.validFromUtc).ToUniversalTime()
    $authExpiry = [DateTimeOffset]::Parse([string]$authorization.expiresAtUtc).ToUniversalTime()
    if ($validFrom -gt $now -or $authExpiry -le $now -or $authExpiry -gt $validFrom.AddMinutes(30)) { throw 'Signed authorization is not yet valid, expired, or exceeds the 30-minute maximum.' }
    $required = @('pa_tickets_test','pa_tickets_h_verify','pa_tickets_test_user','migration_026','DROP_DATABASE','SCHEMA_RESET','BROAD_CLEANUP')
    if ($authorization.excludedOperations -isnot [array] -or @($required | Where-Object { @($authorization.excludedOperations) -cnotcontains $_ }).Count -gt 0) { throw 'Signed authorization omits a required operation exclusion.' }
    $lp = $authorization.leastPrivilege
    Assert-JsonFields $lp @('role','database','isSuperuser','canCreateDatabase','canCreateRoles','canReplicate','bypassRowLevelSecurity','inheritsPrivilegedRole','canCreateObjects','canWriteTables','canInsertTables','canUpdateTables','canDeleteTables','canTruncateTables','canAlterSchema','canAdvanceSequences','accessibleDatabases','selectRelationAllowlist','evidenceReference') 'Least-privilege evidence'
    $forbidden = @('isSuperuser','canCreateDatabase','canCreateRoles','canReplicate','bypassRowLevelSecurity','inheritsPrivilegedRole','canCreateObjects','canWriteTables','canInsertTables','canUpdateTables','canDeleteTables','canTruncateTables','canAlterSchema','canAdvanceSequences')
    if ($lp.role -cne $authorization.role -or $lp.database -cne $authorization.database -or @($forbidden | Where-Object { [bool]$lp.$_ }).Count -gt 0 -or $lp.accessibleDatabases -isnot [array] -or @($lp.accessibleDatabases).Count -ne 1 -or @($lp.accessibleDatabases)[0] -cne $authorization.database -or $lp.selectRelationAllowlist -isnot [array] -or @($lp.selectRelationAllowlist).Count -eq 0 -or @($lp.selectRelationAllowlist | Where-Object { $_ -cnotmatch '^PA_TICKETS_[A-Z0-9_]+$' }).Count -gt 0 -or [string]::IsNullOrWhiteSpace([string]$lp.evidenceReference)) { throw 'Signed least-privilege evidence exceeds the approved read-only role boundary.' }
    $artifactText = Get-Content -LiteralPath $artifactFullPath -Raw -ErrorAction Stop
    $artifactRelations = @([regex]::Matches($artifactText, '\bPA_TICKETS_[A-Z0-9_]+\b') | ForEach-Object { $_.Value } | Sort-Object -Unique)
    $approvedRelations = @($lp.selectRelationAllowlist | Sort-Object -Unique)
    if (($artifactRelations -join "`n") -cne ($approvedRelations -join "`n")) { throw 'Least-privilege relation allowlist does not exactly match the pinned fingerprint artifact.' }
    $evidencePath = [IO.Path]::GetFullPath([string]$authorization.leastPrivilegeEvidencePath)
    $evidenceRoot = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'Genesis\PA_TICKETS\LocalE2E\Evidence')).TrimEnd('\') + '\'
    if (-not $evidencePath.StartsWith($evidenceRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $evidencePath -PathType Leaf) -or $authorization.leastPrivilegeEvidenceSha256 -cnotmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash -cne $authorization.leastPrivilegeEvidenceSha256) { throw 'Least-privilege evidence file is absent, outside the fixed custody directory, or has an altered hash.' }
}

function Assert-ActiveRevocation($revocation, [string] $authorizationPayloadSha256, [string] $peerPayloadSha256, [DateTimeOffset] $now) {
    Assert-JsonFields $revocation @('schemaVersion','authorizationVersion','peerPolicyVersion','status','authorizationPayloadSha256','peerPayloadSha256','issuedAtUtc','expiresAtUtc') 'Signed revocation statement'
    $issued = [DateTimeOffset]::Parse([string]$revocation.issuedAtUtc).ToUniversalTime()
    $expiry = [DateTimeOffset]::Parse([string]$revocation.expiresAtUtc).ToUniversalTime()
    if ($revocation.schemaVersion -ne 1 -or $revocation.authorizationVersion -ne 1 -or $revocation.peerPolicyVersion -ne 1 -or $revocation.status -cne 'ACTIVE' -or $revocation.authorizationPayloadSha256 -cne $authorizationPayloadSha256 -or $revocation.peerPayloadSha256 -cne $peerPayloadSha256 -or $issued -gt $now -or $issued -lt $now.AddMinutes(-5) -or $expiry -le $now -or $expiry -gt $issued.AddMinutes(5)) { throw 'Revocation statement is missing, stale, expired, revoked, or bound to different evidence.' }
}

function Assert-S5LocalPeerBinding($peer, $facts, [string] $artifactPath, [string] $artifactSha256, [string] $authorizationPayloadSha256, [DateTimeOffset] $now) {
    Assert-JsonFields $peer @('schemaVersion','addressFamily','localAddress','localPort','listenerProcessId','processId','processAncestorIds','processStartTimeUtc','processImagePath','processImageSha256','processSignerThumbprint','serviceName','serviceProcessId','serviceAccount','serviceState','dataDirectory','configurationFile','capturedAtUtc','expiresAtUtc','host','port','database','role','artifactPath','artifactSha256','authorizationPayloadSha256') 'Signed peer evidence'
    $expected = [ordered]@{ addressFamily=$facts.AddressFamily; localAddress=$facts.LocalAddress; localPort=$facts.LocalPort; listenerProcessId=$facts.ProcessId; processId=$facts.ProcessId; processImagePath=$facts.ProcessImagePath; processImageSha256=$facts.ProcessImageSha256; processSignerThumbprint=$facts.ProcessSignerThumbprint; serviceName=$facts.ServiceName; serviceProcessId=$facts.ServiceProcessId; serviceAccount=$facts.ServiceAccount; serviceState=$facts.ServiceState; dataDirectory=$facts.DataDirectory; configurationFile=$facts.ConfigurationFile }
    foreach ($key in $expected.Keys) { if ([string]$peer.$key -cne [string]$expected[$key]) { throw "Signed peer evidence does not match live Windows fact '$key'." } }
    if ((@($peer.processAncestorIds) -join ',') -cne (@($facts.ParentProcessIds) -join ',') -or
        [DateTimeOffset]::Parse([string]$peer.processStartTimeUtc).ToUniversalTime() -ne [DateTimeOffset]::Parse([string]$facts.ProcessStartTimeUtc).ToUniversalTime()) { throw 'Signed peer evidence does not match the live listener ancestry or process start time.' }
    if ($peer.schemaVersion -ne 1 -or $peer.addressFamily -cne 'InterNetworkV6' -or $peer.localAddress -cne '::1' -or $peer.localPort -ne 5432 -or $peer.host -cne 'localhost' -or $peer.port -ne 5432 -or $peer.database -cne 'pa_tickets_codex_e2e' -or $peer.role -cne 'pa_tickets_codex' -or $peer.artifactPath -cne $artifactPath -or $peer.artifactSha256 -cne $artifactSha256 -or $peer.authorizationPayloadSha256 -cne $authorizationPayloadSha256) { throw 'Signed peer evidence is bound to a different endpoint, authorization, or artifact.' }
    $capture = [DateTimeOffset]::Parse([string]$peer.capturedAtUtc).ToUniversalTime(); $expiry = [DateTimeOffset]::Parse([string]$peer.expiresAtUtc).ToUniversalTime(); $processStart = [DateTimeOffset]::Parse([string]$peer.processStartTimeUtc).ToUniversalTime()
    if ($capture -gt $now -or $capture -lt $now.AddMinutes(-2) -or $expiry -le $now -or $expiry -gt $capture.AddMinutes(5) -or $processStart -gt $capture) { throw 'Signed peer evidence is stale, expired, future-dated, or outside policy limits.' }
}

function Write-SanitizedAttempt([string] $directory, $record) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { throw 'Owner evidence-custody directory is not provisioned.' }
    $leaf = "attempt-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))-$([Guid]::NewGuid().ToString('N')).json"
    $path = Join-Path $directory $leaf
    $bytes = [Text.Encoding]::UTF8.GetBytes(($record | ConvertTo-Json -Depth 8 -Compress))
    $stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $recordSha = [BitConverter]::ToString($hash.ComputeHash($bytes)).Replace('-', '') }
    finally { $hash.Dispose() }
    return [pscustomobject]@{ path = $path; sha256 = $recordSha }
}

function Invoke-OwnerLocalEvidenceVerification([string] $authorizationPath, [string] $peerPath, [string] $repositoryRoot, [string] $expectedArtifactPath, [string] $expectedArtifactSha256) {
    $policyPath = Join-Path $env:ProgramData 'Genesis\PA_TICKETS\LocalE2E\trust-policy.json'
    $policy = $null; $ownerCertificate = $null; $revocationCertificate = $null
    try {
        $policy = Get-OwnerLocalPolicy $policyPath
        $artifactFullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $expectedArtifactPath))
        $rootFullPath = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\') + '\'
        if (-not $artifactFullPath.StartsWith($rootFullPath, [StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $artifactFullPath -Algorithm SHA256).Hash -cne $expectedArtifactSha256) { throw 'Pinned SQL artifact changed or is outside the repository before local verification.' }
        $ownerCertificate = Get-PinnedMachineCertificate $policy.ownerSignerThumbprint 'Owner signing'
        $revocationCertificate = Get-PinnedMachineCertificate $policy.revocationSignerThumbprint 'Recovery/revocation'
        $auth = Read-VerifiedEnvelope $authorizationPath $ownerCertificate $policy.ownerSignerThumbprint 'Signed authorization'
        $peer = Read-VerifiedEnvelope $peerPath $ownerCertificate $policy.ownerSignerThumbprint 'Signed peer evidence'
        $revocationPath = Join-Path $env:ProgramData 'Genesis\PA_TICKETS\LocalE2E\current-revocation.json'
        $revocation = Read-VerifiedEnvelope $revocationPath $revocationCertificate $policy.revocationSignerThumbprint 'Signed revocation statement'
        $a = $auth.Payload; $p = $peer.Payload; $r = $revocation.Payload; $now = [DateTimeOffset]::UtcNow
        Assert-S5LocalAuthorization $a $policy.ownerSid $expectedArtifactPath $expectedArtifactSha256 $artifactFullPath $policy.evidenceDirectory $now
        $facts = Get-LocalPostgresListenerFacts $policy
        Assert-S5LocalPeerBinding $p $facts $expectedArtifactPath $expectedArtifactSha256 $auth.PayloadSha256 $now
        Assert-ActiveRevocation $r $auth.PayloadSha256 $peer.PayloadSha256 $now

        $record = [ordered]@{ result='OWNER_CONTROLLED_SIGNED_EVIDENCE_AND_LOCAL_FACTS_VERIFIED'; verifierVersion='1'; operation=$a.operation; target='localhost:5432/pa_tickets_codex_e2e/pa_tickets_codex'; ownerSid=$policy.ownerSid; artifactSha256=$expectedArtifactSha256; authorizationPayloadSha256=$auth.PayloadSha256; peerPayloadSha256=$peer.PayloadSha256; revocationEvidenceId=$revocation.EnvelopeSha256; revocationFreshnessSeconds=[int]($now - [DateTimeOffset]::Parse([string]$r.issuedAtUtc).ToUniversalTime()).TotalSeconds; processId=$facts.ProcessId; processImageSha256=$facts.ProcessImageSha256; processSignerThumbprint=$facts.ProcessSignerThumbprint; serviceName=$facts.ServiceName; capturedAtUtc=$now.ToString('o'); databaseClientInvoked=$false; leastPrivilegeEvidence='OWNER_SIGNED_EVIDENCE_HASH_MATCHED; DATABASE_PRIVILEGES_NOT_LIVE_MEASURED'; assurance='OWNER_CONTROLLED_NOT_INDEPENDENT; LOCAL_ADMIN_TAMPERING_OUT_OF_SCOPE' }
        $audit = Write-SanitizedAttempt $policy.evidenceDirectory $record
        return [pscustomobject]@{ verified=$true; recordPath=$audit.path; recordSha256=$audit.sha256; evidence=$record }
    }
    catch {
        if ($null -ne $policy) {
            try {
                $denied = [ordered]@{ result='DENIED'; operation='S5-03_READ_ONLY_FINGERPRINT'; target='localhost:5432/pa_tickets_codex_e2e/pa_tickets_codex'; ownerSid=$policy.ownerSid; artifactSha256=$expectedArtifactSha256; decisionAtUtc=[DateTime]::UtcNow.ToString('o'); failureCode='LOCAL_EVIDENCE_VERIFICATION_FAILED'; databaseClientInvoked=$false; assurance='OWNER_CONTROLLED_NOT_INDEPENDENT; LOCAL_ADMIN_TAMPERING_OUT_OF_SCOPE' }
                [void](Write-SanitizedAttempt $policy.evidenceDirectory $denied)
            } catch { throw 'Local evidence verification was denied; sanitized denial record could not be committed to the owner custody directory.' }
        }
        throw 'Local evidence verification was denied; a sanitized denial record was written when the owner custody policy was available.'
    }
    finally { if ($null -ne $ownerCertificate) { $ownerCertificate.Dispose() }; if ($null -ne $revocationCertificate) { $revocationCertificate.Dispose() } }
}

Export-ModuleMember -Function Test-DetachedRsaPssSignature, Read-VerifiedEnvelope, Assert-S5LocalAuthorization, Assert-ActiveRevocation, Assert-S5LocalPeerBinding, Invoke-OwnerLocalEvidenceVerification
