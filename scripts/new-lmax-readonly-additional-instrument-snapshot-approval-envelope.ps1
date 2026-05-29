param(
    [Parameter(Mandatory = $true)][string]$PreflightManifestFile,
    [Parameter(Mandatory = $true)][string]$Symbol,
    [Parameter(Mandatory = $true)][string]$RequestedByOperatorId,
    [string]$ReviewedByOperatorId = "",
    [Parameter(Mandatory = $true)][string]$Reason,
    [ValidateSet("Draft", "AcceptedForPlanning", "Rejected")][string]$Decision = "Draft",
    [switch]$ConfirmAllPlanningAttestations,
    [switch]$ConfirmsDemoOnly,
    [switch]$ConfirmsReadOnlyMarketDataOnly,
    [switch]$ConfirmsNoOrderSubmission,
    [switch]$ConfirmsNoSchedulerOrPolling,
    [switch]$ConfirmsNoRuntimeShadowReplaySubmit,
    [switch]$ConfirmsNoTradingMutation,
    [switch]$ConfirmsSingleInstrumentOnly,
    [switch]$ConfirmsFutureExplicitManualRunRequired,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/approval-envelopes",
    [switch]$WhatIfPreview,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$Path) { if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $repoRoot $Path } }
function Test-Sensitive([string]$Value) { $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)" }
function Test-AuthorizationLanguage([string]$Value) { $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)" }
function New-Check([string]$Name, [bool]$Pass, [string]$Detail) { [ordered]@{ name=$Name; decision=if($Pass){"PASS"}else{"FAIL"}; detail=$Detail } }

Write-Host "LMAX read-only Phase 6Q approval envelope creation"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$preflightPath = Resolve-LocalPath $PreflightManifestFile
if (-not (Test-Path -LiteralPath $preflightPath)) { Write-Host "FinalDecision: FAIL"; Write-Host "Preflight manifest not found: $preflightPath"; exit 1 }
$preflightText = Get-Content -Raw -LiteralPath $preflightPath
if (Test-Sensitive $preflightText) { Write-Host "FinalDecision: FAIL"; Write-Host "Preflight manifest contains sensitive-shaped content."; exit 1 }
$preflight = $preflightText | ConvertFrom-Json
$match = @($preflight.results | Where-Object { [string]$_.symbol -eq $Symbol })
if ($match.Count -ne 1) { Write-Host "FinalDecision: FAIL"; Write-Host "Symbol not found exactly once in preflight manifest: $Symbol"; exit 1 }
$source = $match[0]
$request = @($preflight.requests | Where-Object { [string]$_.symbol -eq $Symbol })[0]

$all = $ConfirmAllPlanningAttestations.IsPresent
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$envelope = [ordered]@{
    approvalEnvelopeId = "lmax-readonly-additional-snapshot-approval-$Symbol-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reviewedByOperatorId = $ReviewedByOperatorId
    reason = $Reason
    symbol = [string]$source.symbol
    slashSymbol = [string]$source.slashSymbol
    planningSecurityId = [string]$source.planningSecurityId
    securityIdSource = "8"
    environmentName = [string]$request.environmentName
    venueProfileName = [string]$request.venueProfileName
    requestMode = [string]$request.requestMode
    symbolEncodingMode = [string]$request.symbolEncodingMode
    marketDepth = [int]$request.marketDepth
    maxRuntimeSeconds = [int]$request.maxRuntimeSeconds
    maxWaitSeconds = [int]$request.maxWaitSeconds
    maxEventsPerRun = [int]$request.maxEventsPerRun
    sourcePreflightManifestPath = $preflightPath
    sourcePreflightDecision = [string]$source.finalDecision
    confirmsDemoOnly = ($all -or $ConfirmsDemoOnly.IsPresent)
    confirmsReadOnlyMarketDataOnly = ($all -or $ConfirmsReadOnlyMarketDataOnly.IsPresent)
    confirmsNoOrderSubmission = ($all -or $ConfirmsNoOrderSubmission.IsPresent)
    confirmsNoSchedulerOrPolling = ($all -or $ConfirmsNoSchedulerOrPolling.IsPresent)
    confirmsNoRuntimeShadowReplaySubmit = ($all -or $ConfirmsNoRuntimeShadowReplaySubmit.IsPresent)
    confirmsNoTradingMutation = ($all -or $ConfirmsNoTradingMutation.IsPresent)
    confirmsSingleInstrumentOnly = ($all -or $ConfirmsSingleInstrumentOnly.IsPresent)
    confirmsFutureExplicitManualRunRequired = ($all -or $ConfirmsFutureExplicitManualRunRequired.IsPresent)
    isApprovedForExternalRun = $false
    eligibleForManualSnapshotAttempt = $false
    canRunExternalSnapshot = $false
    noSensitiveContent = $true
    decision = $Decision
}

