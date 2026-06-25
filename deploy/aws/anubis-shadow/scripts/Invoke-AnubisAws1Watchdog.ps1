param(
    [string]$InstallRoot = "C:\Anubis\M2Capture\current",
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$CredentialSecretId = "",
    [string]$ArchiveBucketName = "",
    [string]$Environment = "demo",
    [string]$CloudWatchNamespace = "Anubis/AWS1",
    [switch]$RestartWhenStopped
)

$ErrorActionPreference = "Stop"
$status = & (Join-Path $PSScriptRoot "Get-AnubisAws1Status.ps1") -RecorderRoot $RecorderRoot -Environment $Environment | ConvertFrom-Json

$actions = @()
if ([double]$status.disk_free_percent -lt 10) {
    $actions += "fail_closed_low_disk"
}
if ([int]$status.clock_health_ok -ne 1) {
    $actions += "fail_closed_clock_health"
}
if ([int]$status.process_alive -ne 1 -and $RestartWhenStopped -and $actions.Count -eq 0) {
    & (Join-Path $PSScriptRoot "Start-AnubisAws1Recorder.ps1") -InstallRoot $InstallRoot -RecorderRoot $RecorderRoot -CredentialSecretId $CredentialSecretId -ArchiveBucketName $ArchiveBucketName -Environment $Environment -CloudWatchNamespace $CloudWatchNamespace | Out-Null
    $actions += "restart_requested"
}

[ordered]@{
    status = if ($actions.Count -eq 0) { "WATCHDOG_OK" } else { "WATCHDOG_ACTION" }
    actions = $actions
    observed = $status
    no_order_entry = $true
} | ConvertTo-Json -Depth 8
