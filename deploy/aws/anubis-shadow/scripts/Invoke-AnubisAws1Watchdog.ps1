param(
    [string]$InstallRoot = "C:\Anubis\M2Capture\current",
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$CredentialSecretId = "",
    [string]$ArchiveBucketName = "",
    [string]$Environment = "demo",
    [string]$CloudWatchNamespace = "QQFundPlatform/AWS1",
    [string]$ExpectedAwsCliSha256 = "",
    [switch]$RestartWhenStopped,
    [switch]$PublishMetrics
)

$ErrorActionPreference = "Stop"
$status = & (Join-Path $PSScriptRoot "Get-AnubisAws1Status.ps1") -RecorderRoot $RecorderRoot -Environment $Environment | ConvertFrom-Json

$actions = @()
$warnings = @()

if ($RestartWhenStopped) {
    $actions += "NO_GO_CONTINUOUS_WATCHDOG_OUT_OF_SCOPE"
}

$diskMetric = $status.metrics | Where-Object { $_.name -eq "DiskFreePercent" } | Select-Object -First 1
if ($null -ne $diskMetric -and $diskMetric.evaluation_status -eq "EVALUATED" -and [double]$diskMetric.value -lt 10) {
    $actions += "fail_closed_low_disk"
}
elseif ($null -ne $diskMetric -and $diskMetric.evaluation_status -ne "EVALUATED") {
    $warnings += "disk_free_percent_not_evaluated"
}

$clockMetric = $status.metrics | Where-Object { $_.name -eq "ClockHealthOk" } | Select-Object -First 1
if ($null -ne $clockMetric -and $clockMetric.evaluation_status -eq "EVALUATED" -and [int]$clockMetric.value -ne 1) {
    $actions += "fail_closed_clock_health"
}
elseif ($null -ne $clockMetric -and $clockMetric.evaluation_status -ne "EVALUATED") {
    $warnings += "clock_health_not_evaluated"
}

$publishResult = $null
if ($PublishMetrics) {
    $publishResult = & (Join-Path $PSScriptRoot "Publish-AnubisAws1Metrics.ps1") -RecorderRoot $RecorderRoot -Environment $Environment -Namespace $CloudWatchNamespace -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 | ConvertFrom-Json
}

[ordered]@{
    status = if ($actions.Count -eq 0) { "SMOKE_WATCHDOG_OBSERVER_ONLY" } else { "WATCHDOG_ACTION_REQUIRED" }
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    continuous_recorder_supported = $false
    restart_performed = $false
    actions = $actions
    warnings = $warnings
    publish_result = $publishResult
    observed = $status
    no_order_entry = $true
    note = "Continuous restart watchdog remains out of scope for AWS1 plan-ready smoke mode. Use Start-AnubisAws1Recorder.ps1 for operator-approved bounded captures."
} | ConvertTo-Json -Depth 8
