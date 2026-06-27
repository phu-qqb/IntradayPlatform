param(
    [string]$InstallRoot = "C:\Anubis\M2Capture\current",
    [string]$ConfigPath = "",
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$CredentialSecretId = "",
    [string]$ArchiveBucketName = "",
    [string]$Environment = "demo",
    [string]$CloudWatchNamespace = "QQFundPlatform/AWS1",
    [string]$ExpectedAwsCliSha256 = "",
    [switch]$NoSecretFetch,
    [string]$StateRoot = "C:\Anubis\State",
    [string]$LogRoot = "C:\Anubis\Logs",
    [int]$CommandTimeoutSeconds = 300,
    [int]$RecorderMaxDurationSeconds = 0,
    [int]$StartupBudgetSeconds = 30,
    [int]$FinalizationBudgetSeconds = 120,
    [int]$ArchiveFinalizationBudgetSeconds = 60,
    [int]$ArtifactFreshnessSlackSeconds = 30,
    [string]$AppExecutableOverride = "",
    [string]$AppArgumentsOverride = "",
    [string]$ArtifactVerdictScriptOverride = "",
    [switch]$SkipHostPrerequisites
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConfigPath)) { $ConfigPath = Join-Path $InstallRoot "config\m2c1b_aws_capture_config.json" }

$pidPath = Join-Path $StateRoot "aws1-recorder.pid.json"
$lastRunPath = Join-Path $StateRoot "aws1_last_run_result.json"
$script:stalePidFound = $false
$script:stalePidRemoved = $false
$script:stalePidReason = $null
$script:pidFileRemovedAfterExit = $false
$script:pidFileWritten = $false
$script:processStartTimeUtc = $null
$script:stdout = $null
$script:stderr = $null
$script:appExe = $null
$script:appExeSha = $null
$script:rawChildExitCode = $null
$script:artifactVerdictExitCode = 5
$script:artifactVerdict = $null
$script:finalizationStartedUtc = $null
$script:finalizationCompletedUtc = $null
$script:requiredTimeoutSeconds = $null
$script:recorderMaxDurationSecondsResolved = $null
$script:failureIssue = $null

function Get-HashUpper {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Get-JsonFileOrNull {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json }
    catch { return $null }
}

function Get-Prop {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-IntOrDefault {
    param([object]$Value, [int]$Default)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $Default }
    try { return [int]$Value } catch { return $Default }
}

function Read-PidState {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return (Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json) }
    catch {
        $script:stalePidFound = $true
        $script:stalePidReason = "pid_state_unreadable"
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        $script:stalePidRemoved = -not (Test-Path -LiteralPath $Path)
        return $null
    }
}

