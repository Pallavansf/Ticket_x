$ErrorActionPreference = 'Stop'
$psql = Get-Item "F:\PROGRAMS\Postgres\bin\psql.exe"
function Invoke-Psql {
  param(
    [string[]]$Arguments,
    [string]$FailureMessage
  )

  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    $output = & $psql.FullName @Arguments
    $exitCode = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($null -eq $exitCode -or $exitCode -ne 0) {
    throw "$FailureMessage (psql exit code $exitCode)."
  }

  return $output
}

$connection = $env:PA_TICKETS_TEST_CONNECTION_STRING
if ([string]::IsNullOrWhiteSpace($connection)) { throw 'Set PA_TICKETS_TEST_CONNECTION_STRING. The value is never printed.' }
$items = foreach ($part in ($connection -split ';')) {
  if ([string]::IsNullOrWhiteSpace($part)) { continue }
  $i = $part.IndexOf('='); if ($i -lt 1) { throw 'Invalid PostgreSQL connection segment.' }  
  $key = $part.Substring(0,$i).Trim().ToLowerInvariant(); $value = $part.Substring($i+1).Trim().Trim('"').Trim("'")
  $map=@{host='host';port='port';database='dbname';dbname='dbname';username='user';user='user';password='password';sslmode='sslmode'}
  if ($map.ContainsKey($key)) { '{0}="{1}"' -f $map[$key], $value }
}
$conn = $items -join ' '
$identity = Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-At', '-F', '|', '-q', '-d', $conn, '-c', 'select current_database(), current_user;') -FailureMessage 'PostgreSQL connection identity verification failed.'
$identityParts = ([string]$identity).Trim() -split '\|', 2
if ($identityParts.Count -ne 2 -or $identityParts[0] -cne 'pa_tickets_h_verify' -or $identityParts[1] -cne 'pa_tickets_test_user') {
  throw 'PostgreSQL connection identity mismatch; provisioning was not started.'
}
$verifiedDatabaseName = $identityParts[0]
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-schema.sql')) -FailureMessage 'PostgreSQL schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-phase5-schema.sql')) -FailureMessage 'PostgreSQL Phase 5 schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice3-schema.sql')) -FailureMessage 'PostgreSQL Slice 3 schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot '..\..\database\migrations\025_phase74_operational_reliability.sql')) -FailureMessage 'PostgreSQL Phase 7.4 reliability migration failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot '..\..\database\migrations\026_phase8_6_reporting.sql')) -FailureMessage 'PostgreSQL Phase 8.6 reporting migration failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice1-schema.sql')) -FailureMessage 'PostgreSQL Slice 1 schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice2-schema.sql')) -FailureMessage 'PostgreSQL Slice 2 schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot '..\..\database\migrations\016_change_management.sql')) -FailureMessage 'PostgreSQL Change Management migration failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-c', 'do $$ declare table_name text; begin foreach table_name in array array[''PA_TICKETS_REQUEST_FIELD'', ''PA_TICKETS_FIELD_OPTION'', ''PA_TICKETS_FIELD'', ''PA_TICKETS_REQUEST_TYPE'', ''PA_TICKETS_CATEGORY'', ''PA_TICKETS_SERVICE'', ''PA_TICKETS_USER_CLIENT'', ''PA_TICKETS_USER_ROLE'', ''PA_TICKETS_ROLE_PERMISSION'', ''PA_TICKETS_USER'', ''PA_TICKETS_TEAM'', ''PA_TICKETS_DEPARTMENT'', ''PA_TICKETS_ROLE'', ''PA_TICKETS_PERMISSION'', ''PA_TICKETS_STATUS_TRANSITION'', ''PA_TICKETS_STATUS'', ''PA_TICKETS_PRIORITY'', ''PA_TICKETS'', ''PA_TICKETS_IDEMPOTENCY'', ''PA_TICKETS_APPROVAL_RULE_STEP'', ''PA_TICKETS_APPROVAL_RULE_VERSION'', ''PA_TICKETS_APPROVAL_RULE'', ''PA_TICKETS_WORKFLOW'', ''PA_TICKETS_SLA_POLICY'', ''PA_TICKETS_SLA_CALENDAR'', ''PA_TICKETS_BACKGROUND_JOB'', ''PA_TICKETS_COMMUNICATION_TEMPLATE'', ''PA_TICKETS_NOTIFICATION_RULE''] loop if to_regclass(format(''%I'', table_name)) is not null then execute format(''truncate table %I restart identity cascade'', table_name); end if; end loop; end $$;') -FailureMessage 'PostgreSQL transactional data reset failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice1-schema.sql')) -FailureMessage 'PostgreSQL Slice 1 reference data provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice2-schema.sql')) -FailureMessage 'PostgreSQL Slice 2 reference data provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice4-schema.sql')) -FailureMessage 'PostgreSQL Slice 4 schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice5-schema.sql')) -FailureMessage 'PostgreSQL Slice 5 schema provisioning failed.'
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-slice6-schema.sql')) -FailureMessage 'PostgreSQL Slice 6 operational metadata provisioning failed.'
Write-Output 'POSTGRESQL SCHEMA: PASS'
Write-Output "DATABASE: $verifiedDatabaseName"
Invoke-Psql -Arguments @('-X', '-w', '-v', 'ON_ERROR_STOP=1', '-At', '-q', '-d', $conn, '-f', (Join-Path $PSScriptRoot 'postgresql-smoke.sql')) -FailureMessage 'PostgreSQL smoke test failed.'



