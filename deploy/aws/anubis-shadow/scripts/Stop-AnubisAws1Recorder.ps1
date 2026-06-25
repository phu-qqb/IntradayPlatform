param(
    [int]$GraceSeconds = 320,
    [switch]$ForceAfterTimeout
)

$ErrorActionPreference = "Stop"
$stateRoot = "C:\Anubis\State"
$pidPath = Join-Path $stateRoot "aws1-recorder.pid.json"
$stopRequestPath = Join-Path $stateRoot "aws1-stop-request.json"

function Read-PidState {
    if (-not (Test-Path -LiteralPath $pidPath)) { return $null }
    try { return (Get-Content -Raw -LiteralPath $pidPath | ConvertFrom-Json) }
    catch { return $null }
}

function Get-VerifiedProcess {
    param([object]$State)
    if ($null -eq $State -or $null -eq $State.pid) { return $null }
    $p = Get-Process -Id ([int]$State.pid) -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $null }
    if ([string]$State.executable_path -and -not [string]::Equals($p.Path, [string]$State.executable_path, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "pid_reused_executable_mismatch:$($State.pid)"
    }
    if ([string]$State.process_start_time_utc) {
        $expected = [DateTimeOffset]::Parse([string]$State.process_start_time_utc).UtcDateTime
        $actual = $p.StartTime.ToUniversalTime()
        if ([math]::Abs(($actual - $expected).TotalSeconds) -gt 2) { throw "pid_reused_start_time_mismatch:$($State.pid)" }
    }
    return $p
}

$state = Read-PidState
$process = Get-VerifiedProcess -State $state
if ($null -eq $process) {
    Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
    @{ status = "NOT_RUNNING"; reason = "verified_process_missing" } | ConvertTo-Json
    exit 0
}

New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
[ordered]@{
    status = "STOP_REQUESTED"
    requested_utc = (Get-Date).ToUniversalTime().ToString("o")
    pid = $process.Id
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    cooperative_mechanism = "bounded_capture_natural_exit"
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $stopRequestPath -Encoding UTF8

$exited = $process.WaitForExit($GraceSeconds * 1000)
$forced = $false
if (-not $exited) {
    if (-not $ForceAfterTimeout) {
        @{ status = "STOP_TIMEOUT_NO_FORCE"; pid = $process.Id; grace_seconds = $GraceSeconds; final_manifest_preserved = $true } | ConvertTo-Json
        exit 2
    }
    Stop-Process -Id $process.Id -Force
    $forced = $true
}

Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
@{
    status = "STOPPED"
    pid = $process.Id
    forced = $forced
    final_manifest_preserved = $true
} | ConvertTo-Json
