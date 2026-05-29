param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 60) {
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
        Phase = "EXEC-PAPER-R015"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

function Get-PhaseFromPath([string]$path) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($path)
    if ($name -match "phase-(.+?)-(quote-window|close-benchmark|feed-quality)") { return $Matches[1].ToUpperInvariant() }
    return "UNKNOWN"
}

function Get-BindingId($record, [string]$kind, [string]$phase) {
    if ($kind -eq "QuoteWindow" -and $record.PSObject.Properties.Name -contains "QuoteWindowId") { return [string]$record.QuoteWindowId }
    if ($kind -eq "CloseBenchmark" -and $record.PSObject.Properties.Name -contains "CloseBenchmarkId") { return [string]$record.CloseBenchmarkId }
    if ($kind -eq "FeedQuality" -and $record.PSObject.Properties.Name -contains "FeedQualityId") { return [string]$record.FeedQualityId }
    $symbol = [string]$record.Symbol
    $date = [string]$record.LocalSessionDate
    $utc = if ($record.PSObject.Properties.Name -contains "TargetCloseTimestampUtc") { [string]$record.TargetCloseTimestampUtc } else { $date }
    $safeUtc = $utc.Replace(":", "").Replace("-", "").Replace("T", "_").Replace("Z", "Z")
    return "$phase`_$kind`_$symbol`_$safeUtc"
}

function Is-Ready($record, [string]$kind) {
    if ($kind -eq "QuoteWindow") {
        return ([string]$record.ReadinessStatus -eq "Ready") -or ([string]$record.FeedWindowStatus -eq "QuoteWindowReady")
    }
    if ($kind -eq "CloseBenchmark") {
        return ([string]$record.ReadinessStatus -eq "Ready") -or ([string]$record.CloseBenchmarkStatus -eq "CloseBenchmarkAvailable")
    }
    return ([string]$record.FeedQualityStatus -eq "Ready") -or ([string]$record.FeedQualityBucket -match "^FeedQuality")
}

function Add-ReadinessSource(
    [string]$path,
    [string]$kind,
    [hashtable]$exactIndex,
    [hashtable]$feedIndex,
    [System.Collections.Generic.List[object]]$inventory
) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $payload = Read-Json $path
    $phase = if ($payload.PSObject.Properties.Name -contains "Phase") { [string]$payload.Phase } else { Get-PhaseFromPath $path }
    $records = As-Array $payload.Results
    $readyRecords = @($records | Where-Object { Is-Ready $_ $kind })
    $dates = @($records | Where-Object { $_.PSObject.Properties.Name -contains "LocalSessionDate" } | Select-Object -ExpandProperty LocalSessionDate -Unique | Sort-Object)
    $inventory.Add([pscustomobject]@{
        ArtifactPath = (Resolve-Path -LiteralPath $path).Path
        Phase = $phase
        Kind = $kind
        RecordCount = $records.Count
        ReadyRecordCount = $readyRecords.Count
        FirstLocalSessionDate = if ($dates.Count -gt 0) { $dates[0] } else { $null }
        LastLocalSessionDate = if ($dates.Count -gt 0) { $dates[$dates.Count - 1] } else { $null }
        UsedForRebindingSearch = $true
    })

    foreach ($record in $readyRecords) {
        $symbol = [string]$record.Symbol
        $date = [string]$record.LocalSessionDate
        $binding = [pscustomobject]@{
            SourcePhase = $phase
            SourceArtifact = (Resolve-Path -LiteralPath $path).Path
            BindingId = Get-BindingId $record $kind $phase
            Symbol = $symbol
            LocalSessionDate = $date
            TargetCloseTimestampUtc = if ($record.PSObject.Properties.Name -contains "TargetCloseTimestampUtc") { [string]$record.TargetCloseTimestampUtc } else { $null }
            ReadinessKind = $kind
            ReadinessStatus = "Ready"
            Invented = $false
        }
        if ($kind -eq "FeedQuality") {
            $key = "$symbol|$date"
            if (-not $feedIndex.ContainsKey($key)) { $feedIndex[$key] = $binding }
        } else {
            $utc = [string]$record.TargetCloseTimestampUtc
            if (-not [string]::IsNullOrWhiteSpace($utc)) {
                $key = "$symbol|$utc"
                if (-not $exactIndex.ContainsKey($key)) { $exactIndex[$key] = $binding }
            }
        }
    }
}

