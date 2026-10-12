[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$solution = Join-Path $repositoryRoot 'PA.Tickets.sln'
$scan = & dotnet list $solution package --vulnerable --include-transitive 2>&1
$exitCode = $LASTEXITCODE
$scan | ForEach-Object { Write-Output $_ }

if ($exitCode -ne 0) {
    throw "NuGet vulnerability scan failed with exit code $exitCode."
}

$scanText = $scan -join [Environment]::NewLine
if ($scanText -match 'following vulnerable packages|>\s+\S+\s+\S+\s+(Critical|High|Moderate|Low)\s+https?://') {
    throw 'NuGet vulnerability advisories were found. Review package, resolved version, severity, advisory, and remediation before release.'
}

Write-Output 'NUGET VULNERABILITY GATE: PASS (no advisories reported for the solution)'
