$ErrorActionPreference = 'Stop'
$psql = Get-Command psql -ErrorAction Stop
$connection = $env:PA_TICKETS_TEST_CONNECTION_STRING
if ([string]::IsNullOrWhiteSpace($connection)) { throw 'Set PA_TICKETS_TEST_CONNECTION_STRING. The value is never printed.' }
$items = foreach ($part in ($connection -split ';')) {
  if ([string]::IsNullOrWhiteSpace($part)) { continue }
  $i = $part.IndexOf('='); if ($i -lt 1) { throw 'Invalid PostgreSQL connection segment.' }
  $key = $part.Substring(0,$i).Trim().ToLowerInvariant(); $value = $part.Substring($i+1).Trim()
  $map=@{host='host';port='port';database='dbname';dbname='dbname';username='user';user='user';password='password';sslmode='sslmode'}
  if ($map.ContainsKey($key)) { '{0}="{1}"' -f $map[$key], $value }
}
$conn = $items -join ' '
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-phase5-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Phase 5 schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice3-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 3 schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot '..\..\database\migrations\025_phase74_operational_reliability.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Phase 7.4 reliability migration failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot '..\..\database\migrations\026_phase8_6_reporting.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Phase 8.6 reporting migration failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice1-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 1 schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice2-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 2 schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot '..\..\database\migrations\016_change_management.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Change Management migration failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -c 'do $$ declare table_name text; begin foreach table_name in array array[''PA_TICKETS_REQUEST_FIELD'', ''PA_TICKETS_FIELD_OPTION'', ''PA_TICKETS_FIELD'', ''PA_TICKETS_REQUEST_TYPE'', ''PA_TICKETS_CATEGORY'', ''PA_TICKETS_SERVICE'', ''PA_TICKETS_USER_CLIENT'', ''PA_TICKETS_USER_ROLE'', ''PA_TICKETS_ROLE_PERMISSION'', ''PA_TICKETS_USER'', ''PA_TICKETS_TEAM'', ''PA_TICKETS_DEPARTMENT'', ''PA_TICKETS_ROLE'', ''PA_TICKETS_PERMISSION'', ''PA_TICKETS_STATUS_TRANSITION'', ''PA_TICKETS_STATUS'', ''PA_TICKETS_PRIORITY'', ''PA_TICKETS'', ''PA_TICKETS_IDEMPOTENCY'', ''PA_TICKETS_APPROVAL_RULE_STEP'', ''PA_TICKETS_APPROVAL_RULE_VERSION'', ''PA_TICKETS_APPROVAL_RULE'', ''PA_TICKETS_WORKFLOW'', ''PA_TICKETS_SLA_POLICY'', ''PA_TICKETS_SLA_CALENDAR'', ''PA_TICKETS_BACKGROUND_JOB'', ''PA_TICKETS_COMMUNICATION_TEMPLATE'', ''PA_TICKETS_NOTIFICATION_RULE''] loop execute format(''truncate table %I restart identity cascade'', table_name); end loop; end $$;'
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL transactional data reset failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice1-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 1 reference data provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice2-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 2 reference data provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice4-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 4 schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice5-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 5 schema provisioning failed.' }
& $psql.Source -X -w -v ON_ERROR_STOP=1 -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-slice6-schema.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL Slice 6 operational metadata provisioning failed.' }
Write-Output 'POSTGRESQL SCHEMA: PASS'
Write-Output 'DATABASE: pa_tickets_test (scoped connection)'
& $psql.Source -X -w -v ON_ERROR_STOP=1 -At -q -d $conn -f (Join-Path $PSScriptRoot 'postgresql-smoke.sql')
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL smoke test failed.' }
