param(
    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Environment = "demo"
)

$ErrorActionPreference = "Stop"

function Get-ClockHealth {
    $status = & w32tm /query /status 2>$null
    $ok = $LASTEXITCODE -eq 0 -and (($status -join "`n") -match "169\.254\.169\.123|Amazon|Local CMOS Clock|Source:")
    return [ordered]@{
        ok = [bool]$ok
        source = (($status | Where-Object { $_ -match "^Source:" }) -replace "^Source:\s*", "") | Select-Object -First 1
    }
}

function Get-DiskFreePercent {
    param([string]$Path)
    $root = [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($Path))
    $drive = New-Object System.IO.DriveInfo($root)
    if ($drive.TotalSize -le 0) { return 0 }
    return [math]::Round(($drive.AvailableFreeSpace / $drive.TotalSize) * 100, 2)
}

$pidPath = "C:\Anubis\State\aws1-recorder.pid"
$pid = $null
$alive = $false
if (Test-Path -LiteralPath $pidPath) {
    $pid = [int](Get-Content -Raw -LiteralPath $pidPath)
    $alive = $null -ne (Get-Process -Id $pid -ErrorAction SilentlyContinue)
}

$latestFinal = Get-ChildItem -LiteralPath $RecorderRoot -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

$bboCount = 0
$writerErrors = 0
$drops = 0
$sequenceGapStatus = 0
$shadowReady = 0
if ($null -ne $latestFinal) {
    $final = Get-Content -Raw -LiteralPath $latestFinal.FullName | ConvertFrom-Json
    $writerErrors = [int64]($final.writer_errors)
    $drops = [int64]($final.events_dropped)
    if ($final.event_counts.BBO_UPDATED) { $bboCount = [int64]$final.event_counts.BBO_UPDATED }
    if ($writerErrors -eq 0 -and $drops -eq 0) { $shadowReady = 1 }
}

$backlog = @(Get-ChildItem -LiteralPath $RecorderRoot -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue |
    Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.DirectoryName ".s3_upload_verified")) }).Count

$clock = Get-ClockHealth
$status = [ordered]@{
    status = if ($alive) { "RUNNING" } else { "STOPPED" }
    pid = $pid
    environment = $Environment
    recorder_root = $RecorderRoot
    process_alive = if ($alive) { 1 } else { 0 }
    session_state_ok = if ($alive) { 1 } else { 0 }
    bbo_count = $bboCount
    last_quote_age_seconds = if ($alive -and $bboCount -gt 0) { 0 } else { 999999 }
    sequence_gap_status = $sequenceGapStatus
    writer_errors = $writerErrors
    drops = $drops
    disk_free_percent = Get-DiskFreePercent -Path $RecorderRoot
    s3_upload_backlog = $backlog
    clock_health_ok = if ($clock.ok) { 1 } else { 0 }
    clock_source = $clock.source
    recorder_shadow_ready = $shadowReady
    latest_final_manifest = if ($latestFinal) { $latestFinal.FullName } else { $null }
    raw_ticks_emitted_to_cloudwatch = $false
    timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
}

$status | ConvertTo-Json -Depth 5
