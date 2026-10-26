
$ErrorActionPreference = 'Stop'

$dll = Join-Path $PWD 'tests\PA.Tickets.Domain.Tests\bin\Debug\net8.0\Npgsql.dll'
if (-not (Test-Path $dll)) {
    throw "Npgsql.dll not found. Stop; do not install or restore packages."
}
Add-Type -Path $dll

$secure = Read-Host 'Password for pa_tickets_test_user' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)

try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $secure.Dispose()
}

$builder = [Npgsql.NpgsqlConnectionStringBuilder]::new()
$builder.Host = 'localhost'
$builder.Port = 5432
$builder.Database = 'pa_tickets_h_verify'
$builder.Username = 'pa_tickets_test_user'
$builder.Password = $password

$env:PA_TICKETS_TEST_CONNECTION_STRING = $builder.ConnectionString
$password = $null
$builder = $null

$connection = [Npgsql.NpgsqlConnection]::new(
    $env:PA_TICKETS_TEST_CONNECTION_STRING
)

try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = @'
SELECT current_database(), current_user,
       has_schema_privilege(current_user, 'public', 'USAGE'),
       has_schema_privilege(current_user, 'public', 'CREATE')
'@

    $reader = $command.ExecuteReader()
    if (-not $reader.Read()) { throw 'No identity row returned.' }

    $database = $reader.GetString(0)
    $role = $reader.GetString(1)
    $usage = $reader.GetBoolean(2)
    $create = $reader.GetBoolean(3)

    $reader.Close()

    if ($database -cne 'pa_tickets_h_verify' -or
        $role -cne 'pa_tickets_test_user') {
        throw 'Identity mismatch. Do not provision or run tests.'
    }

    "Database: $database"
    "Role: $role"
    "Public schema USAGE: $usage"
    "Public schema CREATE: $create"

    if (-not $usage -or -not $create) {
        throw 'Schema permissions need review before provisioning.'
    }

    'Identity verified. No provisioning or tests have been run.'
    'Keep this PowerShell process open for subsequent steps.'
    'Do not print the connection string.'
    $global:PA_TICKETS_H_VERIFIED = $true
    $global:PA_TICKETS_H_CONNECTION = $env:PA_TICKETS_TEST_CONNECTION_STRING
} finally {
    if ($reader) { $reader.Dispose() }
    if ($command) { $command.Dispose() }
    $connection.Dispose()
}
