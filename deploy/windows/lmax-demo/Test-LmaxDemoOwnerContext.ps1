[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Read-only activation preflight.  It is intentionally independent of the Worker:
# no migration, seed, queue processing, browser write, or LMAX order action occurs.
$release = 'C:\deploy\IntradayPlatform\releases\f1692d8e16f8ed707fe5e27748d18c11df4a8b64'
$configPath = Join-Path $release 'appsettings.json'
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw 'LMAX_DEMO_WORKER_CONFIG_MISSING' }
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$connection = [string]$config.ConnectionStrings.IntradaySqlServer
if ($connection -notmatch '(?i)(localdb|Trusted_Connection\s*=\s*True|Integrated Security\s*=\s*True)') { throw 'LMAX_DEMO_OWNER_CONTEXT_NOT_LOCALDB_INTEGRATED' }
$instance = [regex]::Match($connection, '(?i)(?:Server|Data Source)\s*=\s*([^;]+)').Groups[1].Value
$database = [regex]::Match($connection, '(?i)(?:Database|Initial Catalog)\s*=\s*([^;]+)').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($instance) -or [string]::IsNullOrWhiteSpace($database)) { throw 'LMAX_DEMO_OWNER_CONTEXT_DATABASE_IDENTITY_MISSING' }

$localDb = & sqllocaldb i $instance.Trim() 2>&1
if ($LASTEXITCODE -ne 0) { throw 'LMAX_DEMO_OWNER_CONTEXT_LOCALDB_INSTANCE_UNAVAILABLE' }
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
[pscustomobject]@{
    marker = 'LMAX_DEMO_OWNER_CONTEXT_PREFLIGHT'
    identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    profile = $env:USERPROFILE
    localdb_instance = $instance.Trim()
    database = $database.Trim()
    localdb_instance_visible = $true
    worker_release_visible = (Test-Path -LiteralPath $release -PathType Container)
    chrome_visible = (Test-Path -LiteralPath $chrome -PathType Leaf)
    sql_access_verified = $false
    broker_gui_authenticated = $false
    no_worker_started = $true
} | ConvertTo-Json -Compress