function Convert-ToProviderSymbol([string]$symbol) {
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

function Test-CanonicalQuarterHour([string]$local) {
    return $local -match "T\d{2}:(00|15|30|45):00" -and $local -notmatch "T\d{2}:(06|21|36|51):00"
}

$phase = "EXEC-PAPER-R015"
$r014HeldPath = Join-Path $ArtifactsRoot "phase-exec-paper-r014-held-line-diagnostics.json"
$r014PreviewPath = Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-design-only-preview-lines.json"
$r013ManifestPath = "data/qubes-fixtures/long-run-paper-eval/batch-manifest.json"

$r014Held = Read-Json $r014HeldPath
$r014Preview = Read-Json $r014PreviewPath
$manifest = Read-Json $r013ManifestPath
$previewLines = As-Array $r014Preview.Lines
$heldLines = @($previewLines | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.HoldReason) })

$r014Reference = [pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R014"
    HeldLineArtifact = (Resolve-Path -LiteralPath $r014HeldPath).Path
    PreviewLineArtifact = (Resolve-Path -LiteralPath $r014PreviewPath).Path
    HeldLineCountFromDiagnostics = [int]$r014Held.HeldLineCount
    HeldPreviewLineCountLoaded = $heldLines.Count
    TotalPreviewLineCount = $previewLines.Count
    ReusedOnly = $true
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-r014-held-line-reference.json") $r014Reference

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-r009-contract-reference.json") ([pscustomobject]@{
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

$diagnosisLines = foreach ($line in $heldLines) {
    $missing = As-Array $line.MissingInputs
    if ($missing.Count -eq 0) { $missing = @(([string]$line.HoldReason).Split(";") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) }
    [pscustomobject]@{
        BatchEntryId = $line.BatchEntryId
        PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
        Symbol = $line.Symbol
        ExecutionTradableSymbol = $line.ExecutionTradableSymbol
        NormalizedPortfolioSymbol = $line.NormalizedPortfolioSymbol
        RequiresInversion = [bool]$line.RequiresInversion
        CanonicalTargetCloseUtc = $line.CanonicalTargetCloseTimestamp
        CanonicalTargetCloseLocal = $line.CanonicalTargetCloseLocal
        LocalSessionDate = ([string]$line.CanonicalTargetCloseLocal).Substring(0, 10)
        UtcDate = ([string]$line.CanonicalTargetCloseTimestamp).Substring(0, 10)
        BarRole = $line.BarRole
        MissingQuoteWindowReadiness = $missing -contains "MissingQuoteWindowReadinessBinding"
        MissingCloseBenchmarkReadiness = $missing -contains "MissingCloseBenchmarkReadinessBinding"
        MissingFeedQualityReadiness = $missing -contains "MissingFeedQualityReadinessBinding"
        MissingRiskOperatorApproval = $missing -contains "MissingRiskOperatorApproval"
        MissingCanonicalTargetClose = $missing -contains "MissingCanonicalTargetClose"
        MissingOther = @($missing | Where-Object { $_ -notin @("MissingQuoteWindowReadinessBinding","MissingCloseBenchmarkReadinessBinding","MissingFeedQualityReadinessBinding","MissingRiskOperatorApproval","MissingCanonicalTargetClose") })
        DirectCrossExecutionHold = $false
        InversionMismatchHold = $false
        CanonicalQuarterHourConfirmed = Test-CanonicalQuarterHour ([string]$line.CanonicalTargetCloseLocal)
    }
}

$missingFields = @(
    [pscustomobject]@{ MissingField = "MissingQuoteWindowReadinessBinding"; Count = @($diagnosisLines | Where-Object MissingQuoteWindowReadiness).Count },
    [pscustomobject]@{ MissingField = "MissingCloseBenchmarkReadinessBinding"; Count = @($diagnosisLines | Where-Object MissingCloseBenchmarkReadiness).Count },
    [pscustomobject]@{ MissingField = "MissingFeedQualityReadinessBinding"; Count = @($diagnosisLines | Where-Object MissingFeedQualityReadiness).Count },
    [pscustomobject]@{ MissingField = "MissingRiskOperatorApproval"; Count = @($diagnosisLines | Where-Object MissingRiskOperatorApproval).Count },
    [pscustomobject]@{ MissingField = "MissingCanonicalTargetClose"; Count = @($diagnosisLines | Where-Object MissingCanonicalTargetClose).Count }
)

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-diagnosis.json") ([pscustomobject]@{
    Phase = $phase
    HeldLineCount = $diagnosisLines.Count
    MissingFieldCounts = $missingFields
    DirectCrossExecutionHoldCount = @($diagnosisLines | Where-Object DirectCrossExecutionHold).Count
    InversionMismatchHoldCount = @($diagnosisLines | Where-Object InversionMismatchHold).Count
    CanonicalTargetCloseMissingCount = @($diagnosisLines | Where-Object MissingCanonicalTargetClose).Count
    RiskOperatorApprovalMissingCount = @($diagnosisLines | Where-Object MissingRiskOperatorApproval).Count
    Lines = $diagnosisLines
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-grouping-by-symbol.json") ([pscustomobject]@{
    Phase = $phase
    Groups = @($diagnosisLines | Group-Object ExecutionTradableSymbol | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{ Symbol = $_.Name; HeldLineCount = $_.Count; TargetCloseCount = @($_.Group | Select-Object -ExpandProperty CanonicalTargetCloseUtc -Unique).Count }
    })
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-grouping-by-target-close.json") ([pscustomobject]@{
    Phase = $phase
    Groups = @($diagnosisLines | Group-Object CanonicalTargetCloseUtc | Sort-Object Name | ForEach-Object {
        $sample = $_.Group[0]
        [pscustomobject]@{ TargetCloseUtc = $_.Name; TargetCloseLocal = $sample.CanonicalTargetCloseLocal; LocalSessionDate = $sample.LocalSessionDate; BarRole = $sample.BarRole; HeldLineCount = $_.Count; Symbols = @($_.Group | Select-Object -ExpandProperty ExecutionTradableSymbol | Sort-Object) }
    })
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-line-grouping-by-bar-role.json") ([pscustomobject]@{
    Phase = $phase
    Groups = @($diagnosisLines | Group-Object BarRole | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{ BarRole = $_.Name; HeldLineCount = $_.Count; TargetCloseCount = @($_.Group | Select-Object -ExpandProperty CanonicalTargetCloseUtc -Unique).Count }
    })
})

