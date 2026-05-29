param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R008 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing required file: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) { Fail "$Name must be true" }
}

function Assert-False($Value, [string]$Name) {
    if ($Value -ne $false) { Fail "$Name must be false" }
}

function Assert-NoCredentialValues([string[]]$Paths) {
    $patterns = @(
        '"CredentialValue"\s*:',
        '"CredentialValues"\s*:',
        '"PasswordValue"\s*:',
        '"SecretValue"\s*:',
        '554=[^|\r\n]+',
        'LMAX_DEMO_FIX_PASSWORD"\s*:\s*"[^"]+"'
    )
    foreach ($path in $Paths) {
        $content = Get-Content -LiteralPath $path -Raw
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) {
                Fail "credential value-like content persisted in $path"
            }
        }
    }
}

function Assert-Candidates($Rows, [string]$Name) {
    if ($null -eq $Rows -or $Rows.Count -ne 3) {
        Fail "$Name must contain exactly three rows"
    }
    $expected = @{
        AUDUSD = "SELL"
        EURUSD = "SELL"
        GBPUSD = "BUY"
    }
    foreach ($row in $Rows) {
        $symbol = [string]$row.Symbol
        if (-not $expected.ContainsKey($symbol)) {
            Fail "$Name contains unsupported/direct-cross symbol $symbol"
        }
        if ([string]$row.Side -ne $expected[$symbol]) {
            Fail "$Name side mismatch for ${symbol}: $($row.Side)"
        }
        if ([decimal]$row.Quantity -ne [decimal]"0.1") {
            Fail "$Name quantity must be 0.1 for ${symbol}: $($row.Quantity)"
        }
    }
}

$requiredFiles = @(
    "cross-rail-r008-summary.md",
    "cross-rail-r008-r006r007-reference.json",
    "cross-rail-r008-pre-submission-safety-check.json",
    "cross-rail-r008-lmax-demo-fix-session-result.json",
    "cross-rail-r008-sandbox-order-submission-result.json",
    "cross-rail-r008-sandbox-fill-report.json",
    "cross-rail-r008-sandbox-flatten-result.json",
    "cross-rail-r008-sandbox-residual-check.json",
    "cross-rail-r008-sandbox-reconciliation-result.json",
    "cross-rail-r008-sandbox-pnl-input-readiness.json",
    "cross-rail-r008-no-production-safety-audit.json",
    "cross-rail-r008-build-test-validator-evidence.json",
    "cross-rail-r008-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R008 artifact: $file"
    }
    $paths += $path
}
$paths += @(Get-ChildItem -Path $ArtifactRoot -Filter "cross-rail-r008-raw-*.json" | Select-Object -ExpandProperty FullName)
Assert-NoCredentialValues $paths

$ref = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-r006r007-reference.json")
$pre = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-pre-submission-safety-check.json")
$fix = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-lmax-demo-fix-session-result.json")
$orders = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-sandbox-order-submission-result.json")
$fills = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-sandbox-fill-report.json")
$flatten = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-sandbox-flatten-result.json")
$residual = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-sandbox-residual-check.json")
$recon = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-sandbox-reconciliation-result.json")
$pnl = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-sandbox-pnl-input-readiness.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-no-production-safety-audit.json")
$evidence = Read-Json (Join-Path $ArtifactRoot "cross-rail-r008-build-test-validator-evidence.json")

if ([string]$ref.RiskReviewId -ne "risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001") {
    Fail "risk review id mismatch"
}
if ([string]$ref.OperatorApprovalId -ne "operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001") {
    Fail "operator approval id mismatch"
}
if ([string]$ref.PaperAccountProfile -ne "ExistingLmaxDemoProfile") {
    Fail "paper account/profile mismatch"
}
Assert-True $ref.PhaseAApprovalBindingPassed "R006R007 phase A approval binding"
Assert-False $ref.PhaseBPreviouslyRun "R006R007 phase B previously run"
Assert-Candidates $ref.CandidateRows "R006R007 candidate rows"

