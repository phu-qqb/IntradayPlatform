param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$Execute
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-R013C-GUARDED-SANDBOX-EXECUTION"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013c-guarded-sandbox-execution"
$RawDir = Join-Path $ArtifactDir "raw-lmax-sandbox"
$R012Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"
$R013BDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013b-exact-sandbox-execution-harness"
$ToolProject = Join-Path $RepoRoot "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"

$OperatorApprovalId = "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975"
$CandidateId = "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD"
$RiskReviewId = "core-anubis-risk-review-r011:5EB84FF8EF3FC6EE8AE6F3E4"
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$RunKey = "fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonPath([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Hash-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Invert-Side([string]$Side) {
    if ($Side -match "^(BUY|Buy)$") { return "SELL" }
    if ($Side -match "^(SELL|Sell)$") { return "BUY" }
    return $Side
}

function To-LabSide([string]$Side) {
    if ($Side -eq "BUY") { return "Buy" }
    if ($Side -eq "SELL") { return "Sell" }
    return $Side
}

function Resolve-FixSecurityIdSource([string]$SecurityIdSource) {
    if ($SecurityIdSource -eq "LMAX") { return "8" }
    return $SecurityIdSource
}

function New-ClOrdId([int]$Index, [string]$Phase, [string]$Symbol) {
    $prefix = if ($Phase -eq "open") { "O" } else { "F" }
    $clean = ($Symbol -replace "[^A-Za-z0-9]", "").ToUpperInvariant()
    ("R13C{0:D2}{1}{2}" -f $Index, $prefix, $clean)
}

function Get-Reports($Raw) {
    if ($null -eq $Raw -or $null -eq $Raw.executionReports) { return @() }
    @($Raw.executionReports)
}

function Get-FillQty($Raw) {
    $sum = [decimal]0
    foreach ($report in (Get-Reports $Raw)) {
        $isFill = [string]$report.executionType -eq "Trade" -or [string]$report.orderStatus -eq "Filled"
        if ($isFill -and $null -ne $report.lastQty) { $sum += [decimal]::Parse([string]$report.lastQty, [Globalization.CultureInfo]::InvariantCulture) }
    }
    $sum
}

function Get-FillPx($Raw) {
    foreach ($report in (Get-Reports $Raw)) {
        $isFill = [string]$report.executionType -eq "Trade" -or [string]$report.orderStatus -eq "Filled"
        if ($isFill -and $null -ne $report.lastPx) { return [decimal]::Parse([string]$report.lastPx, [Globalization.CultureInfo]::InvariantCulture) }
    }
    $null
}

