$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-refinement-r010"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Dec($Value) {
    [decimal]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

$impact = Read-Json "below-min-exposure-impact.json"
$policy = Read-Json "de-minimis-zeroing-policy-decision.json"
$candidate = Read-Json "refined-pms-core-candidate.json"
$risk = Read-Json "risk-review-readiness-decision.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert ((Dec $impact.TotalOmittedNotionalUsd) -eq 601.92) "Omitted notional should be USD 601.92."
Assert ((Dec $impact.TotalOmittedPercentageOfUsd6000000) -lt 0.05) "Omitted share should be under sandbox tolerance."
Assert ($policy.Classification -eq "ZEROING_ACCEPTED_WITH_WARNINGS_REQUIRES_RISK_REVIEW_ATTENTION") "Zeroing should be accepted with warnings."
Assert ($policy.SourceOfTolerance -match "SandboxPreviewSizingOnly") "Tolerance must be sandbox-preview only."
Assert ($candidate.RiskReviewReady -eq $true) "Candidate should be ready for risk review with warnings."
Assert ($candidate.R009Ready -eq $false -and $candidate.ExecutionReadyPreview -eq $false) "Candidate must not be execution-ready."
Assert ($risk.Classification -eq "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_QUANTITY_WARNINGS") "Risk review readiness classification mismatch."
Assert ($boundary.NoR009 -and $boundary.NoDbMutation -and $boundary.NoLedger -and $boundary.NoLmax) "Boundary safety failed."
Assert ($boundary.NoR010PrototypeApprovalTransfer) "Old R010 approval must not transfer."

Write-Host "CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010_FOCUSED_TEST_PASS"