if ([string]$pre.Status -ne "Passed") { Fail "pre-submission safety check must pass" }
Assert-True $pre.AllRequiredFlagsPresent "AllRequiredFlagsPresent"
Assert-True $pre.ApprovalsMatched "ApprovalsMatched"
Assert-True $pre.SandboxOnly "precheck SandboxOnly"
Assert-True $pre.NoLive "precheck NoLive"
Assert-True $pre.NoProduction "precheck NoProduction"
Assert-True $pre.LmaxDemoOnly "precheck LmaxDemoOnly"
Assert-True $pre.FixDemoSessionAllowed "precheck FixDemoSessionAllowed"
Assert-True $pre.CredentialValuesRedacted "precheck CredentialValuesRedacted"
Assert-False $pre.DuplicateCompletedIdempotencyKeys "DuplicateCompletedIdempotencyKeys"
if ([int]$pre.CandidateCount -gt 3) { Fail "candidate count exceeds 3" }
if ([decimal]$pre.MaxOrderQuantity -gt [decimal]"0.1") { Fail "max order quantity exceeds 0.1" }

if ([string]$fix.Status -ne "Opened") { Fail "FIX demo session must be opened for successful R008" }
Assert-True $fix.SandboxOnly "fix SandboxOnly"
Assert-True $fix.CredentialValuesRedacted "fix CredentialValuesRedacted"
Assert-False $fix.ProductionFixSession "fix ProductionFixSession"

if ([string]$orders.Status -ne "Succeeded") { Fail "order submission result must be Succeeded" }
if ([int]$orders.SubmittedOrderCount -ne 3) { Fail "submitted order count must be 3" }
Assert-Candidates $orders.Orders "submitted orders"
$keys = @($orders.Orders | ForEach-Object { [string]$_.IdempotencyKey })
if (($keys | Select-Object -Unique).Count -ne 3) { Fail "duplicate idempotency keys" }
foreach ($order in $orders.Orders) {
    Assert-True $order.Filled "order $($order.Symbol) Filled"
    if ([decimal]$order.FillQuantity -ne [decimal]"0.1") { Fail "fill quantity mismatch for $($order.Symbol)" }
    if ([string]::IsNullOrWhiteSpace([string]$order.SandboxOrderId)) { Fail "missing sandbox order id for $($order.Symbol)" }
}

if ([string]$fills.Status -ne "FillsCaptured") { Fail "fills must be captured" }
if ([int]$fills.FillCount -ne 3) { Fail "fill count must be 3" }
Assert-False $fills.ProductionFill "fills ProductionFill"
Assert-True $fills.CredentialValuesRedacted "fills CredentialValuesRedacted"

if ([string]$flatten.Status -ne "FlattenSucceeded") { Fail "flatten must succeed after fills" }
if ([int]$flatten.FlattenOrderCount -ne 3) { Fail "flatten order count must be 3" }
foreach ($flat in $flatten.FlattenOrders) {
    Assert-True $flat.Filled "flatten $($flat.Symbol) Filled"
    if ([decimal]$flat.FillQuantity -ne [decimal]"0.1") { Fail "flatten quantity mismatch for $($flat.Symbol)" }
}

if ([string]$residual.Status -ne "ResidualZero") { Fail "residual status must be ResidualZero" }
Assert-True $residual.AllResidualsZero "AllResidualsZero"
foreach ($prop in $residual.ResidualBySymbol.PSObject.Properties) {
    if ([decimal]$prop.Value -ne [decimal]0) { Fail "non-zero residual for $($prop.Name)" }
}

if ([string]$recon.Status -ne "ReconciledSandboxLifecycle") { Fail "reconciliation must be ReconciledSandboxLifecycle" }
if ([int]$recon.ExpectedOrders -ne 3 -or [int]$recon.ActualOrders -ne 3) { Fail "order reconciliation counts mismatch" }
if ([int]$recon.ExpectedFills -ne 3 -or [int]$recon.ActualFills -ne 3) { Fail "fill reconciliation counts mismatch" }
if ([int]$recon.ExpectedFlattenOrders -ne 3 -or [int]$recon.ActualFlattenOrders -ne 3) { Fail "flatten reconciliation counts mismatch" }
if (@($recon.Breaks).Count -ne 0) { Fail "reconciliation breaks must be empty" }

Assert-True $pnl.NotProductionPnl "pnl NotProductionPnl"
Assert-True $pnl.FillBasedInputsAvailable "pnl FillBasedInputsAvailable"
Assert-False $pnl.MarkInputsAvailable "pnl MarkInputsAvailable"
Assert-False $pnl.CostInputsAvailable "pnl CostInputsAvailable"

if ([string]$audit.Status -ne "Passed") { Fail "no-production audit status must be Passed" }
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
    Assert-True $audit.$name "no-production audit $name"
}

Assert-True $evidence.SandboxExecutionRan "evidence SandboxExecutionRan"
Assert-True $evidence.NoProductionExecutionOccurred "evidence NoProductionExecutionOccurred"

Write-Host "CROSS-RAIL-R008 validator passed."
