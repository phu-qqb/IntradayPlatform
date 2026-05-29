$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-risk-review-r011"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "r010-intake-validation.json",
    "risk-candidate-identity-lineage.json",
    "risk-exposure-review.json",
    "symbol-execution-universe-risk-review.json",
    "quantity-warning-risk-treatment.json",
    "risk-policy-decision.json",
    "operator-approval-readiness.json",
    "future-package-decision.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)
foreach ($name in $required) {
    Assert (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required artifact: $name"
}

$intake = Read-Json "r010-intake-validation.json"
$identity = Read-Json "risk-candidate-identity-lineage.json"
$exposure = Read-Json "risk-exposure-review.json"
$symbols = Read-Json "symbol-execution-universe-risk-review.json"
$warnings = Read-Json "quantity-warning-risk-treatment.json"
$risk = Read-Json "risk-policy-decision.json"
$approval = Read-Json "operator-approval-readiness.json"
$future = Read-Json "future-package-decision.json"
$contract = Read-Json "contract-status-update.json"
$impact = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert ($intake.Classification -eq "R010_READY_FOR_RISK_REVIEW") "R010 intake not ready."
Assert ($identity.Classification -eq "RISK_CANDIDATE_IDENTITY_READY") "Risk identity not ready."
Assert ($identity.R010PrototypeApprovalTransferability -eq $false) "R010 prototype approval must not transfer."
Assert ($identity.SandboxOnly -and $identity.NotProduction -and $identity.NotAccounting -and $identity.NotExecuted -and $identity.NotLedgerCommit) "Identity safety flags missing."
Assert ($exposure.Classification -in @("RISK_EXPOSURE_REVIEW_PASS_BOUNDED_SANDBOX_PREVIEW","RISK_EXPOSURE_REVIEW_PASS_WITH_QUANTITY_WARNINGS")) "Exposure review did not pass."
Assert ($exposure.ExposureBounded -eq $true) "Exposure must be bounded."
Assert ([decimal]::Parse([string]$exposure.OmittedPercentageOfSandboxTarget, [Globalization.CultureInfo]::InvariantCulture) -eq 0.010032) "Unexpected omitted percentage."
Assert ($symbols.Classification -eq "SYMBOL_EXECUTION_UNIVERSE_REVIEW_PASS_WITH_JPYUSD_CAVEAT") "Symbol review should pass with JPYUSD caveat."
Assert ($symbols.NoDirectCrosses -and $symbols.USDJPYNotEmittedByCore -and $symbols.JPYUSDCaveatPreserved -and $symbols.NoExecutionIntentCreated) "Symbol universe boundary failed."
Assert ($warnings.Classification -eq "QUANTITY_WARNINGS_ACCEPTED_WITH_OPERATOR_DISCLOSURE_REQUIRED") "Quantity warnings treatment invalid."
Assert ($warnings.FutureOperatorApprovalMustReferenceZeroedSymbols) "Operator disclosure for zeroed symbols required."
Assert ($risk.Classification -eq "RISK_REVIEW_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS") "Risk policy did not pass with warnings."
Assert ($risk.R009SubmissionAllowedNow -eq $false -and $risk.RequiresOperatorApprovalBeforeExecution -eq $true) "Risk policy execution boundary failed."
Assert ($approval.Classification -eq "OPERATOR_APPROVAL_READY_WITH_WARNINGS") "Operator approval readiness invalid."
Assert ($approval.FuturePackageCannotSubmitR009UnlessSeparateExecutionPackage -eq $true) "Future package R009 restriction missing."
Assert ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012") "Unexpected future package decision."
Assert ($contract.Statuses."pms-core-operator-approval.v1" -eq "BLOCKED") "Operator approval must remain blocked until R012."
Assert ($contract.Statuses."pms-core-execution-candidate.v1" -eq "BLOCKED") "Execution candidate must remain blocked."
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 readiness must remain blocked."
Assert ($impact.NoExecutionOccurred -and $impact.NoR009ReadinessGranted -and $impact.NoLedgerReadinessChanged -and $impact.NoProductionReadinessChanged) "Readiness impact crossed boundary."
Assert ($boundary.NoCoreExecution -and $boundary.NoManager -and $boundary.NoAnubis -and $boundary.NoCuda -and $boundary.NoCoreNetting) "Core boundary failed."
Assert ($boundary.NoLmax -and $boundary.NoPolygonMassiveCall -and $boundary.NoExternalMarketDataCall) "External call boundary failed."
Assert ($boundary.NoR009 -and $boundary.NoOrderFillReport -and $boundary.NoDbMutation -and $boundary.NoLedger) "Execution/mutation boundary failed."
Assert ($boundary.NoInventedPrices -and $boundary.NoInventedMetadata -and $boundary.NoInventedQuantities -and $boundary.NoR010PrototypeApprovalTransfer) "Invention/R010 boundary failed."
Assert ($summary -match "CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS") "Summary classification missing."
Assert ($summary -match "Is R009 execution allowed\? no") "Summary must block R009 execution."

Write-Host "CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011_VALIDATOR_PASS"