function Test-RecordedProcessAlive {
    param([object]$State)
    if ($null -eq $State -or $null -eq $State.pid) { return $false }
    $p = Get-Process -Id ([int]$State.pid) -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $false }
    if ([string]$State.executable_path -and -not [string]::Equals($p.Path, [string]$State.executable_path, [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    if ([string]$State.process_start_time_utc) {
        $expected = [DateTimeOffset]::Parse([string]$State.process_start_time_utc).UtcDateTime
        $actual = $p.StartTime.ToUniversalTime()
        if ([math]::Abs(($actual - $expected).TotalSeconds) -gt 2) { return $false }
    }
    return $true
}

function Remove-StalePidIfNeeded {
    param([string]$Path)
    $oldState = Read-PidState -Path $Path
    if (Test-RecordedProcessAlive -State $oldState) { throw "recorder_already_running:$($oldState.pid)" }
    if ($null -ne $oldState -and (Test-Path -LiteralPath $Path)) {
        $script:stalePidFound = $true
        $script:stalePidReason = "recorded_process_not_alive"
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        $script:stalePidRemoved = -not (Test-Path -LiteralPath $Path)
    }
}

function Read-MarketDataSecret {
    param([string]$SecretId, [string]$AwsCliPath)
    if ([string]::IsNullOrWhiteSpace($SecretId)) { throw "credential_secret_id_required" }
    $secretText = & $AwsCliPath secretsmanager get-secret-value --secret-id $SecretId --query SecretString --output text
    if ($LASTEXITCODE -ne 0) { throw "aws_cli_secret_fetch_failed:$LASTEXITCODE" }
    return ($secretText | ConvertFrom-Json)
}

function Get-RecorderMaxDuration {
    param([string]$Path, [int]$OverrideSeconds)
    if ($OverrideSeconds -gt 0) { return $OverrideSeconds }
    $config = Get-JsonFileOrNull -Path $Path
    $value = Get-Prop $config "max_duration_seconds"
    $resolved = Get-IntOrDefault -Value $value -Default 300
    if ($resolved -le 0) { return 300 }
    return $resolved
}

function Get-RequiredTimeoutSeconds {
    param([int]$MaxDurationSeconds)
    $archiveBudget = if ([string]::IsNullOrWhiteSpace($ArchiveBucketName)) { 0 } else { [math]::Max(0, $ArchiveFinalizationBudgetSeconds) }
    return ($MaxDurationSeconds + [math]::Max(0, $StartupBudgetSeconds) + [math]::Max(0, $FinalizationBudgetSeconds) + $archiveBudget)
}

function Assert-TimeoutBudget {
    param([int]$MaxDurationSeconds)
    $script:recorderMaxDurationSecondsResolved = $MaxDurationSeconds
    $script:requiredTimeoutSeconds = Get-RequiredTimeoutSeconds -MaxDurationSeconds $MaxDurationSeconds
    if ($CommandTimeoutSeconds -gt 0 -and $CommandTimeoutSeconds -lt $script:requiredTimeoutSeconds) {
        throw "ssm_timeout_budget_insufficient:command_timeout_seconds=$CommandTimeoutSeconds required_timeout_seconds=$($script:requiredTimeoutSeconds) recorder_max_duration_seconds=$MaxDurationSeconds"
    }
}

function New-ArtifactVerdictFallback {
    param([string]$Issue)
    return [ordered]@{
        artifact_verdict = "NO_GO_AWS2E_WRAPPER_ARTIFACTS_INVALID"
        wrapper_should_exit_zero = $false
        issues = @($Issue)
        metrics = [ordered]@{}
        capture_result_path = Join-Path $RecorderRoot "m2c1b_capture_command_result.json"
        run_root = $null
        final_manifest_path = $null
        capture_manifest_path = $null
        data_quality_report_path = $null
    }
}

function Invoke-ArtifactVerdictSafe {
    $artifactVerdictScript = if ([string]::IsNullOrWhiteSpace($ArtifactVerdictScriptOverride)) { Join-Path $PSScriptRoot "Test-AnubisAws1RecorderArtifactVerdict.ps1" } else { $ArtifactVerdictScriptOverride }
    $minimumWriteUtc = if ($null -ne $script:processStartTimeUtc) { $script:processStartTimeUtc.AddSeconds(-[math]::Max(0, $ArtifactFreshnessSlackSeconds)) } else { [datetime]::UtcNow.AddMinutes(-10) }
    $artifactVerdictText = @()
    $script:artifactVerdictExitCode = 5
    try {
        $artifactVerdictText = @(& $artifactVerdictScript -RecorderRoot $RecorderRoot -MinimumWriteUtc $minimumWriteUtc -NoOrderEntry "true" -NoAccountApi "true" -NoDb "true" -NoDatabento "true" -Json 2>&1)
        $script:artifactVerdictExitCode = $LASTEXITCODE
        $script:artifactVerdict = ($artifactVerdictText -join "`n") | ConvertFrom-Json
    }
    catch {
        $script:artifactVerdictExitCode = 5
        $script:artifactVerdict = New-ArtifactVerdictFallback -Issue "artifact_verdict_evaluator_failed:$($_.Exception.GetType().Name)"
    }
}

function New-LastRunResult {
    param([int]$WrapperExitCode, [string]$Status, [string[]]$ExtraIssues = @())
    $artifact = if ($null -ne $script:artifactVerdict) { $script:artifactVerdict } else { New-ArtifactVerdictFallback -Issue "artifact_verdict_not_evaluated" }
    $issues = @($artifact.issues) + @($ExtraIssues | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    return [ordered]@{
        status = $Status
        wrapper_exit_code = $WrapperExitCode
        raw_child_exit_code = $script:rawChildExitCode
        artifact_verdict_exit_code = $script:artifactVerdictExitCode
        artifact_verdict = $artifact.artifact_verdict
        artifact_issues = @($issues)
        artifact_metrics = $artifact.metrics
        artifact_paths = [ordered]@{
            capture_result_path = $artifact.capture_result_path
            run_root = $artifact.run_root
            final_manifest_path = $artifact.final_manifest_path
            capture_manifest_path = $artifact.capture_manifest_path
            data_quality_report_path = $artifact.data_quality_report_path
        }
        executable_path = $script:appExe
        executable_sha256 = $script:appExeSha
        config_path = $ConfigPath
        recorder_root = $RecorderRoot
        stdout_log = $script:stdout
        stderr_log = $script:stderr
        state_root = $StateRoot
        pid_path = $pidPath
        stale_pid_found = [bool]$script:stalePidFound
        stale_pid_removed = [bool]$script:stalePidRemoved
        stale_pid_reason = $script:stalePidReason
        pid_file_written = [bool]$script:pidFileWritten
        pid_file_removed_after_exit = [bool]$script:pidFileRemovedAfterExit
        process_start_time_utc = if ($null -ne $script:processStartTimeUtc) { $script:processStartTimeUtc.ToString("o") } else { $null }
        finalization_started_utc = if ($null -ne $script:finalizationStartedUtc) { $script:finalizationStartedUtc.ToString("o") } else { $null }
        finalization_completed_utc = if ($null -ne $script:finalizationCompletedUtc) { $script:finalizationCompletedUtc.ToString("o") } else { $null }
        command_timeout_seconds = $CommandTimeoutSeconds
        recorder_max_duration_seconds = $script:recorderMaxDurationSecondsResolved
        startup_budget_seconds = $StartupBudgetSeconds
        finalization_budget_seconds = $FinalizationBudgetSeconds
        archive_finalization_budget_seconds = if ([string]::IsNullOrWhiteSpace($ArchiveBucketName)) { 0 } else { $ArchiveFinalizationBudgetSeconds }
        artifact_freshness_slack_seconds = $ArtifactFreshnessSlackSeconds
        required_timeout_seconds = $script:requiredTimeoutSeconds
        archive_upload_attempted = $false
        archive_upload_policy = "not_invoked_by_wrapper_aws2e_backlog_requires_separate_archival_gate"
        secret_values_logged = $false
        no_order_entry = $true
        no_account_api = $true
        no_db = $true
        no_databento = $true
        raw_ticks_emitted_to_cloudwatch = $false
        timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Write-LastRunResult {
    param([object]$Result)
    New-Item -ItemType Directory -Force -Path $StateRoot | Out-Null
    $temp = Join-Path $StateRoot ("aws1_last_run_result." + [guid]::NewGuid().ToString("N") + ".tmp")
    $Result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temp -Encoding UTF8
    Move-Item -LiteralPath $temp -Destination $lastRunPath -Force
    $Result | ConvertTo-Json -Depth 10
}

function Fail-Controlled {
    param([string]$Issue, [string]$Status = "SMOKE_CAPTURE_WRAPPER_NO_GO")
    $script:failureIssue = $Issue
    $result = New-LastRunResult -WrapperExitCode 5 -Status $Status -ExtraIssues @($Issue)
    Write-LastRunResult -Result $result
    exit 5
}

try {
    New-Item -ItemType Directory -Force -Path $StateRoot, $LogRoot, $RecorderRoot | Out-Null
    Remove-StalePidIfNeeded -Path $pidPath

    if (-not (Test-Path -LiteralPath $ConfigPath)) { throw "capture_config_not_found:$ConfigPath" }
    $maxDuration = Get-RecorderMaxDuration -Path $ConfigPath -OverrideSeconds $RecorderMaxDurationSeconds
    Assert-TimeoutBudget -MaxDurationSeconds $maxDuration

    if (-not [string]::IsNullOrWhiteSpace($AppExecutableOverride)) {
        $script:appExe = $AppExecutableOverride
        if (-not (Test-Path -LiteralPath $script:appExe)) { throw "capture_host_exe_not_found:$($script:appExe)" }
        $script:appExeSha = Get-HashUpper $script:appExe
    }
    else {
        $appManifestPath = Join-Path $InstallRoot "app\app_manifest.json"
        if (-not (Test-Path -LiteralPath $appManifestPath)) { throw "app_manifest_missing:$appManifestPath" }
        $appManifest = Get-Content -Raw -LiteralPath $appManifestPath | ConvertFrom-Json
        $script:appExe = Join-Path $InstallRoot (Join-Path "app" $appManifest.executable)
        if (-not (Test-Path -LiteralPath $script:appExe)) { throw "capture_host_exe_not_found:$($script:appExe)" }
        $script:appExeSha = Get-HashUpper $script:appExe
        if ($script:appExeSha -ne ([string]$appManifest.executable_sha256).ToUpperInvariant()) { throw "capture_host_exe_sha256_mismatch:$($script:appExeSha)" }
    }

    $awsCliPath = ""
    if (-not $SkipHostPrerequisites) {
        $prereq = & (Join-Path $PSScriptRoot "Test-AnubisAws1HostPrerequisites.ps1") -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 -Json | ConvertFrom-Json
        if ($prereq.status -ne "PASS") { throw "host_prerequisites_failed:$($prereq | ConvertTo-Json -Compress)" }
        $awsCliPath = [string]$prereq.aws_cli_path
    }

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $script:appExe
    $psi.Arguments = if ([string]::IsNullOrWhiteSpace($AppArgumentsOverride)) { "capture --config `"$ConfigPath`" --operator-approved-market-data-fix-logon --no-order-entry --no-account-api --no-db" } else { $AppArgumentsOverride }
    $psi.WorkingDirectory = $InstallRoot
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Environment["ANUBIS_AWS1_RECORDER_ROOT"] = $RecorderRoot
    $psi.Environment["ANUBIS_AWS1_ENVIRONMENT"] = $Environment
    $psi.Environment["ANUBIS_AWS1_ARCHIVE_BUCKET"] = $ArchiveBucketName
    $psi.Environment["ANUBIS_AWS1_CLOUDWATCH_NAMESPACE"] = $CloudWatchNamespace

    if (-not $NoSecretFetch) {
        if ([string]::IsNullOrWhiteSpace($awsCliPath)) { throw "aws_cli_path_required_for_secret_fetch" }
        $secret = Read-MarketDataSecret -SecretId $CredentialSecretId -AwsCliPath $awsCliPath
        foreach ($name in @("LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID", "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD")) {
            $value = $secret.$name
            if ([string]::IsNullOrWhiteSpace([string]$value)) { throw "secret_missing_required_label:$name" }
            $psi.Environment[$name] = [string]$value
        }
    }

    $stamp = Get-Date -Format "yyyyMMddHHmmss"
    $script:stdout = Join-Path $LogRoot "aws1-recorder-$stamp.out.log"
    $script:stderr = Join-Path $LogRoot "aws1-recorder-$stamp.err.log"
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $stdoutTask = $null
    $stderrTask = $null

    try {
        if (-not $process.Start()) { throw "process_start_failed" }
        $script:processStartTimeUtc = $process.StartTime.ToUniversalTime()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        $pidState = [ordered]@{
            pid = $process.Id
            executable_path = $script:appExe
            executable_sha256 = $script:appExeSha
            process_start_time_utc = $script:processStartTimeUtc.ToString("o")
            operation_mode = "SMOKE_CAPTURE_BOUNDED"
            config_path = $ConfigPath
            recorder_root = $RecorderRoot
            stdout_log = $script:stdout
            stderr_log = $script:stderr
            command_timeout_seconds = $CommandTimeoutSeconds
            required_timeout_seconds = $script:requiredTimeoutSeconds
            stop_request_path = Join-Path $StateRoot "aws1-stop-request.json"
            timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
        }
        $pidState | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $pidPath -Encoding UTF8
        $script:pidFileWritten = $true

        $process.WaitForExit()
        $process.WaitForExit(5000) | Out-Null
        $script:rawChildExitCode = $process.ExitCode
    }
    finally {
        try {
            if ($null -ne $stdoutTask) { $stdoutTask.Wait(5000) | Out-Null; [System.IO.File]::WriteAllText($script:stdout, [string]$stdoutTask.Result, [System.Text.Encoding]::UTF8) }
            elseif ($script:stdout) { [System.IO.File]::WriteAllText($script:stdout, "", [System.Text.Encoding]::UTF8) }
        } catch { if ($script:stdout) { [System.IO.File]::WriteAllText($script:stdout, "stdout_capture_failed", [System.Text.Encoding]::UTF8) } }
        try {
            if ($null -ne $stderrTask) { $stderrTask.Wait(5000) | Out-Null; [System.IO.File]::WriteAllText($script:stderr, [string]$stderrTask.Result, [System.Text.Encoding]::UTF8) }
            elseif ($script:stderr) { [System.IO.File]::WriteAllText($script:stderr, "", [System.Text.Encoding]::UTF8) }
        } catch { if ($script:stderr) { [System.IO.File]::WriteAllText($script:stderr, "stderr_capture_failed", [System.Text.Encoding]::UTF8) } }
        Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
        $script:pidFileRemovedAfterExit = -not (Test-Path -LiteralPath $pidPath)
        if ($null -ne $process) { $process.Dispose() }
    }

    $script:finalizationStartedUtc = (Get-Date).ToUniversalTime()
    Invoke-ArtifactVerdictSafe
    $wrapperExitCode = if ($script:artifactVerdictExitCode -eq 0 -and [bool]$script:artifactVerdict.wrapper_should_exit_zero) { 0 } else { 5 }
    $script:finalizationCompletedUtc = (Get-Date).ToUniversalTime()
    $status = if ($wrapperExitCode -eq 0) { "SMOKE_CAPTURE_ARTIFACTS_VALIDATED" } else { "SMOKE_CAPTURE_ARTIFACTS_NO_GO" }
    $result = New-LastRunResult -WrapperExitCode $wrapperExitCode -Status $status
    Write-LastRunResult -Result $result
    exit $wrapperExitCode
}
catch {
    $issue = $_.Exception.Message
    if ([string]::IsNullOrWhiteSpace($issue)) { $issue = $_.Exception.GetType().Name }
    if ($issue -like "ssm_timeout_budget_insufficient:*") {
        Fail-Controlled -Issue $issue -Status "SMOKE_CAPTURE_PRECHECK_NO_GO"
    }
    Fail-Controlled -Issue "wrapper_controlled_failure:$issue" -Status "SMOKE_CAPTURE_WRAPPER_NO_GO"
}




