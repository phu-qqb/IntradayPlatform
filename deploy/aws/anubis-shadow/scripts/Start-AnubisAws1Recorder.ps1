param(
    [string]$InstallRoot = "C:\Anubis\M2Capture\current",
    [string]$ConfigPath = "",
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$CredentialSecretId = "",
    [string]$ArchiveBucketName = "",
    [string]$Environment = "demo",
    [string]$CloudWatchNamespace = "QQFundPlatform/AWS1",
    [string]$ExpectedAwsCliSha256 = "",
    [switch]$NoSecretFetch
)

$ErrorActionPreference = "Stop"

$stateRoot = "C:\Anubis\State"
$logRoot = "C:\Anubis\Logs"
New-Item -ItemType Directory -Force -Path $stateRoot, $logRoot, $RecorderRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($ConfigPath)) { $ConfigPath = Join-Path $InstallRoot "config\m2c1b_aws_capture_config.json" }

function Get-HashUpper { param([string]$Path) (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant() }

function Read-PidState {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return (Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json) }
    catch { Remove-Item -LiteralPath $Path -Force; return $null }
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

function Read-MarketDataSecret {
    param([string]$SecretId, [string]$AwsCliPath)
    if ([string]::IsNullOrWhiteSpace($SecretId)) { throw "credential_secret_id_required" }
    $secretText = & $AwsCliPath secretsmanager get-secret-value --secret-id $SecretId --query SecretString --output text
    if ($LASTEXITCODE -ne 0) { throw "aws_cli_secret_fetch_failed:$LASTEXITCODE" }
    return ($secretText | ConvertFrom-Json)
}

$pidPath = Join-Path $stateRoot "aws1-recorder.pid.json"
$oldState = Read-PidState -Path $pidPath
if (Test-RecordedProcessAlive -State $oldState) { throw "recorder_already_running:$($oldState.pid)" }
if ($null -ne $oldState) { Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue }

$appManifestPath = Join-Path $InstallRoot "app\app_manifest.json"
if (-not (Test-Path -LiteralPath $appManifestPath)) { throw "app_manifest_missing:$appManifestPath" }
$appManifest = Get-Content -Raw -LiteralPath $appManifestPath | ConvertFrom-Json
$appExe = Join-Path $InstallRoot (Join-Path "app" $appManifest.executable)
if (-not (Test-Path -LiteralPath $appExe)) { throw "capture_host_exe_not_found:$appExe" }
$appExeSha = Get-HashUpper $appExe
if ($appExeSha -ne ([string]$appManifest.executable_sha256).ToUpperInvariant()) { throw "capture_host_exe_sha256_mismatch:$appExeSha" }
if (-not (Test-Path -LiteralPath $ConfigPath)) { throw "capture_config_not_found:$ConfigPath" }

$prereq = & (Join-Path $PSScriptRoot "Test-AnubisAws1HostPrerequisites.ps1") -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 -Json | ConvertFrom-Json
if ($prereq.status -ne "PASS") { throw "host_prerequisites_failed:$($prereq | ConvertTo-Json -Compress)" }
$awsCliPath = [string]$prereq.aws_cli_path

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $appExe
$psi.Arguments = "capture --config `"$ConfigPath`" --operator-approved-market-data-fix-logon --no-order-entry --no-account-api --no-db"
$psi.WorkingDirectory = $InstallRoot
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.Environment["ANUBIS_AWS1_RECORDER_ROOT"] = $RecorderRoot
$psi.Environment["ANUBIS_AWS1_ENVIRONMENT"] = $Environment
$psi.Environment["ANUBIS_AWS1_ARCHIVE_BUCKET"] = $ArchiveBucketName
$psi.Environment["ANUBIS_AWS1_CLOUDWATCH_NAMESPACE"] = $CloudWatchNamespace

if (-not $NoSecretFetch) {
    $secret = Read-MarketDataSecret -SecretId $CredentialSecretId -AwsCliPath $awsCliPath
    foreach ($name in @("LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID", "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD")) {
        $value = $secret.$name
        if ([string]::IsNullOrWhiteSpace([string]$value)) { throw "secret_missing_required_label:$name" }
        $psi.Environment[$name] = [string]$value
    }
}

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$stdout = Join-Path $logRoot "aws1-recorder-$stamp.out.log"
$stderr = Join-Path $logRoot "aws1-recorder-$stamp.err.log"
$stdoutWriter = [System.IO.StreamWriter]::new($stdout, $false, [System.Text.Encoding]::UTF8)
$stderrWriter = [System.IO.StreamWriter]::new($stderr, $false, [System.Text.Encoding]::UTF8)
$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $psi

$process.add_OutputDataReceived([System.Diagnostics.DataReceivedEventHandler]{ param($sender, $eventArgs) if ($null -ne $eventArgs.Data) { $stdoutWriter.WriteLine($eventArgs.Data); $stdoutWriter.Flush() } })
$process.add_ErrorDataReceived([System.Diagnostics.DataReceivedEventHandler]{ param($sender, $eventArgs) if ($null -ne $eventArgs.Data) { $stderrWriter.WriteLine($eventArgs.Data); $stderrWriter.Flush() } })

try {
    if (-not $process.Start()) { throw "process_start_failed" }
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    $pidState = [ordered]@{
        pid = $process.Id
        executable_path = $appExe
        executable_sha256 = $appExeSha
        process_start_time_utc = $process.StartTime.ToUniversalTime().ToString("o")
        operation_mode = "SMOKE_CAPTURE_BOUNDED"
        config_path = $ConfigPath
        recorder_root = $RecorderRoot
        stdout_log = $stdout
        stderr_log = $stderr
        stop_request_path = Join-Path $stateRoot "aws1-stop-request.json"
        timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
    $pidState | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $pidPath -Encoding UTF8

    $process.WaitForExit()
    $process.WaitForExit(5000) | Out-Null
    $exitCode = $process.ExitCode
}
finally {
    $stdoutWriter.Dispose()
    $stderrWriter.Dispose()
    Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
    $process.Dispose()
}

$result = [ordered]@{
    status = if ($exitCode -eq 0) { "SMOKE_CAPTURE_EXITED_CLEANLY" } else { "SMOKE_CAPTURE_EXITED_NONZERO" }
    exit_code = $exitCode
    executable_path = $appExe
    executable_sha256 = $appExeSha
    config_path = $ConfigPath
    recorder_root = $RecorderRoot
    stdout_log = $stdout
    stderr_log = $stderr
    secret_values_logged = $false
    no_order_entry = $true
    timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stateRoot "aws1_last_run_result.json") -Encoding UTF8
$result | ConvertTo-Json -Depth 5
exit $exitCode