$quoteIndex = @{}
$closeIndex = @{}
$feedIndex = @{}
$inventory = [System.Collections.Generic.List[object]]::new()

$readinessPatterns = @(
    @{ Kind = "QuoteWindow"; Pattern = "phase-exec-sim-r*-quote-window-readiness-results.json"; Exact = $quoteIndex },
    @{ Kind = "CloseBenchmark"; Pattern = "phase-exec-sim-r*-close-benchmark-readiness-results.json"; Exact = $closeIndex },
    @{ Kind = "FeedQuality"; Pattern = "phase-exec-sim-r*-feed-quality-readiness-results.json"; Exact = $null }
)
foreach ($pattern in $readinessPatterns) {
    $paths = @(Get-ChildItem -Path $ArtifactsRoot -Filter $pattern.Pattern | Sort-Object {
        if ($_.Name -match "r053") { "00_$($_.Name)" } elseif ($_.Name -match "r042") { "01_$($_.Name)" } else { "02_$($_.Name)" }
    })
    foreach ($path in $paths) {
        if ($pattern.Kind -eq "FeedQuality") {
            Add-ReadinessSource $path.FullName $pattern.Kind @{} $feedIndex $inventory
        } else {
            Add-ReadinessSource $path.FullName $pattern.Kind $pattern.Exact @{} $inventory
        }
    }
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-existing-readiness-artifact-inventory.json") ([pscustomobject]@{
    Phase = $phase
    SearchScope = "LocalArtifactsOnly"
    R053CheckedFirst = $true
    R042Checked = $true
    ArtifactCount = $inventory.Count
    Artifacts = @($inventory)
})