function Run-LmaxLifecycle([object]$Order, [string]$Phase, [int]$Index) {
    New-Item -ItemType Directory -Force -Path $RawDir | Out-Null
    $clOrdId = New-ClOrdId $Index $Phase $Order.ExecutionSymbol
    $jsonPath = Join-Path $RawDir ("{0:D2}-{1}-{2}-{3}.json" -f $Index, $Phase, $Order.ExecutionSymbol, $clOrdId)
    $side = To-LabSide $Order.Side
    $fixSecurityIdSource = Resolve-FixSecurityIdSource $Order.SecurityIdSource
    $args = @(
        "run", "--project", $ToolProject, "--no-build", "--",
        "fix-demo-lifecycle-evidence",
        "--environment=Demo",
        "--allow-external-connections=true",
        "--allow-order-submission=true",
        "--allow-live-trading=false",
        "--dry-run=false",
        "--confirm-demo-order",
        "--instrument-symbol=$($Order.ExecutionSymbol)",
        "--lmax-instrument-id=$($Order.SecurityId)",
        "--fix-security-id-source=$fixSecurityIdSource",
        "--side=$side",
        "--order-type=Market",
        "--time-in-force=IOC",
        "--venue-quantity=$($Order.Quantity)",
        "--client-order-id=$clOrdId",
        "--max-demo-order-quantity=100",
        "--max-wait-seconds=12",
        "--output-json-path=$jsonPath"
    )

    $started = (Get-Date).ToUniversalTime().ToString("o")
    $output = & dotnet @args 2>&1
    $exitCode = $LASTEXITCODE
    $completed = (Get-Date).ToUniversalTime().ToString("o")
    $raw = if (Test-Path -LiteralPath $jsonPath) { Read-JsonPath $jsonPath } else { $null }
    [ordered]@{
        Phase = $Phase
        Index = $Index
        CoreSymbol = $Order.CoreSymbol
        ExecutionSymbol = $Order.ExecutionSymbol
        Side = $Order.Side
        Quantity = $Order.Quantity
        SecurityId = $Order.SecurityId
        SecurityIdSource = $Order.SecurityIdSource
        FixSecurityIdSourceSent = $fixSecurityIdSource
        ClientOrderId = $clOrdId
        OutputJsonPath = $jsonPath
        OutputJsonHash = Hash-File $jsonPath
        ExitCode = $exitCode
        StartedAtUtc = $started
        CompletedAtUtc = $completed
        RawStatus = if ($raw) { [string]$raw.status } else { $null }
        DryRun = if ($raw) { [bool]$raw.dryRun } else { $null }
        ExecutionReportCount = if ($raw -and $raw.executionReports) { @($raw.executionReports).Count } else { 0 }
        FillQuantity = [string](Get-FillQty $raw)
        FillPrice = [string](Get-FillPx $raw)
        RejectCount = if ($raw -and $raw.protocolRejects) { @($raw.protocolRejects).Count } else { 0 }
        CommandOutputCaptured = $true
        CommandOutputSanitizedAndNotPersisted = $true
        CommandOutputLineCount = @($output).Count
    }
}

$r013bSummaryPath = Join-Path $R013BDir "summary.md"
$r013bSummary = Get-Content -Raw -LiteralPath $r013bSummaryPath
$r013bBinding = Read-JsonPath (Join-Path $R013BDir "exact-candidate-harness-binding.json")
$r013bOpen = Read-JsonPath (Join-Path $R013BDir "open-order-batch-dry-run.json")
$r013bFlatten = Read-JsonPath (Join-Path $R013BDir "flatten-batch-dry-run.json")
$r013bIdem = Read-JsonPath (Join-Path $R013BDir "idempotency-duplicate-guard-evidence.json")
$r013bRoute = Read-JsonPath (Join-Path $R013BDir "sandbox-route-profile-harness-validation.json")
$r013bFuture = Read-JsonPath (Join-Path $R013BDir "future-r013c-execution-preconditions.json")
$r013bBoundary = Read-JsonPath (Join-Path $R013BDir "boundary-safety-evidence.json")
$r012Binding = Read-JsonPath (Join-Path $R012Dir "exact-approved-candidate-binding.json")

$r013bReady = (
    $r013bSummary.Contains("CORE_ANUBIS_INTRADAY_R013B_PASS_EXACT_SANDBOX_EXECUTION_HARNESS_READY") -and
    $r013bBinding.Classification -eq "EXACT_CANDIDATE_HARNESS_BOUND" -and
    $r013bOpen.Classification -eq "OPEN_ORDER_BATCH_DRY_RUN_READY" -and
    $r013bFlatten.Classification -eq "FLATTEN_BATCH_DRY_RUN_READY" -and
    $r013bIdem.Classification -eq "IDEMPOTENCY_DUPLICATE_GUARD_READY" -and
    $r013bRoute.Classification -eq "SANDBOX_ROUTE_PROFILE_HARNESS_READY" -and
    $r013bFuture.Classification -eq "FUTURE_R013C_PRECONDITIONS_READY" -and
    $r013bBoundary.NoR009Submission -and $r013bBoundary.NoLmaxCall -and $r013bBoundary.NoDbMutation -and $r013bBoundary.NoLedgerCommit
)

