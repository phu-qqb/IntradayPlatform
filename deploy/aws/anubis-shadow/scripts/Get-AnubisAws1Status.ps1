param(
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Environment = "demo",
    [string]$StateRoot = "C:\Anubis\State"
)

$ErrorActionPreference = "Stop"

function Get-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return (Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json) }
    catch { return $null }
}

function Get-Prop {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Convert-ToDateTimeOffsetOrNull {
    param([object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    try { return [DateTimeOffset]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture) }
    catch { return $null }
}

function Convert-ToInt64OrNull {
    param([object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    try { return [int64]$Value }
    catch { return $null }
}

function New-Metric {
    param(
        [string]$Name,
        [object]$Value,
        [string]$Unit,
        [string]$EvaluationStatus,
        [string]$Evidence
    )
    [ordered]@{
        name = $Name
        value = $Value
        unit = $Unit
        evaluation_status = $EvaluationStatus
        evidence = $Evidence
    }
}

function Get-MetricValue {
    param([object[]]$Metrics, [string]$Name)
    $metric = $Metrics | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if ($null -eq $metric -or $metric.evaluation_status -ne "EVALUATED") { return $null }
    return $metric.value
}

function Test-SafeRelativePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ($Path.Contains("\")) { return $false }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $false }
    foreach ($part in $Path.Split('/')) {
        if ($part -in @("", ".", "..")) { return $false }
    }
    return $true
}

function Get-MapCount {
    param([object]$Map, [string]$Key)
    if ($null -eq $Map) { return $null }
    $prop = $Map.PSObject.Properties | Where-Object { $_.Name -ieq $Key } | Select-Object -First 1
    if ($null -eq $prop) { return [int64]0 }
    return [int64]$prop.Value
}

function Get-VerifiedProcessState {
    param([string]$PidPath)
    if (-not (Test-Path -LiteralPath $PidPath)) {
        return [ordered]@{ evaluated = $true; alive = 0; pid = $null; reason = "pid_file_missing"; state = $null }
    }

    $state = Get-JsonFile -Path $PidPath
    if ($null -eq $state -or $null -eq (Get-Prop $state "pid")) {
        return [ordered]@{ evaluated = $false; alive = $null; pid = $null; reason = "pid_state_unreadable"; state = $state }
    }

    $recordedPid = [int](Get-Prop $state "pid")
    $process = Get-Process -Id $recordedPid -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return [ordered]@{ evaluated = $true; alive = 0; pid = $recordedPid; reason = "process_missing"; state = $state }
    }

    $expectedPath = [string](Get-Prop $state "executable_path")
    if (-not [string]::IsNullOrWhiteSpace($expectedPath)) {
        $actualPath = $process.Path
        if ([string]::IsNullOrWhiteSpace($actualPath) -or -not [string]::Equals($actualPath, $expectedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [ordered]@{ evaluated = $true; alive = 0; pid = $recordedPid; reason = "executable_path_mismatch"; state = $state }
        }
    }

    $expectedStart = Convert-ToDateTimeOffsetOrNull (Get-Prop $state "process_start_time_utc")
    if ($null -ne $expectedStart) {
        $actualStart = $process.StartTime.ToUniversalTime()
        if ([math]::Abs(($actualStart - $expectedStart.UtcDateTime).TotalSeconds) -gt 2) {
            return [ordered]@{ evaluated = $true; alive = 0; pid = $recordedPid; reason = "process_start_time_mismatch"; state = $state }
        }
    }

    return [ordered]@{ evaluated = $true; alive = 1; pid = $recordedPid; reason = "verified_pid_json"; state = $state }
}

function Get-DiskFreePercentMetric {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return New-Metric "DiskFreePercent" $null "Percent" "NOT_EVALUATED" "recorder_root_missing:$Path"
    }

    try {
        $root = [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($Path))
        $drive = [System.IO.DriveInfo]::new($root)
        if (-not $drive.IsReady -or $drive.TotalSize -le 0) {
            return New-Metric "DiskFreePercent" $null "Percent" "NOT_EVALUATED" "drive_not_ready:$root"
        }
        $value = [math]::Round(($drive.AvailableFreeSpace / $drive.TotalSize) * 100, 2)
        return New-Metric "DiskFreePercent" $value "Percent" "EVALUATED" "drive:$root"
    }
    catch {
        return New-Metric "DiskFreePercent" $null "Percent" "NOT_EVALUATED" "disk_query_failed:$($_.Exception.Message)"
    }
}

function Get-ClockMetric {
    $statusLines = @()
    try { $statusLines = @(& w32tm /query /status 2>$null) }
    catch { $statusLines = @() }

    if ($LASTEXITCODE -ne 0 -or $statusLines.Count -eq 0) {
        return [ordered]@{
            metric = New-Metric "ClockHealthOk" $null "Count" "NOT_EVALUATED" "w32tm_status_unavailable"
            source = $null
        }
    }

    $source = (($statusLines | Where-Object { $_ -match "^Source:" }) -replace "^Source:\s*", "") | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($source)) {
        return [ordered]@{
            metric = New-Metric "ClockHealthOk" $null "Count" "NOT_EVALUATED" "w32tm_source_missing"
            source = $null
        }
    }

    $ok = $source -match "169\.254\.169\.123" -or $source -match "(?i)Amazon\s+Time\s+Sync"
    return [ordered]@{
        metric = New-Metric "ClockHealthOk" ($(if ($ok) { 1 } else { 0 })) "Count" "EVALUATED" "source:$source"
        source = $source
    }
}

function Find-LatestFinalManifest {
    param([string]$Root)
    if (-not (Test-Path -LiteralPath $Root)) { return $null }
    return Get-ChildItem -LiteralPath $Root -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Get-LatestBboTime {
    param([string]$RunRoot, [object]$FinalManifest)
    $chunks = @(Get-Prop $FinalManifest "chunks")
    if ($chunks.Count -eq 0) {
        return [ordered]@{ evaluated = $false; time = $null; evidence = "final_manifest_has_no_chunks" }
    }

    $latest = $null
    $checked = 0
    foreach ($chunk in $chunks) {
        $relative = [string](Get-Prop $chunk "file")
        if (-not (Test-SafeRelativePath -Path $relative)) {
            return [ordered]@{ evaluated = $false; time = $null; evidence = "unsafe_chunk_path:$relative" }
        }

        $chunkPath = Join-Path $RunRoot ($relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $chunkPath)) {
            return [ordered]@{ evaluated = $false; time = $null; evidence = "chunk_missing:$relative" }
        }

        $checked++
        foreach ($line in [System.IO.File]::ReadLines($chunkPath)) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try { $event = $line | ConvertFrom-Json }
            catch { continue }
            if ([string](Get-Prop $event "event_type") -ne "BBO_UPDATED") { continue }

            $candidate = Convert-ToDateTimeOffsetOrNull (Get-Prop $event "local_receive_utc")
            if ($null -eq $candidate) { $candidate = Convert-ToDateTimeOffsetOrNull (Get-Prop $event "recorded_utc") }
            if ($null -eq $candidate) { $candidate = Convert-ToDateTimeOffsetOrNull (Get-Prop $event "source_timestamp_utc") }
            if ($null -ne $candidate -and ($null -eq $latest -or $candidate.UtcDateTime -gt $latest.UtcDateTime)) {
                $latest = $candidate
            }
        }
    }

    if ($null -eq $latest) {
        return [ordered]@{ evaluated = $false; time = $null; evidence = "no_bbo_event_timestamp_in_$checked`_chunks" }
    }
    return [ordered]@{ evaluated = $true; time = $latest; evidence = "latest_bbo_from_$checked`_chunks" }
}

$pidPath = Join-Path $StateRoot "aws1-recorder.pid.json"
$processState = Get-VerifiedProcessState -PidPath $pidPath
$latestFinalFile = Find-LatestFinalManifest -Root $RecorderRoot
$final = $null
$runRoot = $null
$capture = $null
$dq = $null

if ($null -ne $latestFinalFile) {
    $runRoot = $latestFinalFile.DirectoryName
    $final = Get-JsonFile -Path $latestFinalFile.FullName
    $capture = Get-JsonFile -Path (Join-Path $runRoot "m2c1b_capture_manifest.json")
    $dq = Get-JsonFile -Path (Join-Path $runRoot "health\data_quality_report.json")
}

$metrics = New-Object System.Collections.Generic.List[object]
if ($processState.evaluated) {
    $metrics.Add((New-Metric "ProcessAlive" $processState.alive "Count" "EVALUATED" $processState.reason))
}
else {
    $metrics.Add((New-Metric "ProcessAlive" $null "Count" "NOT_EVALUATED" $processState.reason))
}

if ($null -ne $final -and $null -ne $capture) {
    $finalized = [bool](Get-Prop $final "finalized")
    $captureStatus = [string](Get-Prop $capture "status")
    $ok = $finalized -and $captureStatus -eq "GO_M2C2_CAPTURE_VALIDATED"
    $metrics.Add((New-Metric "SessionStateOk" ($(if ($ok) { 1 } else { 0 })) "Count" "EVALUATED" "final_manifest_and_capture_manifest"))
}
else {
    $metrics.Add((New-Metric "SessionStateOk" $null "Count" "NOT_EVALUATED" "missing_final_or_capture_manifest"))
}

if ($null -ne $final -and $null -ne (Get-Prop $final "event_counts")) {
    $bbo = Get-MapCount (Get-Prop $final "event_counts") "BBO_UPDATED"
    $metrics.Add((New-Metric "BboCount" $bbo "Count" "EVALUATED" "final_manifest.event_counts"))
}
else {
    $metrics.Add((New-Metric "BboCount" $null "Count" "NOT_EVALUATED" "final_manifest_event_counts_missing"))
}

if ($null -ne $final -and $null -ne $runRoot) {
    $latestBbo = Get-LatestBboTime -RunRoot $runRoot -FinalManifest $final
    if ($latestBbo.evaluated) {
        $age = [math]::Max(0, [math]::Round(((Get-Date).ToUniversalTime() - $latestBbo.time.UtcDateTime).TotalSeconds, 3))
        $metrics.Add((New-Metric "LastQuoteAgeSeconds" $age "Seconds" "EVALUATED" $latestBbo.evidence))
    }
    else {
        $metrics.Add((New-Metric "LastQuoteAgeSeconds" $null "Seconds" "NOT_EVALUATED" $latestBbo.evidence))
    }
}
else {
    $metrics.Add((New-Metric "LastQuoteAgeSeconds" $null "Seconds" "NOT_EVALUATED" "final_manifest_missing"))
}

if ($null -ne $dq) {
    $gap = Convert-ToInt64OrNull (Get-Prop $dq "sequence_gap_count")
    $ooo = Convert-ToInt64OrNull (Get-Prop $dq "sequence_out_of_order_count")
    if ($null -eq $gap) { $gap = 0 }
    if ($null -eq $ooo) { $ooo = 0 }
    $metrics.Add((New-Metric "SequenceGapStatus" ([int64]($gap + $ooo)) "Count" "EVALUATED" "data_quality_report.sequence_gap_count+sequence_out_of_order_count"))
}
else {
    $metrics.Add((New-Metric "SequenceGapStatus" $null "Count" "NOT_EVALUATED" "data_quality_report_missing"))
}

if ($null -ne $final) {
    $writerErrors = Convert-ToInt64OrNull (Get-Prop $final "writer_errors")
    $drops = Convert-ToInt64OrNull (Get-Prop $final "events_dropped")
    $metrics.Add((New-Metric "WriterErrors" ($(if ($null -eq $writerErrors) { 0 } else { $writerErrors })) "Count" "EVALUATED" "final_manifest.writer_errors"))
    $metrics.Add((New-Metric "Drops" ($(if ($null -eq $drops) { 0 } else { $drops })) "Count" "EVALUATED" "final_manifest.events_dropped"))
}
else {
    $metrics.Add((New-Metric "WriterErrors" $null "Count" "NOT_EVALUATED" "final_manifest_missing"))
    $metrics.Add((New-Metric "Drops" $null "Count" "NOT_EVALUATED" "final_manifest_missing"))
}

$metrics.Add((Get-DiskFreePercentMetric -Path $RecorderRoot))

if (Test-Path -LiteralPath $RecorderRoot) {
    $backlog = @(Get-ChildItem -LiteralPath $RecorderRoot -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.DirectoryName ".s3_upload_verified")) }).Count
    $metrics.Add((New-Metric "S3UploadBacklog" $backlog "Count" "EVALUATED" "finalized_runs_without_s3_marker"))
}
else {
    $metrics.Add((New-Metric "S3UploadBacklog" $null "Count" "NOT_EVALUATED" "recorder_root_missing"))
}

