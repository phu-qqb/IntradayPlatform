$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-risk-review-r011"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$exposure = Read-Json "risk-exposure-review.json"
$symbols = Read-Json "symbol-execution-universe-risk-review.json"
$warnings = Read-Json "quantity-warning-risk-treatment.json"
$risk = Read-Json "risk-policy-decision.json"
$approval = Read-Json "operator-approval-readiness.json"
$contract = Read-Json "contract-status-update.json"

Assert ($exposure.ExposureBounded -eq $true) "Risk review should accept bounded sandbox exposure."
Assert ($warnings.Classification -eq "QUANTITY_WARNINGS_ACCEPTED_WITH_OPERATOR_DISCLOSURE_REQUIRED") "Below-min omitted exposure must require disclosure."
Assert ($symbols.NoDirectCrosses -eq $true) "Direct crosses must be absent."
Assert ($symbols.USDJPYNotEmittedByCore -eq $true) "USDJPY Core emission must be blocked."
Assert ($symbols.JPYUSDCaveatPreserved -eq $true) "JPYUSD caveat must be preserved."
Assert ($risk.R009SubmissionAllowedNow -eq $false) "R009 must remain blocked after risk review."
Assert ($risk.RequiresOperatorApprovalBeforeExecution -eq $true) "Operator approval must be required next."
Assert ($approval.Classification -eq "OPERATOR_APPROVAL_READY_WITH_WARNINGS") "Operator approval should be ready with warnings."
Assert ($contract.Statuses."pms-core-execution-candidate.v1" -eq "BLOCKED") "Execution candidate must remain blocked."

Write-Host "CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011_FOCUSED_TEST_PASS"
