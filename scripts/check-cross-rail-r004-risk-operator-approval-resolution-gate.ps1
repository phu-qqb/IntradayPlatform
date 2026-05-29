param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R004 validation failed: $Message"
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
        Fail "$Name must be empty unless resolved with evidence"
    }
}

$artifactRoot = Join-Path $RepoRoot "artifacts\readiness\cross-rail-sandbox-handoff"
$requiredFiles = @(
    "cross-rail-r004-summary.md",
    "cross-rail-r004-r003-reference.json",
    "cross-rail-r004-risk-approval-resolution.json",
    "cross-rail-r004-operator-approval-resolution.json",
    "cross-rail-r004-completed-sandbox-handoff-fields.json",
    "cross-rail-r004-resolved-candidate-set.json",
    "cross-rail-r004-future-r005-preflight-plan.json",
    "cross-rail-r004-missing-input-request.json",
    "cross-rail-r004-sandbox-safety-boundary.json",
    "cross-rail-r004-no-execution-safety-audit.json",
    "cross-rail-r004-build-test-validator-evidence.json",
    "cross-rail-r004-next-gate-plan.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $file))) {
        Fail "Missing required R004 artifact: $file"
    }
}

$r003 = Read-Json (Join-Path $artifactRoot "cross-rail-r004-r003-reference.json")
$risk = Read-Json (Join-Path $artifactRoot "cross-rail-r004-risk-approval-resolution.json")
$operator = Read-Json (Join-Path $artifactRoot "cross-rail-r004-operator-approval-resolution.json")
$completed = Read-Json (Join-Path $artifactRoot "cross-rail-r004-completed-sandbox-handoff-fields.json")
$candidateSet = Read-Json (Join-Path $artifactRoot "cross-rail-r004-resolved-candidate-set.json")
$futurePlan = Read-Json (Join-Path $artifactRoot "cross-rail-r004-future-r005-preflight-plan.json")
$missingRequest = Read-Json (Join-Path $artifactRoot "cross-rail-r004-missing-input-request.json")
$boundary = Read-Json (Join-Path $artifactRoot "cross-rail-r004-sandbox-safety-boundary.json")
$safety = Read-Json (Join-Path $artifactRoot "cross-rail-r004-no-execution-safety-audit.json")

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot "scripts\check-cross-rail-r003-sandbox-approval-resolution-gate.ps1"))) {
    Fail "R003 validator reference is missing"
}

Assert-True $r003.R003ValidatorPassed "R003ValidatorPassed"
Assert-True $r003.ReadOnlyReview "ReadOnlyReview"
if ($r003.R003Status -ne "BlockedMissingSandboxApprovals") {
    Fail "R003 status must be BlockedMissingSandboxApprovals"
}
if ($r003.R003PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "R003 paper account/profile must be ExistingLmaxDemoProfile"
}
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($r003.R003MissingFields) -notcontains $field) {
        Fail "R003 missing field not preserved: $field"
    }
}

if ($risk.Status -eq "Resolved") {
    if ([string]::IsNullOrWhiteSpace([string]$risk.RiskReviewId)) {
        Fail "Risk approval marked resolved without RiskReviewId"
    }
    if ([string]::IsNullOrWhiteSpace([string]$risk.EvidenceFile)) {
        Fail "Risk approval marked resolved without evidence file"
    }
} elseif ($risk.Status -eq "MissingRiskApprovalId") {
    Assert-Empty $risk.RiskReviewId "RiskReviewId"
} else {
    Fail "Invalid risk approval status"
}
Assert-True $risk.DoNotInventRiskReviewId "DoNotInventRiskReviewId"

if ($operator.Status -eq "Resolved") {
    if ([string]::IsNullOrWhiteSpace([string]$operator.OperatorApprovalId)) {
        Fail "Operator approval marked resolved without OperatorApprovalId"
    }
    if ([string]::IsNullOrWhiteSpace([string]$operator.EvidenceFile)) {
        Fail "Operator approval marked resolved without evidence file"
    }
} elseif ($operator.Status -eq "MissingOperatorApprovalId") {
    Assert-Empty $operator.OperatorApprovalId "OperatorApprovalId"
} else {
    Fail "Invalid operator approval status"
}
Assert-True $operator.DoNotInventOperatorApprovalId "DoNotInventOperatorApprovalId"

if ($completed.Status -ne "CompletedFieldsPreserved") {
    Fail "Completed sandbox handoff fields must be preserved"
}
if ($completed.PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "Paper account/profile was not preserved"
}
if ($completed.RiskApprovalStatus -ne "MissingRiskApprovalId" -or
    $completed.OperatorApprovalStatus -ne "MissingOperatorApprovalId") {
    Fail "Approval statuses must remain missing"
}
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($completed.MissingFieldsRemaining) -notcontains $field) {
        Fail "Completed fields missing field not recorded: $field"
    }
}

