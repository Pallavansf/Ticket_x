# Local E2E trust: owner provisioning procedure

**Prepared for explicit owner execution; not executed.** S5-01 and S5-03 remain blocked. This document does not authorize PostgreSQL access. The adopted amendment SHA-256 is B78980670B1E0E8E79C333A36D7B93F46633C02B2172088C8B33469BED28A18E. The only target is localhost, IPv6 ::1, port 5432, database pa_tickets_codex_e2e, role pa_tickets_codex. Use Windows PowerShell 5.1 as the actual owner. Do not record secrets, private keys or unrestricted process command lines.

## Confirm account and artifact

```powershell
$ownerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$ownerSid
if ($ownerSid -cnotmatch '^S-1-5-21-(?:\d+-){3}\d+$') { throw 'Unsupported owner SID; stop.' }
$artifact = 'tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql'
$artifactSha = '2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8'
if ((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash -cne $artifactSha) { throw 'Artifact mismatch; stop.' }
```

Record the actual SID and account, owner, artifact/hash, decision, validity and custody in the existing S5-01 packet. A SID binds an account; it does not independently prove a person's identity.

## Owner-controlled keys and public pins

Inspect existing keys first. The owner explicitly selects a future certificate expiration and executes the following for the routine signing key in the owner's current-user store:

```powershell
# Set $ownerCertNotAfter to the owner's selected future date first.
if ($null -eq $ownerCertNotAfter -or $ownerCertNotAfter -le (Get-Date)) { throw 'Select a future expiration.' }
$ownerCert = New-SelfSignedCertificate -Subject 'CN=PA_TICKETS local owner signing' `
  -CertStoreLocation 'Cert:\CurrentUser\My' -Provider 'Microsoft Software Key Storage Provider' `
  -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyUsage DigitalSignature `
  -KeyExportPolicy NonExportable -NotAfter $ownerCertNotAfter
$ownerPin = $ownerCert.Thumbprint.ToUpperInvariant()
$ownerPin
```

Create a **different** recovery/revocation CNG RSA key, at least 3072 bits and non-exportable, in an owner-controlled offline Windows environment or hardware key store whose private key is unavailable to the routine verifier. On that separate system use the equivalent command with a separate expiration and subject CN=PA_TICKETS local recovery signing. Do not create its private key in the routine account's accessible store. Stop if separation cannot be established.

On each signing system inspect its own certificate, substituting its certificate variable below. This explicitly opens that private key only in the provisioning session:

```powershell
$key = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($ownerCert)
if ($key -isnot [Security.Cryptography.RSACng] -or $key.KeySize -lt 3072 -or
    $key.Key.ExportPolicy -ne [Security.Cryptography.CngExportPolicies]::None) { throw 'Key is not non-exportable CNG RSA >= 3072.' }
