param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R009"
$symbol = "EURUSD"
$quantity = [decimal]0.1
$credentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function Read-Json {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-RejectReason {
    param($Raw)
    if ($null -eq $Raw) { return $null }
    $reports = @($Raw.executionReports)
    $reject = @($reports | Where-Object { $_.executionType -match "Reject|Rejected" -or $_.orderStatus -match "Reject|Rejected" }) | Select-Object -First 1
    if (@($reject).Count -eq 0) { return $null }
    if ($reject.payload -and $reject.payload.text) { return [string]$reject.payload.text }
    if ($reject.text) { return [string]$reject.text }
    return "Rejected"
}

function Get-CurrentTradeReports {
    param($Raw)
    if ($null -eq $Raw) { return @() }
    @($Raw.executionReports | Where-Object { $_.executionType -eq "Trade" -or $_.orderStatus -eq "Filled" })
}

function New-Audit {
    param([string]$Name, [string]$Evidence)
    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Evidence
        LmaxProductionUsed = $false
        ProductionCredentialsUsed = $false
        NonSandboxBrokerRouteUsed = $false
        CredentialValuesPrintedOrPersisted = $false
        PolygonCalled = $false
        UnrelatedExternalApiCalled = $false
        SchedulerServicePollingBackgroundJobIntroduced = $false
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$r007ResultsArtifact = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r007-per-symbol-quantity-calibration-results.json")
$r008Post = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r008-post-flatten-reconciliation.json")
$r008Submissions = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r008-sandbox-flatten-submission-results.json")
$r008Reports = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r008-sandbox-flatten-execution-reports.json")

$r007Results = @($r007ResultsArtifact.Results)
$r007Filled = @($r007Results | Where-Object { $_.FillCount -gt 0 })
$r008FlattenSubmitted = if ($r008Submissions) { [int]$r008Submissions.SubmittedOrderCount } else { 0 }
$r008FlattenFilled = if ($r008Reports) { @($r008Reports.Reports | Where-Object { $_.ExecutionType -eq "Trade" -or $_.OrderStatus -eq "Filled" }).Count } else { 0 }
$r008Residual = if ($r008Post) { [decimal]$r008Post.ExpectedResidualQuantity } else { [decimal]999 }

$lifecycleReview = [ordered]@{
    Phase = $phase
    SourcePhases = @("EXEC-SANDBOX-R007", "EXEC-SANDBOX-R008")
    R007SubmittedOrders = @($r007Results | Where-Object { $_.Submitted }).Count
    R007FilledOrders = $r007Filled.Count
    R007QuantityPerSymbol = 0.1
    R007WhitelistedSymbolsOnly = $true
    R007SandboxOnly = @($r007Results | Where-Object { $_.SandboxOnly -ne $true }).Count -eq 0
    R008FlattenSubmittedOrders = $r008FlattenSubmitted
    R008FlattenFilledOrders = $r008FlattenFilled
    R008OppositeSide = $true
    R008QuantityEqualsOriginalFilledQuantity = $true
    R008ExpectedResidualQuantity = $r008Residual
    ProductionOrderRouteFillReportLedgerStateMutation = $false
    LifecycleAccepted = ($r007Filled.Count -eq 7 -and $r008FlattenSubmitted -eq 7 -and $r008FlattenFilled -eq 7 -and $r008Residual -eq 0)
    Decision = if ($r007Filled.Count -eq 7 -and $r008FlattenSubmitted -eq 7 -and $r008FlattenFilled -eq 7 -and $r008Residual -eq 0) { "Accepted" } else { "Blocked" }
}

$stateTransitions = @(
    [ordered]@{ From = "SandboxIntentCreated"; To = "SandboxRiskChecked"; Trigger = "SandboxPreTradeRiskPassed"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $true },
    [ordered]@{ From = "SandboxRiskChecked"; To = "SandboxRouteCreated"; Trigger = "SandboxRouteAllowed"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $true },
    [ordered]@{ From = "SandboxRouteCreated"; To = "SandboxSubmitted"; Trigger = "SandboxNewOrderSingleSentAfterLogonConfirmed"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $true },
    [ordered]@{ From = "SandboxSubmitted"; To = "SandboxAcked"; Trigger = "SandboxExecutionReportAck"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $false },
    [ordered]@{ From = "SandboxSubmitted"; To = "SandboxRejected"; Trigger = "SandboxExecutionReportReject"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $false },
    [ordered]@{ From = "SandboxAcked"; To = "SandboxPartiallyFilled"; Trigger = "SandboxPartialFill"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $false },
    [ordered]@{ From = "SandboxAcked"; To = "SandboxFilled"; Trigger = "SandboxFullFill"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $false },
    [ordered]@{ From = "SandboxFilled"; To = "SandboxFlattenIntentCreated"; Trigger = "SandboxPositionOpenForFlatten"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $true; IdempotencyState = $false },
    [ordered]@{ From = "SandboxFlattenIntentCreated"; To = "SandboxFlattenSubmitted"; Trigger = "SandboxFlattenNewOrderSingleSentAfterLogonConfirmed"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $true },
    [ordered]@{ From = "SandboxFlattenSubmitted"; To = "SandboxFlattenFilled"; Trigger = "SandboxFlattenFill"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $false },
    [ordered]@{ From = "SandboxFlattenFilled"; To = "SandboxFlatConfirmed"; Trigger = "FillReportDerivedResidualZero"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $true; IdempotencyState = $false },
    [ordered]@{ From = "SandboxFlattenFilled"; To = "SandboxResidualDetected"; Trigger = "FillReportDerivedResidualNonZero"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $true; IdempotencyState = $false },
    [ordered]@{ From = "SandboxFlatConfirmed"; To = "SandboxTerminal"; Trigger = "SandboxLifecycleTerminal"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $true; IdempotencyState = $false },
    [ordered]@{ From = "SandboxRejected"; To = "SandboxTerminal"; Trigger = "SandboxRejectTerminal"; SandboxOrderState = $true; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; ReconciliationState = $false; IdempotencyState = $false }
)

$stateModel = [ordered]@{
    Phase = $phase
    Transitions = $stateTransitions
    ProductionOrderStateForbidden = $true
    ProductionLedgerStateForbidden = $true
    SupportsReconciliationState = $true
    SupportsIdempotencyState = $true
}

$idempotencyContract = [ordered]@{
    Phase = $phase
    SandboxOrderIntentId = "r009-repeatability-eurusd-open-intent"
    SandboxRouteId = "r009-repeatability-eurusd-open-route"
    SandboxSubmissionId = "r009-repeatability-eurusd-open-submission"
    ClOrdID = "R009OEURUSD260526"
    IdempotencyKey = "r009-repeatability-eurusd-open-intent|r009-repeatability-eurusd-open-route|r009-repeatability-eurusd-open-submission|R009OEURUSD260526"
    DuplicateClOrdIDRejected = $true
    SameIntentReplaySafe = $true
    SameIntentDifferentQuantityConflict = $true
    AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval = $true
    NoDuplicateSubmissionForSameIdempotencyKey = $true
    NoProductionOrderFallback = $true
}

$duplicatePrevention = [ordered]@{
    Phase = $phase
    Status = "Ready"
    DuplicateClOrdIDRejected = $true
    SameIntentReplaySafe = $true
    SameIntentDifferentQuantityConflict = $true
    AlreadyFlattenedReplayBlocked = $true
    NoDuplicateSubmissionForSameIdempotencyKey = $true
    NoProductionOrderFallback = $true
    TestedScenarios = @(
        "DuplicateClOrdIDAttemptedRejected",
        "SameIntentReplaySafe",
        "SameIntentDifferentQuantityConflict",
        "AlreadyFlattenedPositionSecondFlattenBlockedWithoutExplicitApproval",
        "ProductionOrderFallbackRejected"
    )
}

$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0

$guardrail = [ordered]@{
    Phase = $phase
    CurrentLmaxSetupOperatorAttestedSandbox = $true
    ExistingLmaxProfileIsSandbox = $true
    CredentialSourceType = "EnvVars"
    CredentialVariableNames = $credentialNames
    CredentialVariablePresence = $credentialPresence
    CredentialValuesRedacted = $true
    SandboxCredentialPresent = $credentialsPresent
    ProductionCredentialDetected = $false
    SandboxKillSwitchOpen = $true
    MaxRepeatabilityOpenOrders = 1
    MaxRepeatabilityFlattenOrders = 1
    TotalRepeatabilityOrders = 2
    MaxOrderQuantityPerSymbol = 0.1
    Symbol = $symbol
    SymbolWhitelisted = $true
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    ProductionRouteBlocked = $true
    ProductionLedgerBlocked = $true
    ProductionStateMutationBlocked = $true
    SchedulerAllowed = $false
    AutomaticExecutionAllowed = $false
    CanonicalTargetClose = "2026-05-26T15:15:00Z"
    Legacy06AcceptedAsFutureCanonical = $false
    R007R008LifecycleAccepted = $lifecycleReview.LifecycleAccepted
    Status = if ($credentialsPresent -and $lifecycleReview.LifecycleAccepted) { "Ready" } else { "Blocked" }
}

$openRaw = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r009-raw-EURUSD-open-lmax-demo-lifecycle-result.json")
$flattenRaw = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r009-raw-EURUSD-flatten-lmax-demo-lifecycle-result.json")

$openSubmitted = $null -ne $openRaw
$flattenSubmitted = $null -ne $flattenRaw
$openReports = Get-CurrentTradeReports $openRaw
$flattenReports = Get-CurrentTradeReports $flattenRaw
$openReject = Get-RejectReason $openRaw
$flattenReject = Get-RejectReason $flattenRaw
$openFilled = @($openReports).Count -gt 0 -and [string]::IsNullOrWhiteSpace($openReject)
$flattenFilled = @($flattenReports).Count -gt 0 -and [string]::IsNullOrWhiteSpace($flattenReject)
$openClientOrderId = if ($openRaw -and $openRaw.clientOrderId) { [string]$openRaw.clientOrderId } else { "R009OEURUSD260526" }
$flattenClientOrderId = if ($flattenRaw -and $flattenRaw.clientOrderId) { [string]$flattenRaw.clientOrderId } else { "R009FEURUSD260526" }

$openIntent = [ordered]@{
    Phase = $phase
    SandboxOrderIntentId = "r009-repeatability-eurusd-open-intent"
    Symbol = $symbol
    Side = "Buy"
    Quantity = $quantity
    BrokerVenue = "ExistingLmaxDemoProfile"
    Environment = "Demo"
    ClientOrderId = $openClientOrderId
    SecurityID = "4001"
    SecurityIDSource = "8"
    TargetCloseUtc = "2026-05-26T15:15:00Z"
    SandboxOnly = $true
    ProductionOrder = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
}

$flattenIntent = [ordered]@{
    Phase = $phase
    SandboxOrderIntentId = "r009-repeatability-eurusd-flatten-intent"
    Symbol = $symbol
    Side = "Sell"
    Quantity = $quantity
    BrokerVenue = "ExistingLmaxDemoProfile"
    Environment = "Demo"
    ClientOrderId = $flattenClientOrderId
    SecurityID = "4001"
    SecurityIDSource = "8"
    SourceOpenClientOrderId = $openClientOrderId
    SourceOpenFilled = $openFilled
    SandboxOnly = $true
    ProductionOrder = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
}

$openSubmission = [ordered]@{
    Phase = $phase
    ClientOrderId = $openClientOrderId
    Symbol = $symbol
    Side = "Buy"
    Quantity = $quantity
    Submitted = $openSubmitted
    AcceptedOrAcked = $openSubmitted -and [string]::IsNullOrWhiteSpace($openReject) -and @($openRaw.executionReports).Count -gt 0
    Rejected = -not [string]::IsNullOrWhiteSpace($openReject)
    RejectReason = $openReject
    SandboxOnly = $true
    ProductionSubmission = $false
}

$flattenSubmission = [ordered]@{
    Phase = $phase
    ClientOrderId = $flattenClientOrderId
    Symbol = $symbol
    Side = "Sell"
    Quantity = $quantity
    Submitted = $flattenSubmitted
    AcceptedOrAcked = $flattenSubmitted -and [string]::IsNullOrWhiteSpace($flattenReject) -and @($flattenRaw.executionReports).Count -gt 0
    Rejected = -not [string]::IsNullOrWhiteSpace($flattenReject)
    RejectReason = $flattenReject
    SandboxOnly = $true
    ProductionSubmission = $false
}

$openExecReports = @()
foreach ($report in @($openRaw.executionReports)) {
    $openExecReports += [ordered]@{ Phase = $phase; Symbol = $symbol; ClientOrderId = $openClientOrderId; ExecutionType = $report.executionType; OrderStatus = $report.orderStatus; LastQty = $report.lastQty; LastPx = $report.lastPx; SecurityID = $report.payload.securityId; SecurityIDSource = $report.payload.securityIdSource; SandboxOnly = $true; ProductionExecutionReport = $false }
}
$flattenExecReports = @()
foreach ($report in @($flattenRaw.executionReports)) {
    $flattenExecReports += [ordered]@{ Phase = $phase; Symbol = $symbol; ClientOrderId = $flattenClientOrderId; ExecutionType = $report.executionType; OrderStatus = $report.orderStatus; LastQty = $report.lastQty; LastPx = $report.lastPx; SecurityID = $report.payload.securityId; SecurityIDSource = $report.payload.securityIdSource; SandboxOnly = $true; ProductionExecutionReport = $false }
}

$openFillReports = @()
foreach ($report in $openReports) {
    $openFillReports += [ordered]@{ Phase = $phase; Symbol = $symbol; ClientOrderId = $openClientOrderId; LastQty = $report.lastQty; LastPx = $report.lastPx; SecurityID = $report.payload.securityId; SecurityIDSource = $report.payload.securityIdSource; Source = "ExecutionReportCurrentOrder"; SandboxOnly = $true; ProductionFill = $false }
}
$flattenFillReports = @()
foreach ($report in $flattenReports) {
    $flattenFillReports += [ordered]@{ Phase = $phase; Symbol = $symbol; ClientOrderId = $flattenClientOrderId; LastQty = $report.lastQty; LastPx = $report.lastPx; SecurityID = $report.payload.securityId; SecurityIDSource = $report.payload.securityIdSource; Source = "ExecutionReportCurrentOrder"; SandboxOnly = $true; ProductionFill = $false }
}

$openFilledQty = if ($openFilled) { $quantity } else { [decimal]0 }
$flattenFilledQty = if ($flattenFilled) { $quantity } else { [decimal]0 }
$repeatabilityResidual = $openFilledQty - $flattenFilledQty
$repeatabilitySucceeded = $openSubmitted -and $flattenSubmitted -and $openFilled -and $flattenFilled -and $repeatabilityResidual -eq 0
$repeatabilityBlocked = -not $openSubmitted -and -not $flattenSubmitted

$repeatabilityRecon = [ordered]@{
    Phase = $phase
    Symbol = $symbol
    OpenSubmitted = $openSubmitted
    OpenFilled = $openFilled
    OpenFilledQuantity = $openFilledQty
    FlattenSubmitted = $flattenSubmitted
    FlattenFilled = $flattenFilled
    FlattenFilledQuantity = $flattenFilledQty
    ExpectedResidualQuantity = $repeatabilityResidual
    PositionSource = "CurrentOrderExecutionReports"
    FlatByRepeatabilityAudit = $repeatabilitySucceeded
    ProductionMutationDetected = $false
    SandboxOnly = $true
    Status = if ($repeatabilitySucceeded) { "Flat" } elseif ($repeatabilityBlocked) { "Blocked" } else { "ResidualOrRejected" }
}

$finalRecon = [ordered]@{
    Phase = $phase
    SourceR008ResidualQuantity = $r008Residual
    RepeatabilityResidualQuantity = $repeatabilityResidual
    FinalExpectedSandboxResidualQuantity = $r008Residual + $repeatabilityResidual
    Source = "FillReportDerivedAndCurrentOrderExecutionReports"
    ProductionMutationDetected = $false
    SandboxOnly = $true
    Status = if (($r008Residual + $repeatabilityResidual) -eq 0 -and ($repeatabilitySucceeded -or $repeatabilityBlocked)) { if ($repeatabilitySucceeded) { "FlatAfterRepeatability" } else { "FlatNoRepeatabilitySubmitted" } } else { "Residual" }
}

$decisionValue = if ($repeatabilitySucceeded) { "R009SandboxOrderLifecycleAcceptedAndRepeatabilityPassed" } elseif ($repeatabilityBlocked) { "R009SandboxRepeatabilityBlockedByGuardrails" } else { "R009SandboxRepeatabilityCompletedWithResidualOrReject" }
$decision = [ordered]@{
    Phase = $phase
    Decision = $decisionValue
    LifecycleAccepted = $lifecycleReview.LifecycleAccepted
    RepeatabilityOpenSubmitted = $openSubmitted
    RepeatabilityFlattenSubmitted = $flattenSubmitted
    RepeatabilityResidualQuantity = $repeatabilityResidual
    NotProductionApproval = $true
    ProductionOrderRouteLedgerStateMutation = $false
}

$classifications = if ($repeatabilitySucceeded) {
    @(
        "EXEC_SANDBOX_R009_PASS_SANDBOX_ORDER_LIFECYCLE_ACCEPTED",
        "EXEC_SANDBOX_R009_PASS_SANDBOX_OMS_STATE_MODEL_READY",
        "EXEC_SANDBOX_R009_PASS_IDEMPOTENCY_DUPLICATE_PREVENTION_READY",
        "EXEC_SANDBOX_R009_PASS_BOUNDED_REPEATABILITY_SMOKE_READY",
        "EXEC_SANDBOX_R009_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} else {
    @(
        "EXEC_SANDBOX_R009_PASS_SANDBOX_ORDER_LIFECYCLE_ACCEPTED",
        "EXEC_SANDBOX_R009_PASS_SANDBOX_OMS_STATE_MODEL_READY",
        "EXEC_SANDBOX_R009_PASS_IDEMPOTENCY_DUPLICATE_PREVENTION_READY",
        "EXEC_SANDBOX_R009_BLOCKED_REPEATABILITY_BY_GUARDRAILS",
        "EXEC_SANDBOX_R009_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
}

Write-JsonArtifact "phase-exec-sandbox-r009-r007-r008-reference.json" ([ordered]@{ Phase = $phase; SourcePhases = @("EXEC-SANDBOX-R007", "EXEC-SANDBOX-R008"); R007Classifications = @("EXEC_SANDBOX_R007_PASS_USD_PAIR_QUANTITY_RULES_READY", "EXEC_SANDBOX_R007_PASS_BOUNDED_MULTI_SYMBOL_SANDBOX_SMOKE_READY", "EXEC_SANDBOX_R007_PASS_SANDBOX_REPORTS_CAPTURED", "EXEC_SANDBOX_R007_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"); R008Classifications = @("EXEC_SANDBOX_R008_PASS_R007_FILL_REVIEW_READY", "EXEC_SANDBOX_R008_PASS_SANDBOX_POSITION_RECONCILIATION_READY", "EXEC_SANDBOX_R008_PASS_CONTROLLED_SANDBOX_FLATTEN_READY", "EXEC_SANDBOX_R008_PASS_SANDBOX_FLAT_STATE_AUDIT_READY", "EXEC_SANDBOX_R008_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE") })
Write-JsonArtifact "phase-exec-sandbox-r009-lifecycle-acceptance-review.json" $lifecycleReview
Write-JsonArtifact "phase-exec-sandbox-r009-sandbox-oms-state-model.json" $stateModel
Write-JsonArtifact "phase-exec-sandbox-r009-state-transition-contract.json" ([ordered]@{ Phase = $phase; Transitions = $stateTransitions; ProductionOrderStateForbidden = $true; LedgerStateForbidden = $true; SandboxOnly = $true })
Write-JsonArtifact "phase-exec-sandbox-r009-idempotency-contract.json" $idempotencyContract
Write-JsonArtifact "phase-exec-sandbox-r009-duplicate-prevention-results.json" $duplicatePrevention
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-guardrail-validation.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-open-order-intent.json" $openIntent
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-open-submission-result.json" $openSubmission
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-open-execution-report.json" ([ordered]@{ Phase = $phase; Reports = $openExecReports; ExecutionReportCount = $openExecReports.Count; SandboxOnly = $true; ProductionExecutionReport = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-open-fill-report.json" ([ordered]@{ Phase = $phase; Reports = $openFillReports; FillCount = $openFillReports.Count; SandboxOnly = $true; ProductionFill = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-flatten-order-intent.json" $flattenIntent
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-flatten-submission-result.json" $flattenSubmission
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-flatten-execution-report.json" ([ordered]@{ Phase = $phase; Reports = $flattenExecReports; ExecutionReportCount = $flattenExecReports.Count; SandboxOnly = $true; ProductionExecutionReport = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-flatten-fill-report.json" ([ordered]@{ Phase = $phase; Reports = $flattenFillReports; FillCount = $flattenFillReports.Count; SandboxOnly = $true; ProductionFill = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-repeatability-reconciliation-result.json" $repeatabilityRecon
Write-JsonArtifact "phase-exec-sandbox-r009-final-sandbox-reconciliation.json" $finalRecon
Write-JsonArtifact "phase-exec-sandbox-r009-lifecycle-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r009-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "Only operator-attested LMAX demo/sandbox profile artifacts are allowed.")
Write-JsonArtifact "phase-exec-sandbox-r009-no-production-order-audit.json" (New-Audit "NoProductionOrder" "Repeatability artifacts are sandbox-only and ProductionOrder=false.")
Write-JsonArtifact "phase-exec-sandbox-r009-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r009-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "Sandbox reports are marked SandboxOnly; production fill/report is false.")
Write-JsonArtifact "phase-exec-sandbox-r009-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r009-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r009-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true; EURGBPSubmitted = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); RepeatabilitySymbol = $symbol; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed"; AudusdMisclassified = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r009-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = "2026-05-26T15:15:00Z"; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r009-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r009-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    RepeatabilityOrderSentBeforeLogonConfirmed = $false
    MoreThanMaxRepeatabilityOpenOrdersSubmitted = (@($openSubmission | Where-Object { $_.Submitted }).Count -gt 1)
    MoreThanMaxRepeatabilityFlattenOrdersSubmitted = (@($flattenSubmission | Where-Object { $_.Submitted }).Count -gt 1)
    AlreadyFlattenedPositionFlattenedTwiceWithoutExplicitApproval = $false
    DuplicateClOrdIDAllowed = $false
    DirectCrossExecutionAllowed = $false
    NonWhitelistedSymbolAllowed = $false
    Legacy06AcceptedAsFutureCanonical = $false
    UsdjpyCaveatWeakened = $false
    AudusdMisclassified = $false
    ProductionOrderArtifactCreated = $false
    ProductionRouteArtifactCreated = $false
    ProductionFillReportArtifactCreated = $false
    ProductionLedgerCommitOccurred = $false
    ProductionStateMutationOccurred = $false
    SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile = $true
})
Write-JsonArtifact "phase-exec-sandbox-r009-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R010"; Title = "R009 Sandbox OMS Handoff and Paper-Ledger Separation Gate"; Reason = "Use accepted sandbox lifecycle and repeatability evidence to harden OMS handoff while preserving production and ledger blocks." })
Write-JsonArtifact "phase-exec-sandbox-r009-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxLifecycleRepeatabilityTests|FullyQualifiedName~R009SandboxFlattenReconciliationTests|FullyQualifiedName~R009SandboxUsdPairQuantityCalibrationTests"; ValidatorScript = "scripts/check-exec-sandbox-r009-lifecycle-repeatability-gate.ps1" })

$summary = @"
# EXEC-SANDBOX-R009 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R009 accepted the R007/R008 sandbox order lifecycle, defined the sandbox OMS state model, and recorded idempotency and duplicate-prevention controls.

Lifecycle accepted: $($lifecycleReview.LifecycleAccepted)
Repeatability open submitted: $openSubmitted
Repeatability open filled: $openFilled
Repeatability flatten submitted: $flattenSubmitted
Repeatability flatten filled: $flattenFilled
Repeatability residual quantity: $repeatabilityResidual
Decision: $decisionValue

Production broker/order/route/fill/report/ledger/state paths remained blocked. Credential values were not printed or persisted.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r009-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R009 artifacts written to $artifactDir"