$clock = Get-ClockMetric
$metrics.Add($clock.metric)

if ($null -ne $dq) {
    $shadowReady = Get-Prop $dq "shadow_ready"
    if ($null -ne $shadowReady) {
        $metrics.Add((New-Metric "RecorderShadowReady" ($(if ([bool]$shadowReady) { 1 } else { 0 })) "Count" "EVALUATED" "data_quality_report.shadow_ready"))
    }
    else {
        $readiness = [string](Get-Prop $dq "shadow_readiness_status")
        if ([string]::IsNullOrWhiteSpace($readiness)) {
            $metrics.Add((New-Metric "RecorderShadowReady" $null "Count" "NOT_EVALUATED" "shadow_readiness_status_missing"))
        }
        else {
            $metrics.Add((New-Metric "RecorderShadowReady" ($(if ($readiness -eq "READY") { 1 } else { 0 })) "Count" "EVALUATED" "data_quality_report.shadow_readiness_status:$readiness"))
        }
    }
}
else {
    $metrics.Add((New-Metric "RecorderShadowReady" $null "Count" "NOT_EVALUATED" "data_quality_report_missing"))
}

$metricsArray = @($metrics.ToArray())
$status = [ordered]@{
    status = if ((Get-MetricValue $metricsArray "ProcessAlive") -eq 1) { "RUNNING" } else { "STOPPED" }
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    pid = $processState.pid
    environment = $Environment
    recorder_root = $RecorderRoot
    state_root = $StateRoot
    process_alive = Get-MetricValue $metricsArray "ProcessAlive"
    session_state_ok = Get-MetricValue $metricsArray "SessionStateOk"
    bbo_count = Get-MetricValue $metricsArray "BboCount"
    last_quote_age_seconds = Get-MetricValue $metricsArray "LastQuoteAgeSeconds"
    sequence_gap_status = Get-MetricValue $metricsArray "SequenceGapStatus"
    writer_errors = Get-MetricValue $metricsArray "WriterErrors"
    drops = Get-MetricValue $metricsArray "Drops"
    disk_free_percent = Get-MetricValue $metricsArray "DiskFreePercent"
    s3_upload_backlog = Get-MetricValue $metricsArray "S3UploadBacklog"
    clock_health_ok = Get-MetricValue $metricsArray "ClockHealthOk"
    clock_source = $clock.source
    recorder_shadow_ready = Get-MetricValue $metricsArray "RecorderShadowReady"
    latest_final_manifest = if ($latestFinalFile) { $latestFinalFile.FullName } else { $null }
    capture_manifest = if ($runRoot) { Join-Path $runRoot "m2c1b_capture_manifest.json" } else { $null }
    data_quality_report = if ($runRoot) { Join-Path $runRoot "health\data_quality_report.json" } else { $null }
    raw_ticks_emitted_to_cloudwatch = $false
    metrics = $metricsArray
    not_evaluated_metrics = @($metricsArray | Where-Object { $_.evaluation_status -ne "EVALUATED" } | ForEach-Object { $_.name })
    timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
}

$status | ConvertTo-Json -Depth 8
