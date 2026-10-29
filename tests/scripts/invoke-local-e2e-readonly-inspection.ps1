[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT', 'S5-03_READ_ONLY_FINGERPRINT')]
    [string] $Operation,

    [Parameter(Mandatory)]
    [switch] $ExecuteReadOnly
)

$ErrorActionPreference = 'Stop'
if (-not $ExecuteReadOnly) { throw 'Pass -ExecuteReadOnly to explicitly invoke the fixed local read-only operation.' }

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifacts = @{
    'LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT' = @{
        Path = 'tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql'
        Sha256 = '0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034'
    }
    'S5-03_READ_ONLY_FINGERPRINT' = @{
        Path = 'tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql'
        Sha256 = '6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342'
    }
}
$artifactPath = Join-Path $repositoryRoot $artifacts[$Operation].Path
if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) { throw 'Pinned SQL artifact is missing.' }
$actualHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -cne $artifacts[$Operation].Sha256) { throw 'Pinned SQL artifact hash mismatch; no connection attempted.' }

$oldOperation = $env:PA_TICKETS_LOCAL_E2E_READONLY_OPERATION
$oldConfirmation = $env:PA_TICKETS_LOCAL_E2E_READONLY_CONFIRM
try {
    $env:PA_TICKETS_LOCAL_E2E_READONLY_OPERATION = $Operation
    $env:PA_TICKETS_LOCAL_E2E_READONLY_CONFIRM = 'RUN_FIXED_READ_ONLY_OPERATION'
    dotnet test (Join-Path $repositoryRoot 'tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj') `
        --no-restore `
        --filter 'FullyQualifiedName~LocalE2eReadOnlyCatalogInspectionExecutionTests'
    if ($LASTEXITCODE -ne 0) { throw 'The bounded local read-only operation failed; do not retry automatically.' }
}
finally {
    $env:PA_TICKETS_LOCAL_E2E_READONLY_OPERATION = $oldOperation
    $env:PA_TICKETS_LOCAL_E2E_READONLY_CONFIRM = $oldConfirmation
}
