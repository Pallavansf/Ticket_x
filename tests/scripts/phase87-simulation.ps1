$ErrorActionPreference = 'Stop'

# This harness is intentionally restricted to the dedicated PostgreSQL test database.
$connection = $env:PA_TICKETS_TEST_CONNECTION_STRING
if ([string]::IsNullOrWhiteSpace($connection)) { throw 'Set PA_TICKETS_TEST_CONNECTION_STRING; simulation will not continue.' }
$dbMatch = [regex]::Match($connection, '(?i)(?:^|;)Database=([^;]+)')
if (-not $dbMatch.Success) { throw 'The PostgreSQL test connection must include an explicit Database value.' }
$database = $dbMatch.Groups[1].Value
if ($database -match '(?i)names_trainer|production|prod' -or $database -ne 'pa_tickets_test') { throw "Refusing to seed database '$database'. The simulation requires the dedicated pa_tickets_test database." }

$parts = foreach ($part in ($connection -split ';')) {
    if ([string]::IsNullOrWhiteSpace($part)) { continue }
    $index = $part.IndexOf('=')
    if ($index -lt 1) { throw 'Invalid PostgreSQL connection segment.' }
    $key = $part.Substring(0, $index).Trim().ToLowerInvariant()
    $value = $part.Substring($index + 1).Trim()
    $map = @{ host = 'host'; port = 'port'; database = 'dbname'; dbname = 'dbname'; username = 'user'; user = 'user'; password = 'password'; sslmode = 'sslmode' }
    if ($map.ContainsKey($key)) { '{0}="{1}"' -f $map[$key], $value }
}
$psqlConnection = $parts -join ' '
$identity = & (Get-Command psql -ErrorAction Stop).Source -X -w -At -d $psqlConnection -c 'select current_database() || ''|'' || current_user;'
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL identity check failed.' }
if (-not $identity.StartsWith('pa_tickets_test|', [StringComparison]::OrdinalIgnoreCase)) { throw "Unexpected PostgreSQL identity: $identity" }
Write-Output "POSTGRESQL TEST DATABASE VERIFIED: $identity"

# Provisioning resets only the dedicated test database and applies all supported schema slices.
& (Join-Path $PSScriptRoot 'provision-postgresql-schema.ps1')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL provisioning failed.' }

# These tests create deterministic synthetic identities/records through existing application services,
# run the real HTTP host, assert persistence and authorization, and clean up generated records.
dotnet test (Join-Path $PSScriptRoot '..\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj') --no-build --filter 'FullyQualifiedName~PostgreSqlApiE2ETests|FullyQualifiedName~ReportExportFormatterTests|FullyQualifiedName~ReportSchedule' --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) { throw 'Phase 8.7 simulation tests failed.' }
Write-Output 'PHASE 8.7 SIMULATION: PASS'