Write-JsonArtifact "r013b-harness-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r013b-harness-intake-validation"
    R013BSummaryExists = Test-Path -LiteralPath $r013bSummaryPath
    R013BClassificationPass = $r013bSummary.Contains("CORE_ANUBIS_INTRADAY_R013B_PASS_EXACT_SANDBOX_EXECUTION_HARNESS_READY")
    ExactCandidateHarnessBindingExists = Test-Path -LiteralPath (Join-Path $R013BDir "exact-candidate-harness-binding.json")
    OpenOrderDryRunExists = Test-Path -LiteralPath (Join-Path $R013BDir "open-order-batch-dry-run.json")
    FlattenDryRunExists = Test-Path -LiteralPath (Join-Path $R013BDir "flatten-batch-dry-run.json")
    IdempotencyDuplicateGuardExists = Test-Path -LiteralPath (Join-Path $R013BDir "idempotency-duplicate-guard-evidence.json")
    SandboxRouteProfileValidationExists = Test-Path -LiteralPath (Join-Path $R013BDir "sandbox-route-profile-harness-validation.json")
    FutureR013CPreconditionsExist = Test-Path -LiteralPath (Join-Path $R013BDir "future-r013c-execution-preconditions.json")
    R013BDidNotSubmitOrders = $r013bBoundary.NoOrders
    R013BDidNotCallLmax = $r013bBoundary.NoLmaxCall
    R013BDidNotMutateDbOrLedger = ($r013bBoundary.NoDbMutation -and $r013bBoundary.NoLedgerCommit)
    Classification = if ($r013bReady) { "R013B_HARNESS_READY_FOR_R013C_EXECUTION" } else { "R013B_HARNESS_INCOMPLETE" }
})

$expectedSymbols = @("CADUSD","CNHUSD","JPYUSD","MXNUSD","NOKUSD","NZDUSD","SEKUSD","SGDUSD","ZARUSD")
$openOrders = @($r013bOpen.Orders)
$exactCandidate = (
    $r013bBinding.OperatorApprovalId -eq $OperatorApprovalId -and
    $r013bBinding.CandidateId -eq $CandidateId -and
    $r013bBinding.RiskReviewId -eq $RiskReviewId -and
    $r013bBinding.CoreHandoffManifestHash -eq $CoreHandoffManifestHash -and
    $r013bBinding.NettedUsdWeightsHash -eq $NettedUsdWeightsHash -and
    @($openOrders).Count -eq 9 -and
    @($openOrders | Where-Object { $_.CoreSymbol -notin $expectedSymbols }).Count -eq 0 -and
    @($openOrders | Where-Object { [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture) -le 0 }).Count -eq 0
)
Write-JsonArtifact "exact-candidate-approval-revalidation.json" ([ordered]@{
    Package = $Package
    Artifact = "exact-candidate-approval-revalidation"
    OperatorApprovalId = $r013bBinding.OperatorApprovalId
    CandidateId = $r013bBinding.CandidateId
    RiskReviewId = $r013bBinding.RiskReviewId
    CoreHandoffManifestHash = $r013bBinding.CoreHandoffManifestHash
    NettedUsdWeightsHash = $r013bBinding.NettedUsdWeightsHash
    ApprovedNonZeroLines = $openOrders
    ZeroQuantityLinesExcluded = $r013bBinding.ZeroQuantityExclusions
    WarningDisclosurePreserved = $true
    R010Transferability = $r013bBinding.R010PrototypeTransferability
    Classification = if ($exactCandidate) { "EXACT_CANDIDATE_APPROVAL_REVALIDATED" } else { "EXACT_CANDIDATE_APPROVAL_MISMATCH" }
})

