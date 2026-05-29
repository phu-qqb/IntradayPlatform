param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R006A validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing required file: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-False($Value, [string]$Name) {
    if ($Value -ne $false) {
        Fail "$Name must be false"
    }
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) {
        Fail "$Name must be true"
    }
}

function Assert-NoCredentialValues([string[]]$Paths) {
    $patterns = @(
        '"CredentialValue"\s*:',
        '"CredentialValues"\s*:',
        '"PasswordValue"\s*:',
        '"SecretValue"\s*:'
    )

    foreach ($path in $Paths) {
        $content = Get-Content -LiteralPath $path -Raw
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) {
                Fail "credential value-like field persisted in $path"
            }
        }
    }
}

function Assert-CandidateRows($Rows, [string]$Name) {
    if ($null -eq $Rows -or $Rows.Count -ne 3) {
        Fail "$Name must contain exactly 3 candidate rows"
    }

    $expected = @{
        AUDUSD = "SELL"
        EURUSD = "SELL"
        GBPUSD = "BUY"
    }
    $supported = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")

    foreach ($row in $Rows) {
        if (-not $expected.ContainsKey([string]$row.Symbol)) {
            Fail "$Name contains unexpected or unsupported symbol: $($row.Symbol)"
        }
        if ($supported -notcontains [string]$row.Symbol) {
            Fail "$Name contains symbol outside supported USD-pair list: $($row.Symbol)"
        }
        if ([string]$row.Side -ne $expected[[string]$row.Symbol]) {
            Fail "$Name side mismatch for $($row.Symbol): $($row.Side)"
        }
        if ([decimal]$row.Quantity -ne [decimal]"0.1") {
            Fail "$Name quantity mismatch for $($row.Symbol): $($row.Quantity)"
        }
        if ($row.PSObject.Properties.Name -contains "ExecutionAllowedNow") {
            Assert-False $row.ExecutionAllowedNow "$Name.$($row.Symbol).ExecutionAllowedNow"
        }
    }
}

function Assert-NoExecutionAudit($Audit) {
    if ([string]$Audit.Status -ne "Passed") {
        Fail "no-execution audit status must be Passed"
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
        "NoNettedUsdWeightsProduced"
    )) {
        Assert-True $Audit.$name "no-execution audit $name"
    }
}

$requiredFiles = @(
    "cross-rail-r006a-summary.md",
    "cross-rail-r006a-r005-reference.json",
    "cross-rail-r006a-risk-review-approval.json",
    "cross-rail-r006a-operator-approval.json",
    "cross-rail-r006a-approval-binding-instructions.json",
    "cross-rail-r006a-candidate-set-reference.json",
    "cross-rail-r006a-sandbox-safety-boundary.json",
    "cross-rail-r006a-no-execution-safety-audit.json",
    "cross-rail-r006a-build-test-validator-evidence.json",
    "cross-rail-r006a-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R006A artifact: $file"
    }
    $paths += $path
}

Assert-NoCredentialValues $paths

$r005 = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-r005-reference.json")
$risk = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-risk-review-approval.json")
$operator = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-operator-approval.json")
$binding = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-approval-binding-instructions.json")
$candidate = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-candidate-set-reference.json")
$safety = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-sandbox-safety-boundary.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-no-execution-safety-audit.json")
$next = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006a-next-gate-plan.json")

if ([string]$r005.R005Status -ne "BlockedMissingSandboxApprovals") {
    Fail "R005 reference must preserve BlockedMissingSandboxApprovals"
}
if ([string]$r005.R005PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "R005 paper account/profile must be ExistingLmaxDemoProfile"
}
foreach ($field in @("MissingRiskApprovalId", "MissingOperatorApprovalId")) {
    if ($r005.R005MissingFields -notcontains $field) {
        Fail "R005 missing fields must include $field"
    }
}
Assert-True $r005.ReadOnlyReview "R005 ReadOnlyReview"
Assert-CandidateRows $r005.R005CandidateRows "R005 candidate rows"

$expectedRiskId = "risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001"
$expectedOperatorId = "operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001"

if ([string]$risk.Status -ne "Issued") {
    Fail "risk approval status must be Issued"
}
if ([string]$risk.RiskReviewId -ne $expectedRiskId) {
    Fail "risk approval ID mismatch"
}
if ([string]$risk.RiskReviewId -match "<|>") {
    Fail "risk approval ID must not be a placeholder"
}
if (-not ([string]$risk.RiskReviewId).StartsWith("risk-review-")) {
    Fail "risk approval ID must start with risk-review-"
}
if ([string]$risk.RiskReviewId -notmatch "cross-rail-r006") {
    Fail "risk approval ID must contain cross-rail-r006"
}
if ([string]$risk.RiskReviewId -notmatch "lmax-demo-sandbox") {
    Fail "risk approval ID must contain sandbox scope"
}
Assert-True $risk.SandboxOnly "risk SandboxOnly"
Assert-True $risk.NoLive "risk NoLive"
Assert-True $risk.NoProduction "risk NoProduction"
Assert-False $risk.ApprovesExecutionNow "risk ApprovesExecutionNow"
Assert-True $risk.ApprovesFuturePreflightOnly "risk ApprovesFuturePreflightOnly"
Assert-True $risk.DoNotTreatAsProductionRiskApproval "risk DoNotTreatAsProductionRiskApproval"
Assert-CandidateRows $risk.CandidateRows "risk approval candidate rows"

