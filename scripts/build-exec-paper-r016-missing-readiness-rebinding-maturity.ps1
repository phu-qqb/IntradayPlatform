param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$IncomingDir = "data/offline-quotes/polygon/incoming"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 80) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $value | ConvertTo-Json -Depth $depth | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-Text([string]$path, [string]$value) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    Set-Content -LiteralPath $path -Value $value -Encoding UTF8
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

function New-Audit([string]$name, [string]$key, [string]$detail) {
    Write-Json (Join-Path $ArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-PAPER-R016"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

function Convert-ToSafeTimestamp([string]$utc) {
    return ([DateTimeOffset]::Parse($utc).ToUniversalTime()).UtcDateTime.ToString("yyyyMMddHHmmss")
}

function Get-CleanSymbol([string]$providerSymbol) {
    return $providerSymbol.Replace("C:", "").Replace("-", "").ToUpperInvariant()
}

function Get-ProviderSymbol([string]$symbol) {
    switch ($symbol) {
        "EURUSD" { "C:EUR-USD" }
        "USDJPY" { "C:USD-JPY" }
        "AUDUSD" { "C:AUD-USD" }
        "GBPUSD" { "C:GBP-USD" }
        "NZDUSD" { "C:NZD-USD" }
        "USDCAD" { "C:USD-CAD" }
        "USDCHF" { "C:USD-CHF" }
        default { "C:$($symbol.Substring(0,3))-$($symbol.Substring(3,3))" }
    }
}

function Get-NormalizedPortfolioSymbol([string]$executionSymbol) {
    switch ($executionSymbol) {
        "USDJPY" { "JPYUSD" }
        "USDCAD" { "CADUSD" }
        "USDCHF" { "CHFUSD" }
        default { $executionSymbol }
    }
}

function Get-RequiresInversion([string]$executionSymbol) {
    return @("USDJPY", "USDCAD", "USDCHF") -contains $executionSymbol
}

function New-WindowAccumulator($window) {
    return [pscustomobject]@{
        Window = $window
        QuoteCount = 0
        QuoteCountLastMinute = 0
        Timestamps = [System.Collections.Generic.List[datetimeoffset]]::new()
        SpreadsBps = [System.Collections.Generic.List[double]]::new()
        LastQuoteTimestampUtc = $null
        LastBid = $null
        LastAsk = $null
        LastMid = $null
    }
}

function Get-Percentile([double[]]$values, [double]$p) {
    if ($values.Count -eq 0) { return $null }
    $sorted = @($values | Sort-Object)
    $index = [int][math]::Floor(($sorted.Count - 1) * $p)
    return [double]$sorted[$index]
}

function Get-MaxGapSeconds([System.Collections.Generic.List[datetimeoffset]]$timestamps) {
    if ($timestamps.Count -lt 2) { return 0 }
    $ordered = @($timestamps | Sort-Object)
    $max = 0.0
    for ($i = 1; $i -lt $ordered.Count; $i++) {
        $gap = ($ordered[$i] - $ordered[$i - 1]).TotalSeconds
        if ($gap -gt $max) { $max = $gap }
    }
    return [math]::Round($max, 3)
}

function Read-QuoteFileForWindows($entry, [object[]]$windows) {
    $rowCount = 0
    $invalidTimestamp = 0
    $invalidSymbol = 0
    $invalidBidAsk = 0
    $askLessThanBid = 0
    $rawPayloadSerializedRows = 0
    $duplicateTimestampRows = 0
    $exactDuplicateRows = 0
    $outOfOrderRows = 0
    $lastTimestamp = $null
    $seenTimestamp = @{}
    $seenExact = @{}
    $windowAccumulators = @{}
    foreach ($window in $windows) {
        $windowAccumulators[[string]$window.WindowId] = New-WindowAccumulator $window
    }
    $feedSpreads = [System.Collections.Generic.List[double]]::new()
    $timestampRegex = [regex]'"timestampUtc":"([^"]+)"'
    $providerRegex = [regex]'"providerSymbol":"([^"]+)"'
    $symbolRegex = [regex]'"executionTradableSymbol":"([^"]+)"'
    $bidRegex = [regex]'"bid":([0-9.Ee+-]+)'
    $askRegex = [regex]'"ask":([0-9.Ee+-]+)'
    $rawRegex = [regex]'"rawPayloadSerialized":(true|false)'

    $reader = [System.IO.StreamReader]::new([string]$entry.QuoteFilePath)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $rowCount++
            $timestampMatch = $timestampRegex.Match($line)
            $providerMatch = $providerRegex.Match($line)
            $symbolMatch = $symbolRegex.Match($line)
            $bidMatch = $bidRegex.Match($line)
            $askMatch = $askRegex.Match($line)
            $rawMatch = $rawRegex.Match($line)

            if (-not $timestampMatch.Success) { $invalidTimestamp++; continue }
            try {
                $timestamp = [DateTimeOffset]::Parse($timestampMatch.Groups[1].Value).ToUniversalTime()
            } catch {
                $invalidTimestamp++
                continue
            }

            if ($null -ne $lastTimestamp -and $timestamp -lt $lastTimestamp) { $outOfOrderRows++ }
            $lastTimestamp = $timestamp

            $timestampKey = $timestamp.UtcDateTime.ToString("o")
            if ($seenTimestamp.ContainsKey($timestampKey)) { $duplicateTimestampRows++ } else { $seenTimestamp[$timestampKey] = $true }

            $providerSymbol = if ($providerMatch.Success) { $providerMatch.Groups[1].Value } else { "" }
            $executionSymbol = if ($symbolMatch.Success) { $symbolMatch.Groups[1].Value } else { "" }
            if ($providerSymbol -ne [string]$entry.ProviderSymbol -or $executionSymbol -ne [string]$entry.Symbol) { $invalidSymbol++ }

            $bid = 0.0
            $ask = 0.0
            if (-not $bidMatch.Success -or -not $askMatch.Success -or
                -not [double]::TryParse($bidMatch.Groups[1].Value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$bid) -or
                -not [double]::TryParse($askMatch.Groups[1].Value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$ask) -or
                [double]::IsNaN($bid) -or [double]::IsNaN($ask) -or
                [double]::IsInfinity($bid) -or [double]::IsInfinity($ask) -or
                $bid -le 0 -or $ask -le 0) {
                $invalidBidAsk++
                continue
            }
            if ($ask -lt $bid) { $askLessThanBid++; continue }
            if ($rawMatch.Success -and $rawMatch.Groups[1].Value -eq "true") { $rawPayloadSerializedRows++ }

            $exactKey = "$timestampKey|$bid|$ask"
            if ($seenExact.ContainsKey($exactKey)) { $exactDuplicateRows++ } else { $seenExact[$exactKey] = $true }

            $mid = ($bid + $ask) / 2.0
            $spread = $ask - $bid
            $spreadBps = if ($mid -gt 0) { ($spread / $mid) * 10000.0 } else { 0.0 }
            $feedSpreads.Add($spreadBps)

            foreach ($window in $windows) {
                if ($timestamp -ge $window.WindowStart -and $timestamp -le $window.TargetClose) {
                    $acc = $windowAccumulators[[string]$window.WindowId]
                    $acc.QuoteCount++
                    if ($timestamp -ge $window.TargetClose.AddMinutes(-1)) { $acc.QuoteCountLastMinute++ }
                    $acc.Timestamps.Add($timestamp)
                    $acc.SpreadsBps.Add($spreadBps)
                    if ($null -eq $acc.LastQuoteTimestampUtc -or $timestamp -le $window.TargetClose) {
                        if ($null -eq $acc.LastQuoteTimestampUtc -or $timestamp -ge $acc.LastQuoteTimestampUtc) {
                            $acc.LastQuoteTimestampUtc = $timestamp
                            $acc.LastBid = $bid
                            $acc.LastAsk = $ask
                            $acc.LastMid = $mid
                        }
                    }
                }
            }
        }
    } finally {
        $reader.Dispose()
    }

    $criticalInvalid = $invalidTimestamp + $invalidSymbol + $invalidBidAsk + $askLessThanBid + $rawPayloadSerializedRows
    return [pscustomobject]@{
        Entry = $entry
        ObservedRowCount = $rowCount
        InvalidTimestampRows = $invalidTimestamp
        InvalidProviderSymbolRows = $invalidSymbol
        InvalidBidAskRows = $invalidBidAsk
        AskLessThanBidRows = $askLessThanBid
        RawPayloadSerializedRows = $rawPayloadSerializedRows
        DuplicateTimestampRows = $duplicateTimestampRows
        ExactDuplicateRows = $exactDuplicateRows
        OutOfOrderRows = $outOfOrderRows
        CriticalInvalidRows = $criticalInvalid
        FeedSpreadsBps = $feedSpreads
        Windows = $windowAccumulators
    }
}

