param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$OutputPath = "artifacts\readiness\qq-fund-platform-aws2c-wrapper-exit-code-fix\AWS2C_WRAPPER_TEST_REPORT.generated.json"
)

$ErrorActionPreference = "Stop"
$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$verdictScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Test-AnubisAws1RecorderArtifactVerdict.ps1"
$tmpRoot = Join-Path $repo ("artifacts\tmp\aws2c-wrapper-verdict-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null

function New-Result {
    param([string]$Name, [bool]$Pass, [object]$Detail)
    [ordered]@{ name = $Name; status = $(if ($Pass) { "PASS" } else { "FAIL" }); detail = $Detail }
}

function Write-GoFixture {
    param(
        [string]$RecorderRoot,
        [string]$Status = "GO_M2C2_CAPTURE_VALIDATED",
        [string]$ReplayStatus = "PASS",
        [int]$MarketDataReceived = 3,
        [int]$BboUpdated = 3,
        [int]$WriterErrorCount = 0,
        [int]$DroppedEventCount = 0,
        [int]$FinalWriterErrors = 0,
        [int]$FinalEventsDropped = 0,
        [int]$SequenceGapCount = 0,
        [int]$SequenceOutOfOrderCount = 0,
        [bool]$WriteFinalManifest = $true,
        [bool]$Finalized = $true
    )
    New-Item -ItemType Directory -Force -Path $RecorderRoot | Out-Null
    $runRoot = Join-Path $RecorderRoot "environment=DEMO\date=2026-06-26\recorder_run=AWS2C_FIXTURE"
    New-Item -ItemType Directory -Force -Path $runRoot, (Join-Path $runRoot "health") | Out-Null

    [ordered]@{
        status = $Status
        recorder_run_id = "AWS2C_FIXTURE"
        run_root = $runRoot
        replay_status = $ReplayStatus
        market_data_received = $MarketDataReceived
        bbo_updated = $BboUpdated
        writer_error_count = $WriterErrorCount
        dropped_event_count = $DroppedEventCount
        inbound_execution_report_observed = $false
        event_counts = [ordered]@{ BBO_UPDATED = $BboUpdated; MARKET_DATA_RECEIVED = $MarketDataReceived }
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $RecorderRoot "m2c1b_capture_command_result.json") -Encoding UTF8

    [ordered]@{
        status = $Status
        recorder_run_id = "AWS2C_FIXTURE"
        run_root = $runRoot
        replay_status = $ReplayStatus
        market_data_received = $MarketDataReceived
        bbo_updated = $BboUpdated
        writer_error_count = $WriterErrorCount
        dropped_event_count = $DroppedEventCount
        event_counts = [ordered]@{ BBO_UPDATED = $BboUpdated; MARKET_DATA_RECEIVED = $MarketDataReceived }
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot "m2c1b_capture_manifest.json") -Encoding UTF8

    if ($WriteFinalManifest) {
        [ordered]@{
            recorder_manifest_version = "canonical_recorder_manifest_v2"
            recorder_run_id = "AWS2C_FIXTURE"
            finalized = $Finalized
            event_counts = [ordered]@{ BBO_UPDATED = $BboUpdated; MARKET_DATA_RECEIVED = $MarketDataReceived }
            events_dropped = $FinalEventsDropped
            writer_errors = $FinalWriterErrors
            failure_reason = $null
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot "final_manifest.json") -Encoding UTF8
    }

    [ordered]@{
        sequence_gap_count = $SequenceGapCount
        sequence_out_of_order_count = $SequenceOutOfOrderCount
        shadow_ready = $true
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot "health\data_quality_report.json") -Encoding UTF8
}

function Invoke-Verdict {
    param(
        [string]$RecorderRoot,
        [int]$RawChildExitCode = 0,
        [string]$NoOrderEntry = "true",
        [string]$NoAccountApi = "true",
        [string]$NoDb = "true",
        [string]$NoDatabento = "true"
    )
    $json = & $verdictScript -RecorderRoot $RecorderRoot -NoOrderEntry $NoOrderEntry -NoAccountApi $NoAccountApi -NoDb $NoDb -NoDatabento $NoDatabento -Json
    $artifactExitCode = $LASTEXITCODE
    $verdict = ($json -join "`n") | ConvertFrom-Json
    $wrapperExitCode = if ($artifactExitCode -eq 0 -and $verdict.wrapper_should_exit_zero) { 0 } else { 5 }
    [ordered]@{
        raw_child_exit_code = $RawChildExitCode
        artifact_exit_code = $artifactExitCode
        wrapper_exit_code = $wrapperExitCode
        verdict = $verdict
    }
}

$results = New-Object System.Collections.Generic.List[object]

$case1 = Join-Path $tmpRoot "case1-go-raw5"
Write-GoFixture -RecorderRoot $case1
$r1 = Invoke-Verdict -RecorderRoot $case1 -RawChildExitCode 5
$results.Add((New-Result "raw_child_exit_5_artifacts_go_wrapper_exits_0" ($r1.wrapper_exit_code -eq 0 -and $r1.raw_child_exit_code -eq 5) $r1))

$case2 = Join-Path $tmpRoot "case2-no-go-raw0"
Write-GoFixture -RecorderRoot $case2 -Status "NO_GO_M2C1B" -ReplayStatus "FAIL" -MarketDataReceived 0 -BboUpdated 0
$r2 = Invoke-Verdict -RecorderRoot $case2 -RawChildExitCode 0
$results.Add((New-Result "raw_child_exit_0_artifacts_no_go_wrapper_exits_nonzero" ($r2.wrapper_exit_code -ne 0) $r2))

$case3 = Join-Path $tmpRoot "case3-missing-final"
Write-GoFixture -RecorderRoot $case3 -WriteFinalManifest:$false
$r3 = Invoke-Verdict -RecorderRoot $case3
$results.Add((New-Result "missing_final_manifest_wrapper_exits_nonzero" ($r3.wrapper_exit_code -ne 0 -and ($r3.verdict.issues -contains "final_manifest_missing")) $r3))

$case4 = Join-Path $tmpRoot "case4-writer-errors"
Write-GoFixture -RecorderRoot $case4 -WriterErrorCount 1 -FinalWriterErrors 1
$r4 = Invoke-Verdict -RecorderRoot $case4
$results.Add((New-Result "writer_errors_wrapper_exits_nonzero" ($r4.wrapper_exit_code -ne 0) $r4))

$case5 = Join-Path $tmpRoot "case5-drops"
Write-GoFixture -RecorderRoot $case5 -DroppedEventCount 1 -FinalEventsDropped 1
$r5 = Invoke-Verdict -RecorderRoot $case5
$results.Add((New-Result "drops_wrapper_exits_nonzero" ($r5.wrapper_exit_code -ne 0) $r5))

$case6 = Join-Path $tmpRoot "case6-sequence-gap"
Write-GoFixture -RecorderRoot $case6 -SequenceGapCount 1
$r6 = Invoke-Verdict -RecorderRoot $case6
$results.Add((New-Result "sequence_gap_wrapper_exits_nonzero" ($r6.wrapper_exit_code -ne 0 -and ($r6.verdict.issues -contains "sequence_gap_status_not_zero")) $r6))

$case7 = Join-Path $tmpRoot "case7-safety-false"
Write-GoFixture -RecorderRoot $case7
$r7 = Invoke-Verdict -RecorderRoot $case7 -NoOrderEntry "false"
$results.Add((New-Result "forbidden_safety_flag_false_wrapper_exits_nonzero" ($r7.wrapper_exit_code -ne 0 -and ($r7.verdict.issues -contains "safety_flag_not_true:no_order_entry")) $r7))

$case8 = Join-Path $tmpRoot "case8-safety-missing"
Write-GoFixture -RecorderRoot $case8
$r8 = Invoke-Verdict -RecorderRoot $case8 -NoDatabento ""
$results.Add((New-Result "forbidden_safety_flag_missing_wrapper_exits_nonzero" ($r8.wrapper_exit_code -ne 0 -and ($r8.verdict.issues -contains "safety_flag_missing:no_databento")) $r8))

$failed = @($results.ToArray() | Where-Object { $_.status -ne "PASS" })
$report = [ordered]@{
    gate = if ($failed.Count -eq 0) { "GO_AWS2C_WRAPPER_EXIT_CODE_TESTS" } else { "NO_GO_AWS2C_WRAPPER_EXIT_CODE_TESTS" }
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    no_broker_capture = $true
    no_secret_values = $true
    fixture_root = $tmpRoot
    results = @($results.ToArray())
    failure_count = $failed.Count
}

$fullOutput = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fullOutput -Encoding UTF8
$report | ConvertTo-Json -Depth 12
if ($failed.Count -eq 0) { exit 0 }
exit 1