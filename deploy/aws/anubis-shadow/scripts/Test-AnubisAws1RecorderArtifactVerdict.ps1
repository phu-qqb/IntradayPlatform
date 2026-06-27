param(
    [Parameter(Mandatory = $true)] [string]$RecorderRoot,
    [datetime]$MinimumWriteUtc = ([datetime]::MinValue),
    [string]$CaptureResultPath = "",
    [string]$NoOrderEntry = "",
    [string]$NoAccountApi = "",
    [string]$NoDb = "",
    [string]$NoDatabento = "",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function New-EmptyVerdict {
    [ordered]@{
        artifact_verdict = "NO_GO_AWS2C_WRAPPER_ARTIFACTS_INVALID"
        wrapper_should_exit_zero = $false
        issues = @()
        recorder_root = $RecorderRoot
        capture_result_path = $null
        run_root = $null
        final_manifest_path = $null
        capture_manifest_path = $null
        data_quality_report_path = $null
        metrics = [ordered]@{}
        safety_flags = [ordered]@{
            no_order_entry = $NoOrderEntry
            no_account_api = $NoAccountApi
            no_db = $NoDb
            no_databento = $NoDatabento
        }
        timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Add-Issue {
    param([System.Collections.Generic.List[string]]$Issues, [string]$Issue)
    if (-not [string]::IsNullOrWhiteSpace($Issue)) { $Issues.Add($Issue) | Out-Null }
}

function Get-JsonFile {
    param([string]$Path, [System.Collections.Generic.List[string]]$Issues, [string]$MissingIssue, [string]$ParseIssue)
    if (-not (Test-Path -LiteralPath $Path)) {
        Add-Issue $Issues $MissingIssue
        return $null
    }
    try { return (Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json) }
    catch {
        Add-Issue $Issues ($ParseIssue + ":" + $_.Exception.GetType().Name)
        return $null
    }
}

function Get-Prop {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-LongOrNull {
    param([object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    try { return [int64]$Value } catch { return $null }
}

function Get-MapLong {
    param([object]$Map, [string]$Key)
    if ($null -eq $Map) { return $null }
    $prop = $Map.PSObject.Properties | Where-Object { $_.Name -ieq $Key } | Select-Object -First 1
    if ($null -eq $prop) { return [int64]0 }
    return Get-LongOrNull $prop.Value
}

function Assert-PositiveMetric {
    param([System.Collections.Generic.List[string]]$Issues, [string]$Name, [object]$Value)
    $long = Get-LongOrNull $Value
    if ($null -eq $long -or $long -le 0) { Add-Issue $Issues "metric_not_positive:$Name" }
    return $long
}

function Assert-ZeroMetric {
    param([System.Collections.Generic.List[string]]$Issues, [string]$Name, [object]$Value)
    $long = Get-LongOrNull $Value
    if ($null -eq $long -or $long -ne 0) { Add-Issue $Issues "metric_not_zero:$Name" }
    return $long
}

function Assert-RequiredTrueFlag {
    param([System.Collections.Generic.List[string]]$Issues, [string]$Name, [string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        Add-Issue $Issues "safety_flag_missing:$Name"
        return
    }
    if (-not [string]::Equals($Value, "true", [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Issue $Issues "safety_flag_not_true:$Name"
    }
}

function Assert-OptionalTrueArtifactFlag {
    param([System.Collections.Generic.List[string]]$Issues, [object]$Object, [string]$Name)
    $value = Get-Prop $Object $Name
    if ($null -eq $value) { return }
    if (-not [bool]$value) { Add-Issue $Issues "artifact_safety_flag_not_true:$Name" }
}

$verdict = New-EmptyVerdict
$issues = [System.Collections.Generic.List[string]]::new()

if ([string]::IsNullOrWhiteSpace($CaptureResultPath)) {
    $CaptureResultPath = Join-Path $RecorderRoot "m2c1b_capture_command_result.json"
}
$verdict.capture_result_path = $CaptureResultPath

Assert-RequiredTrueFlag $issues "no_order_entry" $NoOrderEntry
Assert-RequiredTrueFlag $issues "no_account_api" $NoAccountApi
Assert-RequiredTrueFlag $issues "no_db" $NoDb
Assert-RequiredTrueFlag $issues "no_databento" $NoDatabento

$captureResult = Get-JsonFile -Path $CaptureResultPath -Issues $issues -MissingIssue "capture_result_missing" -ParseIssue "capture_result_unreadable"
if ($null -ne $captureResult -and (Test-Path -LiteralPath $CaptureResultPath)) {
    $captureWriteUtc = (Get-Item -LiteralPath $CaptureResultPath).LastWriteTimeUtc
    if ($captureWriteUtc -lt $MinimumWriteUtc.ToUniversalTime()) {
        Add-Issue $issues "capture_result_stale"
    }
}

if ($null -ne $captureResult) {
    $status = [string](Get-Prop $captureResult "status")
    if ($status -ne "GO_M2C2_CAPTURE_VALIDATED") { Add-Issue $issues "capture_status_not_go:$status" }

    $replayStatus = [string](Get-Prop $captureResult "replay_status")
    if ($replayStatus -ne "PASS") { Add-Issue $issues "replay_status_not_pass:$replayStatus" }

    $verdict.metrics.market_data_received = Assert-PositiveMetric $issues "market_data_received" (Get-Prop $captureResult "market_data_received")
    $verdict.metrics.bbo_updated = Assert-PositiveMetric $issues "bbo_updated" (Get-Prop $captureResult "bbo_updated")
    $verdict.metrics.writer_error_count = Assert-ZeroMetric $issues "writer_error_count" (Get-Prop $captureResult "writer_error_count")
    $verdict.metrics.dropped_event_count = Assert-ZeroMetric $issues "dropped_event_count" (Get-Prop $captureResult "dropped_event_count")

    foreach ($flag in @("no_order_entry", "no_account_api", "no_db", "no_databento")) {
        Assert-OptionalTrueArtifactFlag $issues $captureResult $flag
    }

    $runRoot = [string](Get-Prop $captureResult "run_root")
    if ([string]::IsNullOrWhiteSpace($runRoot)) {
        Add-Issue $issues "run_root_missing"
    }
    else {
        $verdict.run_root = $runRoot
        $finalPath = Join-Path $runRoot "final_manifest.json"
        $captureManifestPath = Join-Path $runRoot "m2c1b_capture_manifest.json"
        $dqPath = Join-Path $runRoot "health\data_quality_report.json"
        $verdict.final_manifest_path = $finalPath
        $verdict.capture_manifest_path = $captureManifestPath
        $verdict.data_quality_report_path = $dqPath

        $final = Get-JsonFile -Path $finalPath -Issues $issues -MissingIssue "final_manifest_missing" -ParseIssue "final_manifest_unreadable"
        if ($null -ne $final) {
            if (-not [bool](Get-Prop $final "finalized")) { Add-Issue $issues "final_manifest_not_finalized" }
            $verdict.metrics.final_writer_errors = Assert-ZeroMetric $issues "final_manifest.writer_errors" (Get-Prop $final "writer_errors")
            $verdict.metrics.final_events_dropped = Assert-ZeroMetric $issues "final_manifest.events_dropped" (Get-Prop $final "events_dropped")
            $finalBbo = Get-MapLong (Get-Prop $final "event_counts") "BBO_UPDATED"
            $verdict.metrics.final_bbo_updated = Assert-PositiveMetric $issues "final_manifest.event_counts.BBO_UPDATED" $finalBbo
            $failureReason = [string](Get-Prop $final "failure_reason")
            if (-not [string]::IsNullOrWhiteSpace($failureReason)) { Add-Issue $issues "final_manifest_failure_reason_present" }
        }

        $captureManifest = Get-JsonFile -Path $captureManifestPath -Issues $issues -MissingIssue "capture_manifest_missing" -ParseIssue "capture_manifest_unreadable"
        if ($null -ne $captureManifest) {
            $manifestStatus = [string](Get-Prop $captureManifest "status")
            if ($manifestStatus -ne "GO_M2C2_CAPTURE_VALIDATED") { Add-Issue $issues "capture_manifest_status_not_go:$manifestStatus" }
            foreach ($flag in @("no_order_entry", "no_account_api", "no_db", "no_databento")) {
                Assert-OptionalTrueArtifactFlag $issues $captureManifest $flag
            }
        }

        $dq = Get-JsonFile -Path $dqPath -Issues $issues -MissingIssue "data_quality_report_missing" -ParseIssue "data_quality_report_unreadable"
        if ($null -ne $dq) {
            $gap = Get-LongOrNull (Get-Prop $dq "sequence_gap_count")
            if ($null -eq $gap) { $gap = 0 }
            $ooo = Get-LongOrNull (Get-Prop $dq "sequence_out_of_order_count")
            if ($null -eq $ooo) { $ooo = 0 }
            $sequenceGapStatus = [int64]($gap + $ooo)
            $verdict.metrics.sequence_gap_status = $sequenceGapStatus
            if ($sequenceGapStatus -ne 0) { Add-Issue $issues "sequence_gap_status_not_zero" }
        }
    }
}

$verdict.issues = @($issues.ToArray())
if ($issues.Count -eq 0) {
    $verdict.artifact_verdict = "GO_AWS2C_WRAPPER_ARTIFACTS_VALIDATED"
    $verdict.wrapper_should_exit_zero = $true
}

$output = $verdict | ConvertTo-Json -Depth 8
if ($Json) { $output } else { $output }
if ($verdict.wrapper_should_exit_zero) { exit 0 }
exit 5