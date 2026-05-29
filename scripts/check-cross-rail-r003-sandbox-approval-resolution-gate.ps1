param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R003 validation failed: $Message"
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
    "cross-rail-r003-summary.md",
    "cross-rail-r003-r002-reference.json",
    "cross-rail-r003-approval-resolution-inputs.json",
    "cross-rail-r003-paper-account-resolution.json",
    "cross-rail-r003-risk-operator-approval-resolution.json",
    "cross-rail-r003-resolved-candidate-set.json",
    "cross-rail-r003-future-r004-preflight-plan.json",
    "cross-rail-r003-missing-input-request.json",
    "cross-rail-r003-sandbox-safety-boundary.json",
    "cross-rail-r003-no-execution-safety-audit.json",
    "cross-rail-r003-build-test-validator-evidence.json",
    "cross-rail-r003-next-gate-plan.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $file))) {
        Fail "Missing required R003 artifact: $file"
    }
}

$r002 = Read-Json (Join-Path $artifactRoot "cross-rail-r003-r002-reference.json")
$inputs = Read-Json (Join-Path $artifactRoot "cross-rail-r003-approval-resolution-inputs.json")
$paper = Read-Json (Join-Path $artifactRoot "cross-rail-r003-paper-account-resolution.json")
$approvals = Read-Json (Join-Path $artifactRoot "cross-rail-r003-risk-operator-approval-resolution.json")
$candidateSet = Read-Json (Join-Path $artifactRoot "cross-rail-r003-resolved-candidate-set.json")
$futurePlan = Read-Json (Join-Path $artifactRoot "cross-rail-r003-future-r004-preflight-plan.json")
$missingRequest = Read-Json (Join-Path $artifactRoot "cross-rail-r003-missing-input-request.json")
$boundary = Read-Json (Join-Path $artifactRoot "cross-rail-r003-sandbox-safety-boundary.json")
$safety = Read-Json (Join-Path $artifactRoot "cross-rail-r003-no-execution-safety-audit.json")

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot "scripts\check-cross-rail-r002-sandbox-account-risk-operator-field-completion.ps1"))) {
    Fail "R002 validator reference is missing"
}

Assert-True $r002.R002ValidatorPassed "R002ValidatorPassed"
Assert-True $r002.ReadOnlyReview "ReadOnlyReview"
if ($r002.R002Status -ne "BlockedMissingSandboxApprovals") {
    Fail "R002 status must be BlockedMissingSandboxApprovals"
}
foreach ($field in @("MissingPaperAccount", "MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($r002.R002MissingFields) -notcontains $field) {
        Fail "R002 missing field not preserved: $field"
    }
}

if ($inputs.Status -ne "InputsMissing") {
    Fail "R003 must remain InputsMissing unless all approvals are resolved"
}
Assert-False $inputs.OperatorSuppliedPaperAccount "OperatorSuppliedPaperAccount"
Assert-False $inputs.OperatorSuppliedRiskReviewId "OperatorSuppliedRiskReviewId"
Assert-False $inputs.OperatorSuppliedOperatorApprovalId "OperatorSuppliedOperatorApprovalId"
Assert-True $inputs.ArtifactDerivedPaperAccount "ArtifactDerivedPaperAccount"
Assert-False $inputs.ArtifactDerivedRiskReviewId "ArtifactDerivedRiskReviewId"
Assert-False $inputs.ArtifactDerivedOperatorApprovalId "ArtifactDerivedOperatorApprovalId"
Assert-True $inputs.DoNotInventIds "DoNotInventIds"

