param(
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Environment = "demo",
    [string]$Namespace = "QQFundPlatform/AWS1",
    [string]$ExpectedAwsCliSha256 = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$status = & (Join-Path $PSScriptRoot "Get-AnubisAws1Status.ps1") -RecorderRoot $RecorderRoot -Environment $Environment | ConvertFrom-Json
$evaluatedMetrics = @($status.metrics | Where-Object { $_.evaluation_status -eq "EVALUATED" })
$skippedMetrics = @($status.metrics | Where-Object { $_.evaluation_status -ne "EVALUATED" })

$metrics = @($evaluatedMetrics | ForEach-Object {
    [ordered]@{
        MetricName = [string]$_.name
        Value = [double]$_.value
        Unit = [string]$_.unit
        Evidence = [string]$_.evidence
    }
})

if ($DryRun) {
    [ordered]@{
        status = "DRY_RUN"
        namespace = $Namespace
        operation_mode = "SMOKE_CAPTURE_BOUNDED"
        metric_count = $metrics.Count
        skipped_metric_count = $skippedMetrics.Count
        metrics = $metrics
        skipped_metrics = @($skippedMetrics | Select-Object name, evaluation_status, evidence)
    } | ConvertTo-Json -Depth 6
    exit 0
}

if ($metrics.Count -eq 0) {
    [ordered]@{
        status = "NO_METRICS_EVALUATED"
        namespace = $Namespace
        skipped_metric_count = $skippedMetrics.Count
        skipped_metrics = @($skippedMetrics | Select-Object name, evaluation_status, evidence)
    } | ConvertTo-Json -Depth 6
    exit 2
}

$prereq = & (Join-Path $PSScriptRoot "Test-AnubisAws1HostPrerequisites.ps1") -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 -Json | ConvertFrom-Json
if ($prereq.status -ne "PASS") { throw "host_prerequisites_failed:$($prereq | ConvertTo-Json -Compress)" }
$awsCliPath = [string]$prereq.aws_cli_path

$metricJson = $metrics | ForEach-Object {
    [ordered]@{
        MetricName = $_.MetricName
        Value = $_.Value
        Unit = $_.Unit
        Dimensions = @(
            @{ Name = "Environment"; Value = $Environment },
            @{ Name = "HostRole"; Value = "m2-capture-only" },
            @{ Name = "OperationMode"; Value = "SMOKE_CAPTURE_BOUNDED" }
        )
    }
} | ConvertTo-Json -Depth 8 -Compress

$temp = Join-Path $env:TEMP ("anubis-aws1-metrics-" + [guid]::NewGuid().ToString("N") + ".json")
Set-Content -LiteralPath $temp -Value $metricJson -Encoding UTF8
try {
    & $awsCliPath cloudwatch put-metric-data --namespace $Namespace --metric-data "file://$temp"
    if ($LASTEXITCODE -ne 0) { throw "cloudwatch_put_metric_data_failed:$LASTEXITCODE" }
}
finally {
    Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
}

[ordered]@{
    status = "PUBLISHED"
    namespace = $Namespace
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    metric_count = $metrics.Count
    skipped_metric_count = $skippedMetrics.Count
    skipped_metrics = @($skippedMetrics | Select-Object name, evaluation_status, evidence)
} | ConvertTo-Json -Depth 6
