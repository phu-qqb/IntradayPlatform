$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "r011-intake-validation.json",
    "exact-approved-candidate-binding.json",
    "quantity-warning-disclosure-statement.json",
    "operator-approval-statement.json",
    "operator-approval-id.json",
    "future-r013-execution-preconditions.json",
    "approval-guardrails.json",
    "readiness-impact.json",
    "contract-status-update.json",
    "boundary-safety-evidence.json",
    "summary.md"
)
foreach ($name in $required) {
    Assert (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required artifact: $name"
}

$intake = Read-Json "r011-intake-validation.json"
$binding = Read-Json "exact-approved-candidate-binding.json"
$disclosure = Read-Json "quantity-warning-disclosure-statement.json"
$statement = Read-Json "operator-approval-statement.json"
$approvalId = Read-Json "operator-approval-id.json"
$preconditions = Read-Json "future-r013-execution-preconditions.json"
$guardrails = Read-Json "approval-guardrails.json"
$impact = Read-Json "readiness-impact.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert ($intake.Classification -eq "R011_READY_FOR_OPERATOR_APPROVAL") "R011 intake not ready."
Assert ($binding.Classification -eq "EXACT_CORE_ANUBIS_CANDIDATE_BOUND_FOR_OPERATOR_APPROVAL") "Candidate binding not exact."
Assert ($binding.ExecuteNow -eq $false) "Binding must not execute now."
Assert ($binding.R010PrototypeTransferability -eq $false) "R010 prototype approval must not transfer."
Assert ($binding.ZeroedBelowMinSymbols -contains "AUDUSD" -and $binding.ZeroedBelowMinSymbols -contains "CHFUSD" -and $binding.ZeroedBelowMinSymbols -contains "EURUSD" -and $binding.ZeroedBelowMinSymbols -contains "GBPUSD") "Zeroed below-min symbols missing."
Assert ([string]$binding.OmittedExposureUsd -eq "601.92") "Omitted exposure mismatch."
Assert ([string]$binding.OmittedExposurePct -eq "0.010032%") "Omitted exposure pct mismatch."
Assert ($binding.JPYUSDCaveat -match "JPYUSD") "JPYUSD caveat missing."
Assert ($disclosure.Classification -eq "QUANTITY_WARNING_DISCLOSURE_READY") "Quantity warning disclosure not ready."
Assert ($disclosure.Disclosures -match "AUDUSD SELL was zeroed below min.") "AUDUSD disclosure missing."
Assert ($disclosure.FutureExecutionApprovalMustPreserveOrReReviewWarnings) "Future approval warning preservation missing."
Assert ($statement.Classification -eq "OPERATOR_APPROVAL_EXPLICIT_FOR_CORE_ANUBIS_FUTURE_SANDBOX_EXECUTION") "Operator approval not explicit."
Assert ($statement.NotImmediateExecution -and $statement.RequiresSeparateExecutionPackage -and $statement.NoExecutionInThisPackage) "Approval must be future-only."
Assert ($statement.ApprovalDecision -eq "APPROVED_FOR_FUTURE_BOUNDED_SANDBOX_EXECUTION_PACKAGE") "Approval decision mismatch."
Assert ($approvalId.Classification -eq "OPERATOR_APPROVAL_ID_CREATED") "Operator approval ID not created."
Assert ($approvalId.OperatorApprovalId -match "^core-anubis-intraday-operator-approval-r012:[A-F0-9]{24}$") "Operator approval ID format invalid."
Assert ($approvalId.NotImmediateExecution -and $approvalId.NotProduction -and $approvalId.NotAccounting -and $approvalId.NotLedger) "Approval ID safety flags missing."
Assert ($preconditions.Classification -eq "FUTURE_R013_PRECONDITIONS_READY") "Future R013 preconditions not ready."
Assert ($preconditions.Preconditions -match "sandbox/demo profile only.") "Sandbox-only precondition missing."
Assert ($preconditions.Preconditions -match "JPYUSD execution inversion plan exists before any execution involving JPYUSD.") "JPYUSD precondition missing."
Assert ($guardrails.Classification -eq "APPROVAL_GUARDRAILS_READY") "Approval guardrails not ready."
Assert ($guardrails.Guardrails -match "Future R013 must not submit zero-quantity lines as orders.") "Zero-quantity guardrail missing."
Assert ($guardrails.Guardrails -match "Future R013 must not use production/live route.") "Production/live guardrail missing."
Assert ($impact.Classification -eq "OPERATOR_APPROVAL_CAPTURED_NO_EXECUTION_READINESS_CHANGE") "Readiness impact invalid."
Assert ($impact.NoExecutionOccurred -and $impact.NoR009SubmissionOccurred -and $impact.NoLedgerReadinessChanged -and $impact.NoProductionReadinessChanged) "Readiness impact crossed boundary."
Assert ($contract.Statuses."pms-core-operator-approval.v1" -eq "YES") "Operator approval status should be YES."
Assert ($contract.R009ExecutionOccurred -eq $false -and $contract.R009SubmissionAllowedNow -eq $false) "R009 execution must remain false."
Assert ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production must remain blocked."
Assert ($boundary.NoCoreExecution -and $boundary.NoManager -and $boundary.NoAnubis -and $boundary.NoCuda -and $boundary.NoCoreNetting) "Core boundary failed."
Assert ($boundary.NoLmax -and $boundary.NoPolygonMassiveCall -and $boundary.NoExternalMarketDataCall) "External call boundary failed."
Assert ($boundary.NoR009 -and $boundary.NoOrderFillReport -and $boundary.NoDbMutation -and $boundary.NoLedger) "Execution/mutation boundary failed."
Assert ($boundary.NoInventedPrices -and $boundary.NoInventedMetadata -and $boundary.NoInventedQuantities -and $boundary.NoR010PrototypeApprovalTransfer) "Invention/R010 boundary failed."
Assert ($summary -match "CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012_PASS_APPROVAL_CAPTURED_FOR_FUTURE_SANDBOX_EXECUTION") "Summary classification missing."
Assert ($summary -match "Is R009 execution allowed now\? no") "Summary must block R009 execution now."

Write-Host "CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012_VALIDATOR_PASS"
