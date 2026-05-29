param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R006R007 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing required file: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) {
        Fail "$Name must be true"
    }
}

function Assert-False($Value, [string]$Name) {
    if ($Value -ne $false) {
        Fail "$Name must be false"
    }
}

function Assert-NoCredentialValues([string[]]$Paths) {
    $patterns = @(
        '"CredentialValue"\s*:',
        '"CredentialValues"\s*:',
        '"PasswordValue"\s*:',
        '"SecretValue"\s*:',
        'LMAX_DEMO_FIX_PASSWORD"\s*:\s*"[^"]+"'
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
        $symbol = [string]$row.Symbol
        if (-not $expected.ContainsKey($symbol)) {
            Fail "$Name contains unexpected or direct-cross symbol: $symbol"
        }
        if ($supported -notcontains $symbol) {
            Fail "$Name contains unsupported symbol: $symbol"
        }
        if ([string]$row.Side -ne $expected[$symbol]) {
            Fail "$Name side mismatch for ${symbol}: $($row.Side)"
        }
        if ([decimal]$row.Quantity -gt [decimal]"0.1") {
            Fail "$Name quantity exceeds 0.1 for ${symbol}: $($row.Quantity)"
        }
        if ([decimal]$row.Quantity -ne [decimal]"0.1") {
            Fail "$Name quantity must be 0.1 for ${symbol}: $($row.Quantity)"
        }
        if ($row.PSObject.Properties.Name -contains "ExecutionAllowedNow") {
            Assert-False $row.ExecutionAllowedNow "$Name.$symbol.ExecutionAllowedNow"
        }
    }
}

function Assert-NoProductionAudit($Audit) {
    if ([string]$Audit.Status -ne "Passed") {
        Fail "no-production audit status must be Passed"
    }

    foreach ($name in @(
        "NoProductionLmaxCall",
        "NoProductionFixSession",
        "NoProductionOrders",
        "NoProductionRoutes",
        "NoProductionFills",
        "NoProductionSchedules",
        "NoProductionBrokerSubmission",
        "NoProductionLiveTradingStateMutation",
        "NoProductionCredentialUse",
        "CredentialValuesRedacted",
        "NoQubesExecutableRun",
        "NoNettingRun",
        "NoNettedUsdWeightsProduced"
    )) {
        Assert-True $Audit.$name "no-production audit $name"
    }
}

$requiredFiles = @(
    "cross-rail-r006r007-summary.md",
    "cross-rail-r006r007-r005-reference.json",
    "cross-rail-r006r007-r006a-approval-reference.json",
    "cross-rail-r006r007-approval-binding-validation.json",
    "cross-rail-r006r007-approved-candidate-set.json",
    "cross-rail-r006r007-final-idempotency-plan.json",
    "cross-rail-r006r007-pre-submission-safety-check.json",
    "cross-rail-r006r007-lmax-demo-sandbox-command.json",
    "cross-rail-r006r007-sandbox-order-submission-result.json",
    "cross-rail-r006r007-sandbox-fill-report.json",
    "cross-rail-r006r007-sandbox-flatten-result.json",
    "cross-rail-r006r007-sandbox-residual-check.json",
    "cross-rail-r006r007-reconciliation-result.json",
    "cross-rail-r006r007-pnl-readiness-result.json",
    "cross-rail-r006r007-sandbox-safety-boundary.json",
    "cross-rail-r006r007-no-production-safety-audit.json",
    "cross-rail-r006r007-build-test-validator-evidence.json",
    "cross-rail-r006r007-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R006R007 artifact: $file"
    }
    $paths += $path
}

Assert-NoCredentialValues $paths

$r005 = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-r005-reference.json")
$r006a = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-r006a-approval-reference.json")
$binding = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-approval-binding-validation.json")
$candidate = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-approved-candidate-set.json")
$idempotency = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-final-idempotency-plan.json")
$pre = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-pre-submission-safety-check.json")
$command = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-lmax-demo-sandbox-command.json")
$orders = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-sandbox-order-submission-result.json")
$fills = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-sandbox-fill-report.json")
$flatten = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-sandbox-flatten-result.json")
$residual = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-sandbox-residual-check.json")
$recon = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-reconciliation-result.json")
$pnl = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-pnl-readiness-result.json")
$safety = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-sandbox-safety-boundary.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r006r007-no-production-safety-audit.json")

$expectedRiskId = "risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001"
$expectedOperatorId = "operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001"

