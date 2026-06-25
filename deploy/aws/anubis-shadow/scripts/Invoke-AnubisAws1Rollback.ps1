param(
    [string]$InstallRoot = "C:\Anubis\M2Capture",
    [string]$TargetReleaseId = ""
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "Stop-AnubisAws1Recorder.ps1") | Out-Null

$currentRoot = Join-Path $InstallRoot "current"
$releaseRoot = if ([string]::IsNullOrWhiteSpace($TargetReleaseId)) {
    Get-ChildItem -LiteralPath (Join-Path $InstallRoot "releases") -Directory |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -Skip 1 -First 1 |
        ForEach-Object FullName
}
else {
    Join-Path $InstallRoot "releases\$TargetReleaseId"
}

if ([string]::IsNullOrWhiteSpace($releaseRoot) -or -not (Test-Path -LiteralPath $releaseRoot)) {
    throw "rollback_release_not_found"
}

$rollbackBackup = Join-Path $InstallRoot ("failed-current-" + (Get-Date -Format "yyyyMMddHHmmss"))
if (Test-Path -LiteralPath $currentRoot) {
    Move-Item -LiteralPath $currentRoot -Destination $rollbackBackup
}
Copy-Item -LiteralPath $releaseRoot -Destination $currentRoot -Recurse -Force

[ordered]@{
    status = "ROLLED_BACK"
    active_release = (Split-Path -Leaf $releaseRoot)
    previous_current_backup = $rollbackBackup
    recorder_restarted = $false
    no_delete_of_spool = $true
} | ConvertTo-Json -Depth 4