$phase = "EXEC-PAPER-R016"
$incomingFull = (Resolve-Path -LiteralPath $IncomingDir -ErrorAction SilentlyContinue)
$downloadPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-offline-quote-download-plan.json")
$missingWindowsPayload = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-readiness-window-requirements.json")
$r015Rebound = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-rebound-line-results.json")
$r015StillHeld = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-still-held-line-diagnostics.json")
$r014Preview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-design-only-preview-lines.json")

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-r015-missing-readiness-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R015"
    PriorHeldLineCount = 420
    R015ReboundLineCount = [int]$r015Rebound.ReboundLineCount
    R015StillHeldLineCount = [int]$r015StillHeld.StillHeldLineCount
    MissingWindowCount = [int]$missingWindowsPayload.MissingWindowCount
    DownloadTemplateCount = [int]$downloadPlan.CommandTemplateCount
    CommandsExecutedInR015 = [int]$downloadPlan.CommandsExecutedInR015
    FilesDownloadedInR015 = [int]$downloadPlan.FilesDownloadedInR015
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-r014-preview-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R014"
    PreviewLineCount = [int]$r014Preview.PreviewLineCount
    ExpectedMaximumPreviewLineCount = [int]$r014Preview.ExpectedMaximumPreviewLineCount
    ReusedExistingPreviewLinesOnly = $true
    ManualNoExternalRunNow = $false
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-file-intake-contract.json") ([pscustomobject]@{
    Phase = $phase
    IncomingDirectory = if ($incomingFull) { $incomingFull.Path } else { (Join-Path (Get-Location) $IncomingDir) }
    ReadLocalFilesOnly = $true
    ExternalApiAllowed = $false
    DownloadsAllowed = $false
    RequiredProvider = "PolygonOfflineFile"
    RequiredDataset = "HistoricalBboQuotes"
    RequiredFormat = "NDJSON"
    RequiresManifest = $true
    RequiresSha256Match = $true
    RequiresContainsSecretsFalse = $true
    RequiresContainsRawProviderPayloadFalse = $true
})