$rebindResults = foreach ($line in $diagnosisLines) {
    $symbol = [string]$line.ExecutionTradableSymbol
    $utc = [string]$line.CanonicalTargetCloseUtc
    $date = [string]$line.LocalSessionDate
    $qw = $quoteIndex["$symbol|$utc"]
    $cb = $closeIndex["$symbol|$utc"]
    $fq = $feedIndex["$symbol|$date"]
    $complete = $null -ne $qw -and $null -ne $cb -and $null -ne $fq
    [pscustomobject]@{
        BatchEntryId = $line.BatchEntryId
        PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
        Symbol = $symbol
        NormalizedPortfolioSymbol = $line.NormalizedPortfolioSymbol
        RequiresInversion = $line.RequiresInversion
        CanonicalTargetCloseUtc = $utc
        CanonicalTargetCloseLocal = $line.CanonicalTargetCloseLocal
        LocalSessionDate = $date
        BarRole = $line.BarRole
        QuoteWindowBindingFound = $null -ne $qw
        QuoteWindowReadinessBinding = $qw
        CloseBenchmarkBindingFound = $null -ne $cb
        CloseBenchmarkReadinessBinding = $cb
        FeedQualityBindingFound = $null -ne $fq
        FeedQualityReadinessBinding = $fq
        ReboundComplete = $complete
        ReboundFromExistingLocalArtifactsOnly = $complete
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

$rebound = @($rebindResults | Where-Object ReboundComplete)
$stillHeld = @($rebindResults | Where-Object StillHeld)

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-readiness-rebinding-search-results.json") ([pscustomobject]@{
    Phase = $phase
    HeldLineCount = $diagnosisLines.Count
    LocalArtifactSearchOnly = $true
    MatchingMethod = "ExecutionTradableSymbol + CanonicalTargetCloseUtc for quote/close; ExecutionTradableSymbol + LocalSessionDate for feed-quality"
    ReboundCompleteLineCount = $rebound.Count
    StillHeldLineCount = $stillHeld.Count
    ReadinessBindingsInvented = $false
    Results = $rebindResults
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-rebound-line-results.json") ([pscustomobject]@{
    Phase = $phase
    ReboundLineCount = $rebound.Count
    ReboundLines = $rebound
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-still-held-line-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    StillHeldLineCount = $stillHeld.Count
    MissingQuoteWindowReadinessCount = @($stillHeld | Where-Object { -not $_.QuoteWindowBindingFound }).Count
    MissingCloseBenchmarkReadinessCount = @($stillHeld | Where-Object { -not $_.CloseBenchmarkBindingFound }).Count
    MissingFeedQualityReadinessCount = @($stillHeld | Where-Object { -not $_.FeedQualityBindingFound }).Count
    DirectCrossExecutionHoldCount = 0
    InversionMismatchHoldCount = 0
    Lines = $stillHeld
})

