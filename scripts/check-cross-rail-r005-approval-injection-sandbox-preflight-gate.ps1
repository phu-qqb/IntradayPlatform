param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R005 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required file: $Path"
    }

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-True($Value, [string]$Name) {
    if ($true -ne $Value) {
        Fail "$Name must be true"
    }
}

function Assert-False($Value, [string]$Name) {
    if ($true -eq $Value) {
        Fail "$Name must be false"
    }
}

function Assert-Empty($Value, [string]$Name) {
    if ($null -ne $Value -and -not [string]::IsNullOrWhiteSpace([string]$Value)) {
        Fail "$Name must be empty unless resolved with explicit evidence"
    }
}

$artifactRoot = Join-Path $RepoRoot "artifacts\readiness\cross-rail-sandbox-handoff"
$requiredFiles = @(
    "cross-rail-r005-summary.md",
    "cross-rail-r005-r004-reference.json",
    "cross-rail-r005-approval-input-validation.json",
    "cross-rail-r005-risk-approval-record.json",
    "cross-rail-r005-operator-approval-record.json",
    "cross-rail-r005-approved-candidate-set.json",
    "cross-rail-r005-idempotency-preview.json",
    "cross-rail-r005-reconciliation-link-preview.json",
    "cross-rail-r005-pnl-link-preview.json",
    "cross-rail-r005-future-r006-bounded-sandbox-submission-plan.json",
    "cross-rail-r005-missing-input-request.json",
    "cross-rail-r005-sandbox-safety-boundary.json",
    "cross-rail-r005-no-execution-safety-audit.json",
    "cross-rail-r005-build-test-validator-evidence.json",
    "cross-rail-r005-next-gate-plan.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $file))) {
        Fail "Missing required R005 artifact: $file"
    }
}

$r004 = Read-Json (Join-Path $artifactRoot "cross-rail-r005-r004-reference.json")
$inputValidation = Read-Json (Join-Path $artifactRoot "cross-rail-r005-approval-input-validation.json")
$risk = Read-Json (Join-Path $artifactRoot "cross-rail-r005-risk-approval-record.json")
$operator = Read-Json (Join-Path $artifactRoot "cross-rail-r005-operator-approval-record.json")
$candidateSet = Read-Json (Join-Path $artifactRoot "cross-rail-r005-approved-candidate-set.json")
$idempotency = Read-Json (Join-Path $artifactRoot "cross-rail-r005-idempotency-preview.json")
$recon = Read-Json (Join-Path $artifactRoot "cross-rail-r005-reconciliation-link-preview.json")
$pnl = Read-Json (Join-Path $artifactRoot "cross-rail-r005-pnl-link-preview.json")
$futurePlan = Read-Json (Join-Path $artifactRoot "cross-rail-r005-future-r006-bounded-sandbox-submission-plan.json")
$missingRequest = Read-Json (Join-Path $artifactRoot "cross-rail-r005-missing-input-request.json")
$boundary = Read-Json (Join-Path $artifactRoot "cross-rail-r005-sandbox-safety-boundary.json")
$safety = Read-Json (Join-Path $artifactRoot "cross-rail-r005-no-execution-safety-audit.json")

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot "scripts\check-cross-rail-r004-risk-operator-approval-resolution-gate.ps1"))) {
    Fail "R004 validator reference is missing"
}

Assert-True $r004.R004ValidatorPassed "R004ValidatorPassed"
Assert-True $r004.ReadOnlyReview "ReadOnlyReview"
if ($r004.R004Status -ne "BlockedMissingSandboxApprovals") {
    Fail "R004 status must be BlockedMissingSandboxApprovals"
}
if ($r004.R004PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "R004 paper account/profile must be ExistingLmaxDemoProfile"
}
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($r004.R004MissingFields) -notcontains $field) {
        Fail "R004 missing field not preserved: $field"
    }
}

if ($inputValidation.Status -ne "InputsMissing") {
    Fail "Approval inputs must remain missing unless explicit non-placeholder IDs are supplied"
}
Assert-False $inputValidation.OperatorSuppliedRiskReviewId "OperatorSuppliedRiskReviewId"
Assert-False $inputValidation.OperatorSuppliedOperatorApprovalId "OperatorSuppliedOperatorApprovalId"
Assert-True $inputValidation.RiskReviewIdIsPlaceholder "RiskReviewIdIsPlaceholder"
Assert-True $inputValidation.OperatorApprovalIdIsPlaceholder "OperatorApprovalIdIsPlaceholder"
Assert-False $inputValidation.RiskReviewIdAccepted "RiskReviewIdAccepted"
Assert-False $inputValidation.OperatorApprovalIdAccepted "OperatorApprovalIdAccepted"
Assert-True $inputValidation.DoNotInventIds "DoNotInventIds"

