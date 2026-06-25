param(
    [string]$InstallRoot = "C:\Anubis\M2Capture\current",
    [string]$ConfigPath = "",
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$CredentialSecretId = "",
    [string]$ArchiveBucketName = "",
    [string]$Environment = "demo",
    [string]$CloudWatchNamespace = "Anubis/AWS1",
    [switch]$NoSecretFetch
)

$ErrorActionPreference = "Stop"

$stateRoot = "C:\Anubis\State"
$logRoot = "C:\Anubis\Logs"
New-Item -ItemType Directory -Force -Path $stateRoot, $logRoot, $RecorderRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $InstallRoot "config\m2c1b_aws_capture_config.json"
}

function Read-MarketDataSecret {
    param([string]$SecretId)
    if ([string]::IsNullOrWhiteSpace($SecretId)) {
        throw "credential_secret_id_required"
    }

    if (Get-Command aws -ErrorAction SilentlyContinue) {
        $secretText = aws secretsmanager get-secret-value --secret-id $SecretId --query SecretString --output text
        if ($LASTEXITCODE -ne 0) { throw "aws_cli_secret_fetch_failed:$LASTEXITCODE" }
        return ($secretText | ConvertFrom-Json)
    }

    if (Get-Command Get-SECSecretValue -ErrorAction SilentlyContinue) {
        $secret = Get-SECSecretValue -SecretId $SecretId
        return ($secret.SecretString | ConvertFrom-Json)
    }

    throw "no_supported_secret_client"
}

$pidPath = Join-Path $stateRoot "aws1-recorder.pid"
if (Test-Path -LiteralPath $pidPath) {
    $existingPid = [int](Get-Content -Raw -LiteralPath $pidPath)
    $existing = Get-Process -Id $existingPid -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        throw "recorder_already_running:$existingPid"
    }
    Remove-Item -LiteralPath $pidPath -Force
}

$appDll = Join-Path $InstallRoot "app\QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly.dll"
if (-not (Test-Path -LiteralPath $appDll)) {
    throw "capture_host_dll_not_found:$appDll"
}
if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "capture_config_not_found:$ConfigPath"
}

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = "dotnet"
$psi.Arguments = "`"$appDll`" capture --config `"$ConfigPath`" --operator-approved-market-data-fix-logon --no-order-entry --no-account-api --no-db"
$psi.WorkingDirectory = $InstallRoot
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.Environment["ANUBIS_AWS1_RECORDER_ROOT"] = $RecorderRoot
$psi.Environment["ANUBIS_AWS1_ENVIRONMENT"] = $Environment
$psi.Environment["ANUBIS_AWS1_ARCHIVE_BUCKET"] = $ArchiveBucketName
$psi.Environment["ANUBIS_AWS1_CLOUDWATCH_NAMESPACE"] = $CloudWatchNamespace

if (-not $NoSecretFetch) {
    $secret = Read-MarketDataSecret -SecretId $CredentialSecretId
    foreach ($name in @("LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID", "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD")) {
        $value = $secret.$name
        if ([string]::IsNullOrWhiteSpace([string]$value)) {
            throw "secret_missing_required_label:$name"
        }
        $psi.Environment[$name] = [string]$value
    }
}

$stdout = Join-Path $logRoot ("aws1-recorder-" + (Get-Date -Format "yyyyMMddHHmmss") + ".out.log")
$stderr = $stdout -replace "\.out\.log$", ".err.log"
$process = [System.Diagnostics.Process]::Start($psi)
Set-Content -LiteralPath $pidPath -Value $process.Id -Encoding ASCII

Start-Job -ScriptBlock {
    param($ProcessId, $StdoutPath, $StderrPath)
    $p = [System.Diagnostics.Process]::GetProcessById($ProcessId)
    $p.StandardOutput.ReadToEnd() | Set-Content -LiteralPath $StdoutPath -Encoding UTF8
    $p.StandardError.ReadToEnd() | Set-Content -LiteralPath $StderrPath -Encoding UTF8
} -ArgumentList $process.Id, $stdout, $stderr | Out-Null

[ordered]@{
    status = "STARTED"
    pid = $process.Id
    config_path = $ConfigPath
    recorder_root = $RecorderRoot
    stdout_log = $stdout
    stderr_log = $stderr
    secret_values_logged = $false
    no_order_entry = $true
} | ConvertTo-Json -Depth 4
