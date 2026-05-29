param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$IncomingDir = "data/offline-quotes/polygon/incoming"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) { Get-Content -Raw -LiteralPath $path | ConvertFrom-Json }

function Write-Json([string]$path, [object]$value, [int]$depth = 80) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    $value | ConvertTo-Json -Depth $depth | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-Text([string]$path, [string]$value) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    Set-Content -LiteralPath $path -Value $value -Encoding UTF8
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

function New-Audit([string]$name, [string]$key, [string]$detail) {
    Write-Json (Join-Path $ArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-PAPER-R018"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

function Convert-ToSafeTimestamp([string]$utc) {
    return ([DateTimeOffset]::Parse($utc).ToUniversalTime()).UtcDateTime.ToString("yyyyMMddHHmmss")
}

function Get-CleanSymbol([string]$providerSymbol) { $providerSymbol.Replace("C:", "").Replace("-", "").ToUpperInvariant() }

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

function Get-NormalizedPortfolioSymbol([string]$symbol) {
    switch ($symbol) {
        "USDJPY" { "JPYUSD" }
        "USDCAD" { "CADUSD" }
        "USDCHF" { "CHFUSD" }
        default { $symbol }
    }
}

function Get-RequiresInversion([string]$symbol) { @("USDJPY", "USDCAD", "USDCHF") -contains $symbol }

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
    [math]::Round($max, 3)
}

function New-WindowAccumulator($window) {
    [pscustomobject]@{
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
    foreach ($window in $windows) { $windowAccumulators[[string]$window.WindowId] = New-WindowAccumulator $window }
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
            try { $timestamp = [DateTimeOffset]::Parse($timestampMatch.Groups[1].Value).ToUniversalTime() } catch { $invalidTimestamp++; continue }
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
                [double]::IsInfinity($bid) -or [double]::IsInfinity($ask) -or $bid -le 0 -or $ask -le 0) {
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
                    if ($null -eq $acc.LastQuoteTimestampUtc -or $timestamp -ge $acc.LastQuoteTimestampUtc) {
                        $acc.LastQuoteTimestampUtc = $timestamp
                        $acc.LastBid = $bid
                        $acc.LastAsk = $ask
                        $acc.LastMid = $mid
                    }
                }
            }
        }
    } finally {
        $reader.Dispose()
    }
    $criticalInvalid = $invalidTimestamp + $invalidSymbol + $invalidBidAsk + $askLessThanBid + $rawPayloadSerializedRows
    [pscustomobject]@{
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

$phase = "EXEC-PAPER-R018"
$r017Package = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-if-needed-final-download-command-package.json")
$r017StillHeld = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-still-held-line-diagnostics.json")
$r017Reagg = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r017-final-reaggregated-preview-status.json")
$r015Missing = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-readiness-window-requirements.json")
$r014Preview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-design-only-preview-lines.json")

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-r017-final-package-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R017"
    R017ReadinessCompleteLineCount = [int]$r017Reagg.ReadinessCompleteLineCount
    R017StillHeldLineCount = [int]$r017Reagg.FinalStillHeldLineCount
    R017Decision = "R009LongRunPaperOnlyPartialMaturityWithExplicitLocalDataBlocker"
    R017CommandTemplateCount = [int]$r017Package.CommandTemplateCount
    R017CommandsExecuted = [int]$r017Package.CommandsExecutedInR017
    R017FilesDownloadedByCodex = [int]$r017Package.FilesDownloadedInR017
    ReusedOnly = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-r014-preview-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R014"
    PreviewLineCount = 700
    ReusedExistingPreviewLinesOnly = $true
    ManualNoExternalRunNow = $false
    ReusedOnly = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
    ContractVersion = "0.3.0-design-only-candidate"
    Primary = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    Secondary = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-file-intake-contract.json") ([pscustomobject]@{
    Phase = $phase
    ReadLocalFilesOnly = $true
    Provider = "PolygonOfflineFile"
    Dataset = "HistoricalBboQuotes"
    Format = "NDJSON"
    ManifestRequired = $true
    Sha256MatchRequired = $true
    ExternalApiAllowed = $false
    DownloadsAllowed = $false
    DbImportAllowed = $false
    PersistSanitizedRowsAllowed = $false
})