if ([string]$r005.R005Status -ne "BlockedMissingSandboxApprovals") {
    Fail "R005 status must be BlockedMissingSandboxApprovals"
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

if ([string]$r006a.RiskReviewId -ne $expectedRiskId) {
    Fail "R006A risk approval mismatch"
}
if ([string]$r006a.OperatorApprovalId -ne $expectedOperatorId) {
    Fail "R006A operator approval mismatch"
}
Assert-True $r006a.ApprovalsAreSandboxOnly "R006A approvals sandbox-only"
Assert-False $r006a.ApprovalsAuthorizeProduction "R006A approvals authorize production"
Assert-False $r006a.ApprovalsAuthorizeExecutionNow "R006A approvals authorize execution now"
Assert-True $r006a.ApprovalsAreNotCredentials "R006A approvals not credentials"
Assert-True $r006a.ApprovalsAreNotOrderRouteOrFillIds "R006A approvals not order/route/fill ids"

if ([string]$binding.Status -ne "InputsResolved") {
    Fail "approval binding must be InputsResolved"
}
Assert-True $binding.RiskReviewIdMatchesR006A "risk approval must match R006A"
Assert-True $binding.OperatorApprovalIdMatchesR006A "operator approval must match R006A"
Assert-False $binding.RiskReviewIdIsPlaceholder "risk approval placeholder"
Assert-False $binding.OperatorApprovalIdIsPlaceholder "operator approval placeholder"
Assert-False $binding.ApprovalsAuthorizeProduction "approvals authorize production"
Assert-False $binding.ApprovalsAuthorizeExecutionNow "approvals authorize execution now"
Assert-True $binding.PhaseAApprovalBindingPassed "Phase A approval binding"

Assert-CandidateRows $candidate.CandidateRows "approved candidate set"
if ([string]$candidate.PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "approved candidate set must use ExistingLmaxDemoProfile"
}
if ([string]$candidate.RiskReviewId -ne $expectedRiskId) {
    Fail "approved candidate set risk approval mismatch"
}
if ([string]$candidate.OperatorApprovalId -ne $expectedOperatorId) {
    Fail "approved candidate set operator approval mismatch"
}
Assert-True $candidate.SandboxOnly "candidate SandboxOnly"
Assert-True $candidate.NoLive "candidate NoLive"
Assert-True $candidate.NoProduction "candidate NoProduction"
Assert-False $candidate.OrdersSubmittedNow "candidate OrdersSubmittedNow"

if ([string]$idempotency.Status -ne "IdempotencyPlanDefined") {
    Fail "idempotency plan must be defined"
}
if ($idempotency.PerCandidateIdempotencyKeyPreview.Count -ne 3) {
    Fail "idempotency plan must contain exactly 3 keys"
}
$keys = @($idempotency.PerCandidateIdempotencyKeyPreview | ForEach-Object { [string]$_.IdempotencyKeyPreview })
if (($keys | Select-Object -Unique).Count -ne 3) {
    Fail "duplicate idempotency keys detected"
}
Assert-True $idempotency.DuplicateSubmissionPrevented "DuplicateSubmissionPrevented"

if ([string]$pre.Status -ne "Passed") {
    if ([string]$pre.Status -ne "Failed") {
        Fail "pre-submission safety check status must be Passed or Failed"
    }
}
Assert-True $pre.PhaseAApprovalBindingPassed "precheck PhaseAApprovalBindingPassed"
Assert-True $pre.CredentialValuesRedacted "precheck CredentialValuesRedacted"
Assert-True $pre.SandboxOnly "precheck SandboxOnly"
Assert-True $pre.NoProduction "precheck NoProduction"
if ([int]$pre.MaxCandidateCount -gt 3) {
    Fail "max candidate count exceeds 3"
}
if ([decimal]$pre.MaxOrderQuantity -gt [decimal]"0.1") {
    Fail "max order quantity exceeds 0.1"
}

if ([string]$pre.Status -eq "Failed") {
    Assert-False $pre.AllPreconditionsSatisfied "failed precheck AllPreconditionsSatisfied"
    Assert-True $pre.MissingExecutionSafetyInvocation "failed precheck MissingExecutionSafetyInvocation"
    Assert-False $pre.PhaseBExecutionAllowed "failed precheck PhaseBExecutionAllowed"
    Assert-False $pre.FixDemoSessionAllowedNow "failed precheck FixDemoSessionAllowedNow"
    Assert-False $pre.LmaxDemoCallAllowedNow "failed precheck LmaxDemoCallAllowedNow"
    Assert-False $pre.OrdersSubmittedNow "failed precheck OrdersSubmittedNow"
}

if ([string]$command.Status -eq "Executed") {
    Assert-True $pre.AllPreconditionsSatisfied "executed command requires all preconditions"
    Assert-True $pre.PhaseBExecutionAllowed "executed command requires PhaseBExecutionAllowed"
} else {
    if ([string]$command.Status -ne "NotExecuted") {
        Fail "command status must be Executed or NotExecuted"
    }
    Assert-False $command.LmaxDemoCallMade "command LmaxDemoCallMade"
    Assert-False $command.FixDemoSessionOpened "command FixDemoSessionOpened"
}
Assert-False $command.ProductionLiveAllowed "command ProductionLiveAllowed"
Assert-True $command.CredentialValuesRedacted "command CredentialValuesRedacted"

if ([string]$orders.Status -eq "Succeeded") {
    if ([int]$orders.SubmittedOrderCount -gt 3) {
        Fail "submitted order count exceeds 3"
    }
    Assert-CandidateRows $orders.Orders "submitted orders"
} else {
    if ([string]$orders.Status -notin @("ControlledFailure", "NotExecuted")) {
        Fail "order submission status must be Succeeded, ControlledFailure, or NotExecuted"
    }
    if ([int]$orders.SubmittedOrderCount -ne 0) {
        Fail "non-executed or controlled failure artifact must not claim submitted orders"
    }
}

if ([string]$fills.Status -eq "FillsCaptured") {
    Assert-False $fills.ProductionFill "fills ProductionFill"
} elseif ([string]$fills.Status -notin @("NoFills", "ControlledFailure")) {
    Fail "fill status must be FillsCaptured, NoFills, or ControlledFailure"
}

if ([string]$orders.Status -eq "Succeeded" -and [int]$fills.FillCount -gt 0) {
    if ([string]$flatten.Status -eq "FlattenNotRequired") {
        Fail "flatten must be attempted after fills unless explicitly not required"
    }
}

if ([string]$flatten.Status -eq "FlattenSucceeded") {
    if ([string]$residual.Status -ne "ResidualZero") {
        Fail "flatten succeeded requires ResidualZero"
    }
    Assert-True $residual.AllResidualsZero "residual AllResidualsZero"
} elseif ([string]$flatten.Status -notin @("FlattenNotRequired", "ControlledFailure")) {
    Fail "flatten status must be FlattenSucceeded, FlattenNotRequired, or ControlledFailure"
}

if ([string]$residual.Status -eq "ResidualZero") {
    Assert-True $residual.AllResidualsZero "residual zero status must have AllResidualsZero"
}

if ([string]$recon.Status -eq "ReconciledSandboxLifecycle") {
    if ([int]$recon.ExpectedOrders -gt 3 -or [int]$recon.ActualOrders -gt 3) {
        Fail "reconciled lifecycle order counts exceed 3"
    }
} elseif ([string]$recon.Status -notin @("ControlledFailure", "NotAvailable")) {
    Fail "reconciliation status must be ReconciledSandboxLifecycle, ControlledFailure, or NotAvailable"
}

Assert-True $pnl.NotProductionPnl "PnL NotProductionPnl"
Assert-False $safety.ProductionLiveAllowed "safety ProductionLiveAllowed"
Assert-False $safety.ProductionCredentialUse "safety ProductionCredentialUse"
Assert-False $safety.ProductionLedgerMutation "safety ProductionLedgerMutation"
Assert-True $safety.CredentialValuesRedacted "safety CredentialValuesRedacted"
Assert-False $safety.DirectCrossExecutionAllowed "safety DirectCrossExecutionAllowed"
Assert-False $safety.UnsupportedDirectCrossesSubmitted "safety UnsupportedDirectCrossesSubmitted"
Assert-False $safety.QubesExecutableRun "safety QubesExecutableRun"
Assert-False $safety.NettingRun "safety NettingRun"
Assert-False $safety.NettedUsdWeightsProduced "safety NettedUsdWeightsProduced"

Assert-NoProductionAudit $audit

$executionSimArtifacts = Get-ChildItem -Path "artifacts/readiness/execution-sim" -Filter "*r006r007*" -ErrorAction SilentlyContinue
if ($executionSimArtifacts.Count -gt 0) {
    Fail "R006R007 artifacts must not be written under execution-sim"
}

Write-Host "CROSS-RAIL-R006R007 validator passed."
