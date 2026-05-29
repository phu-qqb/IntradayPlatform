param(
    [string[]]$RecordFile = @(),
    [string]$RecordsDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/real",
    [switch]$IncludeTemplates
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$allowlist = [ordered]@{
    GBPUSD = "GBP/USD"
    USDJPY = "USD/JPY"
    EURGBP = "EUR/GBP"
    AUDUSD = "AUD/USD"
}
$issues = @()
$invalidRecordPaths = @{}

function Add-Issue([string]$Severity, [string]$Code, [string]$Message, [string]$Symbol = "") {
    $script:issues += [ordered]@{
        severity = $Severity
        code = $Code
        symbol = $Symbol
        message = $Message
    }
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Test-Sensitive([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=)"
}

function Test-AuthorizationLanguage([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"
}

function Add-InvalidRecord([string]$Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        $script:invalidRecordPaths[$Path] = $true
    }
}

function Test-Record($record, [string]$sourcePath) {
    $symbol = [string]$record.symbol
    if ([string]::IsNullOrWhiteSpace($symbol)) {
        Add-Issue "Error" "SymbolRequired" "Record $sourcePath is missing symbol."
        Add-InvalidRecord $sourcePath
        return
    }

    $symbol = $symbol.ToUpperInvariant()
    $recordFailed = $false
    if (-not $allowlist.Contains($symbol)) { Add-Issue "Error" "UnknownSymbol" "$symbol is not in the Phase 6B allowlist." $symbol; $recordFailed = $true }
    elseif ([string]$record.slashSymbol -ne $allowlist[$symbol]) { Add-Issue "Error" "SlashSymbolMismatch" "$symbol slash symbol must be $($allowlist[$symbol])." $symbol; $recordFailed = $true }
    if ([string]::IsNullOrWhiteSpace([string]$record.proposedSecurityId)) { Add-Issue "Error" "ProposedSecurityIdRequired" "$symbol proposedSecurityId is required." $symbol; $recordFailed = $true }
    if ([string]::IsNullOrWhiteSpace([string]$record.evidenceSourceType)) { Add-Issue "Error" "EvidenceSourceTypeRequired" "$symbol evidenceSourceType is required." $symbol; $recordFailed = $true }
    if ([string]::IsNullOrWhiteSpace([string]$record.evidenceReference)) { Add-Issue "Error" "EvidenceReferenceRequired" "$symbol evidenceReference is required." $symbol; $recordFailed = $true }
    if ([string]::IsNullOrWhiteSpace([string]$record.capturedBy)) { Add-Issue "Error" "CapturedByRequired" "$symbol capturedBy is required." $symbol; $recordFailed = $true }
    if ([bool]$record.isApprovedForExternalRun) { Add-Issue "Error" "ExternalRunApprovalForbidden" "$symbol must keep IsApprovedForExternalRun=false." $symbol; $recordFailed = $true }
    if (-not [bool]$record.noSensitiveContent) { Add-Issue "Error" "SensitiveContentFlagFalse" "$symbol must keep noSensitiveContent=true." $symbol; $recordFailed = $true }

    $combined = @($record.recordId, $record.symbol, $record.slashSymbol, $record.proposedSecurityId, $record.evidenceReference, $record.capturedBy, $record.reviewedBy, $record.reviewReason, $record.notes) -join " "
    if (Test-Sensitive $combined) { Add-Issue "Error" "SensitiveContentDetected" "$symbol contains credential-shaped or sensitive content." $symbol; $recordFailed = $true }
    if (Test-AuthorizationLanguage $combined) { Add-Issue "Error" "TradingAuthorizationImplied" "$symbol contains order/trading/external-run/Production/UAT authorization language." $symbol; $recordFailed = $true }

    if ([string]$record.decision -eq "AcceptedForPlanning") {
        if ([string]$record.proposedSecurityId -match "(?i)^(PHASE6C-|PHASE6D-|TBD-)") { Add-Issue "Error" "PlaceholderSecurityIdNotAccepted" "$symbol accepted record uses a placeholder SecurityID." $symbol; $recordFailed = $true }
        if ([string]::IsNullOrWhiteSpace([string]$record.reviewedBy)) { Add-Issue "Error" "ReviewedByRequired" "$symbol accepted record requires reviewedBy." $symbol; $recordFailed = $true }
        if ($null -eq $record.reviewedAtUtc -or [string]::IsNullOrWhiteSpace([string]$record.reviewedAtUtc)) { Add-Issue "Error" "ReviewedAtRequired" "$symbol accepted record requires reviewedAtUtc." $symbol; $recordFailed = $true }
        if ([string]::IsNullOrWhiteSpace([string]$record.reviewReason)) { Add-Issue "Error" "ReviewReasonRequired" "$symbol accepted record requires reviewReason." $symbol; $recordFailed = $true }
        if ([string]$record.confidence -notin @("High", "Confirmed")) { Add-Issue "Error" "ConfidenceTooLow" "$symbol accepted record requires High or Confirmed confidence." $symbol; $recordFailed = $true }
    }

    if ($recordFailed) {
        Add-InvalidRecord $sourcePath
    }
}

Write-Host "LMAX read-only SecurityID confirmation records review"
Write-Host "Local-only. No LMAX connection, no external API, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$paths = @()
if ($RecordFile.Count -gt 0) {
    $paths = @($RecordFile | ForEach-Object { Resolve-LocalPath $_ })
} else {
    $resolvedDir = Resolve-LocalPath $RecordsDirectory
    if (Test-Path -LiteralPath $resolvedDir) {
        $paths = @(Get-ChildItem -LiteralPath $resolvedDir -Filter "*.json" | Where-Object {
            $IncludeTemplates.IsPresent -or $_.FullName -notmatch "\\templates\\"
        } | Select-Object -ExpandProperty FullName)
    }
}

$records = @()
foreach ($path in $paths) {
    try {
        $record = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $record | Add-Member -NotePropertyName sourcePath -NotePropertyValue $path -Force
        $records += $record
        Test-Record $record $path
    } catch {
        Add-Issue "Error" "RecordReadFailed" "Could not read ${path}: $($_.Exception.Message)"
        Add-InvalidRecord $path
    }
}

foreach ($symbol in $allowlist.Keys) {
    $accepted = @($records | Where-Object { [string]$_.symbol -eq $symbol -and [string]$_.decision -eq "AcceptedForPlanning" })
    if ($accepted.Count -eq 0) {
        Add-Issue "Warning" "AcceptedRecordMissing" "$symbol does not yet have an AcceptedForPlanning confirmation record." $symbol
    }
    $ids = @($accepted | ForEach-Object { [string]$_.proposedSecurityId } | Sort-Object -Unique)
    if ($ids.Count -gt 1) {
        Add-Issue "Error" "ConflictingProposedSecurityIds" "$symbol has conflicting accepted proposed SecurityIDs." $symbol
    }
}

$perInstrument = @()
foreach ($symbol in ($allowlist.Keys | Sort-Object)) {
    $symbolRecords = @($records | Where-Object { [string]$_.symbol -eq $symbol })
    $accepted = @($symbolRecords | Where-Object { [string]$_.decision -eq "AcceptedForPlanning" } | Sort-Object -Property reviewedAtUtc,createdAtUtc -Descending)
    $pending = @($symbolRecords | Where-Object { [string]$_.decision -in @("Draft", "NeedsMoreEvidence") })
    $rejected = @($symbolRecords | Where-Object { [string]$_.decision -eq "Rejected" })
    $conflictingIds = @($accepted | ForEach-Object { [string]$_.proposedSecurityId } | Sort-Object -Unique)
    $latestAccepted = if ($accepted.Count -gt 0) { $accepted[0] } else { $null }
    $status = if ($conflictingIds.Count -gt 1) {
        "conflicting"
    } elseif ($accepted.Count -gt 0) {
        "accepted"
    } elseif ($pending.Count -gt 0) {
        "pending"
    } elseif ($rejected.Count -gt 0) {
        "rejected"
    } else {
        "missing"
    }

    $perInstrument += [ordered]@{
        symbol = $symbol
        slashSymbol = $allowlist[$symbol]
        status = $status
        recordCount = $symbolRecords.Count
        acceptedForPlanningCount = $accepted.Count
        pendingCount = $pending.Count
        rejectedCount = $rejected.Count
        hasConflictingAcceptedSecurityIds = $conflictingIds.Count -gt 1
        latestAcceptedProposedSecurityId = if ($null -ne $latestAccepted) { [string]$latestAccepted.proposedSecurityId } else { $null }
        confidence = if ($null -ne $latestAccepted) { [string]$latestAccepted.confidence } else { $null }
        evidenceSourceType = if ($null -ne $latestAccepted) { [string]$latestAccepted.evidenceSourceType } else { $null }
        evidenceReference = if ($null -ne $latestAccepted) { [string]$latestAccepted.evidenceReference } else { $null }
        reviewedBy = if ($null -ne $latestAccepted) { [string]$latestAccepted.reviewedBy } else { $null }
        reviewedAtUtc = if ($null -ne $latestAccepted) { [string]$latestAccepted.reviewedAtUtc } else { $null }
        isApprovedForExternalRun = $false
    }
}

$errors = @($issues | Where-Object { $_.severity -eq "Error" })
$warnings = @($issues | Where-Object { $_.severity -eq "Warning" })
$decision = if ($errors.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6h-securityid-confirmation-records-review.json"
$legacyReportPath = Join-Path $reportDir "phase6f-securityid-confirmation-records-review.json"
$report = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    recordsDirectory = if ($RecordFile.Count -gt 0) { $null } else { Resolve-LocalPath $RecordsDirectory }
    totalRecordCount = $records.Count
    acceptedForPlanningCount = @($records | Where-Object { [string]$_.decision -eq "AcceptedForPlanning" }).Count
    missingInstrumentCount = @($perInstrument | Where-Object { $_.status -eq "missing" }).Count
    conflictCount = @($perInstrument | Where-Object { $_.hasConflictingAcceptedSecurityIds }).Count
    invalidRecordCount = $invalidRecordPaths.Count
    perInstrument = $perInstrument
    issues = $issues
    isApprovedForExternalRun = $false
    externalConnectionAttempted = $false
    externalApiCallsAttempted = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $legacyReportPath -Encoding UTF8

Write-Host "FinalDecision: $decision"
Write-Host "TotalRecordCount: $($records.Count)"
Write-Host "AcceptedForPlanningCount: $($report.acceptedForPlanningCount)"
Write-Host "MissingInstrumentCount: $($report.missingInstrumentCount)"
Write-Host "ConflictCount: $($report.conflictCount)"
Write-Host "InvalidRecordCount: $($report.invalidRecordCount)"
Write-Host ""
Write-Host "Instrument summary:"
foreach ($row in $perInstrument) {
    Write-Host ("{0}: status={1}; records={2}; accepted={3}; pending={4}; latestAcceptedSecurityId={5}; confidence={6}; evidence={7}; reviewedBy={8}; reviewedAtUtc={9}; isApprovedForExternalRun={10}" -f $row.symbol, $row.status, $row.recordCount, $row.acceptedForPlanningCount, $row.pendingCount, $row.latestAcceptedProposedSecurityId, $row.confidence, $row.evidenceSourceType, $row.reviewedBy, $row.reviewedAtUtc, $row.isApprovedForExternalRun)
}
if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "Issues:"
    foreach ($issue in $issues) {
        Write-Host ("{0}: {1} - {2}" -f $issue.severity, $issue.code, $issue.message)
    }
}
Write-Host "Report: $reportPath"
if ($decision -eq "FAIL") { exit 1 }