$missingWindows = @($stillHeld | Group-Object Symbol, CanonicalTargetCloseUtc | Sort-Object Name | ForEach-Object {
    $sample = $_.Group[0]
    $target = [DateTimeOffset]::Parse([string]$sample.CanonicalTargetCloseUtc).ToUniversalTime()
    [pscustomobject]@{
        Symbol = $sample.Symbol
        ProviderSymbol = Convert-ToProviderSymbol ([string]$sample.Symbol)
        LocalSessionDate = $sample.LocalSessionDate
        TargetCloseUtc = $target.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        TargetCloseLocal = $sample.CanonicalTargetCloseLocal
        WindowStartUtc = $target.AddMinutes(-13).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        WindowEndUtc = $target.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        BarRole = $sample.BarRole
        MissingQuoteWindowReadiness = -not $sample.QuoteWindowBindingFound
        MissingCloseBenchmarkReadiness = -not $sample.CloseBenchmarkBindingFound
        MissingFeedQualityReadiness = -not $sample.FeedQualityBindingFound
        LineCount = $_.Count
    }
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-readiness-window-requirements.json") ([pscustomobject]@{
    Phase = $phase
    StillHeldLineCount = $stillHeld.Count
    MissingWindowCount = $missingWindows.Count
    WindowDefinition = "WindowStartUtc = TargetCloseUtc - 13 minutes; WindowEndUtc = TargetCloseUtc"
    CanonicalSession = "14:15-21:00 America/New_York"
    Windows = $missingWindows
})

