[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Deployment invariant: the installed release copy of this script must have a
# valid trusted Authenticode signature for the task's AllSigned owner context.
# This diagnostic never starts the Worker, writes to LocalDB, opens a browser,
# or sends an LMAX order.
$release = 'C:\deploy\IntradayPlatform\releases\f1692d8e16f8ed707fe5e27748d18c11df4a8b64'
$resultPath = Join-Path $PSScriptRoot 'owner-context-result.json'
$startedAtUtc = [DateTimeOffset]::UtcNow
$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()

$result = [ordered]@{
    schema_version = 'lmax_demo_owner_context_result_v1'
    marker = 'LMAX_DEMO_OWNER_CONTEXT_PREFLIGHT'
    started_at_utc = $startedAtUtc.ToString('o')
    completed_at_utc = $null
    status = 'FAILED'
    failure_code = $null
    identity = $currentIdentity.Name
    sid = $currentIdentity.User.Value
    profile = $env:USERPROFILE
    localdb_instance = $null
    database = $null
    localdb_instance_visible = $false
    worker_release_visible = $false
    chrome_visible = $false
    sql_access_verified = $false
    broker_gui_authenticated = $false
    no_worker_started = $true
}

function Get-FailureCode {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    $message = [string]$ErrorRecord.Exception.Message
    if ($message -match '^LMAX_DEMO_[A-Z0-9_]+$') {
        return $message
    }

    return 'LMAX_DEMO_OWNER_CONTEXT_PREFLIGHT_FAILED'
}

function Write-OwnerContextResult {
    param([System.Collections.IDictionary]$Result)

    $Result['completed_at_utc'] = [DateTimeOffset]::UtcNow.ToString('o')
    $json = $Result | ConvertTo-Json -Compress -Depth 6
    [System.IO.File]::WriteAllText(
        $resultPath,
        $json,
        [System.Text.UTF8Encoding]::new($false))
    return $json
}

function Normalize-LocalDbInstance {
    param([string]$Value)

    $match = [regex]::Match(
        $Value.Trim(),
        '^\(localdb\)\\(?<instance>[^\\;]+)$',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups['instance'].Value)) {
        throw 'LMAX_DEMO_OWNER_CONTEXT_LOCALDB_INSTANCE_INVALID'
    }

    return $match.Groups['instance'].Value.Trim()
}

try {
    $configPath = Join-Path $release 'appsettings.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw 'LMAX_DEMO_WORKER_CONFIG_MISSING'
    }

    $result['worker_release_visible'] = $true
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $connection = [string]$config.ConnectionStrings.IntradaySqlServer
    if ($connection -notmatch '(?i)\(localdb\)\\' -or $connection -notmatch '(?i)(?:Trusted_Connection|Integrated Security)\s*=\s*True') {
        throw 'LMAX_DEMO_OWNER_CONTEXT_NOT_LOCALDB_INTEGRATED'
    }

    $instance = [regex]::Match(
        $connection,
        '(?i)(?:Server|Data Source)\s*=\s*([^;]+)').Groups[1].Value
    $database = [regex]::Match(
        $connection,
        '(?i)(?:Database|Initial Catalog)\s*=\s*([^;]+)').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($instance) -or [string]::IsNullOrWhiteSpace($database)) {
        throw 'LMAX_DEMO_OWNER_CONTEXT_DATABASE_IDENTITY_MISSING'
    }

    $localDbInstance = Normalize-LocalDbInstance -Value $instance
    $result['localdb_instance'] = $localDbInstance
    $result['database'] = $database.Trim()

    $localDbCommand = Get-Command -Name 'sqllocaldb.exe' -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $localDbCommand) {
        throw 'LMAX_DEMO_OWNER_CONTEXT_LOCALDB_UTILITY_MISSING'
    }

    $null = & $localDbCommand.Path i $localDbInstance 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'LMAX_DEMO_OWNER_CONTEXT_LOCALDB_INSTANCE_UNAVAILABLE'
    }

    $result['localdb_instance_visible'] = $true
    $sqlConnection = $null
    $sqlCommand = $null
    try {
        $sqlConnection = [System.Data.SqlClient.SqlConnection]::new($connection)
        $sqlConnection.Open()
        $sqlCommand = $sqlConnection.CreateCommand()
        $sqlCommand.CommandText = 'SELECT DB_NAME();'
        $observedDatabase = [string]$sqlCommand.ExecuteScalar()
    }
    catch {
        throw 'LMAX_DEMO_OWNER_CONTEXT_SQL_SELECT_FAILED'
    }
    finally {
        if ($null -ne $sqlCommand) { $sqlCommand.Dispose() }
        if ($null -ne $sqlConnection) { $sqlConnection.Dispose() }
    }

    if (-not [string]::Equals($observedDatabase, $result['database'], [StringComparison]::OrdinalIgnoreCase)) {
        throw 'LMAX_DEMO_OWNER_CONTEXT_DATABASE_IDENTITY_MISMATCH'
    }

    $result['sql_access_verified'] = $true
    $chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
    $result['chrome_visible'] = Test-Path -LiteralPath $chrome -PathType Leaf
    $result['status'] = 'PASSED'
    $json = Write-OwnerContextResult -Result $result
    Write-Output $json
    exit 0
}
catch {
    $result['failure_code'] = Get-FailureCode -ErrorRecord $_
    try {
        $json = Write-OwnerContextResult -Result $result
        Write-Output $json
    }
    catch {
        # Signature failures occur before this script can execute; if a later
        # filesystem failure prevents the receipt, Task Scheduler remains the
        # separate process-level evidence source.
    }

    exit 1
}