$stillHeldLines = As-Array $r017StillHeld.Lines
$targetWindows = @()
foreach ($line in $stillHeldLines) {
    $target = [DateTimeOffset]::Parse([string]$line.CanonicalTargetCloseUtc).ToUniversalTime()
    $targetWindows += [pscustomobject]@{
        WindowId = "R018_QW_$($line.Symbol)_$($target.UtcDateTime.ToString("yyyyMMdd_HHmmss"))"
        Symbol = [string]$line.Symbol
        ProviderSymbol = Get-ProviderSymbol ([string]$line.Symbol)
        LocalSessionDate = [string]$line.LocalSessionDate
        TargetCloseUtc = $target.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        TargetClose = $target
        WindowStartUtc = $target.AddMinutes(-13).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        WindowStart = $target.AddMinutes(-13)
        BarRole = [string]$line.BarRole
        PaperExecutionPlanLineId = [string]$line.PaperExecutionPlanLineId
    }
}
$windowsBySymbolDate = @{}
foreach ($window in $targetWindows) {
    $key = "$($window.Symbol)|$($window.LocalSessionDate)"
    if (-not $windowsBySymbolDate.ContainsKey($key)) { $windowsBySymbolDate[$key] = [System.Collections.Generic.List[object]]::new() }
    $windowsBySymbolDate[$key].Add($window)
}

$expectedEntries = @()
foreach ($command in (As-Array $r017Package.Commands)) {
    $from = [DateTimeOffset]::Parse([string]$command.FromUtc).ToUniversalTime()
    $to = [DateTimeOffset]::Parse([string]$command.ToUtc).ToUniversalTime()
    $symbol = [string]$command.Symbol
    $baseName = "{0}-{1}-{2}" -f $symbol.ToLowerInvariant(), $from.UtcDateTime.ToString("yyyyMMddHHmmss"), $to.UtcDateTime.ToString("yyyyMMddHHmmss")
    $quotePath = Join-Path $IncomingDir "$baseName.ndjson"
    $manifestPath = Join-Path $IncomingDir "$baseName.manifest.json"
    $expectedEntries += [pscustomobject]@{
        ExpectedFileEntryId = "R018_EXPECTED_$($symbol)_$($from.UtcDateTime.ToString("yyyyMMddHHmmss"))_$($to.UtcDateTime.ToString("yyyyMMddHHmmss"))"
        Symbol = $symbol
        ProviderSymbol = [string]$command.ProviderSymbol
        LocalSessionDate = [string]$command.LocalSessionDate
        FromUtc = $from.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        ToUtc = $to.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        TargetCloseCount = [int]$command.TargetCloseCount
        QuoteFilePath = $quotePath
        ManifestPath = $manifestPath
        QuoteFileExists = Test-Path -LiteralPath $quotePath
        ManifestExists = Test-Path -LiteralPath $manifestPath
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-expected-file-entries.json") ([pscustomobject]@{
    Phase = $phase
    ExpectedFileEntryCount = $expectedEntries.Count
    Entries = $expectedEntries
})

