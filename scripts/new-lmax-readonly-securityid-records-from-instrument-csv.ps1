param(
    [Parameter(Mandatory = $true)]
    [string]$InstrumentCsvFile,
    [string]$SecondaryCsvFile = "",
    [ValidateSet("DemoLondon")]
    [string]$VenueProfileName = "DemoLondon",
    [Parameter(Mandatory = $true)]
    [string]$CapturedBy,
    [Parameter(Mandatory = $true)]
    [string]$ReviewedBy,
    [Parameter(Mandatory = $true)]
    [string]$ReviewReason,
    [switch]$ConfirmPlanningOnly,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/real",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expected = [ordered]@{
    GBPUSD = @{ slashSymbol = "GBP/USD"; securityId = "4002" }
    EURGBP = @{ slashSymbol = "EUR/GBP"; securityId = "4003" }
    USDJPY = @{ slashSymbol = "USD/JPY"; securityId = "4004" }
    AUDUSD = @{ slashSymbol = "AUD/USD"; securityId = "4007" }
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Add-Issue([string]$Severity, [string]$Code, [string]$Message) {
    $script:issues += [ordered]@{ severity = $Severity; code = $Code; message = $Message }
}

function Normalize-Symbol([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return $Value.Replace("/", "").Replace(" ", "").ToUpperInvariant()
}

function Test-Sensitive([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"
}

function Test-AuthorizationLanguage([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"
}

function Get-ColumnValue($Row, [string]$ColumnName) {
    $property = @($Row.PSObject.Properties | Where-Object { $_.Name.Trim() -eq $ColumnName } | Select-Object -First 1)
    if ($property.Count -eq 0) { return "" }
    return [string]$property[0].Value
}

function Read-InstrumentCsv([string]$Path, [string]$SourceName) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Add-Issue "Error" "CsvFileMissing" "CSV file not found: $Path"
        return @()
    }

    $text = Get-Content -Raw -LiteralPath $Path
    if (Test-Sensitive $text) {
        Add-Issue "Error" "SensitiveContentDetected" "CSV source contains credential-shaped or sensitive content: $SourceName"
        return @()
    }

    $rows = @(Import-Csv -LiteralPath $Path)
    if ($rows.Count -eq 0) {
        Add-Issue "Error" "CsvEmpty" "CSV file has no rows: $SourceName"
        return @()
    }

    $required = @("Instrument Name", "LMAX ID", "LMAX symbol")
    $headerNames = @($rows[0].PSObject.Properties.Name | ForEach-Object { $_.Trim() })
    foreach ($column in $required) {
        if (-not ($headerNames -contains $column)) {
            Add-Issue "Error" "RequiredColumnMissing" "$SourceName is missing required column: $column"
        }
    }

    return @($rows | ForEach-Object {
        [ordered]@{
            sourceName = $SourceName
            instrumentName = Get-ColumnValue $_ "Instrument Name"
            lmaxId = Get-ColumnValue $_ "LMAX ID"
            lmaxSymbol = Get-ColumnValue $_ "LMAX symbol"
            isTokyoProfile = (Get-ColumnValue $_ "LMAX ID").StartsWith("600", [StringComparison]::Ordinal)
            isSelectedForVenueProfile = (Get-ColumnValue $_ "LMAX ID").StartsWith("400", [StringComparison]::Ordinal)
        }
    })
}

Write-Host "LMAX read-only SecurityID records from instrument CSV"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

if (-not $ConfirmPlanningOnly.IsPresent) {
    Write-Host "Decision: FAIL"
    Write-Host "Error: -ConfirmPlanningOnly is required. These records are AcceptedForPlanning only and do not authorize external runs."
    exit 1
}

$issues = @()
$primaryPath = Resolve-LocalPath $InstrumentCsvFile
$secondaryPath = Resolve-LocalPath $SecondaryCsvFile
$primaryName = Split-Path -Leaf $primaryPath
$secondaryName = if ([string]::IsNullOrWhiteSpace($SecondaryCsvFile)) { "" } else { Split-Path -Leaf $secondaryPath }

$rows = @()
$rows += Read-InstrumentCsv $primaryPath $primaryName
if (-not [string]::IsNullOrWhiteSpace($SecondaryCsvFile)) {
    $rows += Read-InstrumentCsv $secondaryPath $secondaryName
}

$selected = [ordered]@{}
$tokyo = [ordered]@{}
foreach ($symbol in $expected.Keys) {
    $slash = $expected[$symbol].slashSymbol
    $matches = @($rows | Where-Object {
        (Normalize-Symbol $_.instrumentName) -eq $symbol -or
        (Normalize-Symbol $_.lmaxSymbol) -eq $symbol -or
        (Normalize-Symbol $_.instrumentName) -eq (Normalize-Symbol $slash) -or
        (Normalize-Symbol $_.lmaxSymbol) -eq (Normalize-Symbol $slash)
    })
    $selectedIds = @($matches | Where-Object { $_.isSelectedForVenueProfile } | ForEach-Object { [string]$_.lmaxId } | Sort-Object -Unique)
    $tokyoIds = @($matches | Where-Object { $_.isTokyoProfile } | ForEach-Object { [string]$_.lmaxId } | Sort-Object -Unique)
    $tokyo[$symbol] = $tokyoIds

    if ($matches.Count -eq 0) {
        Add-Issue "Error" "CandidateRowMissing" "$slash was not found in the supplied LMAX instrument CSV source."
        continue
    }
    if ($selectedIds.Count -eq 0) {
        Add-Issue "Error" "SelectedProfileIdMissing" "$slash did not have a DemoLondon/NewYork 400x SecurityID."
        continue
    }
    if ($selectedIds.Count -gt 1) {
        Add-Issue "Error" "ConflictingSelectedProfileIds" "$slash has conflicting DemoLondon/NewYork SecurityIDs: $($selectedIds -join ', ')"
        continue
    }
    if ($selectedIds[0] -ne $expected[$symbol].securityId) {
        Add-Issue "Error" "UnexpectedDemoLondonSecurityId" "$slash expected $($expected[$symbol].securityId) for DemoLondon/NewYork but extracted $($selectedIds[0])."
        continue
    }
    if ($selectedIds[0] -match "(?i)^(PHASE6C-|PHASE6D-|TBD-)") {
        Add-Issue "Error" "PlaceholderSecurityIdNotAccepted" "$slash extracted placeholder SecurityID."
        continue
    }

    $selected[$symbol] = $selectedIds[0]
}

$evidenceReference = if ([string]::IsNullOrWhiteSpace($secondaryName)) {
    "$primaryName, Instrument Name + LMAX ID + LMAX symbol columns"
} else {
    "$primaryName / $secondaryName, Instrument Name + LMAX ID + LMAX symbol columns"
}
$notes = "Tokyo 600x IDs intentionally not selected for current DemoLondon profile."
$combined = @($CapturedBy, $ReviewedBy, $ReviewReason, $evidenceReference, $notes) -join " "
if (Test-Sensitive $combined) { Add-Issue "Error" "SensitiveContentDetected" "Record metadata contains credential-shaped or sensitive content." }
if (Test-AuthorizationLanguage $combined) { Add-Issue "Error" "TradingAuthorizationImplied" "Record metadata must not imply order, trading, external run, Production, UAT, or execution authorization." }

$failed = @($issues | Where-Object { $_.severity -eq "Error" })
if ($failed.Count -gt 0) {
    Write-Host "Decision: FAIL"
    foreach ($issue in $issues) {
        Write-Host ("{0}: {1} - {2}" -f $issue.severity, $issue.code, $issue.message)
    }
    exit 1
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$recordPaths = @()
foreach ($symbol in $expected.Keys) {
    $recordId = "lmax-readonly-securityid-confirmation-$symbol-$timestamp"
    $outPath = Join-Path $outDir "$recordId.json"
    if ((Test-Path -LiteralPath $outPath) -and -not $Force.IsPresent) {
        Write-Host "Decision: FAIL"
        Write-Host "Error: Output file already exists. Use -Force to overwrite: $outPath"
        exit 1
    }

    $record = [ordered]@{
        recordId = $recordId
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        symbol = $symbol
        slashSymbol = $expected[$symbol].slashSymbol
        proposedSecurityId = $selected[$symbol]
        evidenceSourceType = "OfficialLmaxDocument"
        evidenceReference = $evidenceReference
        capturedBy = $CapturedBy
        reviewedBy = $ReviewedBy
        reviewedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        reviewReason = $ReviewReason
        confidence = "Confirmed"
        decision = "AcceptedForPlanning"
        isApprovedForExternalRun = $false
        noSensitiveContent = $true
        notes = $notes
        externalConnectionAttempted = $false
        externalApiCallsAttempted = $false
        securityListRequestAttempted = $false
        marketDataSnapshotAttempted = $false
        replayAttempted = $false
        runtimeShadowReplaySubmit = $false
        schedulerOrPollingAdded = $false
        orderSubmissionAdded = $false
        gatewayRegistrationAdded = $false
        tradingMutationAdded = $false
    }

    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outPath -Encoding UTF8
    $recordPaths += $outPath
}

$summaryDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $summaryDir -Force | Out-Null
$summaryPath = Join-Path $summaryDir "phase6m-csv-securityid-record-generation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = "PASS"
    venueProfileName = $VenueProfileName
    primaryCsvFile = $primaryPath
    secondaryCsvFile = if ([string]::IsNullOrWhiteSpace($SecondaryCsvFile)) { $null } else { $secondaryPath }
    selectedValues = $selected
    tokyoIdsObservedButNotSelected = $tokyo
    recordPaths = $recordPaths
    isApprovedForExternalRun = $false
    externalConnectionAttempted = $false
    externalApiCallAttempted = $false
    securityListRequestAttempted = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Decision: PASS"
foreach ($symbol in $selected.Keys) {
    Write-Host ("{0}: {1}" -f $symbol, $selected[$symbol])
}
Write-Host "IsApprovedForExternalRun: false"
Write-Host "Records:"
foreach ($path in $recordPaths) { Write-Host $path }
Write-Host "Summary: $summaryPath"