$windowsByCommand = @{}
foreach ($window in (As-Array $missingWindowsPayload.Windows)) {
    $key = "$($window.Symbol)|$($window.LocalSessionDate)"
    if (-not $windowsByCommand.ContainsKey($key)) { $windowsByCommand[$key] = [System.Collections.Generic.List[object]]::new() }
    $windowsByCommand[$key].Add($window)
}

$expectedEntries = foreach ($command in (As-Array $downloadPlan.Commands)) {
    $symbol = [string]$command.Symbol
    $providerSymbol = [string]$command.ProviderSymbol
    $from = [string]$command.FromUtc
    $to = [string]$command.ToUtc
    $safeRange = "$(Convert-ToSafeTimestamp $from)-$(Convert-ToSafeTimestamp $to)"
    $clean = (Get-CleanSymbol $providerSymbol).ToLowerInvariant()
    $quotePath = Join-Path $IncomingDir "$clean-$safeRange.ndjson"
    $manifestPath = Join-Path $IncomingDir "$clean-$safeRange.manifest.json"
    $key = "$symbol|$($command.LocalSessionDate)"
    [pscustomobject]@{
        ExpectedFileEntryId = "R016_EXPECTED_$($symbol)_$safeRange"
        Symbol = $symbol
        ProviderSymbol = $providerSymbol
        LocalSessionDate = [string]$command.LocalSessionDate
        FromUtc = $from
        ToUtc = $to
        QuoteFilePath = $quotePath
        ManifestPath = $manifestPath
        QuoteFileExists = Test-Path -LiteralPath $quotePath
        ManifestExists = Test-Path -LiteralPath $manifestPath
        TargetCloseCount = if ($windowsByCommand.ContainsKey($key)) { $windowsByCommand[$key].Count } else { 0 }
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-expected-file-entries.json") ([pscustomobject]@{
    Phase = $phase
    ExpectedFileEntryCount = @($expectedEntries).Count
    Entries = $expectedEntries
})

$missingFiles = @($expectedEntries | Where-Object { -not $_.QuoteFileExists -or -not $_.ManifestExists } | ForEach-Object {
    [pscustomobject]@{
        ExpectedFileEntryId = $_.ExpectedFileEntryId
        Symbol = $_.Symbol
        LocalSessionDate = $_.LocalSessionDate
        QuoteFilePath = $_.QuoteFilePath
        ManifestPath = $_.ManifestPath
        QuoteFileExists = $_.QuoteFileExists
        ManifestExists = $_.ManifestExists
        MissingReason = @(
            if (-not $_.QuoteFileExists) { "QuoteFileMissing" }
            if (-not $_.ManifestExists) { "ManifestFileMissing" }
        ) -join ";"
    }
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-missing-file-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    MissingFileEntryCount = $missingFiles.Count
    MissingFiles = $missingFiles
})

$manifestResults = @()
$candidateEntries = @()
foreach ($entry in $expectedEntries) {
    if (-not $entry.QuoteFileExists -or -not $entry.ManifestExists) { continue }
    $manifest = Read-Json ([string]$entry.ManifestPath)
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath ([string]$entry.QuoteFilePath)).Hash.ToUpperInvariant()
    $quoteFile = Get-Item -LiteralPath ([string]$entry.QuoteFilePath)
    $valid = $manifest.ProviderName -eq "PolygonOfflineFile" -and
        $manifest.ProviderDatasetType -eq "HistoricalBboQuotes" -and
        $manifest.FileFormat -eq "NDJSON" -and
        $manifest.ProviderSymbol -eq $entry.ProviderSymbol -and
        $manifest.ExecutionTradableSymbol -eq $entry.Symbol -and
        $manifest.TimeRangeStartUtc -eq $entry.FromUtc -and
        $manifest.TimeRangeEndUtc -eq $entry.ToUtc -and
        -not [bool]$manifest.ContainsSecrets -and
        -not [bool]$manifest.ContainsRawProviderPayload -and
        $hash -eq ([string]$manifest.FileHash).ToUpperInvariant() -and
        [int64]$quoteFile.Length -eq [int64]$manifest.FileSizeBytes -and
        $null -ne $manifest.RowCountDeclared
    $result = [pscustomobject]@{
        ExpectedFileEntryId = $entry.ExpectedFileEntryId
        Symbol = $entry.Symbol
        ProviderSymbol = $entry.ProviderSymbol
        LocalSessionDate = $entry.LocalSessionDate
        QuoteFilePath = $entry.QuoteFilePath
        ManifestPath = $entry.ManifestPath
        Provider = $manifest.ProviderName
        Dataset = $manifest.ProviderDatasetType
        Format = $manifest.FileFormat
        TimeRangeStartUtc = $manifest.TimeRangeStartUtc
        TimeRangeEndUtc = $manifest.TimeRangeEndUtc
        ContainsSecrets = [bool]$manifest.ContainsSecrets
        ContainsRawProviderPayload = [bool]$manifest.ContainsRawProviderPayload
        RowCountDeclared = [int]$manifest.RowCountDeclared
        ManifestSha256 = ([string]$manifest.FileHash).ToUpperInvariant()
        ComputedSha256 = $hash
        Sha256Matches = $hash -eq ([string]$manifest.FileHash).ToUpperInvariant()
        FileSizeMatches = [int64]$quoteFile.Length -eq [int64]$manifest.FileSizeBytes
        ManifestAccepted = $valid
        QuarantineReason = if ($valid) { $null } else { "ManifestFileValidationFailed" }
    }
    $manifestResults += $result
    if ($valid) { $candidateEntries += ($entry | Add-Member -NotePropertyName Manifest -NotePropertyValue $manifest -PassThru) }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-manifest-validation-results.json") ([pscustomobject]@{
    Phase = $phase
    ManifestValidatedEntryCount = $manifestResults.Count
    AcceptedManifestCount = @($manifestResults | Where-Object ManifestAccepted).Count
    QuarantinedManifestCount = @($manifestResults | Where-Object { -not $_.ManifestAccepted }).Count
    Results = $manifestResults
})