if ($paper.Status -ne "Resolved") {
    Fail "Paper account/profile should be resolved from accepted artifact evidence"
}
if ($paper.Source -ne "ArtifactEvidence") {
    Fail "Paper account/profile source must be ArtifactEvidence"
}
if ($paper.PaperAccountId -ne "ExistingLmaxDemoProfile") {
    Fail "Unexpected paper account/profile"
}
if ([string]::IsNullOrWhiteSpace([string]$paper.EvidenceFile) -or -not (Test-Path -LiteralPath (Join-Path $RepoRoot $paper.EvidenceFile))) {
    Fail "Paper account/profile evidence file is missing"
}
Assert-True $paper.SandboxOnly "Paper SandboxOnly"
Assert-True $paper.NoLive "Paper NoLive"
Assert-True $paper.CredentialValuesRedacted "Paper CredentialValuesRedacted"
Assert-True $paper.EnvVarPresenceIsNotAccountId "EnvVarPresenceIsNotAccountId"
Assert-True $paper.DoNotInventAccount "DoNotInventAccount"

if ($approvals.RiskApprovalStatus -ne "MissingRiskApprovalId") {
    Fail "Risk approval must remain missing without explicit ID evidence"
}
Assert-Empty $approvals.RiskReviewId "RiskReviewId"
if ($approvals.OperatorApprovalStatus -ne "MissingOperatorApprovalId") {
    Fail "Operator approval must remain missing without explicit ID evidence"
}
Assert-Empty $approvals.OperatorApprovalId "OperatorApprovalId"
Assert-True $approvals.DoNotInventRiskOrOperatorIds "DoNotInventRiskOrOperatorIds"

if ($candidateSet.Status -ne "CandidateSetBlocked") {
    Fail "Candidate set must remain blocked while approvals are missing"
}
if ($candidateSet.PaperAccountStatus -ne "Resolved" -or
    $candidateSet.RiskApprovalStatus -ne "MissingRiskApprovalId" -or
    $candidateSet.OperatorApprovalStatus -ne "MissingOperatorApprovalId") {
    Fail "Candidate set resolution statuses are inconsistent"
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
        Fail "Unexpected side for $($row.Symbol)"
    }
    if ([decimal]$row.Quantity -ne 0.1) {
        Fail "Candidate quantity must remain 0.1"
    }
    Assert-False $row.ExecutionAllowedNow "Candidate ExecutionAllowedNow for $($row.Symbol)"
    Assert-False $row.OrdersSubmittedNow "Candidate OrdersSubmittedNow for $($row.Symbol)"
    Assert-True $row.SandboxOnly "Candidate SandboxOnly for $($row.Symbol)"
    Assert-True $row.NoLive "Candidate NoLive for $($row.Symbol)"
    Assert-True $row.NoProduction "Candidate NoProduction for $($row.Symbol)"
}

if ($futurePlan.Status -ne "BlockedPendingMissingApprovals") {
    Fail "Future R004 plan must remain blocked"
}
if ($futurePlan.FutureR004Readiness -ne "Blocked") {
    Fail "FutureR004Readiness must be Blocked"
}
Assert-False $futurePlan.ExecutionAllowedNow "FuturePlan ExecutionAllowedNow"
Assert-False $futurePlan.OrdersSubmittedNow "FuturePlan OrdersSubmittedNow"
Assert-True $futurePlan.NoActualOrderIdsCreated "NoActualOrderIdsCreated"
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if (@($futurePlan.MissingFields) -notcontains $field) {
        Fail "Future R004 plan missing field not recorded: $field"
    }
}

if ($missingRequest.Status -ne "MissingInputsRequired") {
    Fail "Missing input request must be active"
}
Assert-True $missingRequest.PaperAccountProfileResolved "PaperAccountProfileResolved"
Assert-True $missingRequest.DoNotInvent "Missing request DoNotInvent"

if ($boundary.Status -ne "SafetyBoundaryPreserved") {
    Fail "Sandbox safety boundary must be preserved"
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

$text = Get-ChildItem -LiteralPath $artifactRoot -Filter "cross-rail-r003-*" -File |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
if ($text -match '"CredentialValue"\s*:' -or
    $text -match '"CredentialValues"\s*:' -or
    $text -match '"PasswordValue"\s*:' -or
    $text -match '"SecretValue"\s*:') {
    Fail "Potential credential value persisted in R003 artifacts"
}

Write-Host "CROSS-RAIL-R003 validator passed."
