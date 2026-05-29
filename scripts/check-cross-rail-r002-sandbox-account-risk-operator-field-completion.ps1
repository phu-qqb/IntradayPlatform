param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R002 validation failed: $Message"
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

function Assert-EmptyId($Value, [string]$Name) {
    if ($null -ne $Value -and -not [string]::IsNullOrWhiteSpace([string]$Value)) {
        Fail "$Name is present without accepted evidence"
    }
}

$artifactRoot = Join-Path $RepoRoot "artifacts\readiness\cross-rail-sandbox-handoff"
$requiredFiles = @(
    "cross-rail-r002-summary.md",
    "cross-rail-r002-r001-reference.json",
    "cross-rail-r002-paper-account-profile-assessment.json",
    "cross-rail-r002-risk-operator-approval-assessment.json",
    "cross-rail-r002-completed-handoff-fields.json",
    "cross-rail-r002-bounded-sandbox-candidate-set.json",
    "cross-rail-r002-r009-sandbox-safety-boundary.json",
    "cross-rail-r002-future-r003-command-plan.json",
    "cross-rail-r002-no-execution-safety-audit.json",
    "cross-rail-r002-build-test-validator-evidence.json",
    "cross-rail-r002-next-gate-plan.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R002 artifact: $file"
    }
}

$r001Reference = Read-Json (Join-Path $artifactRoot "cross-rail-r002-r001-reference.json")
$paperAccount = Read-Json (Join-Path $artifactRoot "cross-rail-r002-paper-account-profile-assessment.json")
$approvals = Read-Json (Join-Path $artifactRoot "cross-rail-r002-risk-operator-approval-assessment.json")
$completed = Read-Json (Join-Path $artifactRoot "cross-rail-r002-completed-handoff-fields.json")
$candidateSet = Read-Json (Join-Path $artifactRoot "cross-rail-r002-bounded-sandbox-candidate-set.json")
$boundary = Read-Json (Join-Path $artifactRoot "cross-rail-r002-r009-sandbox-safety-boundary.json")
$futurePlan = Read-Json (Join-Path $artifactRoot "cross-rail-r002-future-r003-command-plan.json")
$safety = Read-Json (Join-Path $artifactRoot "cross-rail-r002-no-execution-safety-audit.json")

$r001Validator = Join-Path $RepoRoot "scripts\check-cross-rail-r001-pms-paper-to-r009-sandbox-handoff-preflight.ps1"
if (-not (Test-Path -LiteralPath $r001Validator)) {
    Fail "R001 validator reference is missing"
}

if ($r001Reference.R001Status -ne "HandoffFieldCompletionIncomplete") {
    Fail "R001 status must be HandoffFieldCompletionIncomplete"
}
Assert-True $r001Reference.R001ValidatorPassed "R001ValidatorPassed"
Assert-True $r001Reference.ReadOnlyReview "ReadOnlyReview"

$expectedMissing = @("MissingPaperAccount", "MissingRiskApprovalId", "MissingOperatorApprovalId")
$actualMissing = @($r001Reference.R001MissingFields)
if (($actualMissing | Sort-Object) -join "|" -ne (($expectedMissing | Sort-Object) -join "|")) {
    Fail "R001 missing fields must exactly include MissingPaperAccount, MissingRiskApprovalId, MissingOperatorApprovalId"
}

if ($paperAccount.Status -eq "Resolved") {
    if ([string]::IsNullOrWhiteSpace([string]$paperAccount.PaperAccountId)) {
        Fail "Paper account marked resolved without PaperAccountId"
    }
    if ([string]::IsNullOrWhiteSpace([string]$paperAccount.PaperAccountEvidenceFile)) {
        Fail "Paper account marked resolved without evidence file"
    }
} elseif ($paperAccount.Status -eq "MissingPaperAccount") {
    Assert-EmptyId $paperAccount.PaperAccountId "PaperAccountId"
} else {
    Fail "Invalid paper account status"
}
Assert-True $paperAccount.CredentialValuesRedacted "CredentialValuesRedacted"
Assert-True $paperAccount.DoNotInventAccount "DoNotInventAccount"
foreach ($name in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    if (-not ($paperAccount.CredentialVariableNames -contains $name)) {
        Fail "Missing credential env var name presence reference: $name"
    }
}

if ($approvals.RiskApprovalStatus -eq "Resolved") {
    if ([string]::IsNullOrWhiteSpace([string]$approvals.RiskReviewId)) {
        Fail "Risk approval marked resolved without RiskReviewId"
    }
    if ([string]::IsNullOrWhiteSpace([string]$approvals.RiskEvidenceFile)) {
        Fail "Risk approval marked resolved without evidence file"
    }
} elseif ($approvals.RiskApprovalStatus -eq "MissingRiskApprovalId") {
    Assert-EmptyId $approvals.RiskReviewId "RiskReviewId"
} else {
    Fail "Invalid risk approval status"
}