$expectedSides = @{
    AUDUSD = "SELL"
    EURUSD = "SELL"
    GBPUSD = "BUY"
}
$supported = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
foreach ($row in @($completed.CandidateRows)) {
    if (@($supported) -notcontains [string]$row.Symbol) {
        Fail "Unsupported candidate symbol: $($row.Symbol)"
    }
    if ($expectedSides[[string]$row.Symbol] -ne [string]$row.Side) {
        Fail "Unexpected candidate side for $($row.Symbol)"
    }
    if ([decimal]$row.Quantity -ne 0.1) {
        Fail "Candidate quantity must remain 0.1"
    }
    if ($row.PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
        Fail "Candidate paper account/profile must be preserved"
    }
    Assert-False $row.ExecutionAllowedNow "Candidate ExecutionAllowedNow for $($row.Symbol)"
    Assert-False $row.OrdersSubmittedNow "Candidate OrdersSubmittedNow for $($row.Symbol)"
    Assert-False $row.RoutesCreatedNow "Candidate RoutesCreatedNow for $($row.Symbol)"
    Assert-False $row.FillsCreatedNow "Candidate FillsCreatedNow for $($row.Symbol)"
    Assert-True $row.SandboxOnly "Candidate SandboxOnly for $($row.Symbol)"
    Assert-True $row.NoLive "Candidate NoLive for $($row.Symbol)"
    Assert-True $row.NoProduction "Candidate NoProduction for $($row.Symbol)"
}

if ($candidateSet.Status -ne "CandidateSetBlocked") {
    Fail "Candidate set must remain blocked"
}
if ($candidateSet.PaperAccountStatus -ne "Resolved" -or
    $candidateSet.RiskApprovalStatus -ne "MissingRiskApprovalId" -or
    $candidateSet.OperatorApprovalStatus -ne "MissingOperatorApprovalId") {
    Fail "Candidate set status is inconsistent"
}
Assert-True $candidateSet.SandboxOnly "CandidateSet SandboxOnly"
Assert-True $candidateSet.NoLive "CandidateSet NoLive"
Assert-True $candidateSet.NoProduction "CandidateSet NoProduction"
Assert-False $candidateSet.OrdersSubmittedNow "CandidateSet OrdersSubmittedNow"
Assert-False $candidateSet.RoutesCreatedNow "CandidateSet RoutesCreatedNow"
Assert-False $candidateSet.FillsCreatedNow "CandidateSet FillsCreatedNow"
Assert-False $candidateSet.ExecutionAllowedNow "CandidateSet ExecutionAllowedNow"

if ($futurePlan.Status -ne "BlockedPendingMissingApprovals") {
    Fail "Future R005 plan must be blocked while approvals are missing"
}
if ($futurePlan.FutureR005Readiness -ne "Blocked") {
    Fail "FutureR005Readiness must be Blocked"
}
if ($futurePlan.RequiredPaperAccountId -ne "ExistingLmaxDemoProfile") {
    Fail "Future plan must preserve ExistingLmaxDemoProfile"
}
Assert-Empty $futurePlan.RequiredRiskReviewId "Future RequiredRiskReviewId"
Assert-Empty $futurePlan.RequiredOperatorApprovalId "Future RequiredOperatorApprovalId"
Assert-False $futurePlan.ExecutionAllowedNow "FuturePlan ExecutionAllowedNow"
Assert-False $futurePlan.OrdersSubmittedNow "FuturePlan OrdersSubmittedNow"
Assert-True $futurePlan.NoActualOrderIdsCreated "NoActualOrderIdsCreated"
Assert-True $futurePlan.NoRouteIdsCreated "NoRouteIdsCreated"
Assert-True $futurePlan.NoFillIdsCreated "NoFillIdsCreated"

if ($missingRequest.Status -ne "MissingInputsRequired") {
    Fail "Missing input request must be active"
}
Assert-True $missingRequest.PaperAccountProfileResolved "PaperAccountProfileResolved"
Assert-True $missingRequest.DoNotInvent "MissingRequest DoNotInvent"
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($missingRequest.MissingInputs) -notcontains $field) {
        Fail "Missing input request does not include $field"
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

$text = Get-ChildItem -LiteralPath $artifactRoot -Filter "cross-rail-r004-*" -File |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
if ($text -match '"CredentialValue"\s*:' -or
    $text -match '"CredentialValues"\s*:' -or
    $text -match '"PasswordValue"\s*:' -or
    $text -match '"SecretValue"\s*:') {
    Fail "Potential credential value persisted in R004 artifacts"
}

Write-Host "CROSS-RAIL-R004 validator passed."