$acceptedFileEntries = @()
$rowValidationResults = @()
$rowComparisons = @()
$duplicateHandling = @()
$quoteWindowResults = @()
$closeBenchmarkResults = @()
$feedQualityResults = @()
$r016QuoteIndex = @{}
$r016CloseIndex = @{}
$r016FeedIndex = @{}

foreach ($entry in $candidateEntries) {
    $key = "$($entry.Symbol)|$($entry.LocalSessionDate)"
    $windowObjects = @()
    if ($windowsByCommand.ContainsKey($key)) {
        foreach ($window in $windowsByCommand[$key]) {
            $target = [DateTimeOffset]::Parse([string]$window.TargetCloseUtc).ToUniversalTime()
            $start = [DateTimeOffset]::Parse([string]$window.WindowStartUtc).ToUniversalTime()
            $windowObjects += [pscustomobject]@{
                WindowId = "R016_QW_$($entry.Symbol)_$($target.UtcDateTime.ToString("yyyyMMdd_HHmmss"))"
                Symbol = $entry.Symbol
                ProviderSymbol = $entry.ProviderSymbol
                LocalSessionDate = $entry.LocalSessionDate
                TargetCloseUtc = $target.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                TargetClose = $target
                WindowStartUtc = $start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                WindowStart = $start
                BarRole = $window.BarRole
            }
        }
    }
    $scan = Read-QuoteFileForWindows $entry $windowObjects
    $rowCountMatches = [int]$scan.ObservedRowCount -eq [int]$entry.Manifest.RowCountDeclared
    $rowValid = $rowCountMatches -and $scan.CriticalInvalidRows -eq 0
    $rowValidationResults += [pscustomobject]@{
        ExpectedFileEntryId = $entry.ExpectedFileEntryId
        Symbol = $entry.Symbol
        ProviderSymbol = $entry.ProviderSymbol
        LocalSessionDate = $entry.LocalSessionDate
        QuoteFilePath = $entry.QuoteFilePath
        ObservedRowCount = $scan.ObservedRowCount
        DeclaredRowCount = [int]$entry.Manifest.RowCountDeclared
        RowCountMatchesManifest = $rowCountMatches
        InvalidTimestampRows = $scan.InvalidTimestampRows
        InvalidProviderSymbolRows = $scan.InvalidProviderSymbolRows
        InvalidBidAskRows = $scan.InvalidBidAskRows
        AskLessThanBidRows = $scan.AskLessThanBidRows
        RawPayloadSerializedRows = $scan.RawPayloadSerializedRows
        RowValidationAcceptedForReadiness = $rowValid
        QuarantineReason = if ($rowValid) { $null } else { "RowValidationFailed" }
    }
    $rowComparisons += [pscustomobject]@{
        ExpectedFileEntryId = $entry.ExpectedFileEntryId
        Symbol = $entry.Symbol
        LocalSessionDate = $entry.LocalSessionDate
        DeclaredRowCount = [int]$entry.Manifest.RowCountDeclared
        ObservedRowCount = $scan.ObservedRowCount
        Difference = $scan.ObservedRowCount - [int]$entry.Manifest.RowCountDeclared
        Matches = $rowCountMatches
    }
    $duplicateHandling += [pscustomobject]@{
        ExpectedFileEntryId = $entry.ExpectedFileEntryId
        Symbol = $entry.Symbol
        LocalSessionDate = $entry.LocalSessionDate
        DuplicateTimestampRows = $scan.DuplicateTimestampRows
        ExactDuplicateRows = $scan.ExactDuplicateRows
        OutOfOrderRows = $scan.OutOfOrderRows
        DeterministicHandling = "Duplicates counted; last valid quote at or before close is selected after timestamp ordering."
    }
    if ($rowValid) {
        $acceptedFileEntries += [pscustomobject]@{
            ExpectedFileEntryId = $entry.ExpectedFileEntryId
            Symbol = $entry.Symbol
            ProviderSymbol = $entry.ProviderSymbol
            LocalSessionDate = $entry.LocalSessionDate
            QuoteFilePath = $entry.QuoteFilePath
            ManifestPath = $entry.ManifestPath
            RowCount = $scan.ObservedRowCount
            AcceptedForReadinessGeneration = $true
        }
        $feedSpreads = @($scan.FeedSpreadsBps)
        $feedReady = $scan.ObservedRowCount -gt 0 -and $scan.CriticalInvalidRows -eq 0
        $feedId = "R016_FQ_$($entry.Symbol)_$($entry.LocalSessionDate.Replace('-', ''))"
        $feed = [pscustomobject]@{
            FeedQualityId = $feedId
            Symbol = $entry.Symbol
            ProviderSymbol = $entry.ProviderSymbol
            LocalSessionDate = $entry.LocalSessionDate
            QuoteFilePath = $entry.QuoteFilePath
            AcceptedRows = $scan.ObservedRowCount
            RejectedRows = $scan.CriticalInvalidRows
            DuplicateTimestampRows = $scan.DuplicateTimestampRows
            ExactDuplicateRows = $scan.ExactDuplicateRows
            OutOfOrderRows = $scan.OutOfOrderRows
            MedianSpreadBps = Get-Percentile ([double[]]$feedSpreads) 0.5
            P95SpreadBps = Get-Percentile ([double[]]$feedSpreads) 0.95
            MaxSpreadBps = if ($feedSpreads.Count -gt 0) { [double](@($feedSpreads | Measure-Object -Maximum).Maximum) } else { $null }
            FeedQualityStatus = if ($feedReady) { "Ready" } else { "NotReady" }
        }
        $feedQualityResults += $feed
        if ($feedReady) { $r016FeedIndex["$($entry.Symbol)|$($entry.LocalSessionDate)"] = $feed }

        foreach ($windowId in $scan.Windows.Keys) {
            $acc = $scan.Windows[$windowId]
            $window = $acc.Window
            $lastAge = if ($null -ne $acc.LastQuoteTimestampUtc) { [math]::Round(($window.TargetClose - $acc.LastQuoteTimestampUtc).TotalSeconds, 3) } else { $null }
            $quoteReady = $acc.QuoteCount -gt 0 -and $null -ne $acc.LastQuoteTimestampUtc -and $lastAge -le 60
            $quoteResult = [pscustomobject]@{
                QuoteWindowId = $window.WindowId
                Symbol = $window.Symbol
                ProviderSymbol = $window.ProviderSymbol
                LocalSessionDate = $window.LocalSessionDate
                TargetCloseTimestampUtc = $window.TargetCloseUtc
                WindowStartUtc = $window.WindowStartUtc
                BarRole = $window.BarRole
                QuoteCount = $acc.QuoteCount
                QuoteCountLastMinute = $acc.QuoteCountLastMinute
                MaxQuoteGapSeconds = Get-MaxGapSeconds $acc.Timestamps
                LastQuoteAgeAtCloseSeconds = $lastAge
                BidAskAvailabilityRatio = if ($acc.QuoteCount -gt 0) { 1.0 } else { 0.0 }
                MidAvailabilityRatio = if ($acc.QuoteCount -gt 0) { 1.0 } else { 0.0 }
                ReadinessStatus = if ($quoteReady) { "Ready" } else { "NotReady" }
            }
            $quoteWindowResults += $quoteResult
            if ($quoteReady) { $r016QuoteIndex["$($window.Symbol)|$($window.TargetCloseUtc)"] = $quoteResult }

            $closeReady = $quoteReady -and $null -ne $acc.LastBid -and $null -ne $acc.LastAsk
            $closeResult = [pscustomobject]@{
                CloseBenchmarkId = "R016_CB_$($window.Symbol)_$($window.TargetClose.UtcDateTime.ToString("yyyyMMdd_HHmmss"))"
                QuoteWindowId = $window.WindowId
                FeedQualityId = $feedId
                Symbol = $window.Symbol
                ProviderSymbol = $window.ProviderSymbol
                LocalSessionDate = $window.LocalSessionDate
                TargetCloseTimestampUtc = $window.TargetCloseUtc
                BarRole = $window.BarRole
                LastValidBidBeforeClose = $acc.LastBid
                LastValidAskBeforeClose = $acc.LastAsk
                LastValidMidBeforeClose = $acc.LastMid
                LastValidQuoteTimestampUtc = if ($null -ne $acc.LastQuoteTimestampUtc) { $acc.LastQuoteTimestampUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") } else { $null }
                CloseQuoteAgeSeconds = $lastAge
                CloseSpreadBps = if ($null -ne $acc.LastBid -and $null -ne $acc.LastAsk -and $acc.LastMid -gt 0) { (($acc.LastAsk - $acc.LastBid) / $acc.LastMid) * 10000.0 } else { $null }
                CloseConstructionMethod = "LastValidQuoteAtOrBeforeCanonicalClose"
                ReadinessStatus = if ($closeReady) { "Ready" } else { "NotReady" }
            }
            $closeBenchmarkResults += $closeResult
            if ($closeReady) { $r016CloseIndex["$($window.Symbol)|$($window.TargetCloseUtc)"] = $closeResult }
        }
    }
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-accepted-file-entries.json") ([pscustomobject]@{
    Phase = $phase
    AcceptedFileEntryCount = $acceptedFileEntries.Count
    Entries = $acceptedFileEntries
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-row-level-validation-results.json") ([pscustomobject]@{
    Phase = $phase
    ValidatedFileCount = $rowValidationResults.Count
    AcceptedForReadinessFileCount = @($rowValidationResults | Where-Object RowValidationAcceptedForReadiness).Count
    QuarantinedRowValidationFileCount = @($rowValidationResults | Where-Object { -not $_.RowValidationAcceptedForReadiness }).Count
    Results = $rowValidationResults
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-row-count-comparison.json") ([pscustomobject]@{
    Phase = $phase
    ComparisonCount = $rowComparisons.Count
    MismatchCount = @($rowComparisons | Where-Object { -not $_.Matches }).Count
    Comparisons = $rowComparisons
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-duplicate-out-of-order-handling.json") ([pscustomobject]@{
    Phase = $phase
    DeterministicHandling = "Duplicates and out-of-order rows are counted; close benchmark uses last valid quote at or before canonical close after timestamp comparison."
    Results = $duplicateHandling
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-quote-window-readiness-results.json") ([pscustomobject]@{
    Phase = $phase
    QuoteWindowReadinessRecords = $quoteWindowResults.Count
    ReadyRecords = @($quoteWindowResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    Results = $quoteWindowResults
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-close-benchmark-readiness-results.json") ([pscustomobject]@{
    Phase = $phase
    CloseBenchmarkReadinessRecords = $closeBenchmarkResults.Count
    ReadyRecords = @($closeBenchmarkResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    Results = $closeBenchmarkResults
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-feed-quality-readiness-results.json") ([pscustomobject]@{
    Phase = $phase
    FeedQualityReadinessRecords = $feedQualityResults.Count
    ReadyRecords = @($feedQualityResults | Where-Object { $_.FeedQualityStatus -eq "Ready" }).Count
    Results = $feedQualityResults
})

$r015ReboundIndex = @{}
foreach ($line in (As-Array $r015Rebound.ReboundLines)) {
    $r015ReboundIndex[[string]$line.PaperExecutionPlanLineId] = $line
}

$r016RebindResults = foreach ($line in (As-Array $r015StillHeld.Lines)) {
    $symbol = [string]$line.Symbol
    $utc = [string]$line.CanonicalTargetCloseUtc
    $date = [string]$line.LocalSessionDate
    $qw = $r016QuoteIndex["$symbol|$utc"]
    $cb = $r016CloseIndex["$symbol|$utc"]
    $fq = $r016FeedIndex["$symbol|$date"]
    $complete = $null -ne $qw -and $null -ne $cb -and $null -ne $fq
    [pscustomobject]@{
        BatchEntryId = $line.BatchEntryId
        PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
        Symbol = $symbol
        NormalizedPortfolioSymbol = $line.NormalizedPortfolioSymbol
        RequiresInversion = [bool]$line.RequiresInversion
        CanonicalTargetCloseUtc = $utc
        CanonicalTargetCloseLocal = $line.CanonicalTargetCloseLocal
        LocalSessionDate = $date
        BarRole = $line.BarRole
        QuoteWindowReadinessBinding = if ($null -ne $qw) { $qw.QuoteWindowId } else { $null }
        CloseBenchmarkReadinessBinding = if ($null -ne $cb) { $cb.CloseBenchmarkId } else { $null }
        FeedQualityReadinessBinding = if ($null -ne $fq) { $fq.FeedQualityId } else { $null }
        ReboundComplete = $complete
        ReadinessBindingInvented = $false
        StillHeld = -not $complete
        HoldReason = if ($complete) { $null } else {
            @(
                if ($null -eq $qw) { "MissingQuoteWindowReadinessBinding" }
                if ($null -eq $cb) { "MissingCloseBenchmarkReadinessBinding" }
                if ($null -eq $fq) { "MissingFeedQualityReadinessBinding" }
            ) -join ";"
        }
    }
}
$r016Rebound = @($r016RebindResults | Where-Object ReboundComplete)
$stillHeld = @($r016RebindResults | Where-Object StillHeld)
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-held-line-rebinding-results.json") ([pscustomobject]@{
    Phase = $phase
    R015StillHeldLineCount = [int]$r015StillHeld.StillHeldLineCount
    R016ReboundLineCount = $r016Rebound.Count
    StillHeldLineCount = $stillHeld.Count
    ReadinessBindingsInvented = $false
    Results = $r016RebindResults
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-still-held-line-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    StillHeldLineCount = $stillHeld.Count
    Lines = $stillHeld
})