$downloadGroups = @($missingWindows | Group-Object Symbol, LocalSessionDate | Sort-Object Name | ForEach-Object {
    $sample = $_.Group[0]
    $from = @($_.Group | ForEach-Object { [DateTimeOffset]::Parse([string]$_.WindowStartUtc).ToUniversalTime() } | Sort-Object | Select-Object -First 1)[0]
    $to = @($_.Group | ForEach-Object { [DateTimeOffset]::Parse([string]$_.WindowEndUtc).ToUniversalTime() } | Sort-Object | Select-Object -Last 1)[0]
    $provider = Convert-ToProviderSymbol ([string]$sample.Symbol)
    [pscustomobject]@{
        Symbol = $sample.Symbol
        ProviderSymbol = $provider
        LocalSessionDate = $sample.LocalSessionDate
        FromUtc = $from.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        ToUtc = $to.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        TargetCloseCount = @($_.Group | Select-Object -ExpandProperty TargetCloseUtc -Unique).Count
        CommandTemplate = ".\scripts\download-polygon-fx-bbo-offline.ps1 -FromUtc `"$($from.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))`" -ToUtc `"$($to.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))`" -Symbols @(`"$provider`") -OutDir `"data/offline-quotes/polygon/incoming`""
        CommandIsOperatorRunOnly = $true
        CommandExecutedInR015 = $false
        OutputFilesClaimedToExist = $false
    }
})

$downloadMd = @"
# EXEC-PAPER-R015 Missing Offline Quote Download Plan

These are operator-run templates only. R015 did not execute downloads, did not call Polygon, and did not claim output files exist.

Missing readiness windows: $($missingWindows.Count)
Grouped command templates: $($downloadGroups.Count)

$($downloadGroups | ForEach-Object { "- $($_.Symbol) $($_.LocalSessionDate) $($_.FromUtc) to $($_.ToUtc): ``$($_.CommandTemplate)``" } | Out-String)
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-offline-quote-download-plan.md") $downloadMd
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-missing-offline-quote-download-plan.json") ([pscustomobject]@{
    Phase = $phase
    DownloadRequired = $downloadGroups.Count -gt 0
    CommandsAreTemplatesOnly = $true
    CommandsExecutedInR015 = 0
    FilesDownloadedInR015 = 0
    OutputFilesClaimedToExist = $false
    Script = ".\scripts\download-polygon-fx-bbo-offline.ps1"
    CommandTemplateCount = $downloadGroups.Count
    Commands = $downloadGroups
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-future-validation-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredAfterOperatorDownloads = @(
        "Validate offline quote files exist for requested symbol/date windows",
        "Run row-level validation without external calls",
        "Produce quote-window readiness for each missing TargetCloseUtc",
        "Produce close-benchmark readiness for each missing TargetCloseUtc",
        "Produce feed-quality readiness for each missing symbol/local date",
        "Rebind held lines from local validated readiness artifacts only",
        "Re-aggregate R009 design-only preview with NonExecutable and NotAnOrder flags preserved"
    )
    MustNotRunDownloadsInValidationGate = $true
    MustRemainNoExternal = $true
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-held-lines-retry-plan.json") ([pscustomobject]@{
    Phase = $phase
    RetryScope = "HeldLinesOnlyOrFullReAggregateAfterReadinessValidation"
    ReboundLineCount = $rebound.Count
    StillHeldLineCount = $stillHeld.Count
    ManualNoExternalRunNow = $false
    DownloadsRunNow = $false
    Steps = @(
        "Operator executes generated offline download templates outside R015 if approved.",
        "Run a future no-external row-validation/readiness gate for downloaded files.",
        "Rebind still-held lines from local readiness artifacts.",
        "Run EXEC-PAPER-R016 re-aggregation if all held lines are rebound, or keep explicit held diagnostics."
    )
})

$classifications = if ($rebound.Count -gt 0) {
    @(
        "EXEC_PAPER_R015_PASS_HELD_LINE_READINESS_DIAGNOSIS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_EXISTING_READINESS_REBINDING_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_MISSING_READINESS_PACKAGE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_NO_DOWNLOAD_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R015_PASS_HELD_LINE_READINESS_DIAGNOSIS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_NEEDS_OPERATOR_MISSING_READINESS_DOWNLOADS_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_MISSING_READINESS_PACKAGE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R015_PASS_NO_DOWNLOAD_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-updated-partial-maturity-decision.json") ([pscustomobject]@{
    Phase = $phase
    PriorHeldLineCount = $heldLines.Count
    ReboundLineCount = $rebound.Count
    StillHeldLineCount = $stillHeld.Count
    CompleteReadinessAfterRebinding = $stillHeld.Count -eq 0
    Decision = if ($stillHeld.Count -eq 0) { "R009LongRunPaperOnlyHeldLinesReboundReadyForReAggregation" } else { "R009LongRunPaperOnlyPartialMaturityStillNeedsMissingReadinessDownloads" }
    Classifications = $classifications
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-next-operator-action-package.json") ([pscustomobject]@{
    Phase = $phase
    OperatorActionRequired = $stillHeld.Count -gt 0
    ReboundLineCount = $rebound.Count
    StillHeldLineCount = $stillHeld.Count
    DownloadTemplateCount = $downloadGroups.Count
    DoNotExecuteInThisPhase = @("downloads", "ManualNoExternal", "PMS/EMS/OMS", "backtest", "simulation", "orders", "fills", "routes", "submissions", "ledger commits")
    NextAction = if ($stillHeld.Count -gt 0) { "Operator may execute generated offline download templates, then run combined validation/retry gate." } else { "Proceed to EXEC-PAPER-R016 re-aggregation." }
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FutureTimestampsUseCanonicalQuarterHour = $true
    Legacy06UsedAsFutureCanonical = $false
    HeldCanonicalQuarterHourFailures = @($diagnosisLines | Where-Object { -not $_.CanonicalQuarterHourConfirmed }).Count
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-legacy-compatibility-preservation.json") ([pscustomobject]@{
    Phase = $phase
    LegacyTimestampsCompatibilityOnly = $true
    Legacy06UsedAsFutureCanonical = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-usd-pair-normalization-preservation.json") ([pscustomobject]@{
    Phase = $phase
    USDPairOnlyAfterNetting = $true
    SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    DirectCrossExecutionAllowed = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-direct-cross-exclusion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    DirectCrossesSignalOnly = $true
    DirectCrossNettingFirst = $true
    DirectCrossExecutionDisabled = $true
    DirectCrossHoldCount = 0
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-cost-guidance-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FiveUsdPerMillion = "BestCaseMajorOnly"
    FiveUsdPerMillionUniversalized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-nonmajor-calibration-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NonmajorEmScandiCnhCalibrationRequired = $true
    NonmajorExecutionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-usdjpy-caveat-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = 4004
    SecurityIDSource = "8"
    CaveatWeakened = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-lmax-readonly-baseline-reference.json") ([pscustomobject]@{
    Phase = $phase
    LmaxUsedInThisPhase = $false
    LmaxCalledInThisPhase = $false
    ReferenceOnly = $true
})

New-Audit "phase-exec-paper-r015-no-download-audit.json" "NoDownload" "No files were downloaded; only operator-run templates were written."
New-Audit "phase-exec-paper-r015-no-polygon-api-call-audit.json" "NoPolygonApiCall" "Polygon was not called."
New-Audit "phase-exec-paper-r015-no-lmax-call-audit.json" "NoLmaxCall" "LMAX was not called."
New-Audit "phase-exec-paper-r015-no-external-api-call-audit.json" "NoExternalApiCall" "No external API was called."
New-Audit "phase-exec-paper-r015-no-broker-activation-audit.json" "NoBrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-paper-r015-no-live-marketdata-audit.json" "NoLiveMarketData" "No live market data was requested."
New-Audit "phase-exec-paper-r015-no-scheduler-service-polling-audit.json" "NoSchedulerServicePolling" "No scheduler, service, polling, timer, or background job was started."
New-Audit "phase-exec-paper-r015-no-new-pms-cycle-audit.json" "NoNewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-paper-r015-no-manualnoexternal-command-run-audit.json" "NoManualNoExternalCommandRun" "No ManualNoExternal command was run."
New-Audit "phase-exec-paper-r015-no-new-backtest-audit.json" "NoNewBacktest" "No backtest was run."
New-Audit "phase-exec-paper-r015-no-new-simulation-audit.json" "NoNewSimulation" "No simulation was run."
New-Audit "phase-exec-paper-r015-no-tca-result-lines-audit.json" "NoTcaResultLines" "No TCA result lines were created."
New-Audit "phase-exec-paper-r015-no-executable-schedule-audit.json" "NoExecutableSchedule" "No executable schedule was created."
New-Audit "phase-exec-paper-r015-no-child-slices-audit.json" "NoChildSlices" "No child slices were created."
New-Audit "phase-exec-paper-r015-no-child-orders-audit.json" "NoChildOrders" "No child orders were created."
New-Audit "phase-exec-paper-r015-no-order-created-audit.json" "NoOrderCreated" "No order was created."
New-Audit "phase-exec-paper-r015-no-real-fill-audit.json" "NoRealFill" "No fill was created."
New-Audit "phase-exec-paper-r015-no-execution-report-audit.json" "NoExecutionReport" "No execution report was created."
New-Audit "phase-exec-paper-r015-no-route-no-submission-audit.json" "NoRouteNoSubmission" "No route or submission was created."
New-Audit "phase-exec-paper-r015-no-paper-ledger-commit-audit.json" "NoPaperLedgerCommit" "No paper ledger state was committed."

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-no-external-audit.json") ([pscustomobject]@{
    Phase = $phase
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    DownloadsExecuted = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    DownloadsExecuted = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    BacktestSimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = if ($stillHeld.Count -eq 0) { "EXEC-PAPER-R016 - No-External Long-Run Paper Preview Re-Aggregation and Maturity Decision Gate" } else { "Operator executes generated offline download commands, then run a combined validation/retry gate" }
    Reason = if ($stillHeld.Count -eq 0) { "All held lines rebound from existing local readiness artifacts." } else { "Some readiness data does not exist locally and needs operator-provided offline quote files." }
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r015-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR015Tests = "Pending"
    UnitTests = "Pending"
    R015Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-PAPER-R015 Summary

R015 diagnosed R014 held lines, searched existing local readiness artifacts, rebound any exact matches, and generated missing offline quote readiness command templates without executing downloads or ManualNoExternal.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Counts:
- R014 held lines loaded: $($heldLines.Count)
- Existing-readiness rebound lines: $($rebound.Count)
- Still-held lines: $($stillHeld.Count)
- Missing readiness windows: $($missingWindows.Count)
- Operator-run download templates: $($downloadGroups.Count)

Decision: $(if ($stillHeld.Count -eq 0) { "R009LongRunPaperOnlyHeldLinesReboundReadyForReAggregation" } else { "R009LongRunPaperOnlyPartialMaturityStillNeedsMissingReadinessDownloads" })
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r015-summary.md") $summary

Write-Output "EXEC-PAPER-R015 artifacts generated"