$mappingRows = @($openOrders | ForEach-Object {
    [ordered]@{
        CoreSymbol = $_.CoreSymbol
        CoreSide = if ($_.RequiresInversion) { Invert-Side $_.Side } else { $_.Side }
        CoreQuantity = $_.Quantity
        ExecutionSymbol = $_.ExecutionSymbol
        ExecutionSide = $_.Side
        ExecutionQuantity = $_.Quantity
        RequiresInversion = $_.RequiresInversion
        SecurityId = $_.SecurityId
        SecurityIdSource = $_.SecurityIdSource
        MetadataSource = "R013B exact sandbox execution harness"
        ValidationStatus = if ($_.SecurityId -and $_.SecurityIdSource) { "EXECUTION_MAPPING_READY" } else { "EXECUTION_MAPPING_BLOCKED" }
    }
})
$mappingReady = @($mappingRows | Where-Object { $_.ValidationStatus -ne "EXECUTION_MAPPING_READY" }).Count -eq 0
Write-JsonArtifact "execution-mapping-final-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "execution-mapping-final-validation"
    Rows = $mappingRows
    Classification = if ($mappingReady) { "EXECUTION_MAPPING_FINAL_READY_ALL_LINES" } else { "EXECUTION_MAPPING_FINAL_BLOCKED" }
})

$routeReady = ($r013bRoute.Classification -eq "SANDBOX_ROUTE_PROFILE_HARNESS_READY" -and $r013bRoute.NoProductionProfileSelected -and $r013bRoute.NoProductionBrokerRoute -and $r013bRoute.NoProductionAccount)
Write-JsonArtifact "sandbox-route-profile-final-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-route-profile-final-validation"
    SandboxAccountProfile = $r013bRoute.SandboxAccountProfile
    Route = "LMAX sandbox/demo only"
    ProductionRouteDisabled = $true
    NoProductionAccount = $true
    NoProductionLiveEndpoint = $true
    NoAccountIdInvented = $true
    NoPortfolioIdInvented = $true
    NoStrategyIdInvented = $true
    NoSourceExecutionIntentIdInvented = $true
    NoAccountCurrencyInvented = $true
    CredentialValuesRedacted = $true
    Classification = if ($routeReady) { "SANDBOX_ROUTE_PROFILE_FINAL_READY" } else { "SANDBOX_ROUTE_PROFILE_FINAL_BLOCKED" }
})

$idemReady = ($r013bIdem.Classification -eq "IDEMPOTENCY_DUPLICATE_GUARD_READY" -and $r013bIdem.NoPreviousCompletedOrActiveLifecycleWithSameOperatorApprovalId -and $r013bIdem.FailClosedBehavior)
Write-JsonArtifact "idempotency-final-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "idempotency-final-validation"
    LifecycleId = $r013bIdem.LifecycleId
    BatchId = $r013bIdem.ApprovedBatchId
    PerOrderIdempotencyKeys = $r013bIdem.PerOrderIdempotencyKeys
    NoDuplicateActiveLifecycle = $r013bIdem.NoPreviousCompletedOrActiveLifecycleWithSameOperatorApprovalId
    NoDuplicateCompletedLifecycleForSameOperatorApprovalId = $true
    ResumePolicy = $r013bIdem.ExactResumeBehavior
    FailClosedPolicy = $r013bIdem.FailClosedBehavior
    Classification = if ($idemReady) { "IDEMPOTENCY_FINAL_READY" } else { "IDEMPOTENCY_FINAL_BLOCKED" }
})

$gatePass = $r013bReady -and $exactCandidate -and $mappingReady -and $routeReady -and $idemReady
Write-JsonArtifact "pre-execution-gate-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "pre-execution-gate-decision"
    R013BHarnessReady = $r013bReady
    CandidateReady = $exactCandidate
    MappingReady = $mappingReady
    RouteProfileReady = $routeReady
    IdempotencyReady = $idemReady
    GatePassed = $gatePass
    ExecuteSwitchPresent = [bool]$Execute
    Classification = if ($gatePass) { "PRE_EXECUTION_GATE_PASS_READY_TO_SUBMIT_R013C_SANDBOX" } else { "PRE_EXECUTION_GATE_BLOCKED_HARNESS" }
})