$originalComplete = 0
$r015Complete = 0
$r016Complete = 0
$reaggregatedLines = foreach ($line in (As-Array $r014Preview.Lines)) {
    $id = [string]$line.PaperExecutionPlanLineId
    $source = "OriginalR014Complete"
    $ready = $true
    $hold = $null
    if (-not [string]::IsNullOrWhiteSpace([string]$line.HoldReason)) {
        if ($r015ReboundIndex.ContainsKey($id)) {
            $source = "R015ExistingReadinessRebound"
            $r015Complete++
        } else {
            $r016 = @($r016RebindResults | Where-Object { $_.PaperExecutionPlanLineId -eq $id } | Select-Object -First 1)
            if ($r016.Count -gt 0 -and $r016[0].ReboundComplete) {
                $source = "R016DownloadedReadinessRebound"
                $r016Complete++
            } else {
                $source = "StillHeldMissingReadiness"
                $ready = $false
                $hold = if ($r016.Count -gt 0) { $r016[0].HoldReason } else { "MissingReadinessBinding" }
            }
        }
    } else {
        $originalComplete++
    }
    [pscustomobject]@{
        PaperExecutionPlanLineId = $id
        BatchEntryId = $line.BatchEntryId
        Symbol = $line.ExecutionTradableSymbol
        CanonicalTargetCloseUtc = $line.CanonicalTargetCloseTimestamp
        BarRole = $line.BarRole
        ReadinessComplete = $ready
        ReadinessSource = $source
        HoldReason = $hold
        NonExecutable = [bool]$line.NonExecutable
        NotAnOrder = [bool]$line.NotAnOrder
        NotSubmitted = [bool]$line.NotSubmitted
        NoBrokerRoute = [bool]$line.NoBrokerRoute
        NoChildSlices = [bool]$line.NoChildSlices
        NoExecutableSchedule = [bool]$line.NoExecutableSchedule
        NoFill = [bool]$line.NoFill
        NoExecutionReport = [bool]$line.NoExecutionReport
        NoRoute = [bool]$line.NoRoute
        NoSubmission = [bool]$line.NoSubmission
        NoPaperLedgerCommit = [bool]$line.NoPaperLedgerCommit
    }
}
$readyCount = @($reaggregatedLines | Where-Object ReadinessComplete).Count
$stillHeldCount = @($reaggregatedLines | Where-Object { -not $_.ReadinessComplete }).Count
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-reaggregated-preview-line-status.json") ([pscustomobject]@{
    Phase = $phase
    PreviewLineCount = $reaggregatedLines.Count
    OriginalR014CompleteLineCount = $originalComplete
    R015ReboundLineCount = $r015Complete
    R016ReboundLineCount = $r016Complete
    ReadinessCompleteLineCount = $readyCount
    StillHeldLineCount = $stillHeldCount
    AllPreviewLinesReadinessComplete = $readyCount -eq 700
    Lines = $reaggregatedLines
})

