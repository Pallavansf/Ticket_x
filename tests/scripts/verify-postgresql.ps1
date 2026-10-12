[CmdletBinding()]
param(
    [string]$DatabaseName = $(if ($env:PA_TICKETS_TEST_DATABASE) { $env:PA_TICKETS_TEST_DATABASE } else { 'pa_tickets_test' })
)
$ErrorActionPreference = 'Stop'
$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) { throw 'psql was not found on PATH.' }
$connection = $env:PA_TICKETS_TEST_CONNECTION_STRING
if ([string]::IsNullOrWhiteSpace($connection)) { throw 'Set PA_TICKETS_TEST_CONNECTION_STRING in the process/user secret store. The value is never printed.' }
if ($DatabaseName -notmatch '^[a-z_][a-z0-9_]*$') { throw 'DatabaseName must contain only lowercase letters, digits, and underscores.' }
function Convert-ToLibpqConnectionString([string]$ConnectionString) {
    $keyMap = @{ host = 'host'; server = 'host'; port = 'port'; database = 'dbname'; dbname = 'dbname'; username = 'user'; user = 'user'; password = 'password'; sslmode = 'sslmode' }
    $items = foreach ($part in ($ConnectionString -split ';')) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        $equals = $part.IndexOf('=')
        if ($equals -lt 1) { throw 'The PostgreSQL connection string contains an invalid key/value segment.' }
        $rawKey = $part.Substring(0, $equals).Trim().ToLowerInvariant()
        if (-not $keyMap.ContainsKey($rawKey)) { continue }
        $value = $part.Substring($equals + 1).Trim()
        if (($value.Length -ge 2) -and (($value[0] -eq '"' -and $value[$value.Length - 1] -eq '"') -or ($value[0] -eq "'" -and $value[$value.Length - 1] -eq "'"))) { $value = $value.Substring(1, $value.Length - 2) }
        $escaped = $value.Replace('\', '\\').Replace('"', '\"')
        '{0}="{1}"' -f $keyMap[$rawKey], $escaped
    }
    if (-not $items) { throw 'The PostgreSQL connection string did not contain supported connection settings.' }
    return ($items -join ' ')
}
function Invoke-Psql([string]$ConnectionString, [string]$Sql) {
$libpqConnection = Convert-ToLibpqConnectionString $ConnectionString
    $result = & $psql.Source -X -w -v ON_ERROR_STOP=1 -At -q -d $libpqConnection -c $Sql 2>&1
    return ($result | Out-String).Trim()
}
$probe = Invoke-Psql $connection "select version(), current_database(), current_user, current_setting('port'), has_database_privilege(current_user, current_database(), 'CREATE');"
$parts = $probe -split '\|', 5
if ($parts.Count -lt 5) { throw 'PostgreSQL probe returned an unexpected shape.' }
Write-Output 'POSTGRESQL CONNECTION: PASS'
Write-Output ("POSTGRESQL VERSION: {0}" -f $parts[0])
Write-Output ("DATABASE: {0}" -f $parts[1])
Write-Output ("CURRENT USER: {0}" -f $parts[2])
Write-Output ("PORT: {0}" -f $parts[3])
Write-Output ("CREATE DATABASE PRIVILEGE (CURRENT DB): {0}" -f $parts[4])
$adminConnection = if ($env:PA_TICKETS_TEST_ADMIN_CONNECTION_STRING) { $env:PA_TICKETS_TEST_ADMIN_CONNECTION_STRING } else { $connection }
$exists = Invoke-Psql $adminConnection ("select exists(select 1 from pg_database where datname = '{0}');" -f $DatabaseName)
if ($exists -eq 't') { Write-Output 'DEDICATED TEST DATABASE: ALREADY EXISTS' }
else {
    $canCreate = Invoke-Psql $adminConnection "select has_database_privilege(current_user, 'postgres', 'CREATE');"
    if ($canCreate -ne 't') { Write-Output 'DEDICATED TEST DATABASE: NOT CREATED (admin connection lacks CREATE privilege on postgres)' }
    else { Invoke-Psql $adminConnection ('create database "{0}";' -f $DatabaseName) | Out-Null; Write-Output 'DEDICATED TEST DATABASE: CREATED' }
}
if ($env:PA_TICKETS_TEST_USER) { Write-Output ("TEST USER: {0}" -f $env:PA_TICKETS_TEST_USER) }
else { Write-Output ("TEST USER: {0} (existing configured user)" -f $parts[2]) }
Write-Output 'PROVIDER: PostgreSQL integration only (production remains Oracle.EntityFrameworkCore/Oracle 19c)'
Write-Output 'SCHEMA STRATEGY: Dedicated database; Oracle migrations are not executed. Provider-specific schema/persistence tests require an explicit PostgreSQL adapter.'