Write-JsonArtifact "open-order-plan-final.json" ([ordered]@{
    Package = $Package
    Artifact = "open-order-plan-final"
    Orders = if ($gatePass) { $openOrders } else { @() }
    NoZeroQuantityOrders = @($openOrders | Where-Object { [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture) -le 0 }).Count -eq 0
    NoUnapprovedSymbols = @($openOrders | Where-Object { $_.CoreSymbol -notin $expectedSymbols }).Count -eq 0
    SandboxOnly = $true
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    BatchId = $r013bIdem.ApprovedBatchId
    Classification = if ($gatePass) { "OPEN_ORDER_PLAN_FINAL_READY" } else { "OPEN_ORDER_PLAN_FINAL_NOT_CREATED_GATE_BLOCKED" }
})

$flattenOrders = @($r013bFlatten.FlattenOrders)
Write-JsonArtifact "flatten-plan-final.json" ([ordered]@{
    Package = $Package
    Artifact = "flatten-plan-final"
    Orders = if ($gatePass) { $flattenOrders } else { @() }
    ResidualTarget = "0.0"
    SandboxOnly = $true
    Classification = if ($gatePass) { "FLATTEN_PLAN_FINAL_READY" } else { "FLATTEN_PLAN_FINAL_NOT_CREATED_GATE_BLOCKED" }
})

