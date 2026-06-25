param(
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Environment = "demo",
    [string]$Namespace = "Anubis/AWS1",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$status = & (Join-Path $PSScriptRoot "Get-AnubisAws1Status.ps1") -RecorderRoot $RecorderRoot -Environment $Environment | ConvertFrom-Json

$metrics = @(
    @{ MetricName = "ProcessAlive"; Value = [double]$status.process_alive; Unit = "Count" },
    @{ MetricName = "SessionStateOk"; Value = [double]$status.session_state_ok; Unit = "Count" },
    @{ MetricName = "BboCount"; Value = [double]$status.bbo_count; Unit = "Count" },
    @{ MetricName = "LastQuoteAgeSeconds"; Value = [double]$status.last_quote_age_seconds; Unit = "Seconds" },
    @{ MetricName = "SequenceGapStatus"; Value = [double]$status.sequence_gap_status; Unit = "Count" },
    @{ MetricName = "WriterErrors"; Value = [double]$status.writer_errors; Unit = "Count" },
    @{ MetricName = "Drops"; Value = [double]$status.drops; Unit = "Count" },
    @{ MetricName = "DiskFreePercent"; Value = [double]$status.disk_free_percent; Unit = "Percent" },
    @{ MetricName = "S3UploadBacklog"; Value = [double]$status.s3_upload_backlog; Unit = "Count" },
    @{ MetricName = "ClockHealthOk"; Value = [double]$status.clock_health_ok; Unit = "Count" },
    @{ MetricName = "RecorderShadowReady"; Value = [double]$status.recorder_shadow_ready; Unit = "Count" }
)

if ($DryRun) {
    [ordered]@{ status = "DRY_RUN"; namespace = $Namespace; metrics = $metrics } | ConvertTo-Json -Depth 5
    exit 0
}

if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw "aws_cli_required_for_metric_publish"
}

$metricJson = $metrics | ForEach-Object {
    @{
        MetricName = $_.MetricName
        Value = $_.Value
        Unit = $_.Unit
        Dimensions = @(
            @{ Name = "Environment"; Value = $Environment },
            @{ Name = "HostRole"; Value = "m2-capture-only" }
        )
    }
} | ConvertTo-Json -Depth 8 -Compress

$temp = Join-Path $env:TEMP ("anubis-aws1-metrics-" + [guid]::NewGuid().ToString("N") + ".json")
Set-Content -LiteralPath $temp -Value $metricJson -Encoding UTF8
try {
    aws cloudwatch put-metric-data --namespace $Namespace --metric-data "file://$temp"
    if ($LASTEXITCODE -ne 0) { throw "cloudwatch_put_metric_data_failed:$LASTEXITCODE" }
}
finally {
    Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
}

@{ status = "PUBLISHED"; namespace = $Namespace; metric_count = $metrics.Count } | ConvertTo-Json