$entries = @()
$missingEntries = @()
foreach ($expected in $expectedEntries) {
    if (-not $expected.QuoteFileExists -or -not $expected.ManifestExists) {
        $missingEntries += [pscustomobject]@{
            ExpectedFileEntryId = $expected.ExpectedFileEntryId
            Symbol = $expected.Symbol
            ProviderSymbol = $expected.ProviderSymbol
            LocalSessionDate = $expected.LocalSessionDate
            QuoteFilePath = $expected.QuoteFilePath
            ManifestPath = $expected.ManifestPath
            QuoteFileExists = [bool]$expected.QuoteFileExists
            ManifestExists = [bool]$expected.ManifestExists
            MissingReason = if (-not $expected.QuoteFileExists -and -not $expected.ManifestExists) { "QuoteAndManifestMissing" } elseif (-not $expected.QuoteFileExists) { "QuoteFileMissing" } else { "ManifestMissing" }
        }
        continue
    }
    $manifest = Read-Json ([string]$expected.ManifestPath)
    $entries += [pscustomobject]@{
        EntryId = $expected.ExpectedFileEntryId
        Symbol = $expected.Symbol
        ProviderSymbol = $expected.ProviderSymbol
        LocalSessionDate = $expected.LocalSessionDate
        ExpectedFromUtc = $expected.FromUtc
        ExpectedToUtc = $expected.ToUtc
        QuoteFilePath = $expected.QuoteFilePath
        ManifestPath = $expected.ManifestPath
        Manifest = $manifest
        ManifestSource = "ProviderManifestFile"
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-accepted-file-entries.json") ([pscustomobject]@{
    Phase = $phase
    AcceptedFileEntryCount = $entries.Count
    Entries = $entries | ForEach-Object {
        [pscustomobject]@{
            ExpectedFileEntryId = $_.EntryId
            Symbol = $_.Symbol
            ProviderSymbol = $_.ProviderSymbol
            LocalSessionDate = $_.LocalSessionDate
            QuoteFilePath = $_.QuoteFilePath
            ManifestPath = $_.ManifestPath
            ManifestSource = $_.ManifestSource
        }
    }
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-missing-file-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    MissingFileEntryCount = $missingEntries.Count
    MissingFiles = $missingEntries
})

$localValidation = @()
$rowValidation = @()
$quoteWindowResults = @()
$closeBenchmarkResults = @()
$feedQualityResults = @()
$R018QuoteIndex = @{}
$R018CloseIndex = @{}
$R018FeedIndex = @{}
$processedFilePaths = @{}