$checks = @(
    New-Check "SourcePreflightDecisionPass" ([string]$envelope.sourcePreflightDecision -eq "PASS") "Source preflight decision must be PASS."
    New-Check "SecurityIdSource8" ([string]$envelope.securityIdSource -eq "8") "SecurityIDSource must be 8."
    New-Check "DemoEnvironment" ([string]$envelope.environmentName -eq "Demo") "Environment must be Demo."
    New-Check "DemoLondonVenueProfile" ([string]$envelope.venueProfileName -eq "DemoLondon") "Venue must be DemoLondon."
    New-Check "RequestModeSnapshotPlusUpdates" ([string]$envelope.requestMode -eq "SnapshotPlusUpdates") "RequestMode must be SnapshotPlusUpdates."
    New-Check "SymbolEncodingModeSecurityIdOnly" ([string]$envelope.symbolEncodingMode -eq "SecurityIdOnly") "SymbolEncodingMode must be SecurityIdOnly."
    New-Check "MarketDepthOne" ([int]$envelope.marketDepth -eq 1) "MarketDepth must be 1."
    New-Check "RequestedByRequired" (-not [string]::IsNullOrWhiteSpace($RequestedByOperatorId)) "RequestedByOperatorId required."
    New-Check "ReasonRequired" (-not [string]::IsNullOrWhiteSpace($Reason)) "Reason required."
    New-Check "IsApprovedForExternalRunFalse" (-not [bool]$envelope.isApprovedForExternalRun) "IsApprovedForExternalRun must remain false."
    New-Check "EligibleForManualSnapshotAttemptFalse" (-not [bool]$envelope.eligibleForManualSnapshotAttempt) "eligibleForManualSnapshotAttempt must remain false."
    New-Check "CanRunExternalSnapshotFalse" (-not [bool]$envelope.canRunExternalSnapshot) "canRunExternalSnapshot must remain false."
)
if ($Decision -eq "AcceptedForPlanning") {
    $checks += New-Check "ReviewedByRequired" (-not [string]::IsNullOrWhiteSpace($ReviewedByOperatorId)) "ReviewedByOperatorId required."
    foreach ($a in @("confirmsDemoOnly","confirmsReadOnlyMarketDataOnly","confirmsNoOrderSubmission","confirmsNoSchedulerOrPolling","confirmsNoRuntimeShadowReplaySubmit","confirmsNoTradingMutation","confirmsSingleInstrumentOnly","confirmsFutureExplicitManualRunRequired")) {
        $checks += New-Check $a ([bool]$envelope[$a]) "$a must be true for AcceptedForPlanning."
    }
}
$safe = @(
    $envelope.approvalEnvelopeId
    $envelope.requestedByOperatorId
    $envelope.reviewedByOperatorId
    $envelope.reason
    $envelope.symbol
    $envelope.slashSymbol
    $envelope.planningSecurityId
    $envelope.securityIdSource
    $envelope.environmentName
    $envelope.venueProfileName
    $envelope.requestMode
    $envelope.symbolEncodingMode
    $envelope.sourcePreflightDecision
    $envelope.decision
) -join " "
if (Test-Sensitive $safe) { $checks += New-Check "NoSensitiveContent" $false "Sensitive-shaped content found." }
if (Test-AuthorizationLanguage $safe) { $checks += New-Check "NoTradingOrExternalAuthorizationLanguage" $false "Authorization language found." }
$final = if (@($checks | Where-Object decision -eq "FAIL").Count -gt 0) { "FAIL" } else { "PASS" }
if ($final -eq "FAIL") { Write-Host "FinalDecision: FAIL"; $checks | Where-Object decision -eq "FAIL" | ForEach-Object { Write-Host "$($_.name): $($_.detail)" }; exit 1 }

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "$($envelope.approvalEnvelopeId).json"
if ((Test-Path -LiteralPath $outPath) -and -not $Force.IsPresent) { Write-Host "FinalDecision: FAIL"; Write-Host "Output exists: $outPath"; exit 1 }
if ($WhatIfPreview.IsPresent) {
    Write-Host "WhatIfPreview: true"
    $envelope | ConvertTo-Json -Depth 10
} else {
    $envelope | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outPath -Encoding UTF8
}
Write-Host "FinalDecision: PASS"
Write-Host "Decision: $Decision"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "CanRunExternalSnapshot: false"
if (-not $WhatIfPreview.IsPresent) { Write-Host "ApprovalEnvelope: $outPath" }
