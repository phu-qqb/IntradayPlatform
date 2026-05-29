param(
    [string]$RecordsDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/real",
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expected = [ordered]@{
    GBPUSD = "GBP/USD"
    EURGBP = "EUR/GBP"
    USDJPY = "USD/JPY"
    AUDUSD = "AUD/USD"
}

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Add-Issue([string]$Severity, [string]$Code, [string]$Message) {
    $script:issues += [ordered]@{ severity = $Severity; code = $Code; message = $Message }
}

function Test-Sensitive([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"
}

function Test-AuthorizationLanguage([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"
}

function Test-Placeholder([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
    return $Value -match "(?i)^(PHASE6C-|PHASE6D-|TBD)|<REAL_DEMO_SECURITY_ID>"
}

Write-Host "LMAX read-only SecurityID planning value apply"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$issues = @()
$resolvedRecordsDirectory = Resolve-LocalPath $RecordsDirectory
if (-not (Test-Path -LiteralPath $resolvedRecordsDirectory)) {
    Add-Issue "Error" "RecordsDirectoryMissing" "Records directory not found: $resolvedRecordsDirectory"
}

$recordFiles = if (Test-Path -LiteralPath $resolvedRecordsDirectory) {
    @(Get-ChildItem -LiteralPath $resolvedRecordsDirectory -Filter "*.json" | Select-Object -ExpandProperty FullName)
} else {
    @()
}

$records = @()
foreach ($path in $recordFiles) {
    try {
        $record = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $record | Add-Member -NotePropertyName sourcePath -NotePropertyValue $path -Force
        $records += $record
    } catch {
        Add-Issue "Error" "RecordReadFailed" "Could not read ${path}: $($_.Exception.Message)"
    }
}

foreach ($record in $records) {
    $combined = @($record.recordId, $record.symbol, $record.slashSymbol, $record.proposedSecurityId, $record.evidenceSourceType, $record.evidenceReference, $record.reviewedBy, $record.reviewReason, $record.notes) -join " "
    if (Test-Sensitive $combined) { Add-Issue "Error" "SensitiveContentDetected" "$($record.symbol) record contains credential-shaped or sensitive content." }
    if (Test-AuthorizationLanguage $combined) { Add-Issue "Error" "TradingAuthorizationImplied" "$($record.symbol) record contains order/trading/external-run/Production/UAT authorization language." }
    if ([bool]$record.isApprovedForExternalRun) { Add-Issue "Error" "ExternalRunApprovalForbidden" "$($record.symbol) record has IsApprovedForExternalRun=true." }
}

$acceptedBySymbol = [ordered]@{}
foreach ($symbol in $expected.Keys) {
    $accepted = @($records | Where-Object { [string]$_.symbol -eq $symbol -and [string]$_.decision -eq "AcceptedForPlanning" })
    if ($accepted.Count -eq 0) {
        Add-Issue "Error" "AcceptedRecordMissing" "$symbol does not have an AcceptedForPlanning confirmation record."
        continue
    }
    $ids = @($accepted | ForEach-Object { [string]$_.proposedSecurityId } | Sort-Object -Unique)
    if ($ids.Count -gt 1) {
        Add-Issue "Error" "ConflictingAcceptedRecords" "$symbol has conflicting accepted SecurityIDs: $($ids -join ', ')"
        continue
    }
    $record = @($accepted | Sort-Object -Property reviewedAtUtc,createdAtUtc -Descending)[0]
    if ([string]$record.slashSymbol -ne $expected[$symbol]) { Add-Issue "Error" "SlashSymbolMismatch" "$symbol slash symbol must be $($expected[$symbol])." }
    if (Test-Placeholder ([string]$record.proposedSecurityId)) { Add-Issue "Error" "PlaceholderSecurityIdNotAccepted" "$symbol accepted record has a placeholder SecurityID." }
    if ([string]$record.confidence -notin @("High", "Confirmed")) { Add-Issue "Error" "ConfidenceTooLow" "$symbol accepted record requires High or Confirmed confidence." }
    if (-not [bool]$record.noSensitiveContent) { Add-Issue "Error" "NoSensitiveContentFalse" "$symbol accepted record must assert noSensitiveContent=true." }
    $acceptedBySymbol[$symbol] = $record
}

$failed = @($issues | Where-Object { $_.severity -eq "Error" })
if ($failed.Count -gt 0) {
    Write-Host "FinalDecision: FAIL"
    foreach ($issue in $issues) {
        Write-Host ("{0}: {1} - {2}" -f $issue.severity, $issue.code, $issue.message)
    }
    exit 1
}

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$manifestId = "lmax-readonly-securityid-planning-manifest-$stamp"
$instruments = @()
foreach ($symbol in $expected.Keys) {
    $record = $acceptedBySymbol[$symbol]
    $instruments += [ordered]@{
        symbol = $symbol
        slashSymbol = $expected[$symbol]
        planningSecurityId = [string]$record.proposedSecurityId
        securityIdSource = "8"
        evidenceSource = [string]$record.evidenceSourceType
        evidenceReference = [string]$record.evidenceReference
        confirmationRecordId = [string]$record.recordId
        confirmationRecordPath = [string]$record.sourcePath
        decision = "AcceptedForPlanning"
        isApprovedForExternalRun = $false
        environmentName = "Demo"
        venueProfileName = "DemoLondon"
        noSensitiveContent = $true
    }
}

$manifest = [ordered]@{
    manifestId = $manifestId
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    instruments = $instruments
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
    noSensitiveContent = $true
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$manifestPath = Join-Path $outDir "$manifestId.json"
if ((Test-Path -LiteralPath $manifestPath) -and -not $Force.IsPresent) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Error: Output file exists. Use -Force to overwrite: $manifestPath"
    exit 1
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "FinalDecision: PASS"
Write-Host "InstrumentCount: $($expected.Count)"
Write-Host "AppliedCount: $($instruments.Count)"
foreach ($instrument in $instruments) {
    Write-Host ("{0}: {1}" -f $instrument.symbol, $instrument.planningSecurityId)
}
Write-Host "IsApprovedForExternalRun: false"
Write-Host "PlanningManifest: $manifestPath"