if ($risk.Status -ne "MissingRiskApprovalId") {
    Fail "Risk approval must not be resolved without explicit operator input"
}
Assert-Empty $risk.RiskReviewId "RiskReviewId"
if ($risk.Source -ne "Missing") {
    Fail "Risk approval source must be Missing"
}
Assert-True $risk.DoNotInventRiskReviewId "DoNotInventRiskReviewId"

if ($operator.Status -ne "MissingOperatorApprovalId") {
    Fail "Operator approval must not be resolved without explicit operator input"
}
Assert-Empty $operator.OperatorApprovalId "OperatorApprovalId"
if ($operator.Source -ne "Missing") {
    Fail "Operator approval source must be Missing"
}
Assert-True $operator.DoNotInventOperatorApprovalId "DoNotInventOperatorApprovalId"

if ($candidateSet.Status -ne "CandidateSetBlocked") {
    Fail "Candidate set must remain blocked while approvals are missing"
}
if ($candidateSet.PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "Candidate set paper account/profile was not preserved"
}
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($candidateSet.MissingFields) -notcontains $field) {
        Fail "Candidate set missing field not recorded: $field"
    }
}
Assert-True $candidateSet.SandboxOnly "CandidateSet SandboxOnly"
Assert-True $candidateSet.NoLive "CandidateSet NoLive"
Assert-True $candidateSet.NoProduction "CandidateSet NoProduction"
Assert-False $candidateSet.OrdersSubmittedNow "CandidateSet OrdersSubmittedNow"
Assert-False $candidateSet.RoutesCreatedNow "CandidateSet RoutesCreatedNow"
Assert-False $candidateSet.FillsCreatedNow "CandidateSet FillsCreatedNow"
Assert-False $candidateSet.ExecutionAllowedNow "CandidateSet ExecutionAllowedNow"

$expectedSides = @{
    AUDUSD = "SELL"
    EURUSD = "SELL"
    GBPUSD = "BUY"
}
$supported = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
foreach ($row in @($candidateSet.CandidateRows)) {
    if (@($supported) -notcontains [string]$row.Symbol) {
        Fail "Unsupported candidate symbol: $($row.Symbol)"
    }
    if ($expectedSides[[string]$row.Symbol] -ne [string]$row.Side) {
        Fail "Unexpected candidate side for $($row.Symbol)"
    }
    if ([decimal]$row.Quantity -ne 0.1) {
        Fail "Candidate quantity must remain 0.1"
    }
    Assert-False $row.ExecutionAllowedNow "Candidate ExecutionAllowedNow for $($row.Symbol)"
    Assert-False $row.OrdersSubmittedNow "Candidate OrdersSubmittedNow for $($row.Symbol)"
    Assert-False $row.RoutesCreatedNow "Candidate RoutesCreatedNow for $($row.Symbol)"
    Assert-False $row.FillsCreatedNow "Candidate FillsCreatedNow for $($row.Symbol)"
    Assert-True $row.SandboxOnly "Candidate SandboxOnly for $($row.Symbol)"
    Assert-True $row.NoLive "Candidate NoLive for $($row.Symbol)"
    Assert-True $row.NoProduction "Candidate NoProduction for $($row.Symbol)"
}

if ($idempotency.Status -ne "Blocked") {
    Fail "R005 idempotency preview must remain blocked without approval IDs"
}
Assert-True $idempotency.NoActualOrderIdsCreated "NoActualOrderIdsCreated"

if ($recon.Status -ne "Blocked") {
    Fail "Reconciliation preview must remain blocked without approval IDs"
}
Assert-True $recon.NoFillsNow "NoFillsNow"

if ($pnl.Status -ne "Blocked") {
    Fail "PnL preview must remain blocked without approval IDs"
}
Assert-True $pnl.NoPnlNow "NoPnlNow"
Assert-True $pnl.NotPnl "NotPnl"

