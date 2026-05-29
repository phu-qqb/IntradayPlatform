param(
    [Parameter(Mandatory = $true)]
    [string]$Symbol,
    [Parameter(Mandatory = $true)]
    [string]$SlashSymbol,
    [Parameter(Mandatory = $true)]
    [string]$ProposedSecurityId,
    [Parameter(Mandatory = $true)]
    [ValidateSet("OfficialLmaxDocument", "ConnectivityLabSanitizedOutput", "OperatorManualConfirmation", "VendorSupportConfirmation", "Other")]
    [string]$EvidenceSourceType,
    [Parameter(Mandatory = $true)]
    [string]$EvidenceReference,
    [Parameter(Mandatory = $true)]
    [string]$CapturedBy,
    [string]$ReviewedBy = "",
    [string]$ReviewReason = "",
    [ValidateSet("Low", "Medium", "High", "Confirmed")]
    [string]$Confidence = "Low",
    [ValidateSet("Draft", "AcceptedForPlanning", "Rejected", "NeedsMoreEvidence")]
    [string]$Decision = "Draft",
    [string]$Notes = "",
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/real",
    [string]$OutputFile = "",
    [switch]$WhatIfPreview,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$allowlist = @{
    GBPUSD = "GBP/USD"
    USDJPY = "USD/JPY"
    EURGBP = "EUR/GBP"
    AUDUSD = "AUD/USD"
}

function Add-Issue([string]$Severity, [string]$Code, [string]$Message) {
    $script:issues += [ordered]@{
        severity = $Severity
        code = $Code
        message = $Message
    }
}

function Test-Sensitive([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=)"
}

function Test-AuthorizationLanguage([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"
}

Write-Host "LMAX read-only SecurityID confirmation record creator"
Write-Host "Local-only. No LMAX connection, no external API, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
Write-Host "Example:"
Write-Host ".\scripts\new-lmax-readonly-securityid-confirmation-record.ps1 -Symbol GBPUSD -SlashSymbol `"GBP/USD`" -ProposedSecurityId `"<sanitized-demo-security-id>`" -EvidenceSourceType OperatorManualConfirmation -EvidenceReference `"<sanitized-reference>`" -CapturedBy `"local-operator`" -ReviewedBy `"local-reviewer`" -ReviewReason `"Planning confirmation only`" -Confidence High -Decision AcceptedForPlanning"

$issues = @()
$normalizedSymbol = $Symbol.ToUpperInvariant()

if (-not $allowlist.ContainsKey($normalizedSymbol)) {
    Add-Issue "Error" "UnknownSymbol" "$normalizedSymbol is not in the Phase 6B allowlist."
} elseif ($allowlist[$normalizedSymbol] -ne $SlashSymbol) {
    Add-Issue "Error" "SlashSymbolMismatch" "$normalizedSymbol slash symbol must be $($allowlist[$normalizedSymbol])."
}

if ([string]::IsNullOrWhiteSpace($ProposedSecurityId)) { Add-Issue "Error" "ProposedSecurityIdRequired" "ProposedSecurityId is required." }
if ([string]::IsNullOrWhiteSpace($EvidenceReference)) { Add-Issue "Error" "EvidenceReferenceRequired" "EvidenceReference is required." }
if ([string]::IsNullOrWhiteSpace($CapturedBy)) { Add-Issue "Error" "CapturedByRequired" "CapturedBy is required." }

if ($Decision -eq "AcceptedForPlanning") {
    if ($ProposedSecurityId -match "(?i)^(PHASE6C-|PHASE6D-|TBD-)") { Add-Issue "Error" "PlaceholderSecurityIdNotAccepted" "AcceptedForPlanning cannot use PHASE6C, PHASE6D, or TBD placeholder SecurityIDs." }
    if ([string]::IsNullOrWhiteSpace($ReviewedBy)) { Add-Issue "Error" "ReviewedByRequired" "AcceptedForPlanning requires ReviewedBy." }
    if ([string]::IsNullOrWhiteSpace($ReviewReason)) { Add-Issue "Error" "ReviewReasonRequired" "AcceptedForPlanning requires ReviewReason." }
    if ($Confidence -notin @("High", "Confirmed")) { Add-Issue "Error" "ConfidenceTooLow" "AcceptedForPlanning requires High or Confirmed confidence." }
} elseif ($ProposedSecurityId -match "(?i)^(PHASE6C-|PHASE6D-|TBD-)" -or [string]::IsNullOrWhiteSpace($ProposedSecurityId)) {
    Add-Issue "Warning" "PlaceholderAllowedForNonAcceptedRecord" "Draft/NeedsMoreEvidence records may use blank or placeholder SecurityIDs; they are not accepted for planning."
}

$combined = @($Symbol, $SlashSymbol, $ProposedSecurityId, $EvidenceReference, $CapturedBy, $ReviewedBy, $ReviewReason, $Notes) -join " "
if (Test-Sensitive $combined) { Add-Issue "Error" "SensitiveContentDetected" "Record contains credential-shaped or sensitive content." }
if (Test-AuthorizationLanguage $combined) { Add-Issue "Error" "TradingAuthorizationImplied" "Record must not imply order, trading, external run, Production, UAT, or execution authorization." }

$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$recordId = "lmax-readonly-securityid-confirmation-$normalizedSymbol-$timestamp"
$record = [ordered]@{
    recordId = $recordId
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    symbol = $normalizedSymbol
    slashSymbol = $SlashSymbol
    proposedSecurityId = $ProposedSecurityId
    evidenceSourceType = $EvidenceSourceType
    evidenceReference = $EvidenceReference
    capturedBy = $CapturedBy
    reviewedBy = $ReviewedBy
    reviewedAtUtc = if ($Decision -eq "AcceptedForPlanning") { [DateTimeOffset]::UtcNow.ToString("o") } else { $null }
    reviewReason = $ReviewReason
    confidence = $Confidence
    decision = $Decision
    isApprovedForExternalRun = $false
    noSensitiveContent = $true
    notes = $Notes
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

$failed = @($issues | Where-Object { $_.severity -eq "Error" })
$warnings = @($issues | Where-Object { $_.severity -eq "Warning" })
$decisionOut = if ($failed.Count -gt 0) { "FAIL" } else { "PASS" }

if ($decisionOut -eq "PASS") {
    if ($WhatIfPreview.IsPresent) {
        Write-Host "Decision: PASS"
        if ($warnings.Count -gt 0) {
            foreach ($issue in $warnings) {
                Write-Host ("{0}: {1} - {2}" -f $issue.severity, $issue.code, $issue.message)
            }
        }
        Write-Host "WhatIfPreview: no file written."
        $record | ConvertTo-Json -Depth 10
        return
    }

    if ([string]::IsNullOrWhiteSpace($OutputFile)) {
        $outDir = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        $outPath = Join-Path $outDir "$recordId.json"
    } else {
        $outPath = if ([IO.Path]::IsPathRooted($OutputFile)) { $OutputFile } else { Join-Path $repoRoot $OutputFile }
        $outDir = Split-Path -Parent $outPath
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }

    if ((Test-Path -LiteralPath $outPath) -and -not $Force.IsPresent) {
        Write-Host "Decision: FAIL"
        Write-Host "Error: Output file already exists. Use -Force to overwrite: $outPath"
        exit 1
    }

    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outPath -Encoding UTF8
    Write-Host "Decision: PASS"
    if ($warnings.Count -gt 0) {
        foreach ($issue in $warnings) {
            Write-Host ("{0}: {1} - {2}" -f $issue.severity, $issue.code, $issue.message)
        }
    }
    Write-Host "Record: $outPath"
} else {
    Write-Host "Decision: FAIL"
    foreach ($issue in $issues) {
        Write-Host ("{0}: {1} - {2}" -f $issue.severity, $issue.code, $issue.message)
    }
    exit 1
}
