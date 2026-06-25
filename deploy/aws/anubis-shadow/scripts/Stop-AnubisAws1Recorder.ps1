param(
    [int]$GraceSeconds = 20
)

$ErrorActionPreference = "Stop"
$pidPath = "C:\Anubis\State\aws1-recorder.pid"

if (-not (Test-Path -LiteralPath $pidPath)) {
    @{ status = "NOT_RUNNING"; reason = "pid_file_missing" } | ConvertTo-Json
    exit 0
}

$pidValue = [int](Get-Content -Raw -LiteralPath $pidPath)
$process = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
if ($null -eq $process) {
    Remove-Item -LiteralPath $pidPath -Force
    @{ status = "NOT_RUNNING"; reason = "process_missing"; pid = $pidValue } | ConvertTo-Json
    exit 0
}

$process.CloseMainWindow() | Out-Null
$exited = $process.WaitForExit($GraceSeconds * 1000)
if (-not $exited) {
    Stop-Process -Id $pidValue -Force
}
Remove-Item -LiteralPath $pidPath -Force

@{
    status = "STOPPED"
    pid = $pidValue
    forced = -not $exited
} | ConvertTo-Json