if ($approvals.OperatorApprovalStatus -eq "Resolved") {
    if ([string]::IsNullOrWhiteSpace([string]$approvals.OperatorApprovalId)) {
        Fail "Operator approval marked resolved without OperatorApprovalId"
    }
    if ([string]::IsNullOrWhiteSpace([string]$approvals.OperatorEvidenceFile)) {
        Fail "Operator approval marked resolved without evidence file"
    }
} elseif ($approvals.OperatorApprovalStatus -eq "MissingOperatorApprovalId") {
    Assert-EmptyId $approvals.OperatorApprovalId "OperatorApprovalId"
} else {
    Fail "Invalid operator approval status"
}
Assert-True $approvals.DoNotInventRiskOrOperatorIds "DoNotInventRiskOrOperatorIds"

if ($completed.Status -ne "CompletedFieldsPreserved") {
    Fail "Completed handoff fields must be preserved"
}

$supported = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$expectedSides = @{
    AUDUSD = "SELL"
    EURUSD = "SELL"
    GBPUSD = "BUY"
}

foreach ($row in @($completed.CandidateRows)) {
    if (@($supported) -notcontains [string]$row.Symbol) {
        Fail "Unsupported candidate symbol: $($row.Symbol)"
    }
    if ($expectedSides.ContainsKey([string]$row.Symbol) -and $expectedSides[[string]$row.Symbol] -ne [string]$row.Side) {
        Fail "Unexpected side for $($row.Symbol)"
    }
    if ([decimal]$row.Quantity -ne 0.1) {
        Fail "Candidate quantity must remain 0.1"
    }
    Assert-False $row.ExecutionAllowedNow "Candidate ExecutionAllowedNow for $($row.Symbol)"
    Assert-True $row.SandboxOnly "Candidate SandboxOnly for $($row.Symbol)"
    Assert-True $row.NoLive "Candidate NoLive for $($row.Symbol)"
    Assert-True $row.NoProduction "Candidate NoProduction for $($row.Symbol)"
}

if ($candidateSet.Status -ne "CandidateSetBlocked") {
    Fail "Candidate set must remain blocked while approvals are missing"
}
if ([int]$candidateSet.CandidateCount -ne 3) {
    Fail "Expected three R001 candidate rows"
}
Assert-True $candidateSet.SandboxOnly "Candidate set SandboxOnly"
Assert-True $candidateSet.NoLive "Candidate set NoLive"
Assert-True $candidateSet.NoProduction "Candidate set NoProduction"
Assert-False $candidateSet.OrdersSubmittedNow "Candidate set OrdersSubmittedNow"
Assert-False $candidateSet.RoutesCreatedNow "Candidate set RoutesCreatedNow"
Assert-False $candidateSet.FillsCreatedNow "Candidate set FillsCreatedNow"

if ($boundary.Status -ne "SafetyBoundaryDefined") {
    Fail "Safety boundary must be defined"
}
Assert-True $boundary.R009Selected "R009Selected"
Assert-True $boundary.SandboxOnly "Boundary SandboxOnly"
Assert-True $boundary.NoLive "Boundary NoLive"
Assert-True $boundary.NoProduction "Boundary NoProduction"
Assert-True $boundary.LmaxSandboxOnly "LmaxSandboxOnly"
Assert-False $boundary.ProductionLiveAllowed "ProductionLiveAllowed"
Assert-False $boundary.ProductionCredentialsAllowed "ProductionCredentialsAllowed"
Assert-False $boundary.ProductionBrokerAllowed "ProductionBrokerAllowed"
Assert-False $boundary.SchedulerAutomaticExecutionAllowed "SchedulerAutomaticExecutionAllowed"
Assert-False $boundary.LedgerMutationAllowedNow "LedgerMutationAllowedNow"
Assert-False $boundary.DirectLmaxCallByThisGate "DirectLmaxCallByThisGate"
Assert-False $boundary.FixSessionOpenedByThisGate "FixSessionOpenedByThisGate"
Assert-False $boundary.OrdersSubmittedByThisGate "OrdersSubmittedByThisGate"
Assert-False $boundary.RoutesCreatedByThisGate "RoutesCreatedByThisGate"
Assert-False $boundary.FillsCreatedByThisGate "FillsCreatedByThisGate"

if ($futurePlan.Status -ne "BlockedPendingMissingFields") {
    Fail "Future R003 plan must be blocked while fields are missing"
}
Assert-False $futurePlan.ExecutionAllowedNow "Future plan ExecutionAllowedNow"
Assert-False $futurePlan.OrdersSubmittedNow "Future plan OrdersSubmittedNow"
foreach ($missing in $expectedMissing) {
    if (@($futurePlan.MissingFields) -notcontains $missing) {
        Fail "Future R003 plan missing field is not recorded: $missing"
    }
}

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

$text = Get-ChildItem -LiteralPath $artifactRoot -Filter "cross-rail-r002-*" -File |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
if ($text -match '"CredentialValue"\s*:' -or
    $text -match '"CredentialValues"\s*:' -or
    $text -match '"PasswordValue"\s*:' -or
    $text -match '"SecretValue"\s*:') {
    Fail "Potential credential value persisted in R002 artifacts"
}

Write-Host "CROSS-RAIL-R002 validator passed."
