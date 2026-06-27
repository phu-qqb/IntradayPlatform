param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$OutputPath = "artifacts\readiness\qq-fund-platform-aws2e-wrapper-finalization-and-ssm-budget-fix\AWS2E_WRAPPER_FINALIZATION_TEST_REPORT.generated.json"
)

$ErrorActionPreference = "Stop"
$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$startScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Start-AnubisAws1Recorder.ps1"
$verdictScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Test-AnubisAws1RecorderArtifactVerdict.ps1"
$tmpRoot = Join-Path $repo ("artifacts\tmp\aws2e-wrapper-finalization-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null

function New-Result {
    param([string]$Name, [bool]$Pass, [object]$Detail)
    [ordered]@{ name = $Name; status = $(if ($Pass) { "PASS" } else { "FAIL" }); detail = $Detail }
}

function Write-Json {
    param([string]$Path, [object]$Value)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-ConfigFixture {
    param([string]$Path, [int]$MaxDurationSeconds = 300)
    Write-Json -Path $Path -Value ([ordered]@{
        mode = "CAPTURE_ONLY"
        environment = "DEMO"
        credential_scope = "MARKET_DATA_ONLY"
        market_data_credential_reference = "aws-secretsmanager:fixture-redacted"
        instruments = @("EURUSD")
        output_root = "fixture"
        max_duration_seconds = $MaxDurationSeconds
    })
}

function Write-CaptureFixture {
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
    $runRoot = Join-Path $RecorderRoot "environment=DEMO\date=2026-06-26\recorder_run=AWS2E_FIXTURE"
    New-Item -ItemType Directory -Force -Path $runRoot, (Join-Path $runRoot "health") | Out-Null

    $capture = [ordered]@{
        status = $Status
        recorder_run_id = "AWS2E_FIXTURE"
        run_root = $runRoot
        replay_status = $ReplayStatus
        market_data_received = $MarketDataReceived
        bbo_updated = $BboUpdated
        writer_error_count = $WriterErrorCount
        dropped_event_count = $DroppedEventCount
        inbound_execution_report_observed = $false
        event_counts = [ordered]@{ BBO_UPDATED = $BboUpdated; MARKET_DATA_RECEIVED = $MarketDataReceived }
    }
    Write-Json -Path (Join-Path $RecorderRoot "m2c1b_capture_command_result.json") -Value $capture
    Write-Json -Path (Join-Path $runRoot "m2c1b_capture_manifest.json") -Value $capture

    if ($WriteFinalManifest) {
        Write-Json -Path (Join-Path $runRoot "final_manifest.json") -Value ([ordered]@{
            recorder_manifest_version = "canonical_recorder_manifest_v2"
            recorder_run_id = "AWS2E_FIXTURE"
            finalized = $Finalized
            event_counts = [ordered]@{ BBO_UPDATED = $BboUpdated; MARKET_DATA_RECEIVED = $MarketDataReceived }
            events_dropped = $FinalEventsDropped
            writer_errors = $FinalWriterErrors
            failure_reason = $null
        })
    }

    Write-Json -Path (Join-Path $runRoot "health\data_quality_report.json") -Value ([ordered]@{
        sequence_gap_count = $SequenceGapCount
        sequence_out_of_order_count = $SequenceOutOfOrderCount
        shadow_ready = $true
    })
}

function Invoke-WrapperCase {
    param(
        [string]$Name,
        [int]$RawChildExitCode = 0,
        [bool]$CreateFixture = $true,
        [hashtable]$FixtureOptions = @{},
        [int]$CommandTimeoutSeconds = 900,
        [int]$RecorderMaxDurationSeconds = 1,
        [int]$StartupBudgetSeconds = 1,
        [int]$FinalizationBudgetSeconds = 1,
        [int]$ArchiveFinalizationBudgetSeconds = 0,
        [string]$ArtifactVerdictScriptOverride = "",
        [bool]$WriteStalePid = $false
    )
    $caseRoot = Join-Path $tmpRoot $Name
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    $installRoot = Join-Path $caseRoot "install"
    $recorderRoot = Join-Path $caseRoot "recorder"
    $stateRoot = Join-Path $caseRoot "state"
    $logRoot = Join-Path $caseRoot "logs"
    New-Item -ItemType Directory -Force -Path $installRoot, $recorderRoot, $stateRoot, $logRoot | Out-Null
    if ($CreateFixture) { Write-CaptureFixture -RecorderRoot $recorderRoot @FixtureOptions }

    $configPath = Join-Path $caseRoot "m2c1b_aws_capture_config.json"
    Write-ConfigFixture -Path $configPath -MaxDurationSeconds $RecorderMaxDurationSeconds

    if ($WriteStalePid) {
        Write-Json -Path (Join-Path $stateRoot "aws1-recorder.pid.json") -Value ([ordered]@{
            pid = 999999
            executable_path = "C:\missing\dead.exe"
            process_start_time_utc = "2026-06-26T00:00:00Z"
        })
    }

    $markerPath = Join-Path $caseRoot "fake-child-started.txt"
    $fakeChild = Join-Path $caseRoot "fake-child.ps1"
    Set-Content -LiteralPath $fakeChild -Encoding UTF8 -Value @(
        'param([int]$ExitCode = 0, [string]$MarkerPath = "")',
        'if (-not [string]::IsNullOrWhiteSpace($MarkerPath)) { Set-Content -LiteralPath $MarkerPath -Value "started" -Encoding ASCII }',
        'exit $ExitCode'
    )

    $powerShell = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
    $childArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$fakeChild`" -ExitCode $RawChildExitCode -MarkerPath `"$markerPath`""
    $args = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $startScript,
        "-InstallRoot", $installRoot,
        "-ConfigPath", $configPath,
        "-RecorderRoot", $recorderRoot,
        "-StateRoot", $stateRoot,
        "-LogRoot", $logRoot,
        "-NoSecretFetch",
        "-SkipHostPrerequisites",
        "-AppExecutableOverride", $powerShell,
        "-AppArgumentsOverride", $childArgs,
        "-CommandTimeoutSeconds", $CommandTimeoutSeconds,
        "-RecorderMaxDurationSeconds", $RecorderMaxDurationSeconds,
        "-StartupBudgetSeconds", $StartupBudgetSeconds,
        "-FinalizationBudgetSeconds", $FinalizationBudgetSeconds,
        "-ArchiveFinalizationBudgetSeconds", $ArchiveFinalizationBudgetSeconds
    )
    if (-not [string]::IsNullOrWhiteSpace($ArtifactVerdictScriptOverride)) {
        $args += @("-ArtifactVerdictScriptOverride", $ArtifactVerdictScriptOverride)
    }

    $output = @(& $powerShell @args 2>&1)
    $exitCode = $LASTEXITCODE
    $lastRunPath = Join-Path $stateRoot "aws1_last_run_result.json"
    $lastRun = if (Test-Path -LiteralPath $lastRunPath) { Get-Content -LiteralPath $lastRunPath -Raw | ConvertFrom-Json } else { $null }

    [ordered]@{
        name = $Name
        exit_code = $exitCode
        output = $output
        case_root = $caseRoot
        child_marker_exists = Test-Path -LiteralPath $markerPath
        last_run_path = $lastRunPath
        last_run_exists = Test-Path -LiteralPath $lastRunPath
        last_run = $lastRun
        pid_exists_after = Test-Path -LiteralPath (Join-Path $stateRoot "aws1-recorder.pid.json")
    }
}

function Invoke-VerdictOnly {
    param(
        [string]$Name,
        [string]$NoOrderEntry = "true",
        [string]$NoAccountApi = "true",
        [string]$NoDb = "true",
        [string]$NoDatabento = "true"
    )
    $caseRoot = Join-Path $tmpRoot $Name
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    $recorderRoot = Join-Path $caseRoot "recorder"
    Write-CaptureFixture -RecorderRoot $recorderRoot
    $json = & $verdictScript -RecorderRoot $recorderRoot -NoOrderEntry $NoOrderEntry -NoAccountApi $NoAccountApi -NoDb $NoDb -NoDatabento $NoDatabento -Json
    $exitCode = $LASTEXITCODE
    $verdict = ($json -join "`n") | ConvertFrom-Json
    [ordered]@{ name = $Name; exit_code = $exitCode; verdict = $verdict }
}

$results = New-Object System.Collections.Generic.List[object]

$r1 = Invoke-WrapperCase -Name "raw_child_exit_5_artifacts_go" -RawChildExitCode 5
$results.Add((New-Result "raw_child_exit_5_artifacts_go_writes_last_run_and_exits_0" ($r1.exit_code -eq 0 -and $r1.last_run_exists -and $r1.last_run.raw_child_exit_code -eq 5 -and $r1.last_run.wrapper_exit_code -eq 0 -and $r1.last_run.artifact_verdict -eq "GO_AWS2C_WRAPPER_ARTIFACTS_VALIDATED") $r1))

$r2 = Invoke-WrapperCase -Name "raw_child_exit_0_artifacts_no_go" -RawChildExitCode 0 -FixtureOptions @{ Status = "NO_GO_M2C1B"; ReplayStatus = "FAIL"; MarketDataReceived = 0; BboUpdated = 0 }
$results.Add((New-Result "raw_child_exit_0_artifacts_no_go_writes_last_run_and_exits_nonzero" ($r2.exit_code -ne 0 -and $r2.last_run_exists -and $r2.last_run.raw_child_exit_code -eq 0 -and $r2.last_run.wrapper_exit_code -ne 0) $r2))

$throwingVerdict = Join-Path $tmpRoot "throwing-verdict.ps1"
Set-Content -LiteralPath $throwingVerdict -Value 'throw "fixture_artifact_evaluator_failure"' -Encoding UTF8
$r3 = Invoke-WrapperCase -Name "artifact_evaluator_throws" -RawChildExitCode 0 -ArtifactVerdictScriptOverride $throwingVerdict
$results.Add((New-Result "artifact_evaluator_throws_writes_no_go_last_run" ($r3.exit_code -ne 0 -and $r3.last_run_exists -and $r3.last_run.status -eq "SMOKE_CAPTURE_ARTIFACTS_NO_GO" -and @($r3.last_run.artifact_issues | Where-Object { $_ -like "artifact_verdict_evaluator_failed:*" }).Count -gt 0) $r3))

$r4 = Invoke-WrapperCase -Name "stale_pid_removed" -RawChildExitCode 5 -WriteStalePid $true
$results.Add((New-Result "stale_pid_for_dead_process_removed_and_does_not_block_start" ($r4.exit_code -eq 0 -and $r4.last_run.stale_pid_found -eq $true -and $r4.last_run.stale_pid_removed -eq $true -and $r4.pid_exists_after -eq $false) $r4))

$r5 = Invoke-WrapperCase -Name "insufficient_timeout_preflight" -CreateFixture $false -RawChildExitCode 0 -CommandTimeoutSeconds 100 -RecorderMaxDurationSeconds 300 -StartupBudgetSeconds 30 -FinalizationBudgetSeconds 120 -ArchiveFinalizationBudgetSeconds 0
$results.Add((New-Result "live_smoke_preflight_fails_before_capture_when_timeout_budget_insufficient" ($r5.exit_code -ne 0 -and $r5.last_run_exists -and $r5.child_marker_exists -eq $false -and $r5.last_run.status -eq "SMOKE_CAPTURE_PRECHECK_NO_GO" -and @($r5.last_run.artifact_issues | Where-Object { $_ -like "ssm_timeout_budget_insufficient:*" }).Count -gt 0) $r5))

$r6 = Invoke-WrapperCase -Name "sufficient_timeout_preflight" -RawChildExitCode 5 -CommandTimeoutSeconds 500 -RecorderMaxDurationSeconds 300 -StartupBudgetSeconds 30 -FinalizationBudgetSeconds 120 -ArchiveFinalizationBudgetSeconds 0
$results.Add((New-Result "sufficient_timeout_finalization_budget_passes_preflight" ($r6.exit_code -eq 0 -and $r6.last_run.command_timeout_seconds -eq 500 -and $r6.last_run.required_timeout_seconds -eq 450) $r6))

$r7 = Invoke-WrapperCase -Name "missing_final_manifest" -RawChildExitCode 0 -FixtureOptions @{ WriteFinalManifest = $false }
$results.Add((New-Result "missing_final_manifest_remains_nonzero" ($r7.exit_code -ne 0 -and $r7.last_run_exists -and @($r7.last_run.artifact_issues | Where-Object { $_ -eq "final_manifest_missing" }).Count -gt 0) $r7))

$r8 = Invoke-WrapperCase -Name "writer_errors" -RawChildExitCode 0 -FixtureOptions @{ WriterErrorCount = 1; FinalWriterErrors = 1 }
$results.Add((New-Result "writer_errors_remain_nonzero" ($r8.exit_code -ne 0 -and $r8.last_run_exists) $r8))

$r9 = Invoke-WrapperCase -Name "drops" -RawChildExitCode 0 -FixtureOptions @{ DroppedEventCount = 1; FinalEventsDropped = 1 }
$results.Add((New-Result "drops_remain_nonzero" ($r9.exit_code -ne 0 -and $r9.last_run_exists) $r9))

$r10 = Invoke-WrapperCase -Name "sequence_gap" -RawChildExitCode 0 -FixtureOptions @{ SequenceGapCount = 1 }
$results.Add((New-Result "sequence_gap_remains_nonzero" ($r10.exit_code -ne 0 -and $r10.last_run_exists -and @($r10.last_run.artifact_issues | Where-Object { $_ -eq "sequence_gap_status_not_zero" }).Count -gt 0) $r10))

$r11 = Invoke-VerdictOnly -Name "safety_false" -NoOrderEntry "false"
$results.Add((New-Result "forbidden_safety_flag_false_remains_nonzero" ($r11.exit_code -ne 0 -and @($r11.verdict.issues | Where-Object { $_ -eq "safety_flag_not_true:no_order_entry" }).Count -gt 0) $r11))

$r12 = Invoke-VerdictOnly -Name "safety_missing" -NoDatabento ""
$results.Add((New-Result "forbidden_safety_flag_missing_remains_nonzero" ($r12.exit_code -ne 0 -and @($r12.verdict.issues | Where-Object { $_ -eq "safety_flag_missing:no_databento" }).Count -gt 0) $r12))

$failed = @($results.ToArray() | Where-Object { $_.status -ne "PASS" })
$report = [ordered]@{
    gate = if ($failed.Count -eq 0) { "GO_AWS2E_WRAPPER_FINALIZATION_OFFLINE_TESTS" } else { "NO_GO_AWS2E_WRAPPER_FINALIZATION_OFFLINE_TESTS" }
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    no_broker_capture = $true
    no_secret_values = $true
    fixture_root = $tmpRoot
    results = @($results.ToArray())
    failure_count = $failed.Count
}

$fullOutput = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $fullOutput -Encoding UTF8
$report | ConvertTo-Json -Depth 20
if ($failed.Count -eq 0) { exit 0 }
exit 1
