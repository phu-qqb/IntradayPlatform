$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-refinement-r010"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "r009-intake-validation.json",
    "quantity-recalculation-verification.json",
    "below-min-exposure-impact.json",
    "de-minimis-zeroing-policy-decision.json",
    "refined-pms-core-candidate.json",
    "exposure-concentration-refreshed.json",
    "risk-review-readiness-decision.json",
    "future-package-decision.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)
foreach ($name in $required) {
    Assert (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required artifact: $name"
}

$intake = Read-Json "r009-intake-validation.json"
$recalc = Read-Json "quantity-recalculation-verification.json"
$impact = Read-Json "below-min-exposure-impact.json"
$policy = Read-Json "de-minimis-zeroing-policy-decision.json"
$candidate = Read-Json "refined-pms-core-candidate.json"
$exposure = Read-Json "exposure-concentration-refreshed.json"
$risk = Read-Json "risk-review-readiness-decision.json"
$future = Read-Json "future-package-decision.json"
$contract = Read-Json "contract-status-update.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert ($intake.Classification -eq "R009_QUANTITY_DERIVATION_READY_FOR_REFINEMENT") "R009 intake not ready."
Assert ($intake.BelowMinSymbolsMatchExpected) "R009 below-min symbols did not match expected."
Assert ($intake.R009DidNotAllowR009ExecutionSubmission -and $intake.R009DidNotMutateDbOrLedger -and $intake.R009DidNotClaimRiskReviewReadiness) "R009 intake boundary failed."
Assert ($recalc.Classification -eq "QUANTITY_RECALCULATION_VERIFIED") "Quantity recalculation failed."
Assert ($impact.Classification -eq "BELOW_MIN_EXPOSURE_IMPACT_READY") "Below-min impact not ready."
Assert ([decimal]::Parse([string]$impact.TotalOmittedNotionalUsd, [Globalization.CultureInfo]::InvariantCulture) -eq 601.92) "Unexpected omitted exposure."
Assert ($policy.Classification -eq "ZEROING_ACCEPTED_WITH_WARNINGS_REQUIRES_RISK_REVIEW_ATTENTION") "Zeroing policy not accepted with warnings."
Assert ($policy.NotAccountingPolicy -and $policy.NotProductionPolicy) "Zeroing policy must be sandbox-only."
Assert ($candidate.Classification -eq "REFINED_PMS_CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_WARNINGS") "Refined candidate not ready with warnings."
Assert ($candidate.RiskReviewReady -eq $true) "Risk review should be ready with warnings."
Assert ($candidate.R009Ready -eq $false -and $candidate.ExecutionReadyPreview -eq $false) "Execution readiness must remain false."
Assert ($candidate.R010PrototypeTransferability -eq $false) "Old R010 prototype approval must not transfer."
Assert ($exposure.Classification -eq "EXPOSURE_CONCENTRATION_READY_WITH_WARNINGS") "Exposure refreshed not ready with warnings."
Assert ($risk.Classification -eq "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_QUANTITY_WARNINGS") "Risk readiness decision invalid."
Assert ($risk.R009SubmissionAllowedInR010 -eq $false -and $risk.R010PrototypeApprovalReusable -eq $false) "Risk decision boundary failed."
Assert ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011") "Unexpected future package."
Assert ($contract.Statuses."pms-core-risk-review.v1" -eq "WITH_WARNINGS") "Risk review contract status should be WITH_WARNINGS."
Assert ($contract.Statuses."pms-execution-candidate.v1" -eq "BLOCKED") "Execution candidate must remain blocked."
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 readiness must remain blocked."
Assert ($readiness.NoExecutionOccurred -and $readiness.NoR009ReadinessGranted -and $readiness.NoLedgerReadinessChanged -and $readiness.NoProductionReadinessChanged) "Readiness impact crossed boundary."
Assert ($boundary.NoCoreExecution -and $boundary.NoManager -and $boundary.NoAnubis -and $boundary.NoCuda -and $boundary.NoCoreNetting) "Core boundary failed."
Assert ($boundary.NoLmax -and $boundary.NoPolygonMassiveCall -and $boundary.NoExternalMarketDataCall) "External call boundary failed."
Assert ($boundary.NoR009 -and $boundary.NoOrderFillReport -and $boundary.NoDbMutation -and $boundary.NoLedger) "Execution/mutation boundary failed."
Assert ($boundary.NoInventedPrices -and $boundary.NoInventedMetadata -and $boundary.NoR010PrototypeApprovalTransfer) "Invention/R010 boundary failed."
Assert ($summary -match "CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010_PASS_READY_FOR_RISK_REVIEW_WITH_WARNINGS") "Summary classification missing."
Assert ($summary -match "Is R009 execution allowed\? no") "Summary must block R009 execution."

Write-Host "CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010_VALIDATOR_PASS"
