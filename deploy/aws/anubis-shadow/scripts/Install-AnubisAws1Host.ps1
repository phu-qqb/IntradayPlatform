param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactZipPath,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactSha256,

    [string]$InstallRoot = "C:\Anubis\M2Capture",
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$CredentialSecretId = "",
    [string]$MarketDataEndpointAlias = "LMAX_DEMO_MARKET_DATA_ONLY",
    [string]$ArchiveBucketName = "",
    [string]$Environment = "demo",
    [string]$CloudWatchNamespace = "Anubis/AWS1",
    [switch]$EnableAutoStart
)

$ErrorActionPreference = "Stop"

function Get-HashUpper {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

if (-not (Test-Path -LiteralPath $ArtifactZipPath)) {
    throw "artifact_not_found:$ArtifactZipPath"
}

$actual = Get-HashUpper $ArtifactZipPath
if ($actual -ne $ArtifactSha256.ToUpperInvariant()) {
    throw "artifact_sha256_mismatch:$actual"
}

$releaseId = $ArtifactSha256.ToUpperInvariant().Substring(0, 16)
$releaseRoot = Join-Path $InstallRoot "releases\$releaseId"
$currentRoot = Join-Path $InstallRoot "current"
$stateRoot = "C:\Anubis\State"
$logRoot = "C:\Anubis\Logs"

New-Item -ItemType Directory -Force -Path $InstallRoot, $RecorderRoot, $stateRoot, $logRoot | Out-Null

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
Expand-Archive -LiteralPath $ArtifactZipPath -DestinationPath $releaseRoot -Force

if (Test-Path -LiteralPath $currentRoot) {
    $backup = Join-Path $InstallRoot ("rollback-" + (Get-Date -Format "yyyyMMddHHmmss"))
    Move-Item -LiteralPath $currentRoot -Destination $backup
}
Copy-Item -LiteralPath $releaseRoot -Destination $currentRoot -Recurse -Force

$configPath = Join-Path $currentRoot "config\m2c1b_aws_capture_config.json"
$credentialReference = if ([string]::IsNullOrWhiteSpace($CredentialSecretId)) { "aws-secretsmanager:market-data-only" } else { "aws-secretsmanager:$CredentialSecretId" }
& (Join-Path $currentRoot "deploy\aws\anubis-shadow\scripts\New-M2C1BConfig.ps1") -OutputPath $configPath -RecorderRoot $RecorderRoot -MarketDataEndpointAlias $MarketDataEndpointAlias -CredentialReference $credentialReference | Out-Null

$taskScript = Join-Path $currentRoot "deploy\aws\anubis-shadow\scripts\Start-AnubisAws1Recorder.ps1"
$taskArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$taskScript`" -InstallRoot `"$currentRoot`" -ConfigPath `"$configPath`" -RecorderRoot `"$RecorderRoot`" -CredentialSecretId `"$CredentialSecretId`" -ArchiveBucketName `"$ArchiveBucketName`" -Environment `"$Environment`" -CloudWatchNamespace `"$CloudWatchNamespace`""
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $taskArgs
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName "AnubisAws1M2CaptureOnly" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -Force | Out-Null
if (-not $EnableAutoStart) {
    Disable-ScheduledTask -TaskName "AnubisAws1M2CaptureOnly" | Out-Null
}

$installManifest = [ordered]@{
    status = "INSTALLED"
    artifact_sha256 = $ArtifactSha256.ToUpperInvariant()
    release_id = $releaseId
    install_root = $InstallRoot
    current_root = $currentRoot
    recorder_root = $RecorderRoot
    config_path = $configPath
    credential_secret_id_present = -not [string]::IsNullOrWhiteSpace($CredentialSecretId)
    archive_bucket_name_present = -not [string]::IsNullOrWhiteSpace($ArchiveBucketName)
    autostart_enabled = [bool]$EnableAutoStart
    no_secret_values = $true
    no_order_entry = $true
    timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
}
$installManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stateRoot "aws1_install_manifest.json") -Encoding UTF8
$installManifest | ConvertTo-Json -Depth 5
