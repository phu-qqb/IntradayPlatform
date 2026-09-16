[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CaptureRunRoot,
    [Parameter(Mandatory = $true)]
    [DateTimeOffset]$CutoffUtc,
    [Parameter(Mandatory = $true)]
    [string]$CycleId,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-AtomicUtf8([string]$Path, [string]$Content) {
    $temporaryPath = $Path + '.tmp'
    [System.IO.File]::WriteAllText($temporaryPath, $Content, (New-Object System.Text.UTF8Encoding($false)))
    if (Test-Path -LiteralPath $Path) {
        $backupPath = $Path + '.replace-backup'
        if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
        [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
        if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
    }
    else {
        [System.IO.File]::Move($temporaryPath, $Path)
    }
}

if ($CutoffUtc.Offset -ne [TimeSpan]::Zero) { throw 'LMAX_DEMO_CUTOFF_UTC_REQUIRED' }
if ([string]::IsNullOrWhiteSpace($CycleId)) { throw 'LMAX_DEMO_CYCLE_ID_REQUIRED' }

$captureRoot = [System.IO.Path]::GetFullPath($CaptureRunRoot)
if (-not (Test-Path -LiteralPath $captureRoot)) { throw 'LMAX_DEMO_CAPTURE_ROOT_NOT_FOUND' }
$finalManifests = @(Get-ChildItem -LiteralPath $captureRoot -Recurse -Filter 'final_manifest.json' -File)
if ($finalManifests.Count -ne 1) { throw 'LMAX_DEMO_CAPTURE_FINAL_MANIFEST_AMBIGUOUS' }
$finalManifestPath = $finalManifests[0].FullName
$finalManifest = Get-Content -LiteralPath $finalManifestPath -Raw | ConvertFrom-Json
if (-not $finalManifest.finalized -or $finalManifest.environment -ne 'DEMO' -or $finalManifest.writer_state -ne 'OK') {
    throw 'LMAX_DEMO_CAPTURE_FINALIZATION_INVALID'
}
if ([string]::IsNullOrWhiteSpace($finalManifest.recorder_run_id) -or [string]::IsNullOrWhiteSpace($finalManifest.run_manifest_sha256)) {
    throw 'LMAX_DEMO_CAPTURE_LINEAGE_INVALID'
}

$declaredSymbols = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$latestBySymbol = @{}
$finalManifestDirectory = Split-Path -Parent $finalManifestPath
foreach ($chunk in @($finalManifest.chunks)) {
    $chunkRelativePath = [string]$chunk.file
    if ([string]::IsNullOrWhiteSpace($chunkRelativePath) -or [System.IO.Path]::IsPathRooted($chunkRelativePath)) {
        throw 'LMAX_DEMO_CAPTURE_CHUNK_PATH_INVALID'
    }
    $chunkPath = Join-Path $finalManifestDirectory $chunkRelativePath
    if (-not (Test-Path -LiteralPath $chunkPath)) { throw 'LMAX_DEMO_CAPTURE_CHUNK_MISSING' }
    if ((Get-Sha256 $chunkPath) -ne ([string]$chunk.sha256).ToLowerInvariant()) { throw 'LMAX_DEMO_CAPTURE_CHUNK_HASH_MISMATCH' }

    foreach ($line in Get-Content -LiteralPath $chunkPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $event = $line | ConvertFrom-Json
        if ($event.event_type -eq 'MARKET_DATA_SUBSCRIPTION_STATE' -and -not [string]::IsNullOrWhiteSpace($event.symbol)) {
            [void]$declaredSymbols.Add([string]$event.symbol)
            continue
        }
        if ($event.event_type -ne 'BBO_UPDATED' -or
            $event.environment -ne 'DEMO' -or
            $event.source_component -ne 'LMAX_MARKET_DATA_CAPTURE_ONLY' -or
            $event.venue -ne 'LMAX_DEMO_READ_ONLY' -or
            $event.book_valid -ne $true -or
            [string]::IsNullOrWhiteSpace($event.symbol)) { continue }

        $sourceTimestamp = [DateTimeOffset]::Parse([string]$event.source_timestamp_utc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal)
        if ($sourceTimestamp.Offset -ne [TimeSpan]::Zero -or $sourceTimestamp -gt $CutoffUtc -or
            ($CutoffUtc - $sourceTimestamp) -gt [TimeSpan]::FromSeconds(300)) { continue }
        $bid = [decimal]$event.bid_price
        $ask = [decimal]$event.ask_price
        if ($bid -le 0 -or $ask -le $bid) { continue }

        $symbol = [string]$event.symbol
        $current = $latestBySymbol[$symbol]
        if ($null -eq $current -or $sourceTimestamp -gt $current.SourceTimestampUtc -or
            ($sourceTimestamp -eq $current.SourceTimestampUtc -and [long]$event.process_event_sequence -gt $current.Sequence)) {
            $latestBySymbol[$symbol] = [pscustomobject]@{
                SourceTimestampUtc = $sourceTimestamp
                Sequence = [long]$event.process_event_sequence
                EventId = [string]$event.event_id
                Bid = $bid
                Ask = $ask
            }
        }
    }
}

if ($declaredSymbols.Count -eq 0) { throw 'LMAX_DEMO_CAPTURE_DECLARED_SYMBOLS_MISSING' }
$missingSymbols = @($declaredSymbols | Where-Object { -not $latestBySymbol.ContainsKey($_) } | Sort-Object)
if ($missingSymbols.Count -gt 0) { throw ('LMAX_DEMO_CAPTURE_BBO_MISSING_OR_STALE:' + ($missingSymbols -join ',')) }

$quotes = @($declaredSymbols | Sort-Object | ForEach-Object {
    $selected = $latestBySymbol[$_]
    [ordered]@{
        symbol = $_
        price = ($selected.Bid + $selected.Ask) / 2
        bid_price = $selected.Bid
        ask_price = $selected.Ask
        # Preserve the canonical recorder's round-trip representation (+00:00)
        # so lineage serialization does not alter an otherwise identical BBO.
        source_timestamp_utc = $selected.SourceTimestampUtc.ToString('o')
        event_id = $selected.EventId
    }
})

$cutoffText = $CutoffUtc.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
$finalManifestSha256 = Get-Sha256 $finalManifestPath
$qualityReportPath = Join-Path $finalManifestDirectory 'health\\data_quality_report.json'
if (-not (Test-Path -LiteralPath $qualityReportPath)) { throw 'LMAX_DEMO_CAPTURE_QUALITY_REPORT_MISSING' }
$input = [ordered]@{
    contract_version = 'lmax_bbo_boundary_input_v1'
    boundary_utc = $cutoffText
    selection_rule = 'latest BBO_UPDATED per recorder-declared LMAX Demo symbol at or before boundary, within 300 seconds'
    capture = [ordered]@{
        cycle_id = $CycleId
        final_manifest_path = $finalManifestPath
        run_manifest_sha256 = ([string]$finalManifest.run_manifest_sha256).ToLowerInvariant()
        finalized = [bool]$finalManifest.finalized
        writer_state = [string]$finalManifest.writer_state
    }
    quotes = $quotes
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$inputPath = Join-Path $OutputDirectory 'lmax-bbo-boundary.json'
Write-AtomicUtf8 $inputPath ($input | ConvertTo-Json -Depth 8)
$evidence = [ordered]@{
    schema = 'lmax_bbo_boundary_evidence_v1'
    normalized_input_sha256 = Get-Sha256 $inputPath
    source_capture = [ordered]@{
        recorder_run_id = [string]$finalManifest.recorder_run_id
        started_at_utc = [string]$finalManifest.start_utc
        ended_at_utc = [string]$finalManifest.end_utc
        sha256 = ([string]$finalManifest.run_manifest_sha256).ToLowerInvariant()
        final_manifest_sha256 = $finalManifestSha256
        data_quality_report_sha256 = Get-Sha256 $qualityReportPath
        environment = [string]$finalManifest.environment
        mode = [string]$finalManifest.mode
        event_counts = $finalManifest.event_counts
    }
}
$evidencePath = Join-Path $OutputDirectory 'lmax-bbo-boundary-evidence.json'
Write-AtomicUtf8 $evidencePath ($evidence | ConvertTo-Json -Depth 8)

[ordered]@{
    marker = 'LMAX_DEMO_BOUNDARY_NORMALIZED'
    cycle_id = $CycleId
    cutoff_utc = $cutoffText
    declared_symbol_count = $declaredSymbols.Count
    normalized_input_sha256 = $evidence.normalized_input_sha256
    evidence_sha256 = Get-Sha256 $evidencePath
    final_manifest_sha256 = $finalManifestSha256
    input_path = $inputPath
    evidence_path = $evidencePath
} | ConvertTo-Json -Compress