if ([string]$operator.Status -ne "Issued") {
    Fail "operator approval status must be Issued"
}
if ([string]$operator.OperatorApprovalId -ne $expectedOperatorId) {
    Fail "operator approval ID mismatch"
}
if ([string]$operator.OperatorApprovalId -match "<|>") {
    Fail "operator approval ID must not be a placeholder"
}
if (-not ([string]$operator.OperatorApprovalId).StartsWith("operator-approval-")) {
    Fail "operator approval ID must start with operator-approval-"
}
if ([string]$operator.OperatorApprovalId -notmatch "cross-rail-r006") {
    Fail "operator approval ID must contain cross-rail-r006"
}
if ([string]$operator.OperatorApprovalId -notmatch "lmax-demo-sandbox") {
    Fail "operator approval ID must contain sandbox scope"
}
Assert-True $operator.SandboxOnly "operator SandboxOnly"
Assert-True $operator.NoLive "operator NoLive"
Assert-True $operator.NoProduction "operator NoProduction"
Assert-False $operator.ApprovesExecutionNow "operator ApprovesExecutionNow"
Assert-True $operator.ApprovesFuturePreflightOnly "operator ApprovesFuturePreflightOnly"
Assert-True $operator.DoNotTreatAsProductionOperatorApproval "operator DoNotTreatAsProductionOperatorApproval"
Assert-CandidateRows $operator.CandidateRows "operator approval candidate rows"

if ([string]$binding.Status -ne "ReadyForR006Binding") {
    Fail "approval binding status must be ReadyForR006Binding"
}
if ([string]$binding.FutureGate -ne "CROSS-RAIL-R006") {
    Fail "approval binding FutureGate must be CROSS-RAIL-R006"
}
if ([string]$binding.RiskReviewId -ne $expectedRiskId) {
    Fail "binding risk approval ID mismatch"
}
if ([string]$binding.OperatorApprovalId -ne $expectedOperatorId) {
    Fail "binding operator approval ID mismatch"
}
Assert-False $binding.ExecutionAllowedNow "binding ExecutionAllowedNow"
Assert-False $binding.OrdersSubmittedNow "binding OrdersSubmittedNow"
Assert-False $binding.LmaxCallAllowedNow "binding LmaxCallAllowedNow"
Assert-False $binding.FixSessionAllowedNow "binding FixSessionAllowedNow"

if ([string]$candidate.Status -ne "CandidateSetReferenced") {
    Fail "candidate set must be referenced"
}
Assert-CandidateRows $candidate.CandidateRows "candidate set"
Assert-True $candidate.SandboxOnly "candidate SandboxOnly"
Assert-True $candidate.NoLive "candidate NoLive"
Assert-True $candidate.NoProduction "candidate NoProduction"
Assert-False $candidate.OrdersSubmittedNow "candidate OrdersSubmittedNow"
Assert-False $candidate.RoutesCreatedNow "candidate RoutesCreatedNow"
Assert-False $candidate.FillsCreatedNow "candidate FillsCreatedNow"
Assert-False $candidate.ExecutionAllowedNow "candidate ExecutionAllowedNow"

if ([string]$safety.Status -ne "SafetyBoundaryPreserved") {
    Fail "safety boundary status must be SafetyBoundaryPreserved"
}
Assert-True $safety.R009Selected "safety R009Selected"
Assert-True $safety.SandboxOnly "safety SandboxOnly"
Assert-True $safety.NoLive "safety NoLive"
Assert-True $safety.NoProduction "safety NoProduction"
Assert-True $safety.LmaxSandboxOnly "safety LmaxSandboxOnly"
Assert-False $safety.ProductionLiveAllowed "safety ProductionLiveAllowed"
Assert-False $safety.DirectLmaxCallByThisGate "safety DirectLmaxCallByThisGate"
Assert-False $safety.FixSessionOpenedByThisGate "safety FixSessionOpenedByThisGate"
Assert-False $safety.OrdersSubmittedByThisGate "safety OrdersSubmittedByThisGate"
Assert-False $safety.RoutesCreatedByThisGate "safety RoutesCreatedByThisGate"
Assert-False $safety.FillsCreatedByThisGate "safety FillsCreatedByThisGate"
Assert-True $safety.ApprovalsAreForFuturePreflightOnly "safety ApprovalsAreForFuturePreflightOnly"

Assert-NoExecutionAudit $audit

if ([string]$next.RecommendedNextGate -ne "CROSS-RAIL-R006") {
    Fail "next gate must be CROSS-RAIL-R006"
}
foreach ($flag in @("SandboxOnly = true", "NoLive = true", "NoProduction = true", "ExecutionAllowedNow = false", "OrdersSubmittedNow = false")) {
    $flagFound = $false
    foreach ($expectedFlag in $next.ExpectedSafetyFlags) {
        if ([string]$expectedFlag -eq $flag) {
            $flagFound = $true
        }
    }
    if (-not $flagFound) {
        Fail "next gate expected safety flags missing $flag"
    }
}

$executionSimArtifacts = Get-ChildItem -Path "artifacts/readiness/execution-sim" -Filter "*r006a*" -ErrorAction SilentlyContinue
if ($executionSimArtifacts.Count -gt 0) {
    Fail "R006A artifacts must not be written under execution-sim"
}

Write-Host "CROSS-RAIL-R006A validator passed."