if ($futurePlan.Status -ne "BlockedPendingMissingApprovals") {
    Fail "Future R006 submission plan must be blocked"
}
if ($futurePlan.FutureR006Readiness -ne "Blocked") {
    Fail "FutureR006Readiness must be Blocked"
}
if ($futurePlan.RequiredPaperAccountId -ne "ExistingLmaxDemoProfile") {
    Fail "Future plan paper account/profile must be ExistingLmaxDemoProfile"
}
Assert-Empty $futurePlan.RequiredRiskReviewId "Future RequiredRiskReviewId"
Assert-Empty $futurePlan.RequiredOperatorApprovalId "Future RequiredOperatorApprovalId"
Assert-False $futurePlan.ExecutionAllowedNow "FuturePlan ExecutionAllowedNow"
Assert-False $futurePlan.OrdersSubmittedNow "FuturePlan OrdersSubmittedNow"
Assert-False $futurePlan.LmaxCallAllowedNow "LmaxCallAllowedNow"
Assert-False $futurePlan.FixSessionAllowedNow "FixSessionAllowedNow"
Assert-True $futurePlan.NoActualOrderIdsCreated "NoActualOrderIdsCreated"
Assert-True $futurePlan.NoRouteIdsCreated "NoRouteIdsCreated"
Assert-True $futurePlan.NoFillIdsCreated "NoFillIdsCreated"

if ($missingRequest.Status -ne "MissingInputsRequired") {
    Fail "Missing input request must be active"
}
Assert-True $missingRequest.DoNotInvent "MissingRequest DoNotInvent"
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($missingRequest.MissingInputs) -notcontains $field) {
        Fail "Missing input request does not include $field"
    }
}
foreach ($placeholder in @("<RISK_REVIEW_ID>", "<OPERATOR_APPROVAL_ID>")) {
    if (@($missingRequest.PlaceholdersRejected) -notcontains $placeholder) {
        Fail "Placeholder rejection not recorded: $placeholder"
    }
}

if ($boundary.Status -ne "SafetyBoundaryPreserved") {
    Fail "Safety boundary must be preserved"
}
Assert-True $boundary.R009Selected "R009Selected"
Assert-True $boundary.SandboxOnly "Boundary SandboxOnly"
Assert-True $boundary.NoLive "Boundary NoLive"
Assert-True $boundary.NoProduction "Boundary NoProduction"
Assert-True $boundary.LmaxSandboxOnly "LmaxSandboxOnly"
Assert-False $boundary.ProductionLiveAllowed "ProductionLiveAllowed"
Assert-False $boundary.ProductionCredentialsAllowed "ProductionCredentialsAllowed"
Assert-False $boundary.ProductionBrokerAllowed "ProductionBrokerAllowed"
Assert-False $boundary.DirectLmaxCallByThisGate "DirectLmaxCallByThisGate"
Assert-False $boundary.FixSessionOpenedByThisGate "FixSessionOpenedByThisGate"
Assert-False $boundary.OrdersSubmittedByThisGate "OrdersSubmittedByThisGate"
Assert-False $boundary.RoutesCreatedByThisGate "RoutesCreatedByThisGate"
Assert-False $boundary.FillsCreatedByThisGate "FillsCreatedByThisGate"
Assert-False $boundary.SchedulerCreatedByThisGate "SchedulerCreatedByThisGate"
Assert-False $boundary.LedgerMutationAllowedNow "LedgerMutationAllowedNow"

if ($safety.Status -ne "Passed") {
    Fail "No-execution safety audit must pass"
}
foreach ($name in @(
    "NoLmaxCall",
    "NoFixSession",
    "NoOrdersSubmitted",
    "NoRoutesCreated",
    "NoFillsCreated",
    "NoSchedulesCreated",
    "NoBrokerSubmission",
    "NoLiveTradingStateMutation",
    "NoProductionCredentialUse",
    "CredentialValuesRedacted",
    "NoQubesExecutableRun",
    "NoNettingRun",
    "NoNettedUsdWeightsProduced",
    "NoPolygonMassiveCall",
    "NoSqlMutation",
    "NoProductionPromotion"
)) {
    Assert-True $safety.$name $name
}

$text = Get-ChildItem -LiteralPath $artifactRoot -Filter "cross-rail-r005-*" -File |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
if ($text -match '"CredentialValue"\s*:' -or
    $text -match '"CredentialValues"\s*:' -or
    $text -match '"PasswordValue"\s*:' -or
    $text -match '"SecretValue"\s*:') {
    Fail "Potential credential value persisted in R005 artifacts"
}

Write-Host "CROSS-RAIL-R005 validator passed."