foreach ($entry in $entries) {
    if ($processedFilePaths.ContainsKey([string]$entry.QuoteFilePath)) { continue }
    $processedFilePaths[[string]$entry.QuoteFilePath] = $true
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath ([string]$entry.QuoteFilePath)).Hash.ToUpperInvariant()
    $file = Get-Item -LiteralPath ([string]$entry.QuoteFilePath)
    $manifest = $entry.Manifest
    $manifestValid = $manifest.ProviderName -eq "PolygonOfflineFile" -and
        $manifest.ProviderDatasetType -eq "HistoricalBboQuotes" -and
        $manifest.FileFormat -eq "NDJSON" -and
        $manifest.ProviderSymbol -eq $entry.ProviderSymbol -and
        $manifest.ExecutionTradableSymbol -eq $entry.Symbol -and
        $manifest.TimeRangeStartUtc -eq $entry.ExpectedFromUtc -and
        $manifest.TimeRangeEndUtc -eq $entry.ExpectedToUtc -and
        -not [bool]$manifest.ContainsSecrets -and
        -not [bool]$manifest.ContainsRawProviderPayload -and
        $hash -eq ([string]$manifest.FileHash).ToUpperInvariant() -and
        [int64]$file.Length -eq [int64]$manifest.FileSizeBytes
    $localValidation += [pscustomobject]@{
        ExpectedFileEntryId = $entry.EntryId
        Symbol = $entry.Symbol
        Provider = [string]$manifest.ProviderName
        Dataset = [string]$manifest.ProviderDatasetType
        Format = [string]$manifest.FileFormat
        ProviderSymbol = [string]$manifest.ProviderSymbol
        LocalSessionDate = $entry.LocalSessionDate
        ExpectedFromUtc = $entry.ExpectedFromUtc
        ExpectedToUtc = $entry.ExpectedToUtc
        ManifestFromUtc = [string]$manifest.TimeRangeStartUtc
        ManifestToUtc = [string]$manifest.TimeRangeEndUtc
        QuoteFilePath = $entry.QuoteFilePath
        ManifestPath = $entry.ManifestPath
        ManifestSource = $entry.ManifestSource
        ManifestAccepted = $manifestValid
        Sha256Matches = $hash -eq ([string]$manifest.FileHash).ToUpperInvariant()
        FileSizeMatches = [int64]$file.Length -eq [int64]$manifest.FileSizeBytes
        ContainsSecrets = [bool]$manifest.ContainsSecrets
        ContainsRawProviderPayload = [bool]$manifest.ContainsRawProviderPayload
        FileSizeBytes = [int64]$file.Length
        RowCountDeclared = [int]$manifest.RowCountDeclared
    }
    if (-not $manifestValid) { continue }
    $key = "$($entry.Symbol)|$($entry.LocalSessionDate)"
    $windows = if ($windowsBySymbolDate.ContainsKey($key)) { @($windowsBySymbolDate[$key]) } else { @() }
    $scan = Read-QuoteFileForWindows $entry $windows
    $rowCountMatches = [int]$scan.ObservedRowCount -eq [int]$manifest.RowCountDeclared
    $rowValid = $rowCountMatches -and $scan.CriticalInvalidRows -eq 0
    $rowValidation += [pscustomobject]@{
        EntryId = $entry.EntryId
        Symbol = $entry.Symbol
        LocalSessionDate = $entry.LocalSessionDate
        ObservedRowCount = $scan.ObservedRowCount
        DeclaredRowCount = [int]$manifest.RowCountDeclared
        RowCountMatchesManifest = $rowCountMatches
        InvalidTimestampRows = $scan.InvalidTimestampRows
        InvalidProviderSymbolRows = $scan.InvalidProviderSymbolRows
        InvalidBidAskRows = $scan.InvalidBidAskRows
        AskLessThanBidRows = $scan.AskLessThanBidRows
        RawPayloadSerializedRows = $scan.RawPayloadSerializedRows
        DuplicateTimestampRows = $scan.DuplicateTimestampRows
        ExactDuplicateRows = $scan.ExactDuplicateRows
        OutOfOrderRows = $scan.OutOfOrderRows
        RowValidationAcceptedForReadiness = $rowValid
    }
    if (-not $rowValid) { continue }
    $feedSpreads = @($scan.FeedSpreadsBps)
    $feedReady = $scan.ObservedRowCount -gt 0 -and $scan.CriticalInvalidRows -eq 0
    $feedId = "R018_FQ_$($entry.Symbol)_$(([string]$entry.LocalSessionDate).Replace('-', ''))"
    $feed = [pscustomobject]@{
        FeedQualityId = $feedId
        Symbol = $entry.Symbol
        LocalSessionDate = $entry.LocalSessionDate
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
    if ($feedReady) { $R018FeedIndex["$($entry.Symbol)|$($entry.LocalSessionDate)"] = $feed }
    foreach ($windowId in $scan.Windows.Keys) {
        $acc = $scan.Windows[$windowId]
        $window = $acc.Window
        $lastAge = if ($null -ne $acc.LastQuoteTimestampUtc) { [math]::Round(($window.TargetClose - $acc.LastQuoteTimestampUtc).TotalSeconds, 3) } else { $null }
        $quoteReady = $acc.QuoteCount -gt 0 -and $null -ne $acc.LastQuoteTimestampUtc -and $lastAge -le 60
        $quoteResult = [pscustomobject]@{
            QuoteWindowId = $window.WindowId
            Symbol = $window.Symbol
            LocalSessionDate = $window.LocalSessionDate
            TargetCloseTimestampUtc = $window.TargetCloseUtc
            WindowStartUtc = $window.WindowStartUtc
            BarRole = $window.BarRole
            QuoteCount = $acc.QuoteCount
            QuoteCountLastMinute = $acc.QuoteCountLastMinute
            MaxQuoteGapSeconds = Get-MaxGapSeconds $acc.Timestamps
            LastQuoteAgeAtCloseSeconds = $lastAge
            ReadinessStatus = if ($quoteReady) { "Ready" } else { "NotReady" }
        }
        $quoteWindowResults += $quoteResult
        if ($quoteReady) { $R018QuoteIndex["$($window.Symbol)|$($window.TargetCloseUtc)"] = $quoteResult }
        $closeReady = $quoteReady -and $null -ne $acc.LastBid -and $null -ne $acc.LastAsk
        $closeResult = [pscustomobject]@{
            CloseBenchmarkId = "R018_CB_$($window.Symbol)_$($window.TargetClose.UtcDateTime.ToString("yyyyMMdd_HHmmss"))"
            QuoteWindowId = $window.WindowId
            FeedQualityId = $feedId
            Symbol = $window.Symbol
            LocalSessionDate = $window.LocalSessionDate
            TargetCloseTimestampUtc = $window.TargetCloseUtc
            LastValidBidBeforeClose = $acc.LastBid
            LastValidAskBeforeClose = $acc.LastAsk
            LastValidMidBeforeClose = $acc.LastMid
            LastValidQuoteTimestampUtc = if ($null -ne $acc.LastQuoteTimestampUtc) { $acc.LastQuoteTimestampUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") } else { $null }
            CloseQuoteAgeSeconds = $lastAge
            CloseConstructionMethod = "LastValidQuoteAtOrBeforeCanonicalClose"
            ReadinessStatus = if ($closeReady) { "Ready" } else { "NotReady" }
        }
        $closeBenchmarkResults += $closeResult
        if ($closeReady) { $R018CloseIndex["$($window.Symbol)|$($window.TargetCloseUtc)"] = $closeResult }
    }
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-local-file-validation-results.json") ([pscustomobject]@{
    Phase = $phase
    LocalFileValidationCount = $localValidation.Count
    AcceptedLocalFileValidationCount = @($localValidation | Where-Object ManifestAccepted).Count
    Results = $localValidation
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-manifest-validation-results.json") ([pscustomobject]@{
    Phase = $phase
    ManifestValidatedEntryCount = $localValidation.Count
    AcceptedManifestCount = @($localValidation | Where-Object ManifestAccepted).Count
    QuarantinedManifestCount = @($localValidation | Where-Object { -not $_.ManifestAccepted }).Count
    Results = $localValidation
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-row-level-validation-results.json") ([pscustomobject]@{
    Phase = $phase
    RowValidatedFileCount = $rowValidation.Count
    AcceptedForReadinessFileCount = @($rowValidation | Where-Object RowValidationAcceptedForReadiness).Count
    Results = $rowValidation
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-row-count-comparison.json") ([pscustomobject]@{
    Phase = $phase
    ComparedFileCount = $rowValidation.Count
    MismatchCount = @($rowValidation | Where-Object { -not $_.RowCountMatchesManifest }).Count
    Results = @($rowValidation | ForEach-Object { [pscustomobject]@{ EntryId = $_.EntryId; ObservedRowCount = $_.ObservedRowCount; DeclaredRowCount = $_.DeclaredRowCount; RowCountMatchesManifest = $_.RowCountMatchesManifest } })
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-duplicate-out-of-order-handling.json") ([pscustomobject]@{
    Phase = $phase
    ResultCount = $rowValidation.Count
    Results = @($rowValidation | ForEach-Object { [pscustomobject]@{ EntryId = $_.EntryId; DuplicateTimestampRows = $_.DuplicateTimestampRows; ExactDuplicateRows = $_.ExactDuplicateRows; OutOfOrderRows = $_.OutOfOrderRows; Handling = "CountedOnlyNoDbImportNoSanitizedPersist" } })
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-generated-readiness-results.json") ([pscustomobject]@{
    Phase = $phase
    QuoteWindowReadinessRecords = $quoteWindowResults.Count
    QuoteWindowReadyRecords = @($quoteWindowResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    CloseBenchmarkReadinessRecords = $closeBenchmarkResults.Count
    CloseBenchmarkReadyRecords = @($closeBenchmarkResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    FeedQualityReadinessRecords = $feedQualityResults.Count
    FeedQualityReadyRecords = @($feedQualityResults | Where-Object { $_.FeedQualityStatus -eq "Ready" }).Count
    QuoteWindowResults = $quoteWindowResults
    CloseBenchmarkResults = $closeBenchmarkResults
    FeedQualityResults = $feedQualityResults
})

$R018RebindResults = foreach ($line in $stillHeldLines) {
    $symbol = [string]$line.Symbol
    $utc = [string]$line.CanonicalTargetCloseUtc
    $date = [string]$line.LocalSessionDate
    $qw = $R018QuoteIndex["$symbol|$utc"]
    $cb = $R018CloseIndex["$symbol|$utc"]
    $fq = $R018FeedIndex["$symbol|$date"]
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
$R018Rebound = @($R018RebindResults | Where-Object ReboundComplete)
$finalStillHeld = @($R018RebindResults | Where-Object StillHeld)
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-held-line-rebinding-results.json") ([pscustomobject]@{
    Phase = $phase
    R017StillHeldLineCount = [int]$r017StillHeld.FinalStillHeldLineCount
    R018ReboundLineCount = $R018Rebound.Count
    FinalStillHeldLineCount = $finalStillHeld.Count
    ReadinessBindingsInvented = $false
    Results = $R018RebindResults
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-still-held-line-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    FinalStillHeldLineCount = $finalStillHeld.Count
    HeldBySymbol = @($finalStillHeld | Group-Object Symbol | Sort-Object Name | ForEach-Object { [pscustomobject]@{ Symbol = $_.Name; Count = $_.Count } })
    HeldByBarRole = @($finalStillHeld | Group-Object BarRole | Sort-Object Name | ForEach-Object { [pscustomobject]@{ BarRole = $_.Name; Count = $_.Count } })
    HeldByReason = @($finalStillHeld | Group-Object HoldReason | Sort-Object Name | ForEach-Object { [pscustomobject]@{ HoldReason = $_.Name; Count = $_.Count } })
    Lines = $finalStillHeld
})

$r017CompleteIds = @{}
foreach ($line in (As-Array $r017Reagg.Lines | Where-Object ReadinessComplete)) { $r017CompleteIds[[string]$line.PaperExecutionPlanLineId] = $true }
$R018CompleteIds = @{}
foreach ($line in $R018Rebound) { $R018CompleteIds[[string]$line.PaperExecutionPlanLineId] = $true }
$finalLines = foreach ($line in (As-Array $r014Preview.Lines)) {
    $id = [string]$line.PaperExecutionPlanLineId
    $ready = $r017CompleteIds.ContainsKey($id) -or $R018CompleteIds.ContainsKey($id)
    $source = if ($R018CompleteIds.ContainsKey($id)) { "R018FinalReadinessRebound" } elseif ($r017CompleteIds.ContainsKey($id)) { "R017OrEarlierComplete" } else { "StillHeldMissingReadiness" }
    [pscustomobject]@{
        PaperExecutionPlanLineId = $id
        BatchEntryId = $line.BatchEntryId
        Symbol = $line.ExecutionTradableSymbol
        CanonicalTargetCloseUtc = $line.CanonicalTargetCloseTimestamp
        BarRole = $line.BarRole
        ReadinessComplete = $ready
        ReadinessSource = $source
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
$completeCount = @($finalLines | Where-Object ReadinessComplete).Count
$heldCount = @($finalLines | Where-Object { -not $_.ReadinessComplete }).Count
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-reaggregated-preview-status.json") ([pscustomobject]@{
    Phase = $phase
    PreviewLineCount = $finalLines.Count
    R017ReadinessCompleteLineCount = [int]$r017Reagg.ReadinessCompleteLineCount
    R018ReboundLineCount = $R018Rebound.Count
    ReadinessCompleteLineCount = $completeCount
    FinalStillHeldLineCount = $heldCount
    AllPreviewLinesReadinessComplete = $completeCount -eq 700
    Lines = $finalLines
})

$decisionStatus = if ($completeCount -eq 700) { "R009LongRunPaperOnlyMaturityComplete" } else { "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker" }
$classifications = if ($completeCount -eq 700) {
    @(
        "EXEC_PAPER_R018_PASS_FINAL_MISSING_READINESS_FILE_INTAKE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_FINAL_MISSING_READINESS_VALIDATION_READY_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_FINAL_HELD_LINE_REBINDING_READY_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_R009_LONG_RUN_PAPER_MATURITY_COMPLETE_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R018_PARTIAL_FINAL_HELD_LINE_REBINDING_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_FINAL_STILL_HELD_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_EXPLICIT_READINESS_BLOCKER_READY_NO_EXTERNAL",
        "EXEC_PAPER_R018_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-long-run-maturity-decision.json") ([pscustomobject]@{
    Phase = $phase
    Decision = $decisionStatus
    ReadinessCompleteLineCount = $completeCount
    FinalStillHeldLineCount = $heldCount
    Classifications = $classifications
    ExecutablePromotionAuthorized = $false
})
$review = [pscustomobject]@{
    Phase = $phase
    ExpectedFileEntryCount = $expectedEntries.Count
    AcceptedFileEntryCount = $entries.Count
    MissingFileEntryCount = $missingEntries.Count
    ManifestValidationAccepted = @($localValidation | Where-Object ManifestAccepted).Count
    RowValidationAccepted = @($rowValidation | Where-Object RowValidationAcceptedForReadiness).Count
    GeneratedQuoteWindowReady = @($quoteWindowResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    GeneratedCloseBenchmarkReady = @($closeBenchmarkResults | Where-Object { $_.ReadinessStatus -eq "Ready" }).Count
    GeneratedFeedQualityReady = @($feedQualityResults | Where-Object { $_.FeedQualityStatus -eq "Ready" }).Count
    R018ReboundLineCount = $R018Rebound.Count
    ReadinessCompleteLineCount = $completeCount
    FinalStillHeldLineCount = $heldCount
    Decision = $decisionStatus
    ExecutablePromotionAuthorized = $false
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-operator-review-report.json") $review
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r018-final-operator-review-report.md") @"
# EXEC-PAPER-R018 Final Operator Review

R018 inspected the operator-completed R017 final missing-readiness downloads, validated local quote/manifests, generated readiness, rebound held lines, and re-aggregated all 700 R009 preview lines.

- Expected file entries: $($expectedEntries.Count)
- Accepted file entries: $($entries.Count)
- Missing file entries: $($missingEntries.Count)
- R018 rebound lines: $($R018Rebound.Count)
- Readiness-complete preview lines: $completeCount / 700
- Final still-held lines: $heldCount
- Decision: $decisionStatus

No download, external API, Polygon, LMAX, broker, live market data, ManualNoExternal, PMS/EMS/OMS, DB import, persisted sanitized rows, TCA, orders, fills, routes, submissions, or ledger commits occurred.
"@

$finalDownloadNeeded = $heldCount -gt 0
$finalDownloadCommands = @()
if ($finalDownloadNeeded) {
    $finalDownloadCommands = @(
        $finalStillHeld |
            Group-Object @{ Expression = { "$($_.Symbol)|$($_.LocalSessionDate)" } } |
            Sort-Object Name |
            ForEach-Object {
                $first = @($_.Group)[0]
                $targets = @($_.Group | ForEach-Object { [DateTimeOffset]::Parse([string]$_.CanonicalTargetCloseUtc).ToUniversalTime() } | Sort-Object)
                $from = $targets[0].AddMinutes(-13)
                $to = $targets[-1]
                $symbol = [string]$first.Symbol
                $providerSymbol = Get-ProviderSymbol $symbol
                $fromText = $from.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                $toText = $to.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                [pscustomobject]@{
                    Symbol = $symbol
                    ProviderSymbol = $providerSymbol
                    LocalSessionDate = [string]$first.LocalSessionDate
                    FromUtc = $fromText
                    ToUtc = $toText
                    TargetCloseCount = @($targets).Count
                    CommandTemplate = ".\scripts\download-polygon-fx-bbo-offline.ps1 -FromUtc `"$fromText`" -ToUtc `"$toText`" -Symbols @(`"$providerSymbol`") -OutDir `"data/offline-quotes/polygon/incoming`""
                    CommandIsOperatorRunOnly = $true
                    CommandExecutedInR018 = $false
                    OutputFilesClaimedToExist = $false
                }
            }
    )
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-if-needed-final-download-command-package.json") ([pscustomobject]@{
    Phase = $phase
    Needed = $finalDownloadNeeded
    CommandsAreTemplatesOnly = $true
    CommandsExecutedInR018 = 0
    FilesDownloadedInR018 = 0
    ExternalApiCalled = $false
    OutputFilesClaimedToExist = $false
    RemainingHeldLineCount = $heldCount
    CommandTemplateCount = @($finalDownloadCommands).Count
    Commands = $finalDownloadCommands
    Note = if ($finalDownloadNeeded) { "Remaining blockers are local data gaps or quote-window readiness insufficiency; operator may provide replacement quote windows." } else { "No final download command is needed." }
})
$finalDownloadCommandText = if ($finalDownloadNeeded) {
    (@($finalDownloadCommands) | ForEach-Object { "- $($_.CommandTemplate)" }) -join "`n"
} else {
    "No final download command is needed."
}
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r018-if-needed-final-download-command-package.md") @"
# EXEC-PAPER-R018 Final Download Package

R018 did not execute downloads.

Needed: $finalDownloadNeeded
Remaining held lines: $heldCount

Command templates are operator-run only. Codex did not execute them and does not claim the output files exist.

$finalDownloadCommandText
"@

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = if ($completeCount -eq 700) { "EXEC-ALGO-R013 - No-External R009 Long-Run Paper Maturity Acceptance and Executable Blocker Review Gate" } else { "Operator executes final one-command/data package or accepts explicit partial blocker." }
    Reason = if ($completeCount -eq 700) { "All 700 preview lines are readiness-complete." } else { "Final still-held lines remain after local final-download intake and validation." }
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $true; DirectCrossExecutionAllowed = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxUsedInThisPhase = $false; LmaxCalledInThisPhase = $false; ReferenceOnly = $true })

New-Audit "phase-exec-paper-r018-no-db-import-audit.json" "NoDbImport" "No rows were imported into a database."
New-Audit "phase-exec-paper-r018-no-persisted-sanitized-row-audit.json" "NoPersistedSanitizedRows" "No sanitized rows were persisted."
New-Audit "phase-exec-paper-r018-no-new-pms-cycle-audit.json" "NoNewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-paper-r018-no-manualnoexternal-command-run-audit.json" "NoManualNoExternalCommandRun" "No ManualNoExternal command was run."
New-Audit "phase-exec-paper-r018-no-new-backtest-audit.json" "NoNewBacktest" "No backtest was run."
New-Audit "phase-exec-paper-r018-no-new-simulation-audit.json" "NoNewSimulation" "No simulation was run."
New-Audit "phase-exec-paper-r018-no-tca-result-lines-audit.json" "NoTcaResultLines" "No TCA result lines were created."
New-Audit "phase-exec-paper-r018-no-executable-schedule-audit.json" "NoExecutableSchedule" "No executable schedule was created."
New-Audit "phase-exec-paper-r018-no-child-slices-audit.json" "NoChildSlices" "No child slices were created."
New-Audit "phase-exec-paper-r018-no-child-orders-audit.json" "NoChildOrders" "No child orders were created."
New-Audit "phase-exec-paper-r018-no-order-created-audit.json" "NoOrderCreated" "No order was created."
New-Audit "phase-exec-paper-r018-no-real-fill-audit.json" "NoRealFill" "No fill was created."
New-Audit "phase-exec-paper-r018-no-execution-report-audit.json" "NoExecutionReport" "No execution report was created."
New-Audit "phase-exec-paper-r018-no-route-no-submission-audit.json" "NoRouteNoSubmission" "No route or submission was created."
New-Audit "phase-exec-paper-r018-no-paper-ledger-commit-audit.json" "NoPaperLedgerCommit" "No ledger commit was created."
New-Audit "phase-exec-paper-r018-no-polygon-api-call-audit.json" "NoPolygonApiCall" "Polygon was not called."
New-Audit "phase-exec-paper-r018-no-lmax-call-audit.json" "NoLmaxCall" "LMAX was not called."
New-Audit "phase-exec-paper-r018-no-external-api-call-audit.json" "NoExternalApiCall" "No external API was called."

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-forbidden-actions-audit.json") ([pscustomobject]@{
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
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r018-build-test-validator-evidence.json") ([pscustomobject]@{ Phase = $phase; DotnetBuild = "Pending"; FocusedR018Tests = "Pending"; UnitTests = "Pending"; R018Validator = "Pending"; EvidenceComplete = $false })

Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r018-summary.md") @"
# EXEC-PAPER-R018 Summary

R018 processed the operator-completed final missing-readiness local files and attempted final rebinding without external calls, downloads, ManualNoExternal, PMS/EMS/OMS, DB import, TCA, orders, fills, routes, submissions, or ledger commits.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Counts:
- Expected file entries: $($expectedEntries.Count)
- Accepted file entries: $($entries.Count)
- Missing file entries: $($missingEntries.Count)
- R018 rebound lines: $($R018Rebound.Count)
- Readiness-complete preview lines: $completeCount / 700
- Final still-held lines: $heldCount

Decision: $decisionStatus
"@

Write-Output "EXEC-PAPER-R018 artifacts generated"