$key.Key.Provider.Provider
$key.Key.UniqueName
$key.Key.ExportPolicy
$key.Dispose()
```

Use the provider's Windows key-permission tools to inspect the actual private-key ACL. The routine key must be restricted to the owner and necessary OS administration; the recovery private key must be inaccessible to the routine verifier. Record provider, identifier, non-exportability, validity, ACL, custody and public thumbprint. Do not assume a filesystem location for a hardware key. The verifier uses public keys only and cannot prove these private-key properties.

Export **public certificates only** using Export-Certificate. Transfer the recovery public certificate by an owner-controlled channel. In an elevated owner session inspect each certificate and compare its thumbprint with the separately recorded pin. Require distinct pins. Import each public certificate with Import-Certificate -FilePath <public-cer-path> -CertStoreLocation 'Cert:\LocalMachine\My'. Confirm exactly one certificate per pin in that store. Never export a PFX or install the recovery private key on the routine verifier. Record creation, installation, pinning and future rotation as owner security-policy changes.

On the appropriate signing system, export a new public certificate file only. Substitute $recoveryCert on the separately controlled recovery system. Verify the resulting thumbprint before import.

```powershell
$publicCerPath = Read-Host 'New absolute path for the public certificate (.cer)'
if (Test-Path -LiteralPath $publicCerPath) { throw 'Output exists; stop.' }
Export-Certificate -Cert $ownerCert -FilePath $publicCerPath -Type CERT
```

For the selected current-user Microsoft Software Key Storage Provider, the owner can inspect the provider-reported key container without modifying its ACL. If it is not present, stop and use that provider's key-permission tooling; hardware keys require provider-specific evidence.

```powershell
$key = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($ownerCert)
if ($key -isnot [Security.Cryptography.RSACng]) { throw 'Not a CNG key.' }
$keyPath = Join-Path $env:APPDATA ('Microsoft\Crypto\Keys\' + $key.Key.UniqueName)
if (-not (Test-Path -LiteralPath $keyPath -PathType Leaf)) { throw 'Key file not found; inspect provider permissions.' }
Get-Acl -LiteralPath $keyPath | Format-List
$key.Dispose()
```

## Policy and evidence custody

Review the Windows service name/account against OS and installation evidence, executable path, SHA-256, valid Authenticode signer thumbprint, data directory and configuration path from OS and installation records. Do not log raw command lines. If the executable signature is not Valid, or the values cannot be established, stop: the verifier has no unsigned-executable exception.

The fixed paths are:
- %ProgramData%\Genesis\PA_TICKETS\LocalE2E\trust-policy.json
- %ProgramData%\Genesis\PA_TICKETS\LocalE2E\Evidence\
- %ProgramData%\Genesis\PA_TICKETS\LocalE2E\current-revocation.json

Before setup inspect these paths. If the root exists, stop and review; do not overwrite it with this first-time procedure. After confirming $ownerSid is the intended owner, the owner can create the directories and set ACLs explicitly in an elevated session:

```powershell
$root = Join-Path $env:ProgramData 'Genesis\PA_TICKETS\LocalE2E'
$evidence = Join-Path $root 'Evidence'
if (Test-Path -LiteralPath $root) { throw 'Existing setup: stop and review.' }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
icacls $root /inheritance:r /grant:r ("*$($ownerSid):(OI)(CI)F") '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F'
if ($LASTEXITCODE -ne 0) { throw 'Root ACL change failed; stop.' }
icacls $evidence /inheritance:r /grant:r ("*$($ownerSid):(OI)(CI)F") '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F'
if ($LASTEXITCODE -ne 0) { throw 'Evidence ACL change failed; stop.' }
Get-Acl -LiteralPath $root | Format-List
Get-Acl -LiteralPath $evidence | Format-List
```

Inspect owner and every allow ACE, including inherited ACEs. Only the actual owner SID, SYSTEM (S-1-5-18) and Administrators (S-1-5-32-544) may have write rights. Stop on unexpected or unresolved writers. Create trust-policy.json as UTF-8 JSON with exactly the fields below; replace placeholders with reviewed values and escape JSON backslashes. Inspect its ACL separately and record its SHA-256 and sanitized ACL evidence.

```json
{
  "schemaVersion": 1,
  "ownerSid": "<actual SID>",
  "ownerSignerThumbprint": "<40 uppercase hex>",
  "revocationSignerThumbprint": "<different 40 uppercase hex>",
  "serviceName": "<actual service>",
  "serviceAccount": "<exact service StartName>",
  "processImagePath": "<absolute executable path>",
  "processImageSha256": "<64 uppercase hex>",
  "processSignerThumbprint": "<valid Authenticode signer thumbprint>",
  "dataDirectory": "<absolute data directory>",
  "configurationFile": "<absolute configuration file>",
  "evidenceDirectory": "<expanded fixed Evidence directory>"
}
```

### Create the exact policy file

Run this only after explicitly creating and inspecting the protected directories and after independently checking the service and executable against the owner's installation records. Enter actual paths and the separately recorded recovery public pin. The service lookup and signature check read Windows state; they do not contact PostgreSQL. The owner must review their results before writing the policy.

```powershell
$serviceName = Read-Host 'Exact Windows PostgreSQL service name'
$service = @(Get-CimInstance Win32_Service | Where-Object { $_.Name -ceq $serviceName })
if ($service.Count -ne 1 -or $service[0].State -cne 'Running') { throw 'Service missing, ambiguous or not running.' }
$imagePath = Read-Host 'Reviewed absolute PostgreSQL executable path'
$dataDirectory = Read-Host 'Reviewed absolute PostgreSQL data directory'
$configurationFile = Read-Host 'Reviewed absolute PostgreSQL configuration file path'
$recoveryPin = (Read-Host 'Separately recorded recovery public certificate thumbprint').ToUpperInvariant()
if ($ownerPin -cnotmatch '^[A-F0-9]{40}$' -or $recoveryPin -cnotmatch '^[A-F0-9]{40}$' -or $ownerPin -ceq $recoveryPin) { throw 'Distinct certificate pins required.' }
if (-not (Test-Path -LiteralPath $imagePath -PathType Leaf) -or -not (Test-Path -LiteralPath $configurationFile -PathType Leaf) -or -not (Test-Path -LiteralPath $dataDirectory -PathType Container)) { throw 'An expected local path is missing.' }
$imageSignature = Get-AuthenticodeSignature -FilePath $imagePath
if ($imageSignature.Status -ne 'Valid' -or $null -eq $imageSignature.SignerCertificate) { throw 'Valid Authenticode signature required.' }
$imageSha = (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash
$imageSigner = $imageSignature.SignerCertificate.Thumbprint.ToUpperInvariant()
$service[0] | Select-Object Name,State,ProcessId,StartName
$imageSha
$imageSigner
$review = Read-Host 'Type REVIEWED only after comparing these values with owner-controlled installation records'
if ($review -cne 'REVIEWED') { throw 'Owner review is required.' }
$policy = [ordered]@{
  schemaVersion=1; ownerSid=$ownerSid; ownerSignerThumbprint=$ownerPin; revocationSignerThumbprint=$recoveryPin
  serviceName=$service[0].Name; serviceAccount=$service[0].StartName
  processImagePath=[IO.Path]::GetFullPath($imagePath); processImageSha256=$imageSha; processSignerThumbprint=$imageSigner
  dataDirectory=[IO.Path]::GetFullPath($dataDirectory); configurationFile=[IO.Path]::GetFullPath($configurationFile)
  evidenceDirectory=[IO.Path]::GetFullPath($evidence)
}
$policyPath = Join-Path $root 'trust-policy.json'
$policyBytes = [Text.UTF8Encoding]::new($false).GetBytes(($policy | ConvertTo-Json -Depth 5))
$stream = [IO.File]::Open($policyPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $stream.Write($policyBytes, 0, $policyBytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
Get-Acl -LiteralPath $policyPath | Format-List
Get-FileHash -LiteralPath $policyPath -Algorithm SHA256
```

Confirm both public pins in LocalMachine\My separately, inspect the resulting file's owner and every allow ACE, and retain the hash and sanitized ACL evidence. The owner confirmation and policy file do not themselves prove PostgreSQL role grants.

## Actual role privileges: separate authorization

The owner must obtain current attributable evidence through a **separately authorized** read-only privilege review or verifiable existing administration records. Cover role and inherited memberships; superuser, CREATEDB, CREATEROLE, REPLICATION, BYPASSRLS; database CONNECT and other databases; schema USAGE/CREATE; effective table SELECT/write/DDL rights and sequence advancement. Keep sanitized evidence under the fixed Evidence directory with source, date, scope, owner review and SHA-256. Do not assert that privileges are absent without evidence. A signed assertion and matching hash do not measure database grants.

The verifier compares the application relation allowlist against this exact offline extraction from the pinned artifact:

```powershell
$relationAllowlist = @([regex]::Matches((Get-Content -LiteralPath $artifact -Raw), '\bPA_TICKETS_[A-Z0-9_]+\b') | ForEach-Object { $_.Value } | Sort-Object -Unique)
$relationAllowlist
```

## Signed envelopes

Each JSON envelope contains exactly schemaVersion=1, signerThumbprint (uppercase 40 hex), payloadBase64 and signatureBase64. Sign the exact UTF-8 JSON payload bytes with RSA-PSS/SHA-256; the payload SHA-256 is the hash of the decoded payload bytes, not the envelope. Do not reserialize the payload after signing. The following helper uses an already provisioned private certificate, creates no keys and refuses to overwrite output. Execute it only deliberately as owner; use the separate recovery system for revocation.

```powershell
function Write-LocalSignedEnvelope($payload, $cert, [string]$outputPath) {
  $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
  if ($rsa -isnot [Security.Cryptography.RSACng] -or $rsa.KeySize -lt 3072 -or $rsa.Key.ExportPolicy -ne [Security.Cryptography.CngExportPolicies]::None) { throw 'Requires non-exportable CNG RSA >= 3072.' }
  try {
    $utf8 = [Text.UTF8Encoding]::new($false)
    $bytes = $utf8.GetBytes(($payload | ConvertTo-Json -Depth 20 -Compress))
    $sig = $rsa.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') } finally { $sha.Dispose() }
    $envObj = [ordered]@{ schemaVersion=1; signerThumbprint=$cert.Thumbprint.ToUpperInvariant(); payloadBase64=[Convert]::ToBase64String($bytes); signatureBase64=[Convert]::ToBase64String($sig) }
    $outBytes = $utf8.GetBytes(($envObj | ConvertTo-Json -Compress))
    $stream = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($outBytes, 0, $outBytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    return $hash
  } finally { if ($null -ne $rsa) { $rsa.Dispose() } }
}
```

### Owner-signed authorization

Construct a PowerShell object named $authorization with exactly these fields and actual values, then use `$authPayloadSha = Write-LocalSignedEnvelope $authorization $ownerCert <new-authorization-envelope-path>`. Do not sign placeholders. Use JSON boolean values and arrays.

```text
schemaVersion=1; operation=S5-03_READ_ONLY_FINGERPRINT; host=localhost; port=5432;
database=pa_tickets_codex_e2e; role=pa_tickets_codex; operatorSid=<owner SID>;
artifactPath=tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql;
artifactSha256=2CE9F2FD1943438821BFDD1C0DB481BD4C0F3F3D5E3618B69BB4280252923AD8;
expectedEffects=READ_ONLY_CATALOG_FINGERPRINT_NO_SCHEMA_OR_DATA_CHANGE;
validFromUtc=<UTC>; expiresAtUtc=<UTC>;
excludedOperations=[pa_tickets_test,pa_tickets_h_verify,pa_tickets_test_user,migration_026,DROP_DATABASE,SCHEMA_RESET,BROAD_CLEANUP];
uncertainOutcome=STOP_NO_RETRY_MARK_UNKNOWN; recoveryPlanReference=<actual owner record>;
evidenceCustodianSid=<owner SID>; evidenceDirectory=<exact policy Evidence path>;
leastPrivilegeEvidencePath=<actual evidence file>; leastPrivilegeEvidenceSha256=<file SHA-256>;
leastPrivilege=<object below>
```

The start must not be future and expiration must be future and no more than 30 minutes after start. leastPrivilege has exactly: role, database, isSuperuser, canCreateDatabase, canCreateRoles, canReplicate, bypassRowLevelSecurity, inheritsPrivilegedRole, canCreateObjects, canWriteTables, canInsertTables, canUpdateTables, canDeleteTables, canTruncateTables, canAlterSchema, canAdvanceSequences, accessibleDatabases, selectRelationAllowlist, evidenceReference. Role/database must match the target; every is/can/bypass/inherits value must be JSON false supported by actual evidence. accessibleDatabases is the single-element array ["pa_tickets_codex_e2e"], and selectRelationAllowlist is exactly the offline list above. evidenceReference identifies the reviewed privilege evidence. The owner-signed S5-01 packet must also identify no test writes or cleanup, validity, revocation, recovery and custody.

Only after the owner has reviewed actual privilege evidence and selected the exact S5-03 scope may the following construct the owner-signed authorization. It records owner assertions; it does not measure grants. Load and verify the local policy in this session before signing.

```powershell
Import-Module .\tests\scripts\LocalE2eTrust.psm1 -Force
$policyPath = Join-Path $env:ProgramData 'Genesis\PA_TICKETS\LocalE2E\trust-policy.json'
$module = Get-Module LocalE2eTrust
$policy = & $module { param($p) Get-OwnerLocalPolicy $p } $policyPath
$privEvidencePath = Read-Host 'Absolute path of owner-reviewed least-privilege evidence'
$recoveryRef = Read-Host 'Existing recovery plan reference'
$authEnvelopePath = Read-Host 'New authorization envelope path'
if (-not (Test-Path -LiteralPath $privEvidencePath -PathType Leaf) -or [string]::IsNullOrWhiteSpace($recoveryRef)) { throw 'Evidence and recovery reference required.' }
$confirmed = Read-Host 'Type VERIFIED only after reviewing actual privilege facts and scope'
if ($confirmed -cne 'VERIFIED') { throw 'Owner privilege review not confirmed.' }
$validFrom = [DateTimeOffset]::UtcNow
$leastPrivilege = [ordered]@{
  role='pa_tickets_codex'; database='pa_tickets_codex_e2e'; isSuperuser=$false
  canCreateDatabase=$false; canCreateRoles=$false; canReplicate=$false; bypassRowLevelSecurity=$false
  inheritsPrivilegedRole=$false; canCreateObjects=$false; canWriteTables=$false; canInsertTables=$false
  canUpdateTables=$false; canDeleteTables=$false; canTruncateTables=$false; canAlterSchema=$false
  canAdvanceSequences=$false; accessibleDatabases=@('pa_tickets_codex_e2e')
  selectRelationAllowlist=@($relationAllowlist); evidenceReference=$privEvidencePath
}
$authorization = [ordered]@{
  schemaVersion=1; operation='S5-03_READ_ONLY_FINGERPRINT'; host='localhost'; port=5432
  database='pa_tickets_codex_e2e'; role='pa_tickets_codex'; operatorSid=$ownerSid
  artifactPath=$artifact; artifactSha256=$artifactSha
  expectedEffects='READ_ONLY_CATALOG_FINGERPRINT_NO_SCHEMA_OR_DATA_CHANGE'
  validFromUtc=$validFrom.ToString('o'); expiresAtUtc=$validFrom.AddMinutes(25).ToString('o')
  excludedOperations=@('pa_tickets_test','pa_tickets_h_verify','pa_tickets_test_user','migration_026','DROP_DATABASE','SCHEMA_RESET','BROAD_CLEANUP')
  uncertainOutcome='STOP_NO_RETRY_MARK_UNKNOWN'; recoveryPlanReference=$recoveryRef
  evidenceCustodianSid=$ownerSid; evidenceDirectory=$policy.evidenceDirectory
  leastPrivilege=$leastPrivilege; leastPrivilegeEvidencePath=$privEvidencePath
  leastPrivilegeEvidenceSha256=(Get-FileHash -LiteralPath $privEvidencePath -Algorithm SHA256).Hash
}
$authPayloadSha = Write-LocalSignedEnvelope $authorization $ownerCert $authEnvelopePath
```

### Owner-signed peer

Collect Windows facts immediately before signing; this does not connect to PostgreSQL. The verifier recollects them when invoked.

```powershell
Import-Module .\tests\scripts\LocalE2eTrust.psm1 -Force
$policyPath = Join-Path $env:ProgramData 'Genesis\PA_TICKETS\LocalE2E\trust-policy.json'
$module = Get-Module LocalE2eTrust
$policy = & $module { param($p) Get-OwnerLocalPolicy $p } $policyPath
$facts = & $module { param($p) Get-LocalPostgresListenerFacts $p } $policy
$captured = [DateTimeOffset]::UtcNow
$peer = [ordered]@{
  schemaVersion=1; addressFamily=$facts.AddressFamily; localAddress=$facts.LocalAddress; localPort=$facts.LocalPort
  listenerProcessId=$facts.ProcessId; processId=$facts.ProcessId; processAncestorIds=@($facts.ParentProcessIds)
  processStartTimeUtc=$facts.ProcessStartTimeUtc; processImagePath=$facts.ProcessImagePath
  processImageSha256=$facts.ProcessImageSha256; processSignerThumbprint=$facts.ProcessSignerThumbprint
  serviceName=$facts.ServiceName; serviceProcessId=$facts.ServiceProcessId; serviceAccount=$facts.ServiceAccount
  serviceState=$facts.ServiceState; dataDirectory=$facts.DataDirectory; configurationFile=$facts.ConfigurationFile
  capturedAtUtc=$captured.ToString('o'); expiresAtUtc=$captured.AddMinutes(5).ToString('o')
  host='localhost'; port=5432; database='pa_tickets_codex_e2e'; role='pa_tickets_codex'
  artifactPath=$artifact; artifactSha256=$artifactSha; authorizationPayloadSha256=$authPayloadSha
}
$peerEnvelopePath = Read-Host 'New peer envelope path'
$peerPayloadSha = Write-LocalSignedEnvelope $peer $ownerCert $peerEnvelopePath
```

The peer capture must be no more than two minutes old at verification. Stop on any OS fact mismatch; do not print raw command lines.

### Separate recovery-signed revocation

On the separate recovery signing system construct an object with exactly schemaVersion=1, authorizationVersion=1, peerPolicyVersion=1, status='ACTIVE', authorizationPayloadSha256=<actual auth payload hash>, peerPayloadSha256=<actual peer payload hash>, issuedAtUtc=<current UTC>, expiresAtUtc=<UTC no more than five minutes after issue>. Sign it with the recovery certificate and the helper. Transfer only the signed envelope to the fixed current-revocation.json path. First creation must not overwrite a file. Preserve and archive a prior statement before an explicitly reviewed replacement; never silently change revoked to active. Missing, revoked, stale, mismatched or expired status fails closed. If transfer exceeds the two-minute peer window, recapture and re-sign fresh peer and revocation evidence.

On the separate recovery system, obtain the two actual payload hashes through the owner-controlled channel and explicitly confirm they have not been revoked before signing ACTIVE. `$recoveryCert` must be the separately provisioned certificate and the signing helper must be loaded there.

```powershell
$authPayloadSha = Read-Host 'Authorization payload SHA-256'
$peerPayloadSha = Read-Host 'Peer payload SHA-256'
$revocationEnvelopePath = Read-Host 'New recovery-signed envelope path'
if ($authPayloadSha -cnotmatch '^[A-F0-9]{64}$' -or $peerPayloadSha -cnotmatch '^[A-F0-9]{64}$') { throw 'Expected uppercase SHA-256 values.' }
$issued = [DateTimeOffset]::UtcNow
$revocation = [ordered]@{
  schemaVersion=1; authorizationVersion=1; peerPolicyVersion=1; status='ACTIVE'
  authorizationPayloadSha256=$authPayloadSha; peerPayloadSha256=$peerPayloadSha
  issuedAtUtc=$issued.ToString('o'); expiresAtUtc=$issued.AddMinutes(5).ToString('o')
}
$revocationPayloadSha = Write-LocalSignedEnvelope $revocation $recoveryCert $revocationEnvelopePath
```

## Run offline verification after setup

After owner setup and packet preparation, from the repository as the bound owner, use the actual envelope paths and run:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\scripts\invoke-postgresql-foundational-fingerprint.ps1 `
  -Mode VerifyLocal -AuthorizationProfilePath $authEnvelopePath `
  -ConnectionProfilePath $peerEnvelopePath
```

The verifier reads the revocation envelope from its fixed path, validates signatures and bindings, recollects Windows facts and writes sanitized attempt evidence when the policy/custody directory are trusted. It reports databaseClientInvoked=false and executionEligible=false. Retain the record path/hash. A failure before the policy is trusted cannot be recorded in that custody directory; retain a sanitized failure separately. VerifyLocal does not measure PostgreSQL grants.

S5-01 requires actual setup, privilege evidence, signed packet and verified local facts. The first S5-03 fingerprint additionally requires its own exact operation authorization and an implemented/reviewed connection guard and database execution path. Execute remains disabled. On any uncertain database outcome, stop, do not retry, mark UNKNOWN and require separately authorized read-only reconciliation.

## Owner decision preparation: Windows process identity and listener correction

**Status: PROCEDURE / PROPOSAL ONLY.** Run this read-only inspection in elevated Windows PowerShell 5.1. It discovers current listeners rather than relying on a previous PID. It does not retrieve process command lines. Stop if path, parent chain, service relationship, or ACL evidence is incomplete.

    $ErrorActionPreference = 'Stop'
    $expectedImage = 'F:\PROGRAMS\Postgres\bin\postgres.exe'
    $expectedHash = 'AF5B897CB69C9CE692A4A15ECD022B540DB85DB1ADD0F66D2B9F0697BE2451A'
    $wi = [Security.Principal.WindowsIdentity]::GetCurrent()
    $wp = [Security.Principal.WindowsPrincipal]::new($wi)
    [pscustomobject]@{ Account=$wi.Name; SID=$wi.User.Value; Elevated=$wp.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    if (-not $wp.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Open elevated PowerShell and rerun.' }
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort 5432 | Select-Object LocalAddress,LocalPort,OwningProcess)
    $listeners | Sort-Object LocalAddress,OwningProcess | Format-Table -AutoSize
    $pids = @($listeners.OwningProcess | Sort-Object -Unique)
    if ($pids.Count -eq 0) { throw 'No listener on port 5432; stop.' }
    $procs = @(Get-CimInstance Win32_Process)
    $svcs = @(Get-CimInstance Win32_Service)
    foreach ($listenerPid in $pids) {
      $p = $procs | Where-Object ProcessId -eq $listenerPid
      if (-not $p -or -not $p.ExecutablePath) { throw "Cannot read executable path for PID $listenerPid; stop." }
      $path = [IO.Path]::GetFullPath($p.ExecutablePath)
      $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
      $sig = Get-AuthenticodeSignature -FilePath $path
      [pscustomobject]@{ ListenerPID=$listenerPid; Addresses=(($listeners | Where-Object OwningProcess -eq $listenerPid | ForEach-Object LocalAddress | Sort-Object -Unique) -join ','); ImagePath=$path; ImageSHA256=$hash; CandidatePathMatch=($path -ieq [IO.Path]::GetFullPath($expectedImage)); CandidateHashMatch=($hash -ceq $expectedHash); Authenticode=[string]$sig.Status; SignerThumbprint=if ($sig.SignerCertificate) {$sig.SignerCertificate.Thumbprint} else {$null}; StartTime=$p.CreationDate; ParentPID=$p.ParentProcessId } | Format-List
      $ancestorPid = [uint32]$p.ParentProcessId
      for ($depth=0; $ancestorPid -and $depth -lt 8; $depth++) {
        $a = $procs | Where-Object ProcessId -eq $ancestorPid
        if (-not $a) { throw "Cannot resolve parent PID $ancestorPid; stop." }
        [pscustomobject]@{ PID=$a.ProcessId; ParentPID=$a.ParentProcessId; ImagePath=$a.ExecutablePath; StartTime=$a.CreationDate } | Format-List
        foreach ($s in ($svcs | Where-Object ProcessId -eq $ancestorPid)) {
          $svcImage = if ($s.PathName -match '^\s*"([^"]+\.exe)"') {$Matches[1]} elseif ($s.PathName -match '^\s*(\S+\.exe)') {$Matches[1]} else {$null}
          [pscustomobject]@{ Service=$s.Name; State=$s.State; Account=$s.StartName; PID=$s.ProcessId; ServiceExecutable=$svcImage } | Format-List
        }
        $ancestorPid = [uint32]$a.ParentProcessId
      }
      $d = Get-Item -LiteralPath (Split-Path -Parent $path)
      while ($d) {
        $acl = Get-Acl -LiteralPath $d.FullName
        [pscustomobject]@{ Directory=$d.FullName; Owner=$acl.Owner; SDDL=$acl.Sddl; Access=(($acl.Access | ForEach-Object {'{0}: {1} {2} Inherited={3}' -f $_.IdentityReference,$_.FileSystemRights,$_.AccessControlType,$_.IsInherited}) -join ' | ')} | Format-List
        if (-not $d.Parent) { break }; $d=$d.Parent
      }
    }
    $svcs | Where-Object {$_.Name -match 'postgres' -or $_.DisplayName -match 'postgres'} | ForEach-Object {
      $exe = if ($_.PathName -match '^\s*"([^"]+\.exe)"') {$Matches[1]} elseif ($_.PathName -match '^\s*(\S+\.exe)') {$Matches[1]} else {$null}
      [pscustomobject]@{ Name=$_.Name; State=$_.State; StartMode=$_.StartMode; Account=$_.StartName; PID=$_.ProcessId; ServiceExecutable=$exe }
    } | Format-List

The elevated output must be reviewed for the exact listener address, listener PID, process image path, process start time, complete parent chain, service PID/account and executable, image hash/signature, and ACLs on the executable directory and every parent directory. The current file hash cannot prove which bytes a running process loaded. Do not assume prior listener PID 5292 or parent PID 4168 remain current. A prior non-elevated check could not read their image paths. The installed candidate previously measured at the path above had the stated SHA-256 and Authenticode status NotSigned; remeasure. This procedure reports ACLs but does not automatically decide effective write access. The owner must resolve nested group membership, aliases, deny ACEs and inherited permissions for every image/parent directory; stop if effective access remains unclear or any untrusted principal can write, replace, delete, take ownership of, or change permissions on those paths.

## Proposed listener configuration change and recovery

Candidate file: F:\PROGRAMS\Postgres\data\postgresql.conf, previously observed with active setting listen_addresses = '*'. First establish from current service settings that this is the effective file and identify any command-line or auto-configuration override.

Proposed replacement: listen_addresses = '::1,127.0.0.1'. This retains IPv6 and IPv4 loopback clients and removes wildcard/non-loopback listeners. The prior snapshot showed listeners on :: and 0.0.0.0; applications may rely on remote IPv4 or IPv6 access. A snapshot with zero established sessions does not establish that no application uses those addresses. The owner must inventory client configurations and accept the impact. If any client needs remote access, this proposal is incompatible and must not be applied as written.

Changing listen_addresses requires a full PostgreSQL service restart, interrupting existing sessions. Before any separately approved change, capture the effective file and active setting, file metadata/hash, service identity, listener addresses/PIDs, process identity and ACL evidence. Make a byte-preserving backup in protected owner-controlled storage; verify its hash and restorability. Agree on a maintenance window.

After an authorized restart, rerun the elevated inspection and confirm a new process start time, verified service/process relationship, and only ::1:5432 plus 127.0.0.1:5432 for that process, with no wildcard/non-loopback listener. OS evidence still does not establish database, role or privileges. If connectivity fails, restore the byte-identical backup, verify its hash, restart the same service within the approved window, verify the prior listeners, and request each affected application's approved health check. Do not add remote binds or firewall rules as an ad hoc repair. Record the failure and recovery evidence.

New owner authorization is required for access-impact acceptance, the exact file/service, backup and custody, configuration edit, maintenance window, restart, and rollback steps. None is granted here.

## Proposed local-only unsigned executable amendment

**DRAFT FOR OWNER DECISION; not adopted, effective or implemented.** Adopted SOP v3.1 continues to require valid Authenticode; the existing verifier still rejects NotSigned. The adopted amendment remains unchanged at SHA-256 B78980670B1E0E8E79C333A36D7B93F46633C02B2172088C8B33469BED28A18E.

Proposed text for a separately versioned amendment:

> For the dedicated localhost / IPv6 loopback ::1:5432, database pa_tickets_codex_e2e and role pa_tickets_codex only, an unsigned PostgreSQL server executable may satisfy executable authentication only when every condition is evidenced and pinned in the owner-controlled local policy: (a) provenance of the exact installed distribution is verified from an authenticated vendor package or equivalent verifiable release chain, with package identity, version, build and architecture; (b) authenticated package contents are verified and the installed executable is mapped to that package by matching its exact SHA-256, without relying on registry publisher/version metadata or a disk hash alone; (c) the executable and every parent directory are protected against untrusted writes and ACL evidence is reviewed; (d) current Windows service, process, ancestry, listener PID/address and image path are verified together; (e) the exact executable SHA-256 is pinned in an attributable owner-approved policy; and (f) missing, stale, revoked or mismatched evidence denies operation. This alternative applies only to that server image and does not authorize unsigned clients, other instances/environments, or any database operation.

Final text must name the package provenance source, trust anchor and evidence custody method. A signed installer alone is insufficient unless the exact installed executable is proven to be a package member. Any package/image replacement requires fresh provenance review, owner approval and policy/verifier update. The owner must accept that a local administrator can alter package, ACL and evidence; this does not resist a malicious local administrator.

New owner decisions: approve or reject this policy direction; name the trusted package source and verification method; accept the local-administrator threat boundary; then adopt separately versioned final text with its own exact SHA-256 and effective timestamp. Until adoption and a reviewed verifier change, Authenticode Valid remains mandatory and the unsigned executable remains a blocker.