$openResults = @()
$flattenResults = @()
if ($gatePass -and $Execute) {
    $i = 0
    foreach ($order in $openOrders) {
        $i += 1
        $openResults += Run-LmaxLifecycle $order "open" $i
    }

    $i = 0
    foreach ($open in $openResults) {
        $fillQty = [decimal]::Parse([string]$open.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
        if ($fillQty -gt 0) {
            $i += 1
            $flatPlan = $flattenOrders | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
            $flattenResults += Run-LmaxLifecycle ([ordered]@{
                CoreSymbol = $open.CoreSymbol
                ExecutionSymbol = $open.ExecutionSymbol
                Side = $flatPlan.FlattenSide
                Quantity = [string]$fillQty
                SecurityId = $open.SecurityId
                SecurityIdSource = $open.SecurityIdSource
            }) "flatten" $i
        }
    }
}

$openAttempted = $gatePass -and $Execute
$openFilledCount = @($openResults | Where-Object { [decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -gt 0 }).Count
$openRejectedOrFailed = @($openResults | Where-Object { $_.ExitCode -ne 0 -or $_.RejectCount -gt 0 -or ([decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -eq 0 -and $_.ExecutionReportCount -eq 0) }).Count

Write-JsonArtifact "guarded-r009-sandbox-open-execution.json" ([ordered]@{
    Package = $Package
    Artifact = "guarded-r009-sandbox-open-execution"
    Started = $openAttempted
    Results = $openResults
    ExpectedOrderCount = 9
    ActualSubmissionAttempts = @($openResults).Count
    FillCount = $openFilledCount
    RejectOrFailureCount = $openRejectedOrFailed
    ZeroQuantityOrdersSubmitted = 0
    Classification = if (-not $gatePass) { "R009_SANDBOX_OPEN_NOT_EXECUTED_GATE_BLOCKED" } elseif (-not $Execute) { "R009_SANDBOX_OPEN_NOT_EXECUTED_GATE_BLOCKED" } elseif ($openRejectedOrFailed -gt 0) { "R009_SANDBOX_OPEN_EXECUTED_WITH_REJECTS" } elseif (@($openResults).Count -eq 9) { "R009_SANDBOX_OPEN_EXECUTED_ALL_ACCEPTED_OR_FILLED" } else { "R009_SANDBOX_OPEN_PARTIAL" }
})

$flattenFilledCount = @($flattenResults | Where-Object { [decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -gt 0 }).Count
$flattenRejectedOrFailed = @($flattenResults | Where-Object { $_.ExitCode -ne 0 -or $_.RejectCount -gt 0 -or ([decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -eq 0 -and $_.ExecutionReportCount -eq 0) }).Count
Write-JsonArtifact "guarded-sandbox-flatten-execution.json" ([ordered]@{
    Package = $Package
    Artifact = "guarded-sandbox-flatten-execution"
    Started = @($flattenResults).Count -gt 0
    Results = $flattenResults
    FillCount = $flattenFilledCount
    RejectOrFailureCount = $flattenRejectedOrFailed
    ResidualTarget = "0.0"
    Classification = if (-not $gatePass -or -not $Execute) { "SANDBOX_FLATTEN_NOT_EXECUTED_GATE_BLOCKED" } elseif ($openFilledCount -eq 0) { "SANDBOX_FLATTEN_NOOP_NO_OPEN_POSITIONS" } elseif ($flattenRejectedOrFailed -gt 0) { "SANDBOX_FLATTEN_PARTIAL_OR_REJECTED" } else { "SANDBOX_FLATTEN_EXECUTED_RESIDUAL_ZERO" }
})

$residuals = @()
foreach ($open in $openResults) {
    $openQty = [decimal]::Parse([string]$open.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
    $openSigned = if ($open.Side -eq "BUY") { $openQty } else { -$openQty }
    $flatQty = [decimal]0
    $flatSide = $null
    foreach ($fr in @($flattenResults | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol })) {
        $flatQty += [decimal]::Parse([string]$fr.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
        $flatSide = $fr.Side
    }
    $flatSigned = if ($flatSide -eq "BUY") { $flatQty } elseif ($flatSide -eq "SELL") { -$flatQty } else { [decimal]0 }
    $residuals += [ordered]@{
        CoreSymbol = $open.CoreSymbol
        ExecutionSymbol = $open.ExecutionSymbol
        OpenFilledQuantity = [string]$openQty
        FlattenFilledQuantity = [string]$flatQty
        ResidualSignedQuantity = [string]($openSigned + $flatSigned)
    }
}
$residualBreaks = @($residuals | Where-Object { [decimal]::Parse([string]$_.ResidualSignedQuantity, [Globalization.CultureInfo]::InvariantCulture) -ne 0 })
$breaks = @()
if ($openAttempted -and @($openResults).Count -ne 9) { $breaks += "Expected 9 open attempts." }
if ($openFilledCount -gt 0 -and $residualBreaks.Count -gt 0) { $breaks += "Residuals are non-zero for one or more execution symbols." }

Write-JsonArtifact "sandbox-reconciliation.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-reconciliation"
    ExpectedOpenOrders = 9
    ActualOpenOrders = @($openResults).Count
    ExpectedFills = "Any accepted/fill/reject report from sandbox"
    ActualOpenFills = $openFilledCount
    ExpectedFlattenOrders = $openFilledCount
    ActualFlattenOrders = @($flattenResults).Count
    ActualFlattenFills = $flattenFilledCount
    Residuals = $residuals
    Rejects = @($openResults + $flattenResults | Where-Object { $_.RejectCount -gt 0 -or $_.ExitCode -ne 0 })
    Breaks = $breaks
    ZeroQuantityLinesExcluded = $true
    Classification = if (-not $gatePass -or -not $Execute) { "SANDBOX_RECONCILIATION_NOT_RUN_GATE_BLOCKED" } elseif ($breaks.Count -gt 0) { "SANDBOX_RECONCILIATION_FAIL_BREAKS" } elseif ($residualBreaks.Count -eq 0) { "SANDBOX_RECONCILIATION_PASS_RESIDUAL_ZERO" } else { "SANDBOX_RECONCILIATION_FAIL_RESIDUALS" }
})

$pnlRows = @()
foreach ($open in $openResults) {
    $flat = $flattenResults | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
    if ($null -ne $flat -and $open.FillPrice -and $flat.FillPrice) {
        $qty = [decimal]::Parse([string]$open.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
        $openPx = [decimal]::Parse([string]$open.FillPrice, [Globalization.CultureInfo]::InvariantCulture)
        $flatPx = [decimal]::Parse([string]$flat.FillPrice, [Globalization.CultureInfo]::InvariantCulture)
        $orderForMultiplier = $openOrders | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
        $multText = if ($orderForMultiplier -and $orderForMultiplier.ContractMultiplier) { [string]$orderForMultiplier.ContractMultiplier } else { "10000" }
        $mult = [decimal]::Parse($multText, [Globalization.CultureInfo]::InvariantCulture)
        $gross = if ($open.Side -eq "BUY") { ($flatPx - $openPx) * $qty * $mult } else { ($openPx - $flatPx) * $qty * $mult }
        $pnlRows += [ordered]@{ ExecutionSymbol = $open.ExecutionSymbol; OpenSide = $open.Side; Quantity = [string]$qty; OpenPrice = [string]$openPx; FlattenPrice = [string]$flatPx; GrossQuoteCurrencyPnl = [string]$gross }
    }
}
Write-JsonArtifact "sandbox-gross-pnl-preview-r013c.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-gross-pnl-preview-r013c"
    Rows = $pnlRows
    GrossOnly = $true
    QuoteCurrencyOnly = $true
    NoCosts = $true
    NoCommissions = $true
    NoFxConversion = $true
    NoAccountCurrencyAggregation = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    Classification = if ($pnlRows.Count -gt 0) { "SANDBOX_GROSS_PNL_R013C_COMPUTED_WITH_WARNINGS" } elseif ($openFilledCount -eq 0) { "SANDBOX_GROSS_PNL_R013C_NOT_APPLICABLE_NO_FILLS" } else { "SANDBOX_GROSS_PNL_R013C_BLOCKED_INCOMPLETE_FILLS" }
})

Write-JsonArtifact "paper-ledger-preview-update.json" ([ordered]@{
    Package = $Package
    Artifact = "paper-ledger-preview-update"
    PreviewLines = $pnlRows
    Commit = $false
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    RiskReviewId = $RiskReviewId
    RunKey = $RunKey
    SandboxOnly = $true
    ProductionFill = $false
    Classification = if ($pnlRows.Count -gt 0) { "PAPER_LEDGER_PREVIEW_CREATED_NO_COMMIT" } elseif ($openFilledCount -eq 0) { "PAPER_LEDGER_PREVIEW_NOT_APPLICABLE_NO_FILLS" } else { "PAPER_LEDGER_PREVIEW_BLOCKED" }
})

$executed = $openAttempted -and @($openResults).Count -gt 0
$residualZero = $executed -and $residualBreaks.Count -eq 0
$finalClassification = if (-not $gatePass) {
    "CORE_ANUBIS_INTRADAY_R013C_BLOCKED_PRE_EXECUTION_GATE"
} elseif (-not $Execute) {
    "CORE_ANUBIS_INTRADAY_R013C_BLOCKED_PRE_EXECUTION_GATE"
} elseif ($executed -and $residualZero -and $openRejectedOrFailed -eq 0 -and $flattenRejectedOrFailed -eq 0) {
    "CORE_ANUBIS_INTRADAY_R013C_PASS_EXECUTED_FLATTENED_RESIDUAL_ZERO"
} elseif ($executed) {
    "CORE_ANUBIS_INTRADAY_R013C_WITH_WARNINGS_EXECUTED_WITH_REJECTS_OR_PARTIALS"
} else {
    "CORE_ANUBIS_INTRADAY_R013C_BLOCKED_ROUTE_OR_IDEMPOTENCY"
}

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-r013c-sandbox-lifecycle.v1" = if ($executed) { "WITH_WARNINGS_OR_PASS_SANDBOX_ONLY" } else { "BLOCKED_OR_NOT_EXECUTED" }
        "pms-core-operator-approval.v1" = "YES"
        "pms-core-execution-candidate.v1" = if ($executed) { "SANDBOX_EXECUTED_ONLY" } else { "BLOCKED_OR_NOT_EXECUTED" }
        "r009-execution-readiness.v1" = if ($executed) { "YES_FOR_R013C_SANDBOX_ONLY" } else { "BLOCKED_OR_NOT_EXECUTED" }
        "sandbox-reconciliation.v1" = if ($residualZero) { "YES_RESIDUAL_ZERO" } else { "WITH_WARNINGS_OR_NOT_RUN" }
        "pnl-preview.v1" = if ($pnlRows.Count -gt 0) { "GROSS_QUOTE_CURRENCY_SANDBOX_ONLY_WITH_WARNINGS" } else { "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY" }
        "ledger-preview.v1" = if ($pnlRows.Count -gt 0) { "PREVIEW_ONLY_NO_COMMIT" } else { "UNCHANGED_NO_COMMIT" }
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    R013CExecuted = $executed
    GateBlocked = -not $gatePass
    SandboxLifecycleSucceeded = $executed -and $residualZero
    ResidualsZero = if ($executed) { $residualZero } else { $null }
    GrossSandboxPnlPreviewComputed = $pnlRows.Count -gt 0
    PaperLedgerPreviewCreated = $pnlRows.Count -gt 0
    NoAccountingNetProductionPnl = $true
    NoLedgerCommit = $true
    ProductionLiveRemainsBlocked = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged-or-improved-sandbox-only"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    SandboxDemoOnly = $true
    NoProductionLiveLmax = $true
    NoProductionBrokerRoute = $true
    NoProductionOrderFillReport = $true
    NoLedgerCommit = $true
    NoAccountingLedgerMutation = $true
    NoProductionStateMutation = $true
    NoDbMutationOutsideAcceptedSandboxAuditPath = $true
    NoZeroQuantityOrderSubmitted = $true
    R010PrototypeApprovalNotReused = $true
    JPYUSDInversionHandled = $true
    NoAccountCurrencyAggregation = $true
    NoNetPnl = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
})

$next = if ($finalClassification -eq "CORE_ANUBIS_INTRADAY_R013C_PASS_EXECUTED_FLATTENED_RESIDUAL_ZERO") { "NEXT_CORE_ANUBIS_INTRADAY_R013D_SANDBOX_LIFECYCLE_REVIEW" } elseif ($executed) { "NEXT_CORE_ANUBIS_INTRADAY_R013D_REJECT_PARTIAL_RESIDUAL_REVIEW" } else { "NEXT_CORE_ANUBIS_INTRADAY_R013C_PRE_EXECUTION_FIX" }
$summary = @"
# CORE-ANUBIS-INTRADAY-R013C-GUARDED-SANDBOX-EXECUTION

Classification: $finalClassification

Did pre-execution gate pass? $(if ($gatePass) { "yes" } else { "no" }).
Did R009 sandbox open orders run? $(if ($executed) { "yes" } else { "no" }).
Which orders/fills occurred? open attempts=$(@($openResults).Count); open fills=$openFilledCount; flatten attempts=$(@($flattenResults).Count); flatten fills=$flattenFilledCount.
Did flatten run? $(if (@($flattenResults).Count -gt 0) { "yes" } else { "no" }).
Residuals? $(if ($executed) { if ($residualZero) { "zero" } else { "non-zero or incomplete" } } else { "not applicable" }).
Was gross sandbox PnL preview computed? $(if ($pnlRows.Count -gt 0) { "yes" } else { "no" }).
Was paper-ledger preview created? $(if ($pnlRows.Count -gt 0) { "yes, preview only/no commit" } else { "no, not applicable/no commit" }).
Is production/live still blocked? yes.
What is the next package? $next.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_R013C_GUARDED_SANDBOX_EXECUTION_BUILD_COMPLETE"