$decisionStatus = if ($readyCount -eq 700) { "R009LongRunPaperOnlyMaturityReady" } elseif ($readyCount -gt 0) { "R009LongRunPaperOnlyPartialMaturityNeedsMoreReadiness" } else { "R009LongRunPaperOnlyBlockedMissingReadiness" }
$classifications = if ($readyCount -eq 700) {
    @(
        "EXEC_PAPER_R016_PASS_MISSING_READINESS_FILE_INTAKE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_MISSING_READINESS_VALIDATION_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_HELD_LINE_REBINDING_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_LONG_RUN_PREVIEW_REAGGREGATION_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_R009_LONG_RUN_MATURITY_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R016_PARTIAL_HELD_LINE_REBINDING_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_STILL_HELD_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_LONG_RUN_PREVIEW_REAGGREGATION_READY_NO_EXTERNAL",
        "EXEC_PAPER_R016_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

$review = [pscustomobject]@{
    Phase = $phase
    ExpectedFileEntries = @($expectedEntries).Count
    AcceptedFileEntries = $acceptedFileEntries.Count
    MissingFileEntries = $missingFiles.Count
    ManifestAccepted = @($manifestResults | Where-Object ManifestAccepted).Count
    RowValidationAccepted = @($rowValidationResults | Where-Object RowValidationAcceptedForReadiness).Count
    QuoteWindowReady = @($quoteWindowResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    CloseBenchmarkReady = @($closeBenchmarkResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    FeedQualityReady = @($feedQualityResults | Where-Object { $_.FeedQualityStatus -eq "Ready" }).Count
    R015ReboundLineCount = $r015Complete
    R016ReboundLineCount = $r016Complete
    ReadinessCompleteLineCount = $readyCount
    StillHeldLineCount = $stillHeldCount
    Decision = $decisionStatus
    ExecutablePromotionAuthorized = $false
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-updated-operator-review-report.json") $review
$reviewMd = @"
# EXEC-PAPER-R016 Updated Operator Review

R016 processed local operator-downloaded quote files only. It did not call Polygon, LMAX, broker, live market data, downloads, ManualNoExternal, PMS/EMS/OMS, backtests, simulations, or TCA generation.

- Expected file entries: $($review.ExpectedFileEntries)
- Accepted file entries: $($review.AcceptedFileEntries)
- Missing file entries: $($review.MissingFileEntries)
- R015 rebound lines retained: $($review.R015ReboundLineCount)
- R016 rebound lines: $($review.R016ReboundLineCount)
- Reaggregated readiness-complete preview lines: $($review.ReadinessCompleteLineCount) / 700
- Still-held lines: $($review.StillHeldLineCount)
- Decision: $($review.Decision)

R009 remains design-only, paper-only, non-executable, not an order, not submitted, and no broker route.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r016-updated-operator-review-report.md") $reviewMd
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-updated-long-run-maturity-decision.json") ([pscustomobject]@{
    Phase = $phase
    Decision = $decisionStatus
    ReadinessCompleteLineCount = $readyCount
    StillHeldLineCount = $stillHeldCount
    Classifications = $classifications
    ExecutablePromotionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = if ($readyCount -eq 700) { "EXEC-ALGO-R013 - No-External R009 Long-Run Paper Maturity Acceptance and Executable Blocker Review Gate" } else { "Generate final missing-readiness operator package or accept partial maturity with explicit blocker" }
    Reason = if ($readyCount -eq 700) { "All long-run preview lines are readiness-complete." } else { "Some expected local quote/manifest data is still missing or invalid." }
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FutureTimestampsUseCanonicalQuarterHour = $true
    Legacy06UsedAsFutureCanonical = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-legacy-compatibility-preservation.json") ([pscustomobject]@{
    Phase = $phase
    LegacyTimestampsCompatibilityOnly = $true
    Legacy06UsedAsFutureCanonical = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-usd-pair-normalization-preservation.json") ([pscustomobject]@{
    Phase = $phase
    USDPairOnlyAfterNetting = $true
    DirectCrossExecutionAllowed = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-direct-cross-exclusion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    DirectCrossesSignalOnly = $true
    DirectCrossNettingFirst = $true
    DirectCrossExecutionDisabled = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-cost-guidance-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FiveUsdPerMillion = "BestCaseMajorOnly"
    FiveUsdPerMillionUniversalized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-nonmajor-calibration-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NonmajorEmScandiCnhCalibrationRequired = $true
    NonmajorExecutionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-usdjpy-caveat-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = 4004
    SecurityIDSource = "8"
    CaveatWeakened = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-lmax-readonly-baseline-reference.json") ([pscustomobject]@{
    Phase = $phase
    LmaxUsedInThisPhase = $false
    LmaxCalledInThisPhase = $false
    ReferenceOnly = $true
})

New-Audit "phase-exec-paper-r016-no-db-import-audit.json" "NoDbImport" "No rows were imported into a database."
New-Audit "phase-exec-paper-r016-no-persisted-sanitized-row-audit.json" "NoPersistedSanitizedRows" "Sanitized quote rows were validated in-memory only."
New-Audit "phase-exec-paper-r016-no-new-pms-cycle-audit.json" "NoNewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-paper-r016-no-manualnoexternal-command-run-audit.json" "NoManualNoExternalCommandRun" "No ManualNoExternal command was run."
New-Audit "phase-exec-paper-r016-no-new-backtest-audit.json" "NoNewBacktest" "No backtest was run."
New-Audit "phase-exec-paper-r016-no-new-simulation-audit.json" "NoNewSimulation" "No simulation was run."
New-Audit "phase-exec-paper-r016-no-tca-result-lines-audit.json" "NoTcaResultLines" "No TCA result lines were created."
New-Audit "phase-exec-paper-r016-no-executable-schedule-audit.json" "NoExecutableSchedule" "No executable schedule was created."
New-Audit "phase-exec-paper-r016-no-child-slices-audit.json" "NoChildSlices" "No child slices were created."
New-Audit "phase-exec-paper-r016-no-child-orders-audit.json" "NoChildOrders" "No child orders were created."
New-Audit "phase-exec-paper-r016-no-order-created-audit.json" "NoOrderCreated" "No order was created."
New-Audit "phase-exec-paper-r016-no-real-fill-audit.json" "NoRealFill" "No fill was created."
New-Audit "phase-exec-paper-r016-no-execution-report-audit.json" "NoExecutionReport" "No execution report was created."
New-Audit "phase-exec-paper-r016-no-route-no-submission-audit.json" "NoRouteNoSubmission" "No route or submission was created."
New-Audit "phase-exec-paper-r016-no-paper-ledger-commit-audit.json" "NoPaperLedgerCommit" "No paper ledger commit was created."
New-Audit "phase-exec-paper-r016-no-polygon-api-call-audit.json" "NoPolygonApiCall" "Polygon was not called."
New-Audit "phase-exec-paper-r016-no-lmax-call-audit.json" "NoLmaxCall" "LMAX was not called."
New-Audit "phase-exec-paper-r016-no-external-api-call-audit.json" "NoExternalApiCall" "No external API was called."

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-no-external-audit.json") ([pscustomobject]@{
    Phase = $phase
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    DownloadsExecuted = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    DownloadsExecuted = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    DbImport = $false
    PersistedSanitizedRows = $false
    BacktestSimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r016-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR016Tests = "Pending"
    UnitTests = "Pending"
    R016Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-PAPER-R016 Summary

R016 processed local operator-downloaded quote files, validated manifests and rows, generated missing readiness records, rebound held lines, and re-aggregated the long-run R009 paper-only preview.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Counts:
- Expected file entries: $($expectedEntries.Count)
- Accepted file entries: $($acceptedFileEntries.Count)
- Missing file entries: $($missingFiles.Count)
- R015 rebound lines retained: $r015Complete
- R016 rebound lines: $r016Complete
- Readiness-complete preview lines: $readyCount / 700
- Still-held preview lines: $stillHeldCount

Decision: $decisionStatus
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r016-summary.md") $summary

Write-Output "EXEC-PAPER-R016 artifacts generated"